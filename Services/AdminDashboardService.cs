using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;

namespace sumile.Services
{
    public class AdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ShiftTableService _shiftTableService;
        private readonly ShiftPdfService _pdfService;

        public AdminDashboardService(
            ApplicationDbContext context,
            ShiftTableService shiftTableService,
            ShiftPdfService pdfService)
        {
            _context = context;
            _shiftTableService = shiftTableService;
            _pdfService = pdfService;
        }

        public async Task<AdminDashboardViewModel?> BuildAsync(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                return null;
            }

            var users = await _context.Users
                .OrderBy(u => u.CustomId)
                .Select(u => new AdminDashboardUserViewModel
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name,
                    UserShiftRole = u.UserShiftRole,
                    IsAdmin = u.IsAdmin
                })
                .ToListAsync();

            var table = await _shiftTableService.BuildAsync(selectedPeriod.Id);
            var shiftDayIds = table.ShiftDays.Select(d => d.Id).ToList();
            var submittedUserIds = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();
            var submittedUserIdSet = submittedUserIds.ToHashSet();
            var targetUsers = users.Where(u => !u.IsAdmin).ToList();
            var targetUserIdSet = targetUsers.Select(u => u.Id).ToHashSet();

            var dashboard = new AdminDashboardViewModel
            {
                RecruitmentPeriods = allPeriods,
                SelectedPeriodId = selectedPeriod.Id,
                Users = users,
                Table = table,
                SubmittedUserCount = submittedUserIdSet.Count(id => targetUserIdSet.Contains(id)),
                TargetUserCount = targetUsers.Count,
                UnsubmittedUsers = targetUsers
                    .Where(u => !submittedUserIdSet.Contains(u.Id))
                    .Select(u => string.IsNullOrWhiteSpace(u.Name) ? u.CustomId.ToString() : u.Name)
                    .ToList(),
                AssignmentSummary = BuildAssignmentSummary(table),
                UserShiftStats = BuildUserStats(table, targetUsers, targetUserIdSet, submittedUserIdSet),
                DiffKeys = await BuildDiffKeysAsync(shiftDayIds)
            };

            var pdfUrl = await _pdfService.EnsureShiftPdfAsync(selectedPeriod.Id);
            var pdfPath = _pdfService.GetShiftPdfPhysicalPath(selectedPeriod.Id);
            if (File.Exists(pdfPath))
            {
                var updatedAt = File.GetLastWriteTime(pdfPath);
                dashboard.ShiftPdfUrl = $"{pdfUrl}?v={updatedAt.Ticks}";
                dashboard.ShiftPdfUpdatedAt = updatedAt;
            }

            return dashboard;
        }

        private static AdminShiftAssignmentSummaryViewModel BuildAssignmentSummary(ShiftTableResult table)
        {
            var summary = new AdminShiftAssignmentSummaryViewModel
            {
                ShiftCellCount = table.RequiredWorkersList.Count(w => w > 0),
                RequiredWorkerTotal = table.RequiredWorkersList.Sum(),
                AssignedTotal = table.TotalAcceptedList.Sum(),
                KeyHolderAssignedTotal = table.KeyHolderAcceptedList.Sum(),
                WorkerShortageCellCount = table.RemainingWorkersList.Count(v => v < 0),
                WorkerShortageSlotCount = table.RemainingWorkersList.Where(v => v < 0).Sum(v => -v),
                OverAssignedSlotCount = table.RemainingWorkersList.Where(v => v > 0).Sum()
            };

            for (var i = 0; i < table.RequiredWorkersList.Count; i++)
            {
                var requiredWorkers = table.RequiredWorkersList[i];
                if (requiredWorkers <= 0)
                {
                    continue;
                }

                var requiredKeyHolders = (int)Math.Ceiling(requiredWorkers / 2.0);
                var keyHolderAssigned = i < table.KeyHolderAcceptedList.Count
                    ? table.KeyHolderAcceptedList[i]
                    : 0;
                var keyHolderShortage = requiredKeyHolders - keyHolderAssigned;
                if (keyHolderShortage > 0)
                {
                    summary.KeyHolderShortageCellCount++;
                    summary.KeyHolderShortageSlotCount += keyHolderShortage;
                }
            }

            return summary;
        }

        private static List<AdminShiftUserStatViewModel> BuildUserStats(
            ShiftTableResult table,
            List<AdminDashboardUserViewModel> targetUsers,
            HashSet<string> targetUserIdSet,
            HashSet<string> submittedUserIdSet)
        {
            var submissionsByUser = table.Submissions
                .Where(s => targetUserIdSet.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return targetUsers
                .Select(user =>
                {
                    submissionsByUser.TryGetValue(user.Id, out var userSubmissions);
                    userSubmissions ??= new List<ShiftSubmission>();

                    var requestedCount = userSubmissions.Count(s => s.ShiftStatus != ShiftState.None);

                    return new AdminShiftUserStatViewModel
                    {
                        UserId = user.Id,
                        CustomId = user.CustomId,
                        Name = string.IsNullOrWhiteSpace(user.Name) ? user.CustomId.ToString() : user.Name,
                        UserShiftRole = user.UserShiftRole,
                        HasSubmitted = submittedUserIdSet.Contains(user.Id),
                        RequestedCount = requestedCount,
                        AssignedCount = userSubmissions.Count(s =>
                            s.ShiftStatus == ShiftState.Accepted ||
                            s.ShiftStatus == ShiftState.KeyHolder),
                        KeyHolderAssignedCount = userSubmissions.Count(s => s.ShiftStatus == ShiftState.KeyHolder),
                        BlankCount = userSubmissions.Count(s => s.ShiftStatus == ShiftState.NotAccepted)
                    };
                })
                .OrderBy(s => s.CustomId)
                .ToList();
        }

        private async Task<HashSet<string>> BuildDiffKeysAsync(List<int> shiftDayIds)
        {
            var diffLogs = await _context.ShiftEditLogs
                .Where(log => shiftDayIds.Contains(log.ShiftDayId))
                .Select(log => new
                {
                    log.TargetUserId,
                    log.ShiftDayId,
                    log.ShiftType
                })
                .Distinct()
                .ToListAsync();

            return new HashSet<string>(
                diffLogs.Select(k => $"{k.TargetUserId}_{k.ShiftDayId}_{(int)k.ShiftType}")
            );
        }
    }
}
