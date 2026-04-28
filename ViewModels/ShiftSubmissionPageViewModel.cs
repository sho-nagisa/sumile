using System.Collections.Generic;
using sumile.Models;

namespace sumile.ViewModels
{
    public class ShiftSubmissionPageViewModel
    {
        public List<ShiftDay> ShiftDays { get; set; } = new();
        public List<ShiftSubmission> ExistingSubmissions { get; set; } = new();
        public List<RecruitmentPeriod> Periods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public RecruitmentPeriod? SelectedPeriod { get; set; }
        public string CurrentUserCustomId { get; set; } = "unknown";
        public string CurrentUserName { get; set; } = "No user";
        public string WeekdayCopyShiftsJson { get; set; } = "[]";
        public string? WeekdayCopySourceLabel { get; set; }
        public string PreviousPeriodCopyShiftsJson { get; set; } = "[]";
        public string? PreviousPeriodCopySourceLabel { get; set; }

        public bool HasCopyTools =>
            !string.IsNullOrWhiteSpace(WeekdayCopySourceLabel) ||
            !string.IsNullOrWhiteSpace(PreviousPeriodCopySourceLabel);
    }
}
