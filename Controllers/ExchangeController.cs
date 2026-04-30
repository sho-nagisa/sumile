using Microsoft.AspNetCore.Mvc;
using sumile.Models;
using sumile.Services;

public class ExchangeController : Controller
{
    private readonly ShiftPdfService _pdfService;
    private readonly ShiftExchangeWorkflowService _exchangeWorkflowService;
    private readonly ExchangePageService _exchangePageService;

    public ExchangeController(
        ShiftPdfService pdfService,
        ShiftExchangeWorkflowService exchangeWorkflowService,
        ExchangePageService exchangePageService)
    {
        _pdfService = pdfService;
        _exchangeWorkflowService = exchangeWorkflowService;
        _exchangePageService = exchangePageService;
    }

    public async Task<IActionResult> Create()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var model = await _exchangePageService.BuildCreateAsync(userId);
        ViewBag.ShiftsByPeriod = model.ShiftsByPeriod;
        ViewBag.TargetUsers = model.TargetUsers;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int offeredShiftSubmissionId, int shiftDayId, ShiftType shiftType, string? targetUserId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var result = await _exchangeWorkflowService.CreateRequestAsync(
            offeredShiftSubmissionId,
            shiftDayId,
            shiftType,
            userId,
            targetUserId,
            DateTime.UtcNow);

        TempData["Message"] = result.Message;
        return RedirectToAction(result.Success ? nameof(Index) : nameof(Create));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(int id)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var result = await _exchangeWorkflowService.ApplyAsync(id, userId, DateTime.UtcNow);
        if (result.NotFound) return NotFound(result.Message);
        if (!result.Success) return BadRequest(result.Message);

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequest(int id)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var result = await _exchangeWorkflowService.CancelRequestAsync(id, userId, DateTime.UtcNow);
        if (result.NotFound) return NotFound(result.Message);
        if (result.Forbidden) return Forbid();
        if (!result.Success) return BadRequest(result.Message);

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelApplication(int id)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var result = await _exchangeWorkflowService.CancelApplicationAsync(id, userId, DateTime.UtcNow);
        if (result.NotFound) return NotFound(result.Message);
        if (!result.Success) return BadRequest(result.Message);

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExchange(int exchangeId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");
        if (!await _exchangePageService.IsAdminUserAsync(userId)) return Unauthorized();

        var result = await _exchangeWorkflowService.RejectExchangeAsync(exchangeId, DateTime.UtcNow);
        if (result.NotFound) return NotFound(result.Message);
        if (!result.Success) return BadRequest(result.Message);

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeExchange(int exchangeId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");
        if (!await _exchangePageService.IsAdminUserAsync(userId)) return Unauthorized();

        var result = await _exchangeWorkflowService.FinalizeAsync(exchangeId, userId, DateTime.UtcNow);
        if (result.NotFound) return NotFound(result.Message);
        if (!result.Success) return BadRequest(result.Message);

        TempData["Message"] = result.Message;
        await _pdfService.GenerateShiftPdfAsync(result.RecruitmentPeriodId!.Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<JsonResult> GetShiftStatusInfo(DateTime date, ShiftType shiftType, int periodId)
    {
        var statusInfo = await _exchangePageService.GetShiftStatusInfoAsync(date, shiftType, periodId);
        return Json(statusInfo);
    }

    public async Task<IActionResult> Index(bool relatedOnly = false)
    {
        var currentUserId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(currentUserId)) return RedirectToAction("Login", "Account");

        var model = await _exchangePageService.BuildIndexAsync(currentUserId, relatedOnly);
        if (model == null) return RedirectToAction("Login", "Account");

        ViewBag.CurrentUserId = model.CurrentUserId;
        ViewBag.CurrentUserRole = model.CurrentUserRole;
        ViewBag.IsAdmin = model.IsAdmin;
        ViewBag.RelatedOnly = model.RelatedOnly;

        return View(model.Exchanges);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, int yourShiftSubmissionId)
    {
        return await Select(id);
    }
}
