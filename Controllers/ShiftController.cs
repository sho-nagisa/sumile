using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using sumile.Models;
using sumile.Services;

namespace sumile.Controllers
{
    [Authorize]
    public class ShiftController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ShiftSubmissionService _shiftSubmissionService;
        private readonly ShiftPageService _shiftPageService;

        public ShiftController(
            UserManager<ApplicationUser> userManager,
            ShiftSubmissionService shiftSubmissionService,
            ShiftPageService shiftPageService)
        {
            _userManager = userManager;
            _shiftSubmissionService = shiftSubmissionService;
            _shiftPageService = shiftPageService;
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
        public async Task<IActionResult> SubmissioList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var model = await _shiftPageService.BuildSubmittedListAsync(currentUser, periodId, includeUsers: true);
            ViewBag.RecruitmentPeriods = model.RecruitmentPeriods;
            ViewBag.SelectedPeriodId = model.SelectedPeriodId;
            ViewBag.Dates = model.Dates;
            ViewBag.Users = model.Users;
            ViewBag.Submissions = model.Submissions;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SubmittedList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var model = await _shiftPageService.BuildSubmittedListAsync(currentUser, periodId, includeUsers: false);
            ViewBag.RecruitmentPeriods = model.RecruitmentPeriods;
            ViewBag.SelectedPeriodId = model.SelectedPeriodId;
            ViewBag.Dates = model.Dates;

            return View(model.Submissions);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Submission));
        }
    }
}
