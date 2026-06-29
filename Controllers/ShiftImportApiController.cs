using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ShiftImportApiController(
            IConfiguration configuration,
            ShiftPdfCsvService pdfCsvService)
        {
            _configuration = configuration;
            _pdfCsvService = pdfCsvService;
        }

        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public IActionResult Import([FromForm] ShiftImportApiRequest request)
        {
            if (!IsAuthorized(request.ApiKey))
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

            if (request.StaffRowNumber < 1)
            {
                return BadRequest(new { message = "staffRowNumber must be 1 or greater." });
            }

            try
            {
                using var stream = request.File.OpenReadStream();
                var result = _pdfCsvService.Convert(stream, BuildPdfOptions(request));

                return Ok(new ShiftImportApiResponse(
                    request.StaffRowNumber,
                    result.DetectedStaffRows,
                    result.Events.Select(ToApiEvent).ToList()));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private bool IsAuthorized(string? formApiKey)
        {
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

        private static ShiftPdfCsvOptions BuildPdfOptions(ShiftImportApiRequest request)
        {
            return new ShiftPdfCsvOptions(
                request.PageNumber,
                request.StaffRowNumber,
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
