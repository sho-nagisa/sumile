namespace sumile.ViewModels
{
    public class ShiftUpdateRequest
    {
        public List<ShiftUpdateModel> ShiftUpdates { get; set; } = new();
        public string? Reason { get; set; }
    }

    public class ShiftUpdateModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public int ShiftType { get; set; }
        public int? ShiftState { get; set; }
        public string ShiftStatus { get; set; } = string.Empty;
        public int RecruitmentPeriodId { get; set; }
    }
}
