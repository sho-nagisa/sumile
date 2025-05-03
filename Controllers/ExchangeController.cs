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
using Microsoft.AspNetCore.Identity;

public class ExchangeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ShiftPdfService _pdfService;

    public ExchangeController(ApplicationDbContext context, ShiftPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int offeredShiftSubmissionId, int shiftDayId, ShiftType shiftType)
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

        var alreadyExists = await _context.ShiftExchanges
            .AnyAsync(e => e.OfferedShiftSubmissionId == offeredShiftSubmissionId && e.Status == "Open");
        if (alreadyExists)
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

        if (exchange == null || exchange.Status != "Open" || exchange.AcceptedShiftSubmissionId != null)
            return NotFound("募集が見つからないか、すでに成立しています。");

        if (exchange.RequestedByUserId == userId)
            return BadRequest("自分の募集には応募できません。");

        var offered = exchange.OfferedShiftSubmission;
        var now = DateTime.UtcNow;

        // ✅ 【チェックコード】ShiftDay と RecruitmentPeriod の存在を確認
        var shiftDay = await _context.ShiftDays
            .Include(d => d.RecruitmentPeriod)
            .FirstOrDefaultAsync(d => d.Id == offered.ShiftDayId);

        if (shiftDay == null)
        {

            throw new Exception($"❌ ShiftDay が存在しません (ShiftDayId = {offered.ShiftDayId})");
        }

        if (shiftDay.RecruitmentPeriod == null)
        {

            throw new Exception($"❌ RecruitmentPeriod が存在しません (ShiftDayId = {shiftDay.Id})");
        }

        var log1 = new ShiftEditLog
        {
            AdminUserId = exchange.RequestedByUserId,
            TargetUserId = offered.UserId,
            ShiftDayId = offered.ShiftDayId,
            ShiftType = offered.ShiftType,
            OldState = offered.ShiftStatus,
            NewState = ShiftState.None,
            EditDate = now,
            Note = "交換による投稿者シフト削除"
        };

        var user = await _context.Users.FindAsync(userId);
        var acceptedSubmission = new ShiftSubmission
        {
            UserId = userId,
            ShiftDayId = offered.ShiftDayId,
            ShiftType = offered.ShiftType,
            IsSelected = true,
            SubmittedAt = now,
            ShiftStatus = ShiftState.Accepted,
            UserType = UserType.Normal,
            UserShiftRole = user?.UserShiftRole ?? UserShiftRole.Normal
        };

        _context.ShiftSubmissions.Add(acceptedSubmission);
        await _context.SaveChangesAsync(); // IDを確定

        var log2 = new ShiftEditLog
        {
            AdminUserId = userId,
            TargetUserId = userId,
            ShiftDayId = offered.ShiftDayId,
            ShiftType = offered.ShiftType,
            OldState = ShiftState.None,
            NewState = ShiftState.Accepted,
            EditDate = now,
            Note = "交換による応募者シフト取得"
        };

        _context.ShiftSubmissions.Remove(offered);
        _context.ShiftEditLogs.AddRange(log1, log2);

        // トラッキングエラーを避けるため AcceptedShiftSubmissionId をIDで紐付け
        exchange.AcceptedByUserId = userId;
        exchange.AcceptedShiftSubmissionId = acceptedSubmission.Id;
        exchange.AcceptedAt = now;
        exchange.UpdatedAt = now;
        exchange.Status = "Accepted";
        _context.ShiftExchanges.Update(exchange);

        await _context.SaveChangesAsync();

        TempData["Message"] = "交換が成立しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeExchange(int exchangeId)
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
                    .ThenInclude(d => d.RecruitmentPeriod)
            .FirstOrDefaultAsync(e => e.Id == exchangeId);

        var accepted = await _context.ShiftSubmissions
            .Include(s => s.ShiftDay)
                .ThenInclude(d => d.RecruitmentPeriod)
            .FirstOrDefaultAsync(s => s.Id == exchange.AcceptedShiftSubmissionId);

        var offered = exchange.OfferedShiftSubmission;
        if (offered == null || accepted == null)
            return BadRequest("シフト情報が不完全です。");

        // ✅ チェック1：ShiftDayがnullでないか
        if (accepted.ShiftDay == null)
            throw new Exception("❌ accepted.ShiftDay が NULL");

        // ✅ チェック2：RecruitmentPeriodがnullでないか
        if (accepted.ShiftDay.RecruitmentPeriod == null)
            throw new Exception($"❌ accepted.ShiftDay に紐づく RecruitmentPeriod が存在しません (ShiftDayId = {accepted.ShiftDay.Id})");

        // ✅ チェック3：RecruitmentPeriodId が DB に存在するか
        var recruitmentPeriodId = accepted.ShiftDay.RecruitmentPeriodId;
        var exists = await _context.RecruitmentPeriods.AnyAsync(r => r.Id == recruitmentPeriodId);
        if (!exists)
            throw new Exception($"❌ RecruitmentPeriodId = {recruitmentPeriodId} が DB に存在しません");

        var now = DateTime.UtcNow;

        var log1 = new ShiftEditLog
        {
            AdminUserId = exchange.RequestedByUserId,
            TargetUserId = offered.UserId,
            ShiftDayId = offered.ShiftDayId,
            ShiftType = offered.ShiftType,
            OldState = offered.ShiftStatus,
            NewState = ShiftState.None,
            EditDate = now,
            Note = "交換確定：投稿者シフト削除"
        };

        var oldAccepted = accepted.ShiftStatus;
        accepted.ShiftStatus = ShiftState.Accepted;
        accepted.SubmittedAt = now;

        var log2 = new ShiftEditLog
        {
            AdminUserId = exchange.AcceptedByUserId,
            TargetUserId = accepted.UserId,
            ShiftDayId = accepted.ShiftDayId,
            ShiftType = accepted.ShiftType,
            OldState = oldAccepted,
            NewState = ShiftState.Accepted,
            EditDate = now,
            Note = "交換確定：応募者シフトをAcceptedへ更新"
        };

        _context.ShiftSubmissions.Remove(offered);
        _context.ShiftSubmissions.Update(accepted);
        _context.ShiftEditLogs.AddRange(log1, log2);
        await _context.SaveChangesAsync();

        TempData["Message"] = "交換が確定されました。";

        // ✅ PDF出力前に RecruitmentPeriodId が有効か確認済み
        await _pdfService.GenerateShiftPdfAsync(accepted.ShiftDay.RecruitmentPeriodId);
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
        var exchanges = await _context.ShiftExchanges
            .Include(e => e.RequestedByUser)
            .Include(e => e.AcceptedByUser)
            .Include(e => e.OfferedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
            .Include(e => e.AcceptedShiftSubmission)
                .ThenInclude(s => s.ShiftDay)
            .ToListAsync();

        var currentUserId = HttpContext.Session.GetString("UserId");
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
        ViewBag.CurrentUserRole = currentUser?.UserShiftRole.ToString() ?? "Normal";

        return View(exchanges);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, int yourShiftSubmissionId)
    {
        var userId = HttpContext.Session.GetString("UserId");

        var exchange = await _context.ShiftExchanges
            .Include(e => e.OfferedShiftSubmission)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exchange == null || exchange.AcceptedShiftSubmissionId != null || exchange.Status != "Open")
            return NotFound();

        if (exchange.RequestedByUserId == userId)
            return BadRequest("自分の交換リクエストには応募できません。");

        exchange.AcceptedShiftSubmissionId = yourShiftSubmissionId;
        exchange.AcceptedByUserId = userId;
        exchange.AcceptedAt = DateTime.UtcNow;
        exchange.UpdatedAt = DateTime.UtcNow;
        exchange.Status = "Accepted";

        _context.ShiftExchanges.Update(exchange);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
