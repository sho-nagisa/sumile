using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;
using sumile.Services;
using sumile.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
///※１：確定版
namespace sumile.Controllers
{
    [Authorize(Policy = AdminPolicy.Name)]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ShiftPdfService _pdfService;
        private readonly AutoShiftAssignmentService _autoShiftAssignmentService;
        private readonly AdminDashboardService _adminDashboardService;
        private readonly AdminSubmissionPeriodService _adminSubmissionPeriodService;
        private readonly AdminShiftEditService _adminShiftEditService;

        public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ShiftPdfService pdfService,
        AutoShiftAssignmentService autoShiftAssignmentService,
        AdminDashboardService adminDashboardService,
        AdminSubmissionPeriodService adminSubmissionPeriodService,
        AdminShiftEditService adminShiftEditService)
        {
            _context = context;
            _userManager = userManager;
            _pdfService = pdfService;
            _autoShiftAssignmentService = autoShiftAssignmentService;
            _adminDashboardService = adminDashboardService;
            _adminSubmissionPeriodService = adminSubmissionPeriodService;
            _adminShiftEditService = adminShiftEditService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            var dashboard = await _adminDashboardService.BuildAsync(periodId);
            if (dashboard == null)
            {
                TempData["Error"] = "募集期間が選択されていません。";
                return RedirectToAction("SetRecruitmentPeriod");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            dashboard.CurrentUserCustomId = currentUser?.CustomId > 0
                ? currentUser.CustomId.ToString()
                : null;

            return View(dashboard);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegeneratePdf(int periodId)
        {
            await _pdfService.GenerateShiftPdfAsync(periodId);
            TempData["SuccessMessage"] = "PDFを再生成しました。";

            return RedirectToAction("Index", new { periodId });
        }

        [HttpGet]
        public async Task<IActionResult> ShiftEditLogs(
            int? periodId,
            string? targetUserId,
            string? adminUserId,
            DateTime? editedFrom,
            DateTime? editedTo,
            bool onlyChanged = false,
            bool onlyCurrentDiff = false)
        {
            var model = await _adminShiftEditService.BuildLogsAsync(
                periodId,
                targetUserId,
                adminUserId,
                editedFrom,
                editedTo,
                onlyChanged,
                onlyCurrentDiff);
            return View(model);
        }

        [HttpGet]///※１
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            var model = await _adminSubmissionPeriodService.BuildDefaultPeriodModelAsync();
            return View(model);
        }

        [HttpPost]///※１
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRecruitmentPeriod(RecruitmentPeriodViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _adminSubmissionPeriodService.CreatePeriodAsync(model);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditShifts(int? periodId)
        {
            var model = await _adminShiftEditService.BuildPageAsync(periodId);
            if (model == null)
            {
                TempData["Error"] = "募集期間が見つかりませんでした。";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShifts([FromBody] ShiftUpdateRequest request, [FromQuery] int periodId)
        {
            try
            {
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminUserId))
                    return Json(new { success = false, error = "管理者のユーザーIDが取得できませんでした。" });

                var result = await _adminShiftEditService.UpdateShiftsAsync(
                    request,
                    periodId,
                    adminUserId,
                    DateTime.UtcNow);
                if (!result.Success)
                    return Json(new { success = false, error = result.ErrorMessage });

                await _pdfService.GenerateShiftPdfAsync(periodId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.InnerException?.Message ?? ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSubmissionStatus(int id)
        {
            var toggled = await _adminSubmissionPeriodService.ToggleSubmissionStatusAsync(id, DateTime.UtcNow);
            if (!toggled)
            {
                return NotFound();
            }

            return RedirectToAction("Index", "Admin");
        }

        [HttpGet]
        public async Task<IActionResult> ManageSubmissionPeriods()
        {
            var model = await _adminSubmissionPeriodService.BuildPeriodListAsync();
            return View(model);
        }

        [HttpGet]///※１
        public async Task<IActionResult> ViewDailyWorkload(int? periodId)
        {
            var model = await _adminSubmissionPeriodService.BuildDailyWorkloadAsync(periodId);
            if (model == null)
            {
                TempData["Error"] = "募集期間が存在しません。";
                return RedirectToAction("Index");
            }

            return View("DailyWorkload", model);
        }

        [HttpGet]///※１
        public async Task<IActionResult> EditDailyWorkload(int? periodId)
        {
            var model = await _adminSubmissionPeriodService.BuildDailyWorkloadAsync(periodId);
            if (model == null)
            {
                TempData["Error"] = "募集期間が見つかりませんでした。";
                return RedirectToAction("Index");
            }

            return View("DailyWorkload", model);
        }

        [HttpPost]///※１
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDailyWorkload(int periodId, Dictionary<string, int> inputCounts, string redirectTo)
        {
            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var shiftDayMap = shiftDays.ToDictionary(d => d.Date.Date, d => d);
            var existing = await _context.DailyWorkloads
                .Where(w => shiftDayMap.Values.Select(d => d.Id).Contains(w.ShiftDayId))
                .ToListAsync();

            foreach (var entry in inputCounts)
            {
                if (!DateTime.TryParse(entry.Key, out var parsedDate)) continue;

                var dateOnly = parsedDate.Date;

                if (!shiftDayMap.TryGetValue(dateOnly, out var shiftDay)) continue;

                var workload = existing.FirstOrDefault(w => w.ShiftDayId == shiftDay.Id);
                if (workload == null)
                {
                    workload = new DailyWorkload
                    {
                        ShiftDayId = shiftDay.Id
                    };
                    _context.DailyWorkloads.Add(workload);
                }

                workload.RequiredCount = entry.Value;
                workload.RequiredWorkers = DailyWorkload.CalculateRequiredWorkers(entry.Value);
            }

            await _context.SaveChangesAsync();
            await _pdfService.GenerateShiftPdfAsync(periodId);
            TempData["Message"] = "保存しました。";

            return redirectTo == "view"
                ? RedirectToAction("ViewDailyWorkload", new { periodId })
                : RedirectToAction("EditDailyWorkload", new { periodId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoAssignShifts(int periodId)
        {
            var result = await _autoShiftAssignmentService.AssignAsync(periodId, DateTime.UtcNow);
            TempData["SuccessMessage"] = "シフトの自動割り当てが完了しました。";
            TempData["AutoAssignSummary"] =
                $"対象 {result.ShiftCellCount}枠 / 必要 {result.RequiredWorkerTotal}人 / 割当 {result.AssignedCount}人 / 鍵持ち {result.KeyHolderAssignedCount}人 / 人数不足 {result.WorkerShortageSlots}人 / 鍵持ち不足 {result.KeyHolderShortageSlots}人";
            if (result.KeyHolderShortages.Any() || result.WorkerShortages.Any())
            {
                var messages = new List<string>();
                if (result.KeyHolderShortages.Any())
                {
                    messages.Add("鍵持ち不足: " + string.Join("、", result.KeyHolderShortages.Take(8).Select(s => s.ToDisplayText())));
                }

                if (result.WorkerShortages.Any())
                {
                    messages.Add("人数不足: " + string.Join("、", result.WorkerShortages.Take(8).Select(s => s.ToDisplayText())));
                }

                TempData["WarningMessage"] = string.Join(" / ", messages);
            }

            await _pdfService.GenerateShiftPdfAsync(periodId);
            return RedirectToAction("Index", new { periodId });
        }

    }
}
