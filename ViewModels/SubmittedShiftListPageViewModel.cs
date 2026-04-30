using sumile.Models;

namespace sumile.ViewModels
{
    public class SubmittedShiftListPageViewModel
    {
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public List<DateTime> Dates { get; set; } = new();
        public List<ShiftSubmission> Submissions { get; set; } = new();
        public List<ShiftUserListItemViewModel> Users { get; set; } = new();
    }
}
