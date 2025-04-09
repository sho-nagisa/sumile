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

            var submissions = await _context.ShiftSubmissions
                .Where(s => selectedPeriod != null && s.RecruitmentPeriodId == selectedPeriod.Id)
                .Include(s => s.User)
                .ToListAsync();

            var diffKeys = await _context.ShiftEditLogs
                .Where(log => selectedPeriod != null && log.RecruitmentPeriodId == selectedPeriod.Id)
                .Select(log => new
                {
                    log.TargetUserId,
                    Date = log.ShiftDate.Date,
                    log.ShiftType
                })
                .Distinct()
                .ToListAsync();

            var diffKeySet = new HashSet<string>(
                diffKeys.Select(k => $"{k.TargetUserId}_{k.Date:yyyy-MM-dd}_{(int)k.ShiftType}")
            );
            ViewBag.DiffKeys = diffKeySet;

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ShiftEditLogs(int? periodId)
        {
            var periods = await _context.RecruitmentPeriods.OrderByDescending(p => p.Id).ToListAsync();
            ViewBag.RecruitmentPeriods = periods;
            ViewBag.SelectedPeriodId = periodId;

            var logs = await _context.ShiftEditLogs
                .Include(l => l.AdminUser)
                .Include(l => l.TargetUser)
                .Where(l => !periodId.HasValue || l.RecruitmentPeriodId == periodId)
                .OrderByDescending(l => l.EditDate)
                .ToListAsync();

            return View(logs);
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
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

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
                    .Where(s => s.RecruitmentPeriodId == selectedPeriod.Id)
                    .Include(s => s.User)
                    .ToListAsync();
            }

            var originalLogs = await _context.ShiftEditLogs
                .Where(l => l.RecruitmentPeriodId == selectedPeriod.Id)
                .GroupBy(l => new { l.TargetUserId, l.ShiftDate, l.ShiftType })
                .Select(g => g.OrderBy(l => l.EditDate).FirstOrDefault())
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;
            ViewBag.OriginalLogs = originalLogs;

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

            return View(periods);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateShifts([FromBody] List<ShiftUpdateModel> shiftUpdates, [FromQuery] int periodId)
        {
            try
            {
                if (shiftUpdates == null || !shiftUpdates.Any())
                {
                    return Json(new { success = false, error = "シフト更新データが空です。" });
                }

                var logs = new List<ShiftEditLog>();
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminUserId))
                {
                    return Json(new { success = false, error = "管理者のユーザーIDが取得できませんでした。" });
                }

                foreach (var shift in shiftUpdates)
                {
                    if (shift == null || string.IsNullOrEmpty(shift.UserId) || string.IsNullOrEmpty(shift.Date))
                        continue;

                    if (!Enum.IsDefined(typeof(ShiftType), shift.ShiftType))
                        continue;

                    if (!DateTime.TryParse(shift.Date, out DateTime parsedDate))
                        continue;

                    var dateUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                    ShiftState newState = shift.ShiftStatus switch
                    {
                        "〇" => ShiftState.Accepted,
                        "△" => ShiftState.WantToGiveAway,
                        "" => ShiftState.NotAccepted,
                        _ => ShiftState.None
                    };

                    ShiftType shiftType = (ShiftType)shift.ShiftType;

                    var existing = await _context.ShiftSubmissions.FirstOrDefaultAsync(s =>
                        s.UserId == shift.UserId &&
                        s.Date.Date == dateUtc.Date &&
                        s.ShiftType == shiftType &&
                        s.RecruitmentPeriodId == periodId);

                    var existingLogs = await _context.ShiftEditLogs
                        .Where(log => log.TargetUserId == shift.UserId &&
                                      log.ShiftDate.Date == dateUtc.Date &&
                                      log.ShiftType == shiftType &&
                                      log.RecruitmentPeriodId == periodId)
                        .ToListAsync();

                    if (existing == null)
                    {
                        var targetUser = await _userManager.FindByIdAsync(shift.UserId);
                        var userRole = targetUser?.UserShiftRole ?? UserShiftRole.Normal;

                        var newSubmission = new ShiftSubmission
                        {
                            UserId = shift.UserId,
                            Date = dateUtc,
                            ShiftType = shiftType,
                            IsSelected = true,
                            SubmittedAt = DateTime.UtcNow,
                            ShiftStatus = newState,
                            UserType = UserType.AdminUpdated,
                            UserShiftRole = userRole,
                            RecruitmentPeriodId = periodId
                        };
                        _context.ShiftSubmissions.Add(newSubmission);

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDate = dateUtc,
                            ShiftType = shiftType,
                            OldState = ShiftState.None,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = "",
                            RecruitmentPeriodId = periodId
                        });
                    }
                    else if (existing.ShiftStatus != newState)
                    {
                        if (!existingLogs.Any())
                        {
                            logs.Add(new ShiftEditLog
                            {
                                AdminUserId = adminUserId,
                                TargetUserId = shift.UserId,
                                ShiftDate = dateUtc,
                                ShiftType = shiftType,
                                OldState = existing.ShiftStatus,
                                NewState = existing.ShiftStatus,
                                EditDate = DateTime.UtcNow,
                                Note = "（初回ログ）",
                                RecruitmentPeriodId = periodId
                            });
                        }

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDate = dateUtc,
                            ShiftType = shiftType,
                            OldState = existing.ShiftStatus,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = "",
                            RecruitmentPeriodId = periodId
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
        [HttpGet]
        public async Task<IActionResult> ViewDailyWorkload(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods.OrderByDescending(p => p.Id).ToListAsync();
            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(p => p.Id == periodId)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                TempData["Error"] = "募集期間が存在しません。";
                return RedirectToAction("Index");
            }

            var start = selectedPeriod.StartDate.Date;
            var end = selectedPeriod.EndDate.Date;
            var dates = Enumerable.Range(0, (end - start).Days + 1)
                                   .Select(i => start.AddDays(i))
                                   .ToList();

            var workloads = await _context.DailyWorkloads
                .Where(w => w.RecruitmentPeriodId == selectedPeriod.Id)
                .ToDictionaryAsync(w => w.Date.Date, w => w);

            ViewBag.Dates = dates;
            ViewBag.SelectedPeriodId = selectedPeriod.Id;
            ViewBag.Periods = allPeriods;

            return View("DailyWorkload", workloads); // ⬅ ビュー名が DailyWorkload.cshtml のままの場合
        }

        [HttpGet]
        public async Task<IActionResult> EditDailyWorkload(int? periodId)
        {
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(p => p.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                TempData["Error"] = "募集期間が見つかりませんでした。";
                return RedirectToAction("Index");
            }

            var dates = Enumerable.Range(0, (selectedPeriod.EndDate - selectedPeriod.StartDate).Days + 1)
                .Select(i => DateTime.SpecifyKind(selectedPeriod.StartDate.AddDays(i).Date, DateTimeKind.Utc)) // 🛠 UTC 指定
                .ToList();

            var workloads = await _context.DailyWorkloads
                .Where(w => w.RecruitmentPeriodId == selectedPeriod.Id)
                .ToListAsync();

            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod.Id;
            ViewBag.Dates = dates;
            ViewBag.Workloads = workloads;

            return View("DailyWorkload");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDailyWorkload(int periodId, Dictionary<string, int> workloads)
        {
            var existing = await _context.DailyWorkloads
                .Where(w => w.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var updated = new List<DailyWorkload>();
            foreach (var entry in workloads)
            {
                if (!DateTime.TryParse(entry.Key, out var date)) continue;

                var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

                var found = existing.FirstOrDefault(w => w.Date.Date == utcDate.Date);
                if (found != null)
                {
                    found.Date = utcDate; // 念のため更新
                    found.RequiredCount = entry.Value;
                    found.RequiredWorkers = DailyWorkload.CalculateRequiredPeople(entry.Value);
                }
                else
                {
                    updated.Add(new DailyWorkload
                    {
                        Date = utcDate,
                        RequiredCount = entry.Value,
                        RequiredWorkers = DailyWorkload.CalculateRequiredPeople(entry.Value),
                        RecruitmentPeriodId = periodId
                    });
                }
            }

            _context.DailyWorkloads.UpdateRange(existing);
            _context.DailyWorkloads.AddRange(updated);
            await _context.SaveChangesAsync();

            TempData["Success"] = "保存しました";
            return RedirectToAction("EditDailyWorkload", new { periodId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DailyWorkloadSave(int periodId, Dictionary<string, int> inputCounts)
        {
            var existing = await _context.DailyWorkloads
                .Where(w => w.RecruitmentPeriodId == periodId)
                .ToListAsync();

            foreach (var entry in inputCounts)
            {
                if (!DateTime.TryParse(entry.Key, out var parsedDate)) continue;

                var utcDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);

                var workload = existing.FirstOrDefault(w => w.Date.Date == utcDate.Date);
                if (workload == null)
                {
                    workload = new DailyWorkload
                    {
                        Date = utcDate,
                        RecruitmentPeriodId = periodId
                    };
                    _context.DailyWorkloads.Add(workload);
                }
                else
                {
                    workload.Date = utcDate;
                }

                workload.RequiredCount = entry.Value;
                workload.RequiredWorkers = DailyWorkload.CalculateRequiredPeople(entry.Value);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "保存しました。";
            return RedirectToAction("ViewDailyWorkload", new { periodId }); // ⬅ ここもリネーム反映
        }
        [HttpPost]
        public async Task<IActionResult> AutoAssignShifts(int periodId)
        {
            var period = await _context.RecruitmentPeriods.FindAsync(periodId);
            if (period == null)
                return NotFound();

            var startDate = period.StartDate.Date;
            var endDate = period.EndDate.Date;
            var days = (endDate - startDate).Days + 1;
            var dates = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();

            var workloads = await _context.DailyWorkloads
                .Where(w => w.RecruitmentPeriodId == periodId)
                .ToDictionaryAsync(w => w.Date.Date);

            var submissions = await _context.ShiftSubmissions
                .Where(s => s.RecruitmentPeriodId == periodId &&
                            s.ShiftStatus == ShiftState.Accepted &&
                            s.UserShiftRole != UserShiftRole.New)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var newAssignments = new List<ShiftSubmission>();

            foreach (var date in dates)
            {
                if (!workloads.TryGetValue(date, out var workload))
                    continue;

                int required = workload.RequiredCount;
                int targetNormal = required / 2;
                int targetKey = required - targetNormal;

                foreach (ShiftType type in Enum.GetValues(typeof(ShiftType)))
                {
                    var candidates = submissions
                        .Where(s => s.Date.Date == date && s.ShiftType == type)
                        .OrderBy(s => s.UserShiftRole)
                        .ToList();

                    var selected = new List<ShiftSubmission>();

                    var keyHolders = candidates.Where(s => s.UserShiftRole == UserShiftRole.KeyHolder).Take(targetKey).ToList();
                    var normals = candidates.Where(s => s.UserShiftRole == UserShiftRole.Normal).Take(targetNormal).ToList();

                    selected.AddRange(keyHolders);
                    selected.AddRange(normals);

                    // 足りなければNormalで補う
                    int stillNeeded = required - selected.Count;
                    if (stillNeeded > 0)
                    {
                        var remainingNormals = candidates
                            .Where(s => s.UserShiftRole == UserShiftRole.Normal && !selected.Contains(s))
                            .Take(stillNeeded);
                        selected.AddRange(remainingNormals);
                    }

                    foreach (var s in selected)
                    {
                        s.IsSelected = true;
                        s.SubmittedAt = now;
                        s.ShiftStatus = ShiftState.Accepted;
                        // ログは残さない
                        _context.ShiftSubmissions.Update(s);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "シフトの自動割り当てが完了しました。";
            return RedirectToAction("Index", new { periodId });
        }

    }

    public class ShiftUpdateModel
    {
        public string UserId { get; set; }
        public string Date { get; set; }
        public int ShiftType { get; set; }
        public string ShiftStatus { get; set; }
        public int RecruitmentPeriodId { get; set; }
    }
}
