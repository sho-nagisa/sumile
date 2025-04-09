using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class ExchangeController : Controller
{
    private readonly ApplicationDbContext _context;

    public ExchangeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // 📌 交換希望作成画面（GET）
    public async Task<IActionResult> Create()
    {
        var userId = HttpContext.Session.GetString("UserId");

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var userShifts = await _context.ShiftSubmissions
            .Include(s => s.RecruitmentPeriod)
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var shiftsByPeriod = userShifts
            .GroupBy(s => s.RecruitmentPeriod)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.ShiftsByPeriod = shiftsByPeriod;

        return View();
    }

    // 📌 交換希望作成（POST）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int offeredShiftSubmissionId)
    {
        var userId = HttpContext.Session.GetString("UserId");

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        // 自分のシフトか確認
        var submission = await _context.ShiftSubmissions
            .FirstOrDefaultAsync(s => s.Id == offeredShiftSubmissionId && s.UserId == userId);

        if (submission == null)
        {
            return BadRequest("無効なシフトが選択されました。");
        }

        // 既にこのシフトで募集してないかチェック
        var existing = await _context.ShiftExchanges
            .AnyAsync(e => e.OfferedShiftSubmissionId == offeredShiftSubmissionId && e.Status == "Open");

        if (existing)
        {
            TempData["Message"] = "このシフトはすでに交換募集済みです。";
            return RedirectToAction(nameof(Index));
        }

        var exchange = new ShiftExchange
        {
            RequestedByUserId = userId,
            OfferedShiftSubmissionId = offeredShiftSubmissionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = "Open"
        };

        _context.ShiftExchanges.Add(exchange);
        await _context.SaveChangesAsync();

        TempData["Message"] = "交換希望を登録しました。";
        return RedirectToAction(nameof(Index));
    }

    // ExchangeController の Select および FinalizeExchange アクションの修正版

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(int id)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exchange == null || exchange.Status != "Open" || exchange.AcceptedShiftSubmissionId != null)
            return NotFound("募集が見つからないか、すでに成立しています。");

        if (exchange.RequestedByUserId == userId)
            return BadRequest("自分の募集には応募できません。");

        var offered = exchange.OfferedShiftSubmission;
        if (offered == null)
            return BadRequest("対象シフトが不正です。");

        var now = DateTime.UtcNow;

        // 投稿者のシフト削除ログ
        var log1 = new ShiftEditLog
        {
            AdminUserId = exchange.RequestedByUserId,
            TargetUserId = offered.UserId,
            ShiftDate = offered.Date,
            ShiftType = offered.ShiftType,
            OldState = offered.ShiftStatus,
            NewState = ShiftState.None,
            EditDate = now,
            Note = "交換による投稿者シフト削除",
            RecruitmentPeriodId = offered.RecruitmentPeriodId
        };

        // 応募者の既存シフトを取得（この時点では未登録なので新規作成）
        var user = await _context.Users.FindAsync(userId);
        var acceptedSubmission = new ShiftSubmission
        {
            UserId = userId,
            Date = offered.Date,
            ShiftType = offered.ShiftType,
            IsSelected = true,
            SubmittedAt = now,
            ShiftStatus = ShiftState.Accepted,
            UserType = UserType.Normal,
            UserShiftRole = user?.UserShiftRole ?? UserShiftRole.Normal,
            RecruitmentPeriodId = offered.RecruitmentPeriodId
        };

        var log2 = new ShiftEditLog
        {
            AdminUserId = userId,
            TargetUserId = userId,
            ShiftDate = acceptedSubmission.Date,
            ShiftType = acceptedSubmission.ShiftType,
            OldState = ShiftState.None,
            NewState = ShiftState.Accepted,
            EditDate = now,
            Note = "交換による応募者シフト取得",
            RecruitmentPeriodId = acceptedSubmission.RecruitmentPeriodId
        };

        _context.ShiftSubmissions.Remove(offered);
        _context.ShiftSubmissions.Add(acceptedSubmission);

        exchange.AcceptedByUserId = userId;
        exchange.AcceptedShiftSubmission = acceptedSubmission;
        exchange.AcceptedAt = now;
        exchange.UpdatedAt = now;
        exchange.Status = "Accepted";

        _context.ShiftEditLogs.AddRange(log1, log2);
        _context.ShiftExchanges.Update(exchange);
        await _context.SaveChangesAsync();

        TempData["Message"] = "交換が成立しました。投稿者のシフトは削除され、応募者のシフトが追加されました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeExchange(int exchangeId)
    {
        var userId = HttpContext.Session.GetString("UserId");

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
            .FirstOrDefaultAsync(e => e.Id == exchangeId);

        if (exchange == null || exchange.Status != "Accepted")
            return NotFound("交換が成立していない、または無効です。");

        var offered = exchange.OfferedShiftSubmission;
        var accepted = await _context.ShiftSubmissions
            .FirstOrDefaultAsync(s => s.Id == exchange.AcceptedShiftSubmissionId);

        if (offered == null || accepted == null)
            return BadRequest("シフト情報が不完全です。");

        var now = DateTime.UtcNow;

        var log1 = new ShiftEditLog
        {
            AdminUserId = exchange.RequestedByUserId,
            TargetUserId = offered.UserId,
            ShiftDate = offered.Date,
            ShiftType = offered.ShiftType,
            OldState = offered.ShiftStatus,
            NewState = ShiftState.None,
            EditDate = now,
            Note = "交換確定：投稿者シフト削除",
            RecruitmentPeriodId = offered.RecruitmentPeriodId
        };

        var oldAccepted = accepted.ShiftStatus;
        accepted.ShiftStatus = ShiftState.Accepted;
        accepted.SubmittedAt = now;

        var log2 = new ShiftEditLog
        {
            AdminUserId = exchange.AcceptedByUserId,
            TargetUserId = accepted.UserId,
            ShiftDate = accepted.Date,
            ShiftType = accepted.ShiftType,
            OldState = oldAccepted,
            NewState = ShiftState.Accepted,
            EditDate = now,
            Note = "交換確定：応募者シフトをAcceptedへ更新",
            RecruitmentPeriodId = accepted.RecruitmentPeriodId
        };

        _context.ShiftSubmissions.Remove(offered);
        _context.ShiftSubmissions.Update(accepted);
        _context.ShiftEditLogs.AddRange(log1, log2);
        await _context.SaveChangesAsync();

        TempData["Message"] = "交換が確定され、投稿者のシフトが削除され、応募者のシフトがAcceptedに更新されました。";
        return RedirectToAction(nameof(Index));
    }
    // ExchangeController.cs に追加

    [HttpGet]
    public async Task<JsonResult> GetShiftStatusInfo(DateTime date, ShiftType shiftType, int periodId)
    {
        var submissions = await _context.ShiftSubmissions
            .Where(s => s.Date == date && s.ShiftType == shiftType && s.RecruitmentPeriodId == periodId)
            .ToListAsync();

        var redCount = submissions.Count(s => s.UserShiftRole == UserShiftRole.KeyHolder);
        var blackCount = submissions.Count(s => s.UserShiftRole != UserShiftRole.KeyHolder);
        var total = redCount + blackCount;

        return Json(new { redCount, blackCount, total });
    }

    public async Task<IActionResult> Index()
    {
        var exchanges = await _context.ShiftExchanges
            .Include(e => e.RequestedByUser)
            .Include(e => e.AcceptedByUser)
            .Include(e => e.OfferedShiftSubmission)
            .Include(e => e.AcceptedShiftSubmission)
            .ToListAsync();

        var currentUserId = HttpContext.Session.GetString("UserId");
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
        ViewBag.CurrentUserRole = currentUser?.UserShiftRole.ToString() ?? "Normal";

        return View(exchanges);
    }
    // 📌 応募（成立）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, int yourShiftSubmissionId)
    {
        var userId = HttpContext.Session.GetString("UserId");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exchange == null || exchange.AcceptedShiftSubmissionId != null || exchange.Status != "Open")
        {
            return NotFound();
        }

        if (exchange.RequestedByUserId == userId)
        {
            return BadRequest("自分の交換リクエストには応募できません。");
        }

        exchange.AcceptedShiftSubmissionId = yourShiftSubmissionId;
        exchange.AcceptedByUserId = userId;
        exchange.AcceptedAt = DateTime.UtcNow;   // ← 修正
        exchange.UpdatedAt = DateTime.UtcNow;    // ← 修正
        exchange.Status = "Accepted";

        _context.ShiftExchanges.Update(exchange);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
