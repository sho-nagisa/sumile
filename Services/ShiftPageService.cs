using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;

namespace sumile.Services
{
    public class ShiftPageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ShiftTableService _shiftTableService;

        public ShiftPageService(ApplicationDbContext context, ShiftTableService shiftTableService)
        {
            _context = context;
            _shiftTableService = shiftTableService;
        }

        public async Task<ShiftIndexPageViewModel> BuildIndexAsync(ApplicationUser currentUser, int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            var users = await _context.Users
                .OrderBy(u => u.CustomId)
                .Select(u => new ShiftUserListItemViewModel
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name,
                    UserShiftRole = u.UserShiftRole
                })
                .ToListAsync();

            return new ShiftIndexPageViewModel
            {
                CurrentUserCustomId = currentUser.CustomId > 0 ? currentUser.CustomId.ToString() : "No user",
                Users = users,
                RecruitmentPeriods = allPeriods,
                SelectedPeriodId = selectedPeriod?.Id,
                Table = await _shiftTableService.BuildAsync(periodId)
            };
        }

        public async Task<ShiftSubmissionPageViewModel> BuildSubmissionAsync(ApplicationUser currentUser, int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            if (!periodId.HasValue)
                periodId = allPeriods.FirstOrDefault(p => p.IsOpen)?.Id ?? allPeriods.FirstOrDefault()?.Id;

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(p => p.Id == periodId.Value)
                : null;

            var shiftDays = await GetShiftDaysForPeriodAsync(periodId);
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var existingSubmissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == currentUser.Id && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            var weekdayCopyOption = await BuildWeekdayCopyOptionAsync(currentUser.Id, selectedPeriod, shiftDays);
            var previousPeriodCopyOption = await BuildPreviousPeriodCopyOptionAsync(currentUser.Id, selectedPeriod, shiftDays);

            return new ShiftSubmissionPageViewModel
            {
                Periods = allPeriods,
                SelectedPeriodId = periodId,
                SelectedPeriod = selectedPeriod,
                IsSubmissionOpen = selectedPeriod?.IsOpen == true,
                HasSubmitted = existingSubmissions.Any(),
                ShiftDays = shiftDays,
                ExistingSubmissions = existingSubmissions,
                CurrentUserCustomId = currentUser.CustomId > 0 ? currentUser.CustomId.ToString() : "No user",
                CurrentUserName = string.IsNullOrEmpty(currentUser.Name) ? "No user" : currentUser.Name,
                WeekdayCopyShiftsJson = JsonConvert.SerializeObject(weekdayCopyOption.Cells),
                WeekdayCopySourceLabel = weekdayCopyOption.SourceLabel,
                PreviousPeriodCopyShiftsJson = JsonConvert.SerializeObject(previousPeriodCopyOption.Cells),
                PreviousPeriodCopySourceLabel = previousPeriodCopyOption.SourceLabel
            };
        }

        public async Task<SubmittedShiftListPageViewModel> BuildSubmittedListAsync(ApplicationUser currentUser, int? periodId, bool includeUsers)
        {
            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            var shiftDays = await GetShiftDaysForPeriodAsync(periodId);
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var submissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == currentUser.Id && shiftDayIds.Contains(s.ShiftDayId))
                .Include(s => s.ShiftDay)
                .OrderBy(s => s.ShiftDay.Date)
                .ThenBy(s => s.ShiftType)
                .ToListAsync();

            var model = new SubmittedShiftListPageViewModel
            {
                RecruitmentPeriods = recruitmentPeriods,
                SelectedPeriodId = periodId,
                Dates = shiftDays.Select(d => d.Date).ToList(),
                Submissions = submissions
            };

            if (includeUsers)
            {
                model.Users.Add(new ShiftUserListItemViewModel
                {
                    Id = currentUser.Id,
                    CustomId = currentUser.CustomId,
                    Name = currentUser.Name,
                    UserShiftRole = currentUser.UserShiftRole
                });
            }

            return model;
        }

        public async Task<bool> IsPeriodOpenAsync(int periodId)
        {
            return await _context.RecruitmentPeriods
                .AnyAsync(p => p.Id == periodId && p.IsOpen);
        }

        public async Task<bool> PeriodExistsAsync(int periodId)
        {
            return await _context.RecruitmentPeriods.AnyAsync(p => p.Id == periodId);
        }

        private async Task<List<ShiftDay>> GetShiftDaysForPeriodAsync(int? periodId)
        {
            if (!periodId.HasValue) return new List<ShiftDay>();
            return await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId.Value)
                .OrderBy(d => d.Date)
                .ToListAsync();
        }

        private static string ToSubmissionSymbol(ShiftState? state)
        {
            return state switch
            {
                ShiftState.Accepted => "〇",
                ShiftState.KeyHolder => "〇",
                ShiftState.WantToGiveAway => "△",
                _ => "×"
            };
        }

        private static string FormatPeriodLabel(RecruitmentPeriod period)
        {
            return $"{period.StartDate:yyyy/MM/dd} ～ {period.EndDate:MM/dd}";
        }

        private async Task<Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState>> LoadUserSubmissionStatesAsync(
            string userId,
            IReadOnlyCollection<ShiftDay> shiftDays,
            IReadOnlyCollection<int> periodIds)
        {
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            if (!shiftDayIds.Any() || !periodIds.Any())
            {
                return new Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState>();
            }

            var backupStates = await _context.SubmitBackups
                .Where(b =>
                    b.UserId == userId &&
                    periodIds.Contains(b.RecruitmentPeriodId) &&
                    shiftDayIds.Contains(b.ShiftDayId))
                .ToListAsync();

            var states = backupStates
                .GroupBy(b => (b.ShiftDayId, b.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(b => b.BackedUpAt).First().ShiftStatus);

            var currentSubmissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == userId && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            foreach (var submissionGroup in currentSubmissions.GroupBy(s => (s.ShiftDayId, s.ShiftType)))
            {
                if (states.ContainsKey(submissionGroup.Key))
                {
                    continue;
                }

                states[submissionGroup.Key] = submissionGroup
                    .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                    .ThenByDescending(s => s.Id)
                    .First()
                    .ShiftStatus;
            }

            return states;
        }

        private static List<ShiftCopyCellViewModel> BuildCopyCells(
            IEnumerable<(ShiftDay TargetDay, ShiftDay SourceDay)> dayPairs,
            Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState> sourceStates)
        {
            var cells = new List<ShiftCopyCellViewModel>();

            foreach (var (targetDay, sourceDay) in dayPairs)
            {
                foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
                {
                    sourceStates.TryGetValue((sourceDay.Id, shiftType), out var state);
                    var symbol = ToSubmissionSymbol(state);

                    if (symbol == "×")
                    {
                        continue;
                    }

                    cells.Add(new ShiftCopyCellViewModel
                    {
                        Date = targetDay.Date.ToString("yyyy-MM-dd"),
                        ShiftType = shiftType.ToString(),
                        ShiftSymbol = symbol
                    });
                }
            }

            return cells;
        }

        private async Task<ShiftCopyOptionViewModel> BuildWeekdayCopyOptionAsync(
            string userId,
            RecruitmentPeriod? selectedPeriod,
            List<ShiftDay> targetDays)
        {
            var result = new ShiftCopyOptionViewModel();
            if (selectedPeriod == null || !targetDays.Any())
            {
                return result;
            }

            var sourcePeriods = await _context.RecruitmentPeriods
                .Where(p => p.Id != selectedPeriod.Id && p.StartDate < selectedPeriod.StartDate)
                .OrderByDescending(p => p.StartDate)
                .Take(6)
                .ToListAsync();

            var sourcePeriodIds = sourcePeriods.Select(p => p.Id).ToList();
            if (!sourcePeriodIds.Any())
            {
                return result;
            }

            var sourceDays = await _context.ShiftDays
                .Where(d => sourcePeriodIds.Contains(d.RecruitmentPeriodId))
                .OrderByDescending(d => d.Date)
                .ToListAsync();

            var latestDayByWeekday = sourceDays
                .GroupBy(d => d.Date.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.First());

            var dayPairs = targetDays
                .Where(target => latestDayByWeekday.ContainsKey(target.Date.DayOfWeek))
                .Select(target => (TargetDay: target, SourceDay: latestDayByWeekday[target.Date.DayOfWeek]))
                .ToList();

            if (!dayPairs.Any())
            {
                return result;
            }

            var sourceStates = await LoadUserSubmissionStatesAsync(userId, sourceDays, sourcePeriodIds);
            result.Cells = BuildCopyCells(dayPairs, sourceStates);

            var usedSourceDates = dayPairs
                .Select(pair => pair.SourceDay.Date.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            result.SourceLabel = usedSourceDates.Count == 1
                ? $"{usedSourceDates.First():yyyy/MM/dd} の同じ曜日"
                : $"{usedSourceDates.First():yyyy/MM/dd} ～ {usedSourceDates.Last():MM/dd} の同じ曜日";

            return result;
        }

        private async Task<ShiftCopyOptionViewModel> BuildPreviousPeriodCopyOptionAsync(
            string userId,
            RecruitmentPeriod? selectedPeriod,
            List<ShiftDay> targetDays)
        {
            var result = new ShiftCopyOptionViewModel();
            if (selectedPeriod == null || !targetDays.Any())
            {
                return result;
            }

            var sourcePeriod = await _context.RecruitmentPeriods
                .Where(p => p.Id != selectedPeriod.Id && p.StartDate < selectedPeriod.StartDate)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (sourcePeriod == null)
            {
                return result;
            }

            var sourceDays = await GetShiftDaysForPeriodAsync(sourcePeriod.Id);
            if (!sourceDays.Any())
            {
                return result;
            }

            var sourceStates = await LoadUserSubmissionStatesAsync(userId, sourceDays, new[] { sourcePeriod.Id });
            var dayPairs = targetDays
                .Take(Math.Min(targetDays.Count, sourceDays.Count))
                .Select((target, index) => (TargetDay: target, SourceDay: sourceDays[index]))
                .ToList();

            result.Cells = BuildCopyCells(dayPairs, sourceStates);
            result.SourceLabel = FormatPeriodLabel(sourcePeriod);
            return result;
        }
    }
}
