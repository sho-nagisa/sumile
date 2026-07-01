using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Models;
using sumile.Services;
using sumile.ViewModels;

namespace sumile.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/shift-import")]
    public class ShiftImportApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ShiftPdfCsvService _pdfCsvService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShiftImportApiController(
            IConfiguration configuration,
            ShiftPdfCsvService pdfCsvService,
            UserManager<ApplicationUser> userManager)
        {
            _configuration = configuration;
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

                return Ok(new ShiftImportApiResponse(
                    result.SelectedStaffRowNumber,
                    result.DetectedStaffRows,
                    result.Events.Select(ToApiEvent).ToList()));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
                item.Description);
        }
    }
}
