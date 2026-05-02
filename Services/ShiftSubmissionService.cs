using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sumile.Data;
using sumile.Models;

namespace sumile.Services
{
    public class ShiftSubmissionService
    {
        private readonly ApplicationDbContext _context;

        public ShiftSubmissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SubmitShiftsAsync(
            ApplicationUser user,
            string selectedShifts,
            int periodId,
            UserType userType,
            DateTime submittedAt)
        {
            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var selectedList = string.IsNullOrEmpty(selectedShifts)
                ? new List<ShiftSubmissionViewModel>()
                : JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts)
                  ?? new List<ShiftSubmissionViewModel>();

            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var existingSubmissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == user.Id && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            var existingByCell = existingSubmissions
                .GroupBy(s => (s.ShiftDayId, s.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                        .ThenByDescending(s => s.Id)
                        .First());

            foreach (var day in shiftDays)
            {
                foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
                {
                    var selected = selectedList.FirstOrDefault(s =>
                        DateTime.Parse(s.Date).Date == day.Date.Date &&
                        s.ShiftType == shiftType);

                    var status = selected?.ShiftSymbol switch
                    {
                        "〇" => ShiftState.Accepted,
                        "△" => ShiftState.WantToGiveAway,
                        _ => ShiftState.None
                    };

                    if (existingByCell.TryGetValue((day.Id, shiftType), out var submission))
                    {
                        submission.ShiftStatus = status;
                        submission.IsSelected = status != ShiftState.None;
                        submission.SubmittedAt = submittedAt;
                        submission.UserType = userType;
                        submission.UserShiftRole = user.UserShiftRole;
                        continue;
                    }

                    _context.ShiftSubmissions.Add(new ShiftSubmission
                    {
                        UserId = user.Id,
                        ShiftDayId = day.Id,
                        ShiftType = shiftType,
                        ShiftStatus = status,
                        IsSelected = status != ShiftState.None,
                        SubmittedAt = submittedAt,
                        UserType = userType,
                        UserShiftRole = user.UserShiftRole
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
