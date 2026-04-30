using sumile.Models;
using sumile.Services;

namespace sumile.ViewModels
{
    public class ShiftIndexPageViewModel
    {
        public string CurrentUserCustomId { get; set; } = "No user";
        public List<ShiftUserListItemViewModel> Users { get; set; } = new();
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public ShiftTableResult Table { get; set; } = new();
    }

    public class ShiftUserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public int CustomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public UserShiftRole UserShiftRole { get; set; }
    }
}
