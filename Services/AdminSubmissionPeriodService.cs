using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;

namespace sumile.Services
{
    public class AdminSubmissionPeriodService
    {
        private readonly ApplicationDbContext _context;

        public AdminSubmissionPeriodService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RecruitmentPeriodViewModel> BuildDefaultPeriodModelAsync()
        {
            var latest = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            latest ??= new RecruitmentPeriod
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(9)
            };

            return new RecruitmentPeriodViewModel
            {
                StartDate = latest.StartDate,
                EndDate = latest.EndDate
            };
        }

        public async Task CreatePeriodAsync(RecruitmentPeriodViewModel model)
        {
            var startUtc = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);

            var newRecruitment = new RecruitmentPeriod
            {
                StartDate = startUtc,
                EndDate = endUtc,
                IsOpen = true
            };

            _context.RecruitmentPeriods.Add(newRecruitment);
            await _context.SaveChangesAsync();

            var days = new List<ShiftDay>();
            for (var date = startUtc.Date; date <= endUtc.Date; date = date.AddDays(1))
            {
                days.Add(new ShiftDay
                {
                    Date = date,
                    RecruitmentPeriodId = newRecruitment.Id
                });
            }

            _context.ShiftDays.AddRange(days);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ToggleSubmissionStatusAsync(int periodId, DateTime updatedAt)
        {
            var period = await _context.RecruitmentPeriods.FindAsync(periodId);
            if (period == null)
            {
                return false;
            }

            if (period.IsOpen)
            {
                await BackupSubmissionsAsync(periodId, updatedAt);
            }

            period.IsOpen = !period.IsOpen;
            _context.RecruitmentPeriods.Update(period);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<AdminSubmissionPeriodListViewModel> BuildPeriodListAsync()
        {
            var periods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            var targetUserIds = await _context.Users
                .Where(u => !u.IsAdmin)
                .Select(u => u.Id)
                .ToListAsync();
            var targetUserIdSet = targetUserIds.ToHashSet();
            var periodIds = periods.Select(p => p.Id).ToList();
            var shiftDays = await _context.ShiftDays
                .Where(d => periodIds.Contains(d.RecruitmentPeriodId))
                .Select(d => new { d.Id, d.RecruitmentPeriodId })
                .ToListAsync();
            var periodByShiftDayId = shiftDays.ToDictionary(d => d.Id, d => d.RecruitmentPeriodId);
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var submittedUserIds = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .Select(s => new { s.UserId, s.ShiftDayId })
                .Distinct()
                .ToListAsync();

            var submittedCounts = submittedUserIds
                .Where(s => targetUserIdSet.Contains(s.UserId))
                .Where(s => periodByShiftDayId.ContainsKey(s.ShiftDayId))
                .GroupBy(s => periodByShiftDayId[s.ShiftDayId])
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).Distinct().Count());

            return new AdminSubmissionPeriodListViewModel
            {
                Periods = periods,
                TargetUserCount = targetUserIds.Count,
                SubmittedCounts = submittedCounts
            };
        }

        private async Task BackupSubmissionsAsync(int periodId, DateTime backedUpAt)
        {
            var shiftDayIds = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .Select(d => d.Id)
                .ToListAsync();

            var existingBackups = await _context.SubmitBackups
                .Where(b => b.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var existingBackupKeys = existingBackups
                .Select(b => GetShiftCellKey(b.UserId, b.ShiftDayId, b.ShiftType))
                .ToHashSet();

            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            var backups = submissions
                .Where(s => !existingBackupKeys.Contains(GetShiftCellKey(s.UserId, s.ShiftDayId, s.ShiftType)))
                .Select(s => new SubmitBackup
                {
                    RecruitmentPeriodId = periodId,
                    UserId = s.UserId,
                    ShiftDayId = s.ShiftDayId,
                    ShiftType = s.ShiftType,
                    ShiftStatus = s.ShiftStatus,
                    BackedUpAt = backedUpAt
                })
                .ToList();

            _context.SubmitBackups.AddRange(backups);
        }

        private static string GetShiftCellKey(string userId, int shiftDayId, ShiftType shiftType)
        {
            return $"{userId}_{shiftDayId}_{(int)shiftType}";
        }
    }
}
