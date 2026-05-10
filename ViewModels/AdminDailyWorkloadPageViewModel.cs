using sumile.Models;

namespace sumile.ViewModels
{
    public class AdminDailyWorkloadPageViewModel
    {
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int SelectedPeriodId { get; set; }
        public List<ShiftDay> ShiftDays { get; set; } = new();
        public Dictionary<int, DailyWorkload> WorkloadMap { get; set; } = new();
    }
}
