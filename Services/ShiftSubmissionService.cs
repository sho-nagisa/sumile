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
            var existing = await _context.ShiftSubmissions
                .Where(s => s.UserId == user.Id && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            _context.ShiftSubmissions.RemoveRange(existing);

            var submissions = new List<ShiftSubmission>();

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

                    submissions.Add(new ShiftSubmission
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

            _context.ShiftSubmissions.AddRange(submissions);
            await _context.SaveChangesAsync();
        }
    }
}
