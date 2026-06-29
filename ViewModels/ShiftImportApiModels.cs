using Microsoft.AspNetCore.Http;

namespace sumile.ViewModels
{
    public class ShiftImportApiRequest
    {
        public IFormFile? File { get; set; }
        public int PageNumber { get; set; } = 1;
        public int StaffRowNumber { get; set; } = 1;
        public string SubjectPrefix { get; set; } = "\u3075\u306a\u3084";
        public string MorningStartTime { get; set; } = "07:00";
        public string MorningEndTime { get; set; } = "08:00";
        public string NightStartTime { get; set; } = "18:30";
        public string NightEndTime { get; set; } = "19:30";
        public bool IncludeTriangle { get; set; } = true;
        public string? ApiKey { get; set; }
    }

    public sealed record ShiftImportApiResponse(
        int StaffRowNumber,
        int DetectedStaffRows,
        IReadOnlyList<ShiftImportApiEvent> Events);

    public sealed record ShiftImportApiEvent(
        string Title,
        string Date,
        string Shift,
        string ShiftName,
        string Status,
        string StartDate,
        string StartTime,
        string EndDate,
        string EndTime,
        string Start,
        string End,
        string Notes);
}
