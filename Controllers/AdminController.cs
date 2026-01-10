using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ShiftPdfService _pdfService;
        private readonly ShiftTableService _shiftTableService;

        public AdminController(
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

        private async Task<bool> IsAdminUser()
        {
            var isAdminStr = HttpContext.Session.GetString("IsAdmin");
            if (!string.IsNullOrEmpty(isAdminStr))
            {
                return isAdminStr == "True";
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user?.IsAdmin ?? false;
            HttpContext.Session.SetString("IsAdmin", isAdmin.ToString());
            return isAdmin;
        }
        
        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                TempData["Error"] = "募集期間が選択されていません。";
                return RedirectToAction("SetRecruitmentPeriod");
            }
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
            var shiftDayIds = table.ShiftDays.Select(d => d.Id).ToList();
            var diffLogs = await _context.ShiftEditLogs
                .Where(log => shiftDayIds.Contains(log.ShiftDayId))
                .Select(log => new
                {
                    log.TargetUserId,
                    log.ShiftDayId,
                    log.ShiftType
                })
                .Distinct()
                .ToListAsync();

            var diffKeySet = new HashSet<string>(
                diffLogs.Select(k => $"{k.TargetUserId}_{k.ShiftDayId}_{(int)k.ShiftType}")
            );
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegeneratePdf(int periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            await _pdfService.GenerateShiftPdfAsync(periodId);
            TempData["SuccessMessage"] = "PDFを再生成しました。";

            return RedirectToAction("Index", new { periodId });
        }

        [HttpGet]
        public async Task<IActionResult> ShiftEditLogs(int? periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var periods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            ViewBag.RecruitmentPeriods = periods;
            ViewBag.SelectedPeriodId = periodId;

            var logs = await _context.ShiftEditLogs
                .Include(l => l.AdminUser)
                .Include(l => l.TargetUser)
                .Include(l => l.ShiftDay)
                .Where(l => !periodId.HasValue || l.ShiftDay.RecruitmentPeriodId == periodId)
                .OrderByDescending(l => l.EditDate)
                .ToListAsync();

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            if (!await IsAdminUser()) return Unauthorized();
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
            if (!await IsAdminUser()) return Unauthorized();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var startUtc = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);

            var newRecruitment = new RecruitmentPeriod
            {
                StartDate = startUtc,
                EndDate = endUtc,
                IsOpen = true // ← 必要なら開放フラグもここでON
            };

            _context.RecruitmentPeriods.Add(newRecruitment);
            await _context.SaveChangesAsync(); // ← IDが確定される

            var days = new List<ShiftDay>();
            for (var date = startUtc.Date; date <= endUtc.Date; date = date.AddDays(1))
            {
                days.Add(new ShiftDay
                {
                    Date = date,
                    RecruitmentPeriodId = newRecruitment.Id
                });
            }

            _context.ShiftDays.AddRange(days);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditShifts(int? periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            var shiftDays = selectedPeriod != null
                ? await _context.ShiftDays
                    .Where(d => d.RecruitmentPeriodId == selectedPeriod.Id)
                    .OrderBy(d => d.Date)
                    .ToListAsync()
                : new List<ShiftDay>();

            var users = await _userManager.Users
                .Select(u => new { u.Id, u.CustomId, u.Name })
                .ToListAsync();

            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();

            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .Include(s => s.User)
                .Include(s => s.ShiftDay)
                .ToListAsync();

            var originalLogs = await _context.ShiftEditLogs
                .Where(l => shiftDayIds.Contains(l.ShiftDayId))
                .Include(l => l.ShiftDay)
                .GroupBy(l => new { l.TargetUserId, l.ShiftDayId, l.ShiftType })
                .Select(g => g.OrderBy(l => l.EditDate).FirstOrDefault())
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = shiftDays;
            ViewBag.Submissions = submissions;
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod?.Id;
            ViewBag.OriginalLogs = originalLogs;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateShifts([FromBody] List<ShiftUpdateModel> shiftUpdates, [FromQuery] int periodId)
        {
            try
            {
                if (shiftUpdates == null || !shiftUpdates.Any())
                    return Json(new { success = false, error = "シフト更新データが空です。" });

                var logs = new List<ShiftEditLog>();
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminUserId))
                    return Json(new { success = false, error = "管理者のユーザーIDが取得できませんでした。" });

                var shiftDays = await _context.ShiftDays
                    .Where(d => d.RecruitmentPeriodId == periodId)
                    .ToListAsync();
                var shiftDayDict = shiftDays.ToDictionary(d => d.Date.Date, d => d.Id);

                foreach (var shift in shiftUpdates)
                {
                    if (shift == null || string.IsNullOrEmpty(shift.UserId) || string.IsNullOrEmpty(shift.Date))
                        continue;
                    if (!Enum.IsDefined(typeof(ShiftType), shift.ShiftType))
                        continue;
                    if (!DateTime.TryParse(shift.Date, out DateTime parsedDate))
                        continue;

                    var dateUtc = parsedDate.Date;
                    if (!shiftDayDict.TryGetValue(dateUtc, out var shiftDayId))
                        continue;

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
                        s.ShiftDayId == shiftDayId &&
                        s.ShiftType == shiftType);

                    var existingLogs = await _context.ShiftEditLogs
                        .Where(log => log.TargetUserId == shift.UserId && log.ShiftDayId == shiftDayId && log.ShiftType == shiftType)
                        .ToListAsync();

                    if (existing == null)
                    {
                        var targetUser = await _userManager.FindByIdAsync(shift.UserId);
                        var userRole = targetUser?.UserShiftRole ?? UserShiftRole.Normal;

                        var newSubmission = new ShiftSubmission
                        {
                            UserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            IsSelected = true,
                            SubmittedAt = DateTime.UtcNow,
                            ShiftStatus = newState,
                            UserType = UserType.AdminUpdated,
                            UserShiftRole = userRole
                        };
                        _context.ShiftSubmissions.Add(newSubmission);

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            OldState = ShiftState.None,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = ""
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
                                ShiftDayId = shiftDayId,
                                ShiftType = shiftType,
                                OldState = existing.ShiftStatus,
                                NewState = existing.ShiftStatus,
                                EditDate = DateTime.UtcNow,
                                Note = "（初回ログ）"
                            });
                        }

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            OldState = existing.ShiftStatus,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = ""
                        });

                        existing.ShiftStatus = newState;
                        existing.SubmittedAt = DateTime.UtcNow;
                        _context.ShiftSubmissions.Update(existing);
                    }
                }

                if (logs.Any())
                    _context.ShiftEditLogs.AddRange(logs);

                await _context.SaveChangesAsync();
                await _pdfService.GenerateShiftPdfAsync(periodId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.InnerException?.Message ?? ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ToggleSubmissionStatus(int id)
        {
            if (!await IsAdminUser()) return Unauthorized();
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
            if (!await IsAdminUser()) return Unauthorized();
            var periods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(periods);
        }

        [HttpGet]
        public async Task<IActionResult> ViewDailyWorkload(int? periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var allPeriods = await _context.RecruitmentPeriods.OrderByDescending(p => p.Id).ToListAsync();
            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(p => p.Id == periodId)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                TempData["Error"] = "募集期間が存在しません。";
                return RedirectToAction("Index");
            }

            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == selectedPeriod.Id)
                .OrderBy(d => d.Date)
                .ToListAsync();

            var workloads = await _context.DailyWorkloads
                .Where(w => shiftDays.Select(d => d.Id).Contains(w.ShiftDayId))
                .ToDictionaryAsync(w => w.ShiftDayId, w => w);

            ViewBag.ShiftDays = shiftDays;
            ViewBag.SelectedPeriodId = selectedPeriod.Id;
            ViewBag.Periods = allPeriods;

            return View("DailyWorkload", workloads);
        }

        [HttpGet]
        public async Task<IActionResult> EditDailyWorkload(int? periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

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

            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == selectedPeriod.Id)
                .OrderBy(d => d.Date)
                .ToListAsync();

            var workloads = await _context.DailyWorkloads
                .Where(w => shiftDays.Select(d => d.Id).Contains(w.ShiftDayId))
                .ToDictionaryAsync(w => w.ShiftDayId); // ← ★辞書に変更

            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriod.Id;
            ViewBag.ShiftDays = shiftDays;
            ViewBag.WorkloadMap = workloads; // ← ★辞書で渡す

            return View("DailyWorkload");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDailyWorkload(int periodId, Dictionary<string, int> inputCounts, string redirectTo)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var shiftDayMap = shiftDays.ToDictionary(d => d.Date.Date, d => d);
            var existing = await _context.DailyWorkloads
                .Where(w => shiftDayMap.Values.Select(d => d.Id).Contains(w.ShiftDayId))
                .ToListAsync();

            foreach (var entry in inputCounts)
            {
                if (!DateTime.TryParse(entry.Key, out var parsedDate)) continue;

                var dateOnly = parsedDate.Date;

                if (!shiftDayMap.TryGetValue(dateOnly, out var shiftDay)) continue;

                var workload = existing.FirstOrDefault(w => w.ShiftDayId == shiftDay.Id);
                if (workload == null)
                {
                    workload = new DailyWorkload
                    {
                        ShiftDayId = shiftDay.Id
                    };
                    _context.DailyWorkloads.Add(workload);
                }

                workload.RequiredCount = entry.Value;
                workload.RequiredWorkers = DailyWorkload.CalculateRequiredPeople(entry.Value);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "保存しました。";

            return redirectTo == "view"
                ? RedirectToAction("ViewDailyWorkload", new { periodId })
                : RedirectToAction("EditDailyWorkload", new { periodId });
        }

        [HttpPost]
        public async Task<IActionResult> AutoAssignShifts(int periodId)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == periodId)
                .ToListAsync();

            var workloads = await _context.DailyWorkloads
                .ToDictionaryAsync(w => w.ShiftDayId);

            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDays.Select(d => d.Id).Contains(s.ShiftDayId) &&
                            s.ShiftStatus == ShiftState.Accepted &&
                            s.UserShiftRole != UserShiftRole.New)
                .Include(s => s.ShiftDay)
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var shiftDay in shiftDays)
            {
                if (!workloads.TryGetValue(shiftDay.Id, out var workload))
                    continue;

                int required = workload.RequiredCount;
                int targetNormal = required / 2;
                int targetKey = required - targetNormal;

                foreach (ShiftType type in Enum.GetValues(typeof(ShiftType)))
                {
                    var candidates = submissions
                        .Where(s => s.ShiftDayId == shiftDay.Id && s.ShiftType == type)
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
                        _context.ShiftSubmissions.Update(s);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "シフトの自動割り当てが完了しました。";
            await _pdfService.GenerateShiftPdfAsync(periodId);
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
