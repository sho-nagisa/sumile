using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;

namespace sumile.Services
{
    public class AdminShiftEditService
    {
        private readonly ApplicationDbContext _context;

        public AdminShiftEditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminShiftEditResult> UpdateShiftsAsync(
            ShiftUpdateRequest request,
            int periodId,
            string adminUserId,
            DateTime updatedAt)
        {
            if (request?.ShiftUpdates == null || !request.ShiftUpdates.Any())
            {
                return AdminShiftEditResult.Failure("シフト更新データが空です。");
            }

            var trimmedReason = request.Reason?.Trim();
            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();
            var shiftDayDict = shiftDays.ToDictionary(d => d.Date.Date, d => d.Id);

            var backups = await _context.SubmitBackups
                .Where(b => b.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var initialStateByKey = backups
                .GroupBy(b => GetShiftCellKey(b.UserId, b.ShiftDayId, b.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(b => b.BackedUpAt).First().ShiftStatus);

            var existingSubmissions = await _context.ShiftSubmissions
                .Where(s => shiftDayDict.Values.Contains(s.ShiftDayId))
                .ToListAsync();

            var hasInitialConfirmation = await _context.ShiftEditLogs
                .AnyAsync(l => shiftDayDict.Values.Contains(l.ShiftDayId));

            var submissionByKey = existingSubmissions
                .GroupBy(s => GetShiftCellKey(s.UserId, s.ShiftDayId, s.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                        .ThenByDescending(s => s.Id)
                        .First());

            var targetUserIds = request.ShiftUpdates
                .Where(s => !string.IsNullOrEmpty(s.UserId))
                .Select(s => s.UserId)
                .Distinct()
                .ToList();
            var userRoles = await _context.Users
                .Where(u => targetUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserShiftRole);

            var logs = new List<ShiftEditLog>();

            foreach (var shift in request.ShiftUpdates)
            {
                if (!TryParseUpdate(shift, shiftDayDict, out var shiftDayId, out var shiftType, out var newState))
                {
                    continue;
                }

                var submissionKey = GetShiftCellKey(shift.UserId, shiftDayId, shiftType);
                submissionByKey.TryGetValue(submissionKey, out var existing);
                var currentState = existing?.ShiftStatus ?? ShiftState.None;
                if (currentState == newState)
                {
                    continue;
                }

                var initialState = GetInitialState(initialStateByKey, shift.UserId, shiftDayId, shiftType);
                var note = BuildEditLogNote(hasInitialConfirmation, initialState, currentState, newState, trimmedReason);

                if (existing == null)
                {
                    if (newState == ShiftState.None)
                    {
                        continue;
                    }

                    var newSubmission = new ShiftSubmission
                    {
                        UserId = shift.UserId,
                        ShiftDayId = shiftDayId,
                        ShiftType = shiftType,
                        IsSelected = IsSelectedState(newState),
                        SubmittedAt = updatedAt,
                        ShiftStatus = newState,
                        UserType = UserType.AdminUpdated,
                        UserShiftRole = userRoles.TryGetValue(shift.UserId, out var userRole)
                            ? userRole
                            : UserShiftRole.Normal
                    };
                    _context.ShiftSubmissions.Add(newSubmission);
                    submissionByKey[submissionKey] = newSubmission;
                }
                else
                {
                    existing.ShiftStatus = newState;
                    existing.IsSelected = IsSelectedState(newState);
                    existing.SubmittedAt = updatedAt;
                    existing.UserType = UserType.AdminUpdated;
                    _context.ShiftSubmissions.Update(existing);
                }

                logs.Add(new ShiftEditLog
                {
                    AdminUserId = adminUserId,
                    TargetUserId = shift.UserId,
                    ShiftDayId = shiftDayId,
                    ShiftType = shiftType,
                    OldState = currentState,
                    NewState = newState,
                    EditDate = updatedAt,
                    Note = note
                });
            }

            if (logs.Any())
            {
                _context.ShiftEditLogs.AddRange(logs);
            }

            await _context.SaveChangesAsync();
            return AdminShiftEditResult.Ok();
        }

        private static bool TryParseUpdate(
            ShiftUpdateModel? shift,
            IReadOnlyDictionary<DateTime, int> shiftDayDict,
            out int shiftDayId,
            out ShiftType shiftType,
            out ShiftState newState)
        {
            shiftDayId = 0;
            shiftType = ShiftType.Morning;
            newState = ShiftState.None;

            if (shift == null || string.IsNullOrEmpty(shift.UserId) || string.IsNullOrEmpty(shift.Date))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(ShiftType), shift.ShiftType))
            {
                return false;
            }

            if (!DateTime.TryParse(shift.Date, out var parsedDate))
            {
                return false;
            }

            if (!shiftDayDict.TryGetValue(parsedDate.Date, out shiftDayId))
            {
                return false;
            }

            shiftType = (ShiftType)shift.ShiftType;
            if (shift.ShiftState.HasValue && Enum.IsDefined(typeof(ShiftState), shift.ShiftState.Value))
            {
                newState = (ShiftState)shift.ShiftState.Value;
            }
            else
            {
                newState = shift.ShiftStatus switch
                {
                    "〇" => ShiftState.Accepted,
                    "△" => ShiftState.WantToGiveAway,
                    "🔴" => ShiftState.KeyHolder,
                    "×" => ShiftState.None,
                    "" => ShiftState.NotAccepted,
                    _ => ShiftState.None
                };
            }

            return true;
        }

        private static string GetShiftCellKey(string userId, int shiftDayId, ShiftType shiftType)
        {
            return $"{userId}_{shiftDayId}_{(int)shiftType}";
        }

        private static bool IsSelectedState(ShiftState state)
        {
            return state != ShiftState.None && state != ShiftState.NotAccepted;
        }

        private static ShiftState GetInitialState(
            IReadOnlyDictionary<string, ShiftState> initialStateByKey,
            string userId,
            int shiftDayId,
            ShiftType shiftType)
        {
            return initialStateByKey.TryGetValue(GetShiftCellKey(userId, shiftDayId, shiftType), out var state)
                ? state
                : ShiftState.None;
        }

        private static string BuildEditLogNote(
            bool isInitialConfirmation,
            ShiftState initialState,
            ShiftState oldState,
            ShiftState newState,
            string? reason)
        {
            string actionLabel;

            if (isInitialConfirmation)
            {
                actionLabel = initialState == ShiftState.None ? "新規作成" : "初回確定";
            }
            else if (newState == initialState)
            {
                actionLabel = "初期状態に合わせて変更";
            }
            else
            {
                actionLabel = "変更";
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return actionLabel;
            }

            return $"{actionLabel}: {reason.Trim()}";
        }
    }

    public class AdminShiftEditResult
    {
        private AdminShiftEditResult(bool success, string? errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public string? ErrorMessage { get; }

        public static AdminShiftEditResult Ok()
        {
            return new AdminShiftEditResult(true, null);
        }

        public static AdminShiftEditResult Failure(string errorMessage)
        {
            return new AdminShiftEditResult(false, errorMessage);
        }
    }
}
