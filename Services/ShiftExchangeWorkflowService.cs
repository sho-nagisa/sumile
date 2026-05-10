using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;

namespace sumile.Services
{
    public class ShiftExchangeWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public ShiftExchangeWorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShiftExchangeWorkflowResult> CancelRequestAsync(
            int exchangeId,
            string userId,
            DateTime updatedAt)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var exchange = await LoadExchangeForUpdateAsync(exchangeId);
                if (exchange == null)
                {
                    return ShiftExchangeWorkflowResult.NotFoundResult("交換募集が見つかりません。");
                }

                if (exchange.RequestedByUserId != userId)
                {
                    return ShiftExchangeWorkflowResult.ForbiddenResult("この交換募集は取り消せません。");
                }

                if (!IsCancelableByRequesterStatus(exchange.Status))
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("この交換募集は取り消せません。");
                }

                exchange.Status = ShiftExchange.StatusCanceled;
                exchange.UpdatedAt = updatedAt;
                _context.ShiftExchanges.Update(exchange);
                await _context.SaveChangesAsync();

                return ShiftExchangeWorkflowResult.SuccessResult("交換募集を取り消しました。");
            });
        }

        public async Task<ShiftExchangeWorkflowResult> CancelApplicationAsync(
            int exchangeId,
            string userId,
            DateTime updatedAt)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var exchange = await LoadExchangeForUpdateAsync(exchangeId);
                if (exchange == null)
                {
                    return ShiftExchangeWorkflowResult.NotFoundResult("交換募集が見つかりません。");
                }

                if (!IsPendingApprovalStatus(exchange.Status) || exchange.AcceptedByUserId != userId)
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("この応募は取り消せません。");
                }

                exchange.AcceptedByUserId = null;
                exchange.AcceptedAt = null;
                exchange.AcceptedShiftSubmissionId = null;
                exchange.Status = ShiftExchange.StatusOpen;
                exchange.UpdatedAt = updatedAt;
                _context.ShiftExchanges.Update(exchange);
                await _context.SaveChangesAsync();

                return ShiftExchangeWorkflowResult.SuccessResult("応募を取り消しました。");
            });
        }

        public async Task<ShiftExchangeWorkflowResult> RejectExchangeAsync(
            int exchangeId,
            DateTime updatedAt)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var exchange = await LoadExchangeForUpdateAsync(exchangeId);
                if (exchange == null || !IsPendingApprovalStatus(exchange.Status))
                {
                    return ShiftExchangeWorkflowResult.NotFoundResult("承認待ちの交換が見つかりません。");
                }

                exchange.Status = ShiftExchange.StatusRejected;
                exchange.UpdatedAt = updatedAt;
                _context.ShiftExchanges.Update(exchange);
                await _context.SaveChangesAsync();

                return ShiftExchangeWorkflowResult.SuccessResult("交換を却下しました。");
            });
        }

        public async Task<ShiftExchangeWorkflowResult> CreateRequestAsync(
            int offeredShiftSubmissionId,
            int shiftDayId,
            ShiftType shiftType,
            string requesterUserId,
            string? targetUserId,
            DateTime createdAt)
        {
            var submission = await _context.ShiftSubmissions
                .FirstOrDefaultAsync(s =>
                    s.Id == offeredShiftSubmissionId &&
                    s.ShiftDayId == shiftDayId &&
                    s.ShiftType == shiftType &&
                    s.UserId == requesterUserId);

            if (submission == null)
            {
                return ShiftExchangeWorkflowResult.InvalidResult("無効なシフトが選択されました。");
            }

            if (!IsExchangeableOfferedShiftStatus(submission.ShiftStatus))
            {
                return ShiftExchangeWorkflowResult.InvalidResult("交換に出せるシフトではありません。");
            }

            if (!string.IsNullOrEmpty(targetUserId))
            {
                if (targetUserId == requesterUserId)
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("自分自身だけを表示先にはできません。");
                }

                var targetExists = await _context.Users.AnyAsync(u => u.Id == targetUserId);
                if (!targetExists)
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("表示先ユーザーが見つかりません。");
                }
            }

            var alreadyExists = await _context.ShiftExchanges
                .AnyAsync(e =>
                    e.OfferedShiftSubmissionId == offeredShiftSubmissionId &&
                    (e.Status == ShiftExchange.StatusOpen ||
                     e.Status == ShiftExchange.StatusPendingApproval ||
                     e.Status == ShiftExchange.StatusAcceptedLegacy));
            if (alreadyExists)
            {
                return ShiftExchangeWorkflowResult.InvalidResult("このシフトはすでに交換募集済みです。");
            }

            _context.ShiftExchanges.Add(new ShiftExchange
            {
                RequestedByUserId = requesterUserId,
                TargetUserId = string.IsNullOrEmpty(targetUserId) ? null : targetUserId,
                OfferedShiftSubmissionId = offeredShiftSubmissionId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                Status = ShiftExchange.StatusOpen
            });
            await _context.SaveChangesAsync();

            return ShiftExchangeWorkflowResult.SuccessResult("交換希望を登録しました。");
        }

        public async Task<ShiftExchangeWorkflowResult> ApplyAsync(int exchangeId, string userId, DateTime updatedAt)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var exchange = await LoadExchangeForUpdateAsync(exchangeId);
                if (exchange == null || !IsOpenStatus(exchange.Status))
                {
                    return ShiftExchangeWorkflowResult.NotFoundResult("募集が見つからないか、すでに応募済みです。");
                }

                if (exchange.RequestedByUserId == userId)
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("自分の募集には応募できません。");
                }

                if (!string.IsNullOrEmpty(exchange.TargetUserId) && exchange.TargetUserId != userId)
                {
                    return ShiftExchangeWorkflowResult.InvalidResult("この交換募集は指定されたユーザーだけが応募できます。");
                }

                exchange.AcceptedByUserId = userId;
                exchange.AcceptedAt = updatedAt;
                exchange.UpdatedAt = updatedAt;
                exchange.Status = ShiftExchange.StatusPendingApproval;
                _context.ShiftExchanges.Update(exchange);
                await _context.SaveChangesAsync();

                return ShiftExchangeWorkflowResult.SuccessResult("応募しました。管理者の承認待ちです。");
            });
        }

        public async Task<ShiftExchangeFinalizeResult> FinalizeAsync(
            int exchangeId,
            string adminUserId,
            DateTime updatedAt)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var exchange = await LoadExchangeForFinalizeForUpdateAsync(exchangeId);

                if (exchange == null || !IsPendingApprovalStatus(exchange.Status))
                {
                    return ShiftExchangeFinalizeResult.NotFoundResult("承認待ちの交換が見つかりません。");
                }

                if (string.IsNullOrEmpty(exchange.AcceptedByUserId))
                {
                    return ShiftExchangeFinalizeResult.InvalidResult("応募者が設定されていません。");
                }

                var offered = exchange.OfferedShiftSubmission;
                if (offered?.ShiftDay == null)
                {
                    return ShiftExchangeFinalizeResult.InvalidResult("シフト情報が不完全です。");
                }

                if (!IsExchangeableOfferedShiftStatus(offered.ShiftStatus))
                {
                    return ShiftExchangeFinalizeResult.InvalidResult("交換に出せるシフトではありません。");
                }

                var recruitmentPeriodId = offered.ShiftDay.RecruitmentPeriodId;
                var oldOfferedState = offered.ShiftStatus;

                offered.ShiftStatus = ShiftState.NotAccepted;
                offered.IsSelected = false;
                offered.SubmittedAt = updatedAt;
                offered.Source = ShiftSubmissionSource.AdminEdited;

                var accepted = await _context.ShiftSubmissions
                    .FirstOrDefaultAsync(s =>
                        s.UserId == exchange.AcceptedByUserId &&
                        s.ShiftDayId == offered.ShiftDayId &&
                        s.ShiftType == offered.ShiftType);

                var oldAccepted = accepted?.ShiftStatus ?? ShiftState.None;
                var isNewAcceptedSubmission = accepted == null;
                if (accepted == null)
                {
                    accepted = new ShiftSubmission
                    {
                        UserId = exchange.AcceptedByUserId,
                        ShiftDayId = offered.ShiftDayId,
                        ShiftType = offered.ShiftType,
                        UserShiftRole = exchange.AcceptedByUser?.UserShiftRole ?? UserShiftRole.Normal
                    };
                    _context.ShiftSubmissions.Add(accepted);
                }

                accepted.IsSelected = true;
                accepted.SubmittedAt = updatedAt;
                accepted.ShiftStatus = ShiftState.Accepted;
                accepted.Source = ShiftSubmissionSource.AdminEdited;
                accepted.UserShiftRole = exchange.AcceptedByUser?.UserShiftRole ?? accepted.UserShiftRole;

                _context.ShiftSubmissions.Update(offered);
                if (!isNewAcceptedSubmission)
                {
                    _context.ShiftSubmissions.Update(accepted);
                }

                _context.ShiftEditLogs.AddRange(
                    new ShiftEditLog
                    {
                        AdminUserId = adminUserId,
                        TargetUserId = offered.UserId,
                        ShiftDayId = offered.ShiftDayId,
                        ShiftType = offered.ShiftType,
                        OldState = oldOfferedState,
                        NewState = ShiftState.NotAccepted,
                        EditDate = updatedAt,
                        Note = "交換確定：譲渡元を不採用へ変更"
                    },
                    new ShiftEditLog
                    {
                        AdminUserId = adminUserId,
                        TargetUserId = accepted.UserId,
                        ShiftDayId = accepted.ShiftDayId,
                        ShiftType = accepted.ShiftType,
                        OldState = oldAccepted,
                        NewState = ShiftState.Accepted,
                        EditDate = updatedAt,
                        Note = "交換確定：応募者へシフトを付与"
                    });

                exchange.AcceptedShiftSubmission = accepted;
                exchange.UpdatedAt = updatedAt;
                exchange.Status = ShiftExchange.StatusFinalized;
                _context.ShiftExchanges.Update(exchange);
                await _context.SaveChangesAsync();

                return ShiftExchangeFinalizeResult.SuccessResult("交換が確定されました。", recruitmentPeriodId);
            });
        }

        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            if (!_context.Database.IsRelational())
            {
                return await action();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }

        private Task<ShiftExchange?> LoadExchangeForUpdateAsync(int exchangeId)
        {
            if (!_context.Database.IsRelational())
            {
                return _context.ShiftExchanges.FirstOrDefaultAsync(e => e.Id == exchangeId);
            }

            return _context.ShiftExchanges
                .FromSqlInterpolated($"SELECT * FROM \"ShiftExchanges\" WHERE \"Id\" = {exchangeId} FOR UPDATE")
                .SingleOrDefaultAsync();
        }

        private Task<ShiftExchange?> LoadExchangeForFinalizeForUpdateAsync(int exchangeId)
        {
            var query = _context.Database.IsRelational()
                ? _context.ShiftExchanges.FromSqlInterpolated($"SELECT * FROM \"ShiftExchanges\" WHERE \"Id\" = {exchangeId} FOR UPDATE")
                : _context.ShiftExchanges.Where(e => e.Id == exchangeId);

            return query
                .Include(e => e.OfferedShiftSubmission)
                    .ThenInclude(s => s.ShiftDay)
                .Include(e => e.AcceptedByUser)
                .SingleOrDefaultAsync();
        }

        private static bool IsOpenStatus(string status)
        {
            return status == ShiftExchange.StatusOpen;
        }

        private static bool IsPendingApprovalStatus(string status)
        {
            return status == ShiftExchange.StatusPendingApproval ||
                   status == ShiftExchange.StatusAcceptedLegacy;
        }

        private static bool IsExchangeableOfferedShiftStatus(ShiftState state)
        {
            return state == ShiftState.Accepted ||
                   state == ShiftState.WantToGiveAway ||
                   state == ShiftState.KeyHolder;
        }

        private static bool IsCancelableByRequesterStatus(string status)
        {
            return IsOpenStatus(status) || IsPendingApprovalStatus(status);
        }
    }

    public class ShiftExchangeWorkflowResult
    {
        private ShiftExchangeWorkflowResult(bool success, bool notFound, bool forbidden, string message)
        {
            Success = success;
            NotFound = notFound;
            Forbidden = forbidden;
            Message = message;
        }

        public bool Success { get; }
        public bool NotFound { get; }
        public bool Forbidden { get; }
        public string Message { get; }

        public static ShiftExchangeWorkflowResult SuccessResult(string message)
        {
            return new ShiftExchangeWorkflowResult(true, false, false, message);
        }

        public static ShiftExchangeWorkflowResult NotFoundResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, true, false, message);
        }

        public static ShiftExchangeWorkflowResult ForbiddenResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, false, true, message);
        }

        public static ShiftExchangeWorkflowResult InvalidResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, false, false, message);
        }
    }

    public class ShiftExchangeFinalizeResult
    {
        private ShiftExchangeFinalizeResult(
            bool success,
            bool notFound,
            string message,
            int? recruitmentPeriodId)
        {
            Success = success;
            NotFound = notFound;
            Message = message;
            RecruitmentPeriodId = recruitmentPeriodId;
        }

        public bool Success { get; }
        public bool NotFound { get; }
        public string Message { get; }
        public int? RecruitmentPeriodId { get; }

        public static ShiftExchangeFinalizeResult SuccessResult(string message, int recruitmentPeriodId)
        {
            return new ShiftExchangeFinalizeResult(true, false, message, recruitmentPeriodId);
        }

        public static ShiftExchangeFinalizeResult NotFoundResult(string message)
        {
            return new ShiftExchangeFinalizeResult(false, true, message, null);
        }

        public static ShiftExchangeFinalizeResult InvalidResult(string message)
        {
            return new ShiftExchangeFinalizeResult(false, false, message, null);
        }
    }
}
