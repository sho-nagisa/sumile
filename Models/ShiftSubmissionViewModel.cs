
namespace sumile.Models
{   
    public class ShiftSubmissionViewModel
    {
        public string Date { get; set; } = string.Empty;
        public ShiftType ShiftType { get; set; }
        public string ShiftSymbol { get; set; } = string.Empty;
    }
}
