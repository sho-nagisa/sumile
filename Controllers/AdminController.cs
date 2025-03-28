using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sumile.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods.OrderByDescending(r => r.Id).ToListAsync();
            var selectedPeriod = periodId.HasValue ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value) : allPeriods.FirstOrDefault();

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
            var users = await _userManager.Users.Select(u => new { u.Id, u.CustomId, u.Name }).ToListAsync();
            var submissions = await _context.ShiftSubmissions.Include(s => s.User).ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            var latest = await _context.RecruitmentPeriods.OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            if (latest == null)
            {
                latest = new RecruitmentPeriod
                {
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(9)
                };
            }

            var model = new RecruitmentPeriodViewModel
            {
                StartDate = latest.StartDate,
                EndDate = latest.EndDate
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRecruitmentPeriod(RecruitmentPeriodViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var startUtc = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);

            var newRecruitment = new RecruitmentPeriod
            {
                StartDate = startUtc,
                EndDate = endUtc
            };

            _context.RecruitmentPeriods.Add(newRecruitment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditShifts(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods.OrderByDescending(r => r.Id).ToListAsync();
            var selectedPeriod = periodId.HasValue ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value) : allPeriods.FirstOrDefault();

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
            var users = await _userManager.Users.Select(u => new { u.Id, u.CustomId, u.Name }).ToListAsync();
            var submissions = await _context.ShiftSubmissions.Include(s => s.User).ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ToggleSubmissionStatus(int id)
        {
            var period = await _context.RecruitmentPeriods.FindAsync(id);
            if (period == null)
            {
                return NotFound();
            }

            period.IsOpen = !period.IsOpen;
            _context.RecruitmentPeriods.Update(period);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Admin");
        }
        [HttpGet]
        public async Task<IActionResult> ManageSubmissionPeriods()
        {
            var periods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(periods); // Views/Admin/ManageSubmissionPeriods.cshtml を表示
        }


        [HttpPost]
        public async Task<IActionResult> UpdateShifts([FromBody] List<ShiftUpdateModel> shiftUpdates)
        {
            try
            {
                var logs = new List<ShiftEditLog>();
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminUserId))
                {
                    return Json(new { success = false, error = "管理者のユーザーIDが取得できませんでした。" });
                }

                foreach (var shift in shiftUpdates)
                {
                    var parsedDate = DateTime.Parse(shift.Date);
                    var dateUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                    ShiftState newState = shift.ShiftStatus switch
                    {
                        "〇" => ShiftState.Accepted,
                        "△" => ShiftState.WantToGiveAway,
                        "" => ShiftState.NotAccepted,
                        _ => ShiftState.None
                    };

                    var existing = await _context.ShiftSubmissions.FirstOrDefaultAsync(s =>
                        s.UserId == shift.UserId &&
                        s.Date.Date == dateUtc.Date &&
                        s.ShiftType == shift.ShiftType);

                    if (existing == null)
                    {
                        var newSubmission = new ShiftSubmission
                        {
                            UserId = shift.UserId,
                            Date = dateUtc,
                            ShiftType = shift.ShiftType,
                            IsSelected = true,
                            SubmittedAt = DateTime.UtcNow,
                            ShiftStatus = newState,
                            UserType = "AdminUpdated"
                        };
                        _context.ShiftSubmissions.Add(newSubmission);

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDate = dateUtc,
                            ShiftType = shift.ShiftType,
                            OldState = ShiftState.None,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = "" // 修正済み
                        });
                    }
                    else if (existing.ShiftStatus != newState)
                    {
                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDate = dateUtc,
                            ShiftType = shift.ShiftType,
                            OldState = existing.ShiftStatus,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = "" // 修正済み
                        });

                        existing.ShiftStatus = newState;
                        existing.SubmittedAt = DateTime.UtcNow;
                        _context.ShiftSubmissions.Update(existing);
                    }
                }

                if (logs.Any())
                {
                    _context.ShiftEditLogs.AddRange(logs);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }

    public class ShiftUpdateModel
    {
        public string UserId { get; set; }
        public string Date { get; set; }
        public string ShiftType { get; set; }
        public string ShiftStatus { get; set; }
    }
}