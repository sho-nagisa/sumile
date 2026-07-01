using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using sumile.ViewModels;
using System.Text.Json;

namespace sumile.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/shift-import")]
    public class ShiftImportApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ShiftPdfCsvService _pdfCsvService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShiftImportApiController(
            IConfiguration configuration,
            ApplicationDbContext context,
            ShiftPdfCsvService pdfCsvService,
            UserManager<ApplicationUser> userManager)
        {
            _configuration = configuration;
            _context = context;
            _pdfCsvService = pdfCsvService;
            _userManager = userManager;
        }

        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] ShiftImportApiRequest request)
        {
            var shortcutKey = GetShortcutKey(request);
            var importUser = await FindUserByShortcutKeyAsync(shortcutKey);
            if (!string.IsNullOrWhiteSpace(shortcutKey) && importUser == null)
            {
                return Unauthorized(new { message = "Shortcut key is invalid." });
            }

            if (!IsAuthorized(request.ApiKey, importUser != null))
            {
                return Unauthorized(new { message = "API key is invalid." });
            }

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { message = "Upload a PDF file as file." });
            }

            if (request.File.Length > 10_000_000)
            {
                return BadRequest(new { message = "The PDF file must be 10MB or smaller." });
            }

            if (!IsPdfFile(request.File))
            {
                return BadRequest(new { message = "Only PDF files are supported." });
            }

            var staffSearchName = importUser?.ShiftPdfSearchName;
            var staffRowNumber = request.StaffRowNumber ?? importUser?.ShiftPdfStaffRowNumber;
            if (string.IsNullOrWhiteSpace(staffSearchName) && staffRowNumber is null or < 1)
            {
                return BadRequest(new { message = "staffRowNumber or a shortcutKey with saved PDF settings is required." });
            }

            try
            {
                using var stream = request.File.OpenReadStream();
                var result = _pdfCsvService.Convert(stream, BuildPdfOptions(request, staffRowNumber, staffSearchName));
                var events = result.Events.Select(ToApiEvent).ToList();
                var removedEvents = await SaveImportHistoryAndGetRemovedEventsAsync(importUser, result, events);

                return Ok(new ShiftImportApiResponse(
                    result.RangeStartDate.ToString("yyyy-MM-dd"),
                    result.RangeEndDate.ToString("yyyy-MM-dd"),
                    result.SelectedStaffRowNumber,
                    result.DetectedStaffRows,
                    events,
                    removedEvents));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<IReadOnlyList<ShiftImportApiEvent>> SaveImportHistoryAndGetRemovedEventsAsync(
            ApplicationUser? importUser,
            ShiftPdfCsvResult result,
            IReadOnlyList<ShiftImportApiEvent> currentEvents)
        {
            if (importUser == null)
            {
                return Array.Empty<ShiftImportApiEvent>();
            }

            var history = await _context.ShiftImportHistories
                .FirstOrDefaultAsync(item =>
                    item.UserId == importUser.Id &&
                    item.RangeStartDate == result.RangeStartDate &&
                    item.RangeEndDate == result.RangeEndDate);

            var previousEvents = history == null
                ? new List<ShiftImportApiEvent>()
                : DeserializeEvents(history.EventsJson);

            var currentEventKeys = currentEvents
                .Select(item => item.EventKey)
                .ToHashSet(StringComparer.Ordinal);

            var removedEvents = previousEvents
                .Where(item => !currentEventKeys.Contains(item.EventKey))
                .ToList();

            var now = DateTime.UtcNow;
            if (history == null)
            {
                _context.ShiftImportHistories.Add(new ShiftImportHistory
                {
                    UserId = importUser.Id,
                    RangeStartDate = result.RangeStartDate,
                    RangeEndDate = result.RangeEndDate,
                    EventsJson = SerializeEvents(currentEvents),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                history.EventsJson = SerializeEvents(currentEvents);
                history.UpdatedAtUtc = now;
            }

            await _context.SaveChangesAsync();
            return removedEvents;
        }

        private static List<ShiftImportApiEvent> DeserializeEvents(string eventsJson)
        {
            if (string.IsNullOrWhiteSpace(eventsJson))
            {
                return new List<ShiftImportApiEvent>();
            }

            return JsonSerializer.Deserialize<List<ShiftImportApiEvent>>(
                eventsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ShiftImportApiEvent>();
        }

        private static string SerializeEvents(IReadOnlyList<ShiftImportApiEvent> events)
        {
            return JsonSerializer.Serialize(events);
        }

        private bool IsAuthorized(string? formApiKey, bool hasValidShortcutKey)
        {
            if (hasValidShortcutKey)
            {
                return true;
            }

            var expected = Environment.GetEnvironmentVariable("SHIFT_IMPORT_API_KEY")
                ?? _configuration["SHIFT_IMPORT_API_KEY"];

            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

            var provided = Request.Headers["X-Shift-Import-Key"].FirstOrDefault()
                ?? formApiKey;

            return string.Equals(provided, expected, StringComparison.Ordinal);
        }

        private string? GetShortcutKey(ShiftImportApiRequest request)
        {
            return Request.Headers["X-Shift-Import-Shortcut-Key"].FirstOrDefault()
                ?? request.ShortcutKey;
        }

        private async Task<ApplicationUser?> FindUserByShortcutKeyAsync(string? shortcutKey)
        {
            if (string.IsNullOrWhiteSpace(shortcutKey))
            {
                return null;
            }

            return await _userManager.Users
                .FirstOrDefaultAsync(user => user.ShiftImportApiKey == shortcutKey);
        }

        private static ShiftPdfCsvOptions BuildPdfOptions(
            ShiftImportApiRequest request,
            int? staffRowNumber,
            string? staffSearchName)
        {
            return new ShiftPdfCsvOptions(
                request.PageNumber,
                staffRowNumber,
                staffSearchName,
                request.SubjectPrefix,
                request.MorningStartTime,
                request.MorningEndTime,
                request.NightStartTime,
                request.NightEndTime,
                request.IncludeTriangle);
        }

        private static bool IsPdfFile(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = file.ContentType?.ToLowerInvariant() ?? "";

            return extension == ".pdf" || contentType == "application/pdf";
        }

        private static ShiftImportApiEvent ToApiEvent(ShiftPdfCsvEvent item)
        {
            var startDate = item.StartDate.ToString("yyyy-MM-dd");
            var startTime = item.StartTime.ToString("HH:mm");
            var endDate = item.EndDate.ToString("yyyy-MM-dd");
            var endTime = item.EndTime.ToString("HH:mm");
            var eventKey = $"{startDate}:{item.ShiftLabel}:{startTime}-{endTime}";

            return new ShiftImportApiEvent(
                item.Subject,
                startDate,
                item.ShiftLabel,
                item.ShiftName,
                item.Status,
                startDate,
                startTime,
                endDate,
                endTime,
                $"{startDate}T{startTime}:00",
                $"{endDate}T{endTime}:00",
                $"{item.Description}\n{eventKey}",
                eventKey);
        }
    }
}
