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

        public ShiftController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ShiftPdfService pdfService)
        {
            _context = context;
            _userManager = userManager;
            _pdfService = pdfService;
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
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            var shiftDays = selectedPeriod != null
                ? await _context.ShiftDays.Where(d => d.RecruitmentPeriodId == selectedPeriod.Id).OrderBy(d => d.Date).ToListAsync()
                : new List<ShiftDay>();

            var workloads = await _context.DailyWorkloads
                .Where(w => shiftDays.Select(sd => sd.Id).Contains(w.ShiftDayId))
                .ToListAsync();

            var users = await _userManager.Users
                .Select(u => new { u.Id, u.CustomId, u.Name })
                .ToListAsync();

            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .Include(s => s.User)
                .Include(s => s.ShiftDay)
                .ToListAsync();

            ViewBag.Workloads = workloads;
            ViewBag.Users = users;
            ViewBag.Dates = shiftDays.Select(d => d.Date).ToList();
            ViewBag.Submissions = submissions;
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
        public async Task<IActionResult> SubmitShifts([FromForm] string selectedShifts, [FromForm] int periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var userId = currentUser.Id;
            var userTypeStr = HttpContext.Session.GetString("UserType") ?? "Normal";
            UserType userType = Enum.TryParse<UserType>(userTypeStr, true, out var parsedType) ? parsedType : UserType.Normal;
            var userShiftRole = currentUser.UserShiftRole;

            if (string.IsNullOrEmpty(selectedShifts))
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
                return RedirectToAction("Submission", new { periodId });
            }

            var shiftList = JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts);
            if (shiftList == null || shiftList.Count == 0)
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
                return RedirectToAction("Submission", new { periodId });
            }

            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var dateToDay = shiftDays.ToDictionary(d => d.Date.Date, d => d.Id);
            var submissions = new List<ShiftSubmission>();

            foreach (var shift in shiftList)
            {
                var status = shift.ShiftSymbol switch
                {
                    "〇" => ShiftState.Accepted,
                    "△" => ShiftState.WantToGiveAway,
                    _ => ShiftState.NotAccepted
                };

                if (!DateTime.TryParse(shift.Date, out var parsedDate)) continue;
                var dateOnly = parsedDate.Date;

                if (!dateToDay.TryGetValue(dateOnly, out var shiftDayId)) continue;

                submissions.Add(new ShiftSubmission
                {
                    UserId = userId,
                    ShiftDayId = shiftDayId,
                    ShiftType = shift.ShiftType,
                    ShiftStatus = status,
                    IsSelected = true,
                    SubmittedAt = DateTime.UtcNow,
                    UserType = userType,
                    UserShiftRole = userShiftRole
                });
            }

            if (submissions.Any())
            {
                var shiftDayIds = submissions.Select(s => s.ShiftDayId).Distinct().ToList();
                var existing = await _context.ShiftSubmissions
                    .Where(s => s.UserId == userId && shiftDayIds.Contains(s.ShiftDayId))
                    .ToListAsync();

                _context.ShiftSubmissions.RemoveRange(existing);
                _context.ShiftSubmissions.AddRange(submissions);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "シフトが提出されました。";
            }
            else
            {
                TempData["ErrorMessage"] = "有効なシフトが選択されていません。";
            }

            return RedirectToAction("Submission", new { periodId });
        }

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
    }

    public class ShiftSubmissionViewModel
    {
        public string Date { get; set; }
        public ShiftType ShiftType { get; set; }
        public string ShiftSymbol { get; set; }
    }
}
