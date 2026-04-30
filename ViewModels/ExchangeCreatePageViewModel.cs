using sumile.Models;

namespace sumile.ViewModels
{
    public class ExchangeCreatePageViewModel
    {
        public Dictionary<RecruitmentPeriod, List<ShiftSubmission>> ShiftsByPeriod { get; set; } = new();
        public List<ApplicationUser> TargetUsers { get; set; } = new();
    }
}
