using sumile.Models;
using sumile.Services;

namespace sumile.ViewModels
{
    public class AdminEditShiftsPageViewModel
    {
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int SelectedPeriodId { get; set; }
        public List<AdminDashboardUserViewModel> Users { get; set; } = new();
        public ShiftTableResult Table { get; set; } = new();
        public List<SubmitBackup> Backups { get; set; } = new();
        public bool HasInitialConfirmation { get; set; }
    }
}
