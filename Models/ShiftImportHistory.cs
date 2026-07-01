namespace sumile.Models
{
    public class ShiftImportHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public DateOnly RangeStartDate { get; set; }
        public DateOnly RangeEndDate { get; set; }
        public string EventsJson { get; set; } = "[]";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
