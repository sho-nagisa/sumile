using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sumile.Controllers
{
    [Authorize]
    public class ShiftController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ShiftPdfService _pdfService;
        private readonly ShiftTableService _shiftTableService;

        public ShiftController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ShiftPdfService pdfService,
        ShiftTableService shiftTableService)
        {
            _context = context;
            _userManager = userManager;
            _pdfService = pdfService;
            _shiftTableService = shiftTableService;
        }

        private async Task<List<ShiftDay>> GetShiftDaysForPeriod(int? periodId)
        {
            if (!periodId.HasValue) return new List<ShiftDay>();
            return await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId.Value)
                .OrderBy(d => d.Date)
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");
            ViewBag.CurrentUserCustomId = currentUser.CustomId > 0 ?
                currentUser.CustomId.ToString() :
                 "No user";
            // 募集期間（View 用）
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            // ユーザー一覧（View 用）
            ViewBag.Users = await _context.Users
                .OrderBy(u => u.CustomId)
                .Select(u => new
                {
                    u.Id,
                    u.CustomId,
                    u.Name,
                    u.UserShiftRole
                })
                .ToListAsync();

            // ===== ★ ここから Service 利用 =====
            var table = await _shiftTableService.BuildAsync(periodId);
            // ===== ViewBag =====
            // =====service からのデータ=====
            ViewBag.Dates = table.ShiftDays;
            ViewBag.Submissions = table.Submissions;
            ViewBag.Workloads = table.Workloads;
            ViewBag.TotalAcceptedList = table.TotalAcceptedList;
            ViewBag.KeyHolderAcceptedList = table.KeyHolderAcceptedList;
            ViewBag.RemainingWorkersList = table.RemainingWorkersList;

            // ===== その他 View 用データ =====
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Submission(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var openPeriods = await _context.RecruitmentPeriods
                .Where(p => p.IsOpen)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            ViewBag.Periods = openPeriods;

            if (!periodId.HasValue && openPeriods.Any())
                periodId = openPeriods.First().Id;

            ViewBag.SelectedPeriodId = periodId;

            var shiftDays = await GetShiftDaysForPeriod(periodId);
            ViewBag.Dates = shiftDays;

            var userId = currentUser.Id;
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var existingSubmissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == userId && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            ViewBag.ExistingSubmissions = existingSubmissions;
            ViewBag.CurrentUserCustomId = currentUser.CustomId > 0 ? currentUser.CustomId.ToString() : "No user";
            ViewBag.CurrentUserName = string.IsNullOrEmpty(currentUser.Name) ? "No user" : currentUser.Name;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitShifts([FromForm] string selectedShifts,[FromForm] int periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var userId = currentUser.Id;
            var userTypeStr = HttpContext.Session.GetString("UserType") ?? "Normal";
            UserType userType = Enum.TryParse(userTypeStr, out UserType ut) ? ut : UserType.Normal;
            var userShiftRole = currentUser.UserShiftRole;

            // 対象期間の ShiftDay を全取得
            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            // View から送られてきた選択データ
            var selectedList = string.IsNullOrEmpty(selectedShifts)
                ? new List<ShiftSubmissionViewModel>()
                : JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts)
                ?? new List<ShiftSubmissionViewModel>();

            // 既存データは一旦削除（この期間・このユーザー）
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var existing = await _context.ShiftSubmissions
                .Where(s => s.UserId == userId && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            _context.ShiftSubmissions.RemoveRange(existing);

            var submissions = new List<ShiftSubmission>();

            foreach (var day in shiftDays)
            {
                foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
                {
                    // View から該当セルが送られてきているか
                    var selected = selectedList.FirstOrDefault(s =>
                        DateTime.Parse(s.Date).Date == day.Date.Date &&
                        s.ShiftType == shiftType);

                    ShiftState status = selected?.ShiftSymbol switch
                    {
                        "〇" => ShiftState.Accepted,
                        "△" => ShiftState.WantToGiveAway,
                        _   => ShiftState.None   // ← ★ 未選択は必ず None
                    };

                    submissions.Add(new ShiftSubmission
                    {
                        UserId = userId,
                        ShiftDayId = day.Id,
                        ShiftType = shiftType,
                        ShiftStatus = status,
                        IsSelected = status != ShiftState.None,
                        SubmittedAt = DateTime.UtcNow,
                        UserType = userType,
                        UserShiftRole = userShiftRole
                    });
                }
            }

            _context.ShiftSubmissions.AddRange(submissions);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "シフトが提出されました。";
            return RedirectToAction("Submission", new { periodId });
        }

        // シフト提出時の提出済みシフト取得
        [HttpGet]
        public async Task<IActionResult> SubmissioList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            var shiftDays = await GetShiftDaysForPeriod(periodId);
            ViewBag.Dates = shiftDays.Select(d => d.Date).ToList();

            ViewBag.Users = new List<dynamic>()
            {
                new { Id = currentUser.Id, CustomId = currentUser.CustomId, Name = currentUser.Name }
            };

            var submissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == currentUser.Id && shiftDays.Select(d => d.Id).Contains(s.ShiftDayId))
                .Include(s => s.ShiftDay)
                .ToListAsync();

            ViewBag.Submissions = submissions;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SubmittedList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            var shiftDays = await GetShiftDaysForPeriod(periodId);
            ViewBag.Dates = shiftDays.Select(d => d.Date).ToList();

            var submissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == currentUser.Id && shiftDays.Select(d => d.Id).Contains(s.ShiftDayId))
                .Include(s => s.ShiftDay)
                .OrderBy(s => s.ShiftDay.Date)
                .ThenBy(s => s.ShiftType)
                .ToListAsync();

            return View(submissions);
        }

        private async Task<List<DateTime>> GenerateDateListForSubmissionPeriod(int? periodId)
        {
            var openPeriods = await _context.RecruitmentPeriods
                .Where(p => p.IsOpen)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            RecruitmentPeriod selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = openPeriods.FirstOrDefault(p => p.Id == periodId);
            }

            if (selectedPeriod == null)
            {
                selectedPeriod = openPeriods.FirstOrDefault();
            }

            if (selectedPeriod == null)
            {
                return Enumerable.Range(0, 10).Select(i => DateTime.Today.AddDays(i)).ToList();
            }

            var startDate = selectedPeriod.StartDate;
            var endDate = selectedPeriod.EndDate;
            var days = (endDate - startDate).Days + 1;

            if (days < 1)
            {
                days = 10;
                startDate = DateTime.Today;
            }

            return Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Submission));
        }

        // Temporary debug endpoint (allow anonymous) to inspect shiftdays/submissions counts
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DebugCounts(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods.OrderByDescending(r => r.Id).ToListAsync();
            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            var shiftDays = selectedPeriod != null
                ? await _context.ShiftDays.Where(d => d.RecruitmentPeriodId == selectedPeriod.Id).OrderBy(d => d.Date).ToListAsync()
                : new List<ShiftDay>();

            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            return Json(new
            {
                SelectedPeriodId = selectedPeriod?.Id,
                ShiftDaysCount = shiftDays.Count,
                ShiftDayIds = shiftDays.Select(d => d.Id).ToList(),
                SubmissionsCount = submissions.Count
            });
        }
    }
    public class ShiftSubmissionViewModel
    {
        public string Date { get; set; }
        public ShiftType ShiftType { get; set; }
        public string ShiftSymbol { get; set; }
    }
}
