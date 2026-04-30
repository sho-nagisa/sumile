using sumile.Models;

namespace sumile.ViewModels
{
    public class AdminSubmissionPeriodListViewModel
    {
        public List<RecruitmentPeriod> Periods { get; set; } = new();
        public int TargetUserCount { get; set; }
        public Dictionary<int, int> SubmittedCounts { get; set; } = new();
    }
}
