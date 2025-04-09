using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sumile.Data;
using sumile.Models;
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

        public ShiftController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<List<DateTime>> GenerateDateListForRecruitment(int? periodId)
        {
            if (!periodId.HasValue) return new List<DateTime>();

            var period = await _context.RecruitmentPeriods.FindAsync(periodId.Value);
            if (period == null) return new List<DateTime>();

            var start = DateTime.SpecifyKind(period.StartDate.Date, DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(period.EndDate.Date, DateTimeKind.Utc);

            return Enumerable.Range(0, (end - start).Days + 1)
                .Select(i => start.AddDays(i))
                .ToList();
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
            // 追加（Indexの中）
            var workloads = await _context.DailyWorkloads
                .Where(w => selectedPeriod != null && w.RecruitmentPeriodId == selectedPeriod.Id)
                .ToListAsync();

            int days;
            DateTime startDate;

            if (selectedPeriod != null)
            {
                startDate = selectedPeriod.StartDate.Date;
                var endDate = selectedPeriod.EndDate.Date;
                days = (endDate - startDate).Days + 1;

                if (days < 1)
                {
                    days = 10;
                    startDate = DateTime.Today;
                }
            }
            else
            {
                days = 10;
                startDate = DateTime.Today;
            }

            var dates = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();

            var users = await _userManager.Users
                .Select(u => new { u.Id, u.CustomId, u.Name })
                .ToListAsync();

            var submissions = new List<ShiftSubmission>();
            if (selectedPeriod != null)
            {
                submissions = await _context.ShiftSubmissions
                    .Include(s => s.User)
                    .Where(s => s.RecruitmentPeriodId == selectedPeriod.Id)
                    .ToListAsync();
            }

            ViewBag.Workloads = workloads;
            ViewBag.Users = users;
            ViewBag.Dates = dates;
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
            {
                periodId = openPeriods.First().Id;
            }
            ViewBag.SelectedPeriodId = periodId;

            var dates = await GenerateDateListForRecruitment(periodId);
            ViewBag.Dates = dates;

            var userId = currentUser.Id;
            List<ShiftSubmission> existingSubmissions = new();

            if (dates.Any())
            {
                var start = dates.First();
                var end = dates.Last();

                existingSubmissions = await _context.ShiftSubmissions
                    .Where(s =>
                        s.UserId == userId &&
                        s.Date >= start &&
                        s.Date <= end &&
                        s.RecruitmentPeriodId == periodId)
                    .ToListAsync();
            }

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

            // 🔽 CustomId からユーザーのシフトロールを決定（仮に DB に保持している場合）
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
                var dateUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                submissions.Add(new ShiftSubmission
                {
                    UserId = userId,
                    Date = dateUtc,
                    ShiftType = shift.ShiftType,
                    ShiftStatus = status,
                    IsSelected = true,
                    SubmittedAt = DateTime.UtcNow,
                    UserType = userType,
                    UserShiftRole = userShiftRole,
                    RecruitmentPeriodId = periodId
                });
            }

            if (submissions.Any())
            {
                var dates = submissions.Select(s => s.Date.Date).Distinct().ToList();
                var existing = await _context.ShiftSubmissions
                    .Where(s => s.UserId == userId &&
                                s.RecruitmentPeriodId == periodId &&
                                dates.Contains(s.Date.Date))
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
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            var dates = await GenerateDateListForRecruitment(periodId);
            ViewBag.Dates = dates;

            var users = new List<dynamic>()
            {
                new {
                    Id = currentUser.Id,
                    CustomId = currentUser.CustomId,
                    Name = currentUser.Name
                }
            };
            ViewBag.Users = users;

            var submissions = new List<ShiftSubmission>();
            if (dates.Any())
            {
                var periodStart = dates.First();
                var periodEnd = dates.Last();

                submissions = await _context.ShiftSubmissions
                    .Where(s => s.UserId == currentUser.Id &&
                                s.Date >= periodStart && s.Date <= periodEnd)
                    .ToListAsync();
            }
            ViewBag.Submissions = submissions;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SubmittedList(int? periodId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            var dates = await GenerateDateListForRecruitment(periodId);
            ViewBag.Dates = dates;

            if (dates.Any())
            {
                DateTime periodStart = dates.First();
                DateTime periodEnd = dates.Last();

                var submissions = await _context.ShiftSubmissions
                    .Where(s => s.UserId == currentUser.Id &&
                                s.Date >= periodStart && s.Date <= periodEnd)
                    .OrderBy(s => s.Date)
                    .ThenBy(s => s.ShiftType)
                    .ToListAsync();

                return View(submissions);
            }
            else
            {
                return View(new List<ShiftSubmission>());
            }
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
