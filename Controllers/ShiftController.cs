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

        private static string ToSubmissionSymbol(ShiftState? state)
        {
            return state switch
            {
                ShiftState.Accepted => "〇",
                ShiftState.KeyHolder => "〇",
                ShiftState.WantToGiveAway => "△",
                _ => "×"
            };
        }

        private static string FormatPeriodLabel(RecruitmentPeriod period)
        {
            return $"{period.StartDate:yyyy/MM/dd} ～ {period.EndDate:MM/dd}";
        }

        private async Task<Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState>> LoadUserSubmissionStatesAsync(
            string userId,
            IReadOnlyCollection<ShiftDay> shiftDays,
            IReadOnlyCollection<int> periodIds)
        {
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            if (!shiftDayIds.Any() || !periodIds.Any())
            {
                return new Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState>();
            }

            var backupStates = await _context.SubmitBackups
                .Where(b =>
                    b.UserId == userId &&
                    periodIds.Contains(b.RecruitmentPeriodId) &&
                    shiftDayIds.Contains(b.ShiftDayId))
                .ToListAsync();

            var states = backupStates
                .GroupBy(b => (b.ShiftDayId, b.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(b => b.BackedUpAt).First().ShiftStatus);

            var currentSubmissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == userId && shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            foreach (var submissionGroup in currentSubmissions.GroupBy(s => (s.ShiftDayId, s.ShiftType)))
            {
                if (states.ContainsKey(submissionGroup.Key))
                {
                    continue;
                }

                states[submissionGroup.Key] = submissionGroup
                    .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                    .ThenByDescending(s => s.Id)
                    .First()
                    .ShiftStatus;
            }

            return states;
        }

        private List<ShiftCopyCellViewModel> BuildCopyCells(
            IEnumerable<(ShiftDay TargetDay, ShiftDay SourceDay)> dayPairs,
            Dictionary<(int ShiftDayId, ShiftType ShiftType), ShiftState> sourceStates)
        {
            var cells = new List<ShiftCopyCellViewModel>();

            foreach (var (targetDay, sourceDay) in dayPairs)
            {
                foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
                {
                    sourceStates.TryGetValue((sourceDay.Id, shiftType), out var state);
                    var symbol = ToSubmissionSymbol(state);

                    if (symbol == "×")
                    {
                        continue;
                    }

                    cells.Add(new ShiftCopyCellViewModel
                    {
                        Date = targetDay.Date.ToString("yyyy-MM-dd"),
                        ShiftType = shiftType.ToString(),
                        ShiftSymbol = symbol
                    });
                }
            }

            return cells;
        }

        private async Task<ShiftCopyOptionViewModel> BuildWeekdayCopyOptionAsync(
            string userId,
            RecruitmentPeriod? selectedPeriod,
            List<ShiftDay> targetDays)
        {
            var result = new ShiftCopyOptionViewModel();
            if (selectedPeriod == null || !targetDays.Any())
            {
                return result;
            }

            var sourcePeriods = await _context.RecruitmentPeriods
                .Where(p => p.Id != selectedPeriod.Id && p.StartDate < selectedPeriod.StartDate)
                .OrderByDescending(p => p.StartDate)
                .Take(6)
                .ToListAsync();

            var sourcePeriodIds = sourcePeriods.Select(p => p.Id).ToList();
            if (!sourcePeriodIds.Any())
            {
                return result;
            }

            var sourceDays = await _context.ShiftDays
                .Where(d => sourcePeriodIds.Contains(d.RecruitmentPeriodId))
                .OrderByDescending(d => d.Date)
                .ToListAsync();

            var latestDayByWeekday = sourceDays
                .GroupBy(d => d.Date.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.First());

            var dayPairs = targetDays
                .Where(target => latestDayByWeekday.ContainsKey(target.Date.DayOfWeek))
                .Select(target => (TargetDay: target, SourceDay: latestDayByWeekday[target.Date.DayOfWeek]))
                .ToList();

            if (!dayPairs.Any())
            {
                return result;
            }

            var sourceStates = await LoadUserSubmissionStatesAsync(userId, sourceDays, sourcePeriodIds);
            result.Cells = BuildCopyCells(dayPairs, sourceStates);

            var usedSourceDates = dayPairs
                .Select(pair => pair.SourceDay.Date.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            result.SourceLabel = usedSourceDates.Count == 1
                ? $"{usedSourceDates.First():yyyy/MM/dd} の同じ曜日"
                : $"{usedSourceDates.First():yyyy/MM/dd} ～ {usedSourceDates.Last():MM/dd} の同じ曜日";

            return result;
        }

        private async Task<ShiftCopyOptionViewModel> BuildPreviousPeriodCopyOptionAsync(
            string userId,
            RecruitmentPeriod? selectedPeriod,
            List<ShiftDay> targetDays)
        {
            var result = new ShiftCopyOptionViewModel();
            if (selectedPeriod == null || !targetDays.Any())
            {
                return result;
            }

            var sourcePeriod = await _context.RecruitmentPeriods
                .Where(p => p.Id != selectedPeriod.Id && p.StartDate < selectedPeriod.StartDate)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (sourcePeriod == null)
            {
                return result;
            }

            var sourceDays = await GetShiftDaysForPeriod(sourcePeriod.Id);
            if (!sourceDays.Any())
            {
                return result;
            }

            var sourceStates = await LoadUserSubmissionStatesAsync(userId, sourceDays, new[] { sourcePeriod.Id });
            var dayPairs = targetDays
                .Take(Math.Min(targetDays.Count, sourceDays.Count))
                .Select((target, index) => (TargetDay: target, SourceDay: sourceDays[index]))
                .ToList();

            result.Cells = BuildCopyCells(dayPairs, sourceStates);
            result.SourceLabel = FormatPeriodLabel(sourcePeriod);
            return result;
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
            ViewBag.WorkloadCells = table.WorkloadCells;
            ViewBag.ShiftColumns = table.ShiftColumns;
            ViewBag.TotalAcceptedList = table.TotalAcceptedList;
            ViewBag.KeyHolderAcceptedList = table.KeyHolderAcceptedList;
            ViewBag.RequiredWorkersList = table.RequiredWorkersList;
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
            var selectedPeriod = periodId.HasValue
                ? openPeriods.FirstOrDefault(p => p.Id == periodId.Value)
                : null;
            ViewBag.SelectedPeriod = selectedPeriod;

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

            var weekdayCopyOption = await BuildWeekdayCopyOptionAsync(userId, selectedPeriod, shiftDays);
            var previousPeriodCopyOption = await BuildPreviousPeriodCopyOptionAsync(userId, selectedPeriod, shiftDays);

            ViewBag.WeekdayCopyShiftsJson = JsonConvert.SerializeObject(weekdayCopyOption.Cells);
            ViewBag.WeekdayCopySourceLabel = weekdayCopyOption.SourceLabel;
            ViewBag.PreviousPeriodCopyShiftsJson = JsonConvert.SerializeObject(previousPeriodCopyOption.Cells);
            ViewBag.PreviousPeriodCopySourceLabel = previousPeriodCopyOption.SourceLabel;

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

            RecruitmentPeriod? selectedPeriod = null;
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
        public string Date { get; set; } = string.Empty;
        public ShiftType ShiftType { get; set; }
        public string ShiftSymbol { get; set; } = string.Empty;
    }

    public class ShiftCopyOptionViewModel
    {
        public List<ShiftCopyCellViewModel> Cells { get; set; } = new List<ShiftCopyCellViewModel>();
        public string? SourceLabel { get; set; }
    }

    public class ShiftCopyCellViewModel
    {
        public string Date { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string ShiftSymbol { get; set; } = string.Empty;
    }
}
