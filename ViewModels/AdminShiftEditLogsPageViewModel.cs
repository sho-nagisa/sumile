using sumile.Models;

namespace sumile.ViewModels
{
    public class AdminShiftEditLogsPageViewModel
    {
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public List<ApplicationUser> Users { get; set; } = new();
        public List<ShiftEditLog> Logs { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public string? SelectedTargetUserId { get; set; }
        public string? SelectedAdminUserId { get; set; }
        public string? EditedFrom { get; set; }
        public string? EditedTo { get; set; }
        public bool OnlyChanged { get; set; }
        public bool OnlyCurrentDiff { get; set; }
        public Dictionary<string, ShiftState> InitialStateByKey { get; set; } = new();
        public Dictionary<string, ShiftState> CurrentStateByKey { get; set; } = new();
    }
}
