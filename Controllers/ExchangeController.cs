using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using sumile.Services;

public class ExchangeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ShiftPdfService _pdfService;

    public ExchangeController(ApplicationDbContext context, ShiftPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    private static bool IsOpenStatus(string status) =>
        status == ShiftExchange.StatusOpen;

    private static bool IsPendingApprovalStatus(string status) =>
        status == ShiftExchange.StatusPendingApproval ||
        status == ShiftExchange.StatusAcceptedLegacy;

    private async Task<bool> IsAdminUser(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        return await _context.Users.AnyAsync(u => u.Id == userId && u.IsAdmin);
    }

    public async Task<IActionResult> Create()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var userShifts = await _context.ShiftSubmissions
            .Include(s => s.ShiftDay)
                .ThenInclude(d => d.RecruitmentPeriod)
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var shiftsByPeriod = userShifts
            .GroupBy(s => s.ShiftDay.RecruitmentPeriod)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.ShiftsByPeriod = shiftsByPeriod;
        ViewBag.TargetUsers = await _context.Users
            .Where(u => u.Id != userId)
            .OrderBy(u => u.CustomId)
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int offeredShiftSubmissionId, int shiftDayId, ShiftType shiftType, string? targetUserId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var submission = await _context.ShiftSubmissions
            .Include(s => s.ShiftDay)
                .ThenInclude(d => d.RecruitmentPeriod)
            .FirstOrDefaultAsync(s =>
                s.Id == offeredShiftSubmissionId &&
                s.ShiftDayId == shiftDayId &&
                s.ShiftType == shiftType &&
                s.UserId == userId);

        if (submission == null) return BadRequest("無効なシフトが選択されました。");

        if (!string.IsNullOrEmpty(targetUserId))
        {
            if (targetUserId == userId)
            {
                TempData["Message"] = "自分自身だけを表示先にはできません。";
                return RedirectToAction(nameof(Create));
            }

            var targetExists = await _context.Users.AnyAsync(u => u.Id == targetUserId);
            if (!targetExists) return BadRequest("表示先ユーザーが見つかりません。");
        }

        var alreadyExists = await _context.ShiftExchanges
            .AnyAsync(e =>
                e.OfferedShiftSubmissionId == offeredShiftSubmissionId &&
                (e.Status == ShiftExchange.StatusOpen ||
                 e.Status == ShiftExchange.StatusPendingApproval ||
                 e.Status == ShiftExchange.StatusAcceptedLegacy));
        if (alreadyExists)
        {
            TempData["Message"] = "このシフトはすでに交換募集済みです。";
            return RedirectToAction(nameof(Index));
        }

        var exchange = new ShiftExchange
        {
            RequestedByUserId = userId,
            TargetUserId = string.IsNullOrEmpty(targetUserId) ? null : targetUserId,
            OfferedShiftSubmissionId = offeredShiftSubmissionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ShiftExchange.StatusOpen
        };

        _context.ShiftExchanges.Add(exchange);
        await _context.SaveChangesAsync();

        TempData["Message"] = "交換希望を登録しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(int id)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
            .Include(e => e.OfferedShiftSubmission.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exchange == null || !IsOpenStatus(exchange.Status))
            return NotFound("募集が見つからないか、すでに応募済みです。");

        if (exchange.RequestedByUserId == userId)
            return BadRequest("自分の募集には応募できません。");

        if (!string.IsNullOrEmpty(exchange.TargetUserId) && exchange.TargetUserId != userId)
            return BadRequest("この交換募集は指定されたユーザーだけが応募できます。");

        exchange.AcceptedByUserId = userId;
        exchange.AcceptedAt = DateTime.UtcNow;
        exchange.UpdatedAt = DateTime.UtcNow;
        exchange.Status = ShiftExchange.StatusPendingApproval;
        _context.ShiftExchanges.Update(exchange);

        await _context.SaveChangesAsync();

        TempData["Message"] = "応募しました。管理者の承認待ちです。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeExchange(int exchangeId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");
        if (!await IsAdminUser(userId)) return Unauthorized();

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
                    .ThenInclude(d => d.RecruitmentPeriod)
            .Include(e => e.AcceptedByUser)
            .FirstOrDefaultAsync(e => e.Id == exchangeId);

        if (exchange == null || !IsPendingApprovalStatus(exchange.Status))
            return NotFound("承認待ちの交換が見つかりません。");

        if (string.IsNullOrEmpty(exchange.AcceptedByUserId))
            return BadRequest("応募者が設定されていません。");

        var offered = exchange.OfferedShiftSubmission;
        if (offered == null)
            return BadRequest("シフト情報が不完全です。");

        if (offered.ShiftDay == null)
            throw new Exception("❌ offered.ShiftDay が NULL");

        if (offered.ShiftDay.RecruitmentPeriod == null)
            throw new Exception($"❌ offered.ShiftDay に紐づく RecruitmentPeriod が存在しません (ShiftDayId = {offered.ShiftDay.Id})");

        var recruitmentPeriodId = offered.ShiftDay.RecruitmentPeriodId;
        var exists = await _context.RecruitmentPeriods.AnyAsync(r => r.Id == recruitmentPeriodId);
        if (!exists)
            throw new Exception($"❌ RecruitmentPeriodId = {recruitmentPeriodId} が DB に存在しません");

        var now = DateTime.UtcNow;
        var oldOfferedState = offered.ShiftStatus;

        var log1 = new ShiftEditLog
        {
            AdminUserId = userId,
            TargetUserId = offered.UserId,
            ShiftDayId = offered.ShiftDayId,
            ShiftType = offered.ShiftType,
            OldState = oldOfferedState,
            NewState = ShiftState.NotAccepted,
            EditDate = now,
            Note = "交換確定：譲渡元を不採用へ変更"
        };

        offered.ShiftStatus = ShiftState.NotAccepted;
        offered.IsSelected = false;
        offered.SubmittedAt = now;
        offered.UserType = UserType.AdminUpdated;

        var accepted = await _context.ShiftSubmissions
            .FirstOrDefaultAsync(s =>
                s.UserId == exchange.AcceptedByUserId &&
                s.ShiftDayId == offered.ShiftDayId &&
                s.ShiftType == offered.ShiftType);

        var oldAccepted = accepted?.ShiftStatus ?? ShiftState.None;
        var isNewAcceptedSubmission = accepted == null;
        if (accepted == null)
        {
            accepted = new ShiftSubmission
            {
                UserId = exchange.AcceptedByUserId,
                ShiftDayId = offered.ShiftDayId,
                ShiftType = offered.ShiftType,
                UserShiftRole = exchange.AcceptedByUser?.UserShiftRole ?? UserShiftRole.Normal
            };
            _context.ShiftSubmissions.Add(accepted);
        }

        accepted.IsSelected = true;
        accepted.SubmittedAt = now;
        accepted.ShiftStatus = ShiftState.Accepted;
        accepted.UserType = UserType.AdminUpdated;
        accepted.UserShiftRole = exchange.AcceptedByUser?.UserShiftRole ?? accepted.UserShiftRole;

        var log2 = new ShiftEditLog
        {
            AdminUserId = userId,
            TargetUserId = accepted.UserId,
            ShiftDayId = accepted.ShiftDayId,
            ShiftType = accepted.ShiftType,
            OldState = oldAccepted,
            NewState = ShiftState.Accepted,
            EditDate = now,
            Note = "交換確定：応募者へシフトを付与"
        };

        _context.ShiftSubmissions.Update(offered);
        if (!isNewAcceptedSubmission)
        {
            _context.ShiftSubmissions.Update(accepted);
        }
        _context.ShiftEditLogs.AddRange(log1, log2);

        exchange.AcceptedShiftSubmission = accepted;
        exchange.UpdatedAt = now;
        exchange.Status = ShiftExchange.StatusFinalized;
        _context.ShiftExchanges.Update(exchange);

        await _context.SaveChangesAsync();

        TempData["Message"] = "交換が確定されました。";

        await _pdfService.GenerateShiftPdfAsync(recruitmentPeriodId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<JsonResult> GetShiftStatusInfo(DateTime date, ShiftType shiftType, int periodId)
    {
        var shiftDay = await _context.ShiftDays
            .FirstOrDefaultAsync(d => d.Date == date && d.RecruitmentPeriodId == periodId);

        if (shiftDay == null)
            return Json(new { redCount = 0, blackCount = 0, total = 0 });

        var submissions = await _context.ShiftSubmissions
            .Where(s => s.ShiftDayId == shiftDay.Id && s.ShiftType == shiftType)
            .ToListAsync();

        var redCount = submissions.Count(s => s.UserShiftRole == UserShiftRole.KeyHolder);
        var blackCount = submissions.Count(s => s.UserShiftRole != UserShiftRole.KeyHolder);
        var total = redCount + blackCount;

        return Json(new { redCount, blackCount, total });
    }

    public async Task<IActionResult> Index()
    {
        var currentUserId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(currentUserId)) return RedirectToAction("Login", "Account");

        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
        var isAdmin = currentUser?.IsAdmin ?? false;

        var query = _context.ShiftExchanges
            .Include(e => e.RequestedByUser)
            .Include(e => e.AcceptedByUser)
            .Include(e => e.TargetUser)
            .Include(e => e.OfferedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
            .Include(e => e.AcceptedShiftSubmission)
                .ThenInclude(s => s!.ShiftDay)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(e =>
                e.TargetUserId == null ||
                e.TargetUserId == currentUserId ||
                e.RequestedByUserId == currentUserId ||
                e.AcceptedByUserId == currentUserId);
        }

        var exchanges = await query
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync();

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentUserRole = currentUser?.UserShiftRole.ToString() ?? "Normal";
        ViewBag.IsAdmin = isAdmin;

        return View(exchanges);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, int yourShiftSubmissionId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exchange == null || !IsOpenStatus(exchange.Status))
            return NotFound();

        if (exchange.RequestedByUserId == userId)
            return BadRequest("自分の交換リクエストには応募できません。");

        if (!string.IsNullOrEmpty(exchange.TargetUserId) && exchange.TargetUserId != userId)
            return BadRequest("この交換募集は指定されたユーザーだけが応募できます。");

        exchange.AcceptedByUserId = userId;
        exchange.AcceptedAt = DateTime.UtcNow;
        exchange.UpdatedAt = DateTime.UtcNow;
        exchange.Status = ShiftExchange.StatusPendingApproval;

        _context.ShiftExchanges.Update(exchange);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
