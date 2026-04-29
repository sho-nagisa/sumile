using sumile.Models;

namespace sumile.ViewModels
{
    public class AdminShiftUserStatViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public int CustomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public UserShiftRole UserShiftRole { get; set; }
        public bool HasSubmitted { get; set; }
        public int RequestedCount { get; set; }
        public int AssignedCount { get; set; }
        public int KeyHolderAssignedCount { get; set; }
        public int BlankCount { get; set; }

        public double BlankRate =>
            RequestedCount <= 0
                ? 0
                : BlankCount / (double)RequestedCount;
    }
}
