using sumile.Models;

namespace sumile.ViewModels
{
    public class AdminDashboardUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public int CustomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public UserShiftRole UserShiftRole { get; set; }
        public bool IsAdmin { get; set; }
    }
}
