using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using sumile.Models;
using sumile.Services;
using sumile.ViewModels;
using System.Text;

namespace sumile.Controllers
{
    [Authorize]
    public class ShiftController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ShiftSubmissionService _shiftSubmissionService;
        private readonly ShiftPageService _shiftPageService;
        private readonly ShiftPdfCsvService _shiftPdfCsvService;

        public ShiftController(
            UserManager<ApplicationUser> userManager,
            ShiftSubmissionService shiftSubmissionService,
            ShiftPageService shiftPageService,
            ShiftPdfCsvService? shiftPdfCsvService = null)
        {
            _userManager = userManager;
            _shiftSubmissionService = shiftSubmissionService;
            _shiftPageService = shiftPageService;
            _shiftPdfCsvService = shiftPdfCsvService ?? new ShiftPdfCsvService();
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var model = await _shiftPageService.BuildIndexAsync(currentUser, periodId);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Submission(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var model = await _shiftPageService.BuildSubmissionAsync(currentUser, periodId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitShifts([FromForm] string selectedShifts, [FromForm] int periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            if (!await _shiftPageService.PeriodExistsAsync(periodId))
            {
                TempData["ErrorMessage"] = "募集期間が見つかりません。";
                return RedirectToAction("Submission");
            }

            if (!await _shiftPageService.IsPeriodOpenAsync(periodId))
            {
                TempData["ErrorMessage"] = "この募集期間は締め切られているため提出できません。";
                return RedirectToAction("Submission", new { periodId });
            }

            var userTypeStr = HttpContext.Session.GetString("UserType") ?? "Normal";
            var userType = Enum.TryParse(userTypeStr, out UserType parsedUserType)
                ? parsedUserType
                : UserType.Normal;

            await _shiftSubmissionService.SubmitShiftsAsync(
                currentUser,
                selectedShifts,
                periodId,
                userType,
                DateTime.UtcNow);

            var submittedItems = string.IsNullOrWhiteSpace(selectedShifts)
                ? 0
                : (JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts) ?? new()).Count;

            TempData["SuccessMessage"] = submittedItems == 0
                ? "帰省・希望なしとして提出しました。"
                : "シフトが提出されました。";
            return RedirectToAction("Submission", new { periodId });
        }

        [HttpGet]
        public async Task<IActionResult> SubmittedList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var model = await _shiftPageService.BuildSubmittedListAsync(currentUser, periodId, includeUsers: true);
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Submission));
        }

        [HttpGet]
        public IActionResult ImportPdf()
        {
            return View(new ShiftPdfImportViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10_000_000)]
        public IActionResult ImportPdf(ShiftPdfImportViewModel model)
        {
            if (model.PdfFile == null || model.PdfFile.Length == 0)
            {
                ModelState.AddModelError(nameof(model.PdfFile), "PDFファイルを選択してください。");
            }
            else if (model.PdfFile.Length > 10_000_000)
            {
                ModelState.AddModelError(nameof(model.PdfFile), "PDFファイルは10MB以下にしてください。");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using var stream = model.PdfFile!.OpenReadStream();
                var result = _shiftPdfCsvService.Convert(stream, new ShiftPdfCsvOptions(
                    model.PageNumber,
                    model.StaffRowNumber,
                    null,
                    model.SubjectPrefix,
                    model.MorningStartTime,
                    model.MorningEndTime,
                    model.NightStartTime,
                    model.NightEndTime,
                    model.IncludeTriangle));

                if (!result.Events.Any())
                {
                    ModelState.AddModelError("", "指定した行にCSV出力対象の○または△がありませんでした。");
                    return View(model);
                }

                var bytes = AddUtf8Bom(result.Csv);
                var fileName = $"shift-calendar-{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(bytes, "text/csv; charset=utf-8", fileName);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private static byte[] AddUtf8Bom(string text)
        {
            var body = Encoding.UTF8.GetBytes(text);
            var bom = Encoding.UTF8.GetPreamble();
            var bytes = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);
            return bytes;
        }
    }
}
