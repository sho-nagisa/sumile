using sumile.Models;
using sumile.Services;

namespace sumile.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int SelectedPeriodId { get; set; }
        public List<AdminDashboardUserViewModel> Users { get; set; } = new();
        public string? CurrentUserCustomId { get; set; }
        public ShiftTableResult Table { get; set; } = new();
        public HashSet<string> DiffKeys { get; set; } = new();
        public int SubmittedUserCount { get; set; }
        public int TargetUserCount { get; set; }
        public List<string> UnsubmittedUsers { get; set; } = new();
        public AdminShiftAssignmentSummaryViewModel AssignmentSummary { get; set; } = new();
        public List<AdminShiftUserStatViewModel> UserShiftStats { get; set; } = new();
        public string? ShiftPdfUrl { get; set; }
        public DateTime? ShiftPdfUpdatedAt { get; set; }
    }
}
