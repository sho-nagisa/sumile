using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;
using sumile.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
///※１：確定版
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

        private static string GetShiftCellKey(string userId, int shiftDayId, ShiftType shiftType)
        {
            return $"{userId}_{shiftDayId}_{(int)shiftType}";
        }

        private static bool IsSelectedState(ShiftState state)
        {
            return state != ShiftState.None && state != ShiftState.NotAccepted;
        }

        private static ShiftState GetInitialState(
            IReadOnlyDictionary<string, ShiftState> initialStateByKey,
            string userId,
            int shiftDayId,
            ShiftType shiftType)
        {
            return initialStateByKey.TryGetValue(GetShiftCellKey(userId, shiftDayId, shiftType), out var state)
                ? state
                : ShiftState.None;
        }

        private static string ConvertShiftStateToLabel(ShiftState state)
        {
            return state switch
            {
                ShiftState.Accepted => "〇",
                ShiftState.NotAccepted => "空白",
                ShiftState.WantToGiveAway => "△",
                ShiftState.KeyHolder => "赤丸",
                _ => "×"
            };
        }

        private static string ConvertShiftTypeToLabel(ShiftType shiftType)
        {
            return shiftType == ShiftType.Morning ? "上" : "敷";
        }

        private static string BuildEditLogNote(
            bool isInitialConfirmation,
            ShiftState initialState,
            ShiftState oldState,
            ShiftState newState,
            string? reason)
        {
            string actionLabel;

            if (isInitialConfirmation)
            {
                actionLabel = initialState == ShiftState.None ? "新規作成" : "初回確定";
            }
            else if (newState == initialState)
            {
                actionLabel = "初期状態に合わせて変更";
            }
            else
            {
                actionLabel = "変更";
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return actionLabel;
            }

            return $"{actionLabel}: {reason.Trim()}";
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

            var selectedPeriodIdValue = selectedPeriod.Id;

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
            var table = await _shiftTableService.BuildAsync(selectedPeriodIdValue);
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
            ViewBag.WorkloadCells = table.WorkloadCells;
            ViewBag.ShiftColumns = table.ShiftColumns;
            ViewBag.TotalAcceptedList = table.TotalAcceptedList;
            ViewBag.KeyHolderAcceptedList = table.KeyHolderAcceptedList;
            ViewBag.RequiredWorkersList = table.RequiredWorkersList;
            ViewBag.RemainingWorkersList = table.RemainingWorkersList;

            // ===== その他 View 用データ =====
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = selectedPeriodIdValue;

            var pdfUrl = await _pdfService.EnsureShiftPdfAsync(selectedPeriodIdValue);
            var pdfPath = _pdfService.GetShiftPdfPhysicalPath(selectedPeriodIdValue);
            if (System.IO.File.Exists(pdfPath))
            {
                var updatedAt = System.IO.File.GetLastWriteTime(pdfPath);
                ViewBag.ShiftPdfUrl = $"{pdfUrl}?v={updatedAt.Ticks}";
                ViewBag.ShiftPdfUpdatedAt = updatedAt;
            }

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
        public async Task<IActionResult> ShiftEditLogs(
            int? periodId,
            string? targetUserId,
            string? adminUserId,
            DateTime? editedFrom,
            DateTime? editedTo,
            bool onlyChanged = false,
            bool onlyCurrentDiff = false)
        {
            if (!await IsAdminUser()) return Unauthorized();

            var periods = await _context.RecruitmentPeriods
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            var users = await _context.Users
                .OrderBy(u => u.CustomId)
                .ToListAsync();

            ViewBag.RecruitmentPeriods = periods;
            ViewBag.SelectedPeriodId = periodId;
            ViewBag.Users = users;
            ViewBag.SelectedTargetUserId = targetUserId;
            ViewBag.SelectedAdminUserId = adminUserId;
            ViewBag.EditedFrom = editedFrom?.ToString("yyyy-MM-dd");
            ViewBag.EditedTo = editedTo?.ToString("yyyy-MM-dd");
            ViewBag.OnlyChanged = onlyChanged;
            ViewBag.OnlyCurrentDiff = onlyCurrentDiff;

            var logQuery = _context.ShiftEditLogs
                .Include(l => l.AdminUser)
                .Include(l => l.TargetUser)
                .Include(l => l.ShiftDay)
                .Where(l => !periodId.HasValue || l.ShiftDay.RecruitmentPeriodId == periodId);

            if (!string.IsNullOrWhiteSpace(targetUserId))
            {
                logQuery = logQuery.Where(l => l.TargetUserId == targetUserId);
            }

            if (!string.IsNullOrWhiteSpace(adminUserId))
            {
                logQuery = logQuery.Where(l => l.AdminUserId == adminUserId);
            }

            if (onlyChanged)
            {
                logQuery = logQuery.Where(l => l.OldState != l.NewState);
            }

            var logs = await logQuery
                .OrderByDescending(l => l.EditDate)
                .ToListAsync();

            if (editedFrom.HasValue)
            {
                logs = logs
                    .Where(l => l.EditDate.ToLocalTime().Date >= editedFrom.Value.Date)
                    .ToList();
            }

            if (editedTo.HasValue)
            {
                logs = logs
                    .Where(l => l.EditDate.ToLocalTime().Date <= editedTo.Value.Date)
                    .ToList();
            }

            var logShiftDayIds = logs
                .Select(l => l.ShiftDayId)
                .Distinct()
                .ToList();

            var backups = await _context.SubmitBackups
                .Where(b => logShiftDayIds.Contains(b.ShiftDayId))
                .ToListAsync();

            var initialStateByKey = backups
                .GroupBy(b => GetShiftCellKey(b.UserId, b.ShiftDayId, b.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(b => b.BackedUpAt).First().ShiftStatus);

            var currentSubmissions = await _context.ShiftSubmissions
                .Where(s => logShiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            var currentStateByKey = currentSubmissions
                .GroupBy(s => GetShiftCellKey(s.UserId, s.ShiftDayId, s.ShiftType))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                        .ThenByDescending(s => s.Id)
                        .First()
                        .ShiftStatus);

            if (onlyCurrentDiff)
            {
                logs = logs
                    .Where(log =>
                    {
                        var key = GetShiftCellKey(log.TargetUserId, log.ShiftDayId, log.ShiftType);
                        var initialState = initialStateByKey.TryGetValue(key, out var initial) ? initial : ShiftState.None;
                        var currentState = currentStateByKey.TryGetValue(key, out var current) ? current : ShiftState.None;
                        return currentState != initialState;
                    })
                    .ToList();
            }

            ViewBag.InitialStateByKey = initialStateByKey;
            ViewBag.CurrentStateByKey = currentStateByKey;

            return View(logs);
        }

        [HttpGet]///※１
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

        [HttpPost]///※１
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
            if (!await IsAdminUser())
                return Unauthorized();

            // ===== 募集期間一覧 =====
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // ===== 選択中の期間 =====
            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(p => p.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                TempData["Error"] = "募集期間が見つかりませんでした。";
                return RedirectToAction(nameof(Index));
            }

            // ===== ★ ShiftTableService 利用（表示ロジック集約） =====
            var table = await _shiftTableService.BuildAsync(selectedPeriod.Id);

            // ===== 初回状態（SubmitBackup） =====
            var backups = await _context.SubmitBackups
                .Where(b => b.RecruitmentPeriodId == selectedPeriod.Id)
                .ToListAsync();

            var shiftDayIds = table.ShiftDays
                .Select(d => d.Id)
                .ToList();

            var hasInitialConfirmation = await _context.ShiftEditLogs
                .AnyAsync(l => shiftDayIds.Contains(l.ShiftDayId));

            // ===== ユーザー一覧（星表示用に UserShiftRole を含める） =====
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

            // ===== View に渡す（表描画用） =====
            ViewBag.Dates                = table.ShiftDays;
            ViewBag.Submissions           = table.Submissions;
            ViewBag.Workloads             = table.Workloads;
            ViewBag.WorkloadCells         = table.WorkloadCells;
            ViewBag.ShiftColumns          = table.ShiftColumns;

            // 集計（Index と同一ロジック）
            ViewBag.TotalAcceptedList     = table.TotalAcceptedList;
            ViewBag.KeyHolderAcceptedList = table.KeyHolderAcceptedList;
            ViewBag.RequiredWorkersList   = table.RequiredWorkersList;
            ViewBag.RemainingWorkersList  = table.RemainingWorkersList;

            // 初回状態（差分比較用・将来拡張）
            ViewBag.Backups               = backups;
            ViewBag.HasInitialConfirmation = hasInitialConfirmation;

            // 募集期間情報
            ViewBag.RecruitmentPeriods    = allPeriods;
            ViewBag.SelectedPeriodId      = selectedPeriod.Id;

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShifts([FromBody] ShiftUpdateRequest request, [FromQuery] int periodId)
        {
            try
            {
                if (!await IsAdminUser())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        error = "管理者のみこの操作を実行できます。"
                    });
                }

                if (request?.ShiftUpdates == null || !request.ShiftUpdates.Any())
                    return Json(new { success = false, error = "シフト更新データが空です。" });

                var logs = new List<ShiftEditLog>();
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminUserId))
                    return Json(new { success = false, error = "管理者のユーザーIDが取得できませんでした。" });

                var trimmedReason = request.Reason?.Trim();

                var shiftDays = await _context.ShiftDays
                    .Where(d => d.RecruitmentPeriodId == periodId)
                    .ToListAsync();
                var shiftDayDict = shiftDays.ToDictionary(d => d.Date.Date, d => d.Id);

                var backups = await _context.SubmitBackups
                    .Where(b => b.RecruitmentPeriodId == periodId)
                    .ToListAsync();

                var initialStateByKey = backups
                    .GroupBy(b => GetShiftCellKey(b.UserId, b.ShiftDayId, b.ShiftType))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(b => b.BackedUpAt).First().ShiftStatus);

                var existingSubmissions = await _context.ShiftSubmissions
                    .Where(s => shiftDayDict.Values.Contains(s.ShiftDayId))
                    .ToListAsync();

                var hasInitialConfirmation = await _context.ShiftEditLogs
                    .AnyAsync(l => shiftDayDict.Values.Contains(l.ShiftDayId));

                var submissionByKey = existingSubmissions
                    .GroupBy(s => GetShiftCellKey(s.UserId, s.ShiftDayId, s.ShiftType))
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                            .ThenByDescending(s => s.Id)
                            .First());

                foreach (var shift in request.ShiftUpdates)
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

                    ShiftState newState;
                    if (shift.ShiftState.HasValue && Enum.IsDefined(typeof(ShiftState), shift.ShiftState.Value))
                    {
                        newState = (ShiftState)shift.ShiftState.Value;
                    }
                    else
                    {
                        newState = shift.ShiftStatus switch
                        {
                            "〇" => ShiftState.Accepted,
                            "△" => ShiftState.WantToGiveAway,
                            "🔴" => ShiftState.KeyHolder,
                            "×" => ShiftState.None,
                            ""  => ShiftState.NotAccepted,
                            _   => ShiftState.None
                        };
                    }


                    ShiftType shiftType = (ShiftType)shift.ShiftType;
                    var submissionKey = GetShiftCellKey(shift.UserId, shiftDayId, shiftType);

                    submissionByKey.TryGetValue(submissionKey, out var existing);
                    var currentState = existing?.ShiftStatus ?? ShiftState.None;
                    if (currentState == newState)
                    {
                        continue;
                    }

                    var initialState = GetInitialState(initialStateByKey, shift.UserId, shiftDayId, shiftType);
                    var note = BuildEditLogNote(hasInitialConfirmation, initialState, currentState, newState, trimmedReason);

                    if (existing == null)
                    {
                        if (newState == ShiftState.None)
                        {
                            continue;
                        }

                        var targetUser = await _userManager.FindByIdAsync(shift.UserId);
                        var userRole = targetUser?.UserShiftRole ?? UserShiftRole.Normal;

                        var newSubmission = new ShiftSubmission
                        {
                            UserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            IsSelected = IsSelectedState(newState),
                            SubmittedAt = DateTime.UtcNow,
                            ShiftStatus = newState,
                            UserType = UserType.AdminUpdated,
                            UserShiftRole = userRole
                        };
                        _context.ShiftSubmissions.Add(newSubmission);
                        submissionByKey[submissionKey] = newSubmission;

                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            OldState = ShiftState.None,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = note
                        });
                    }
                    else
                    {
                        logs.Add(new ShiftEditLog
                        {
                            AdminUserId = adminUserId,
                            TargetUserId = shift.UserId,
                            ShiftDayId = shiftDayId,
                            ShiftType = shiftType,
                            OldState = existing.ShiftStatus,
                            NewState = newState,
                            EditDate = DateTime.UtcNow,
                            Note = note
                        });

                        existing.ShiftStatus = newState;
                        existing.IsSelected = IsSelectedState(newState);
                        existing.SubmittedAt = DateTime.UtcNow;
                        existing.UserType = UserType.AdminUpdated;
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

            // ===== 締切にする瞬間のみバックアップ =====
            if (period.IsOpen)
            {
                // 対象期間の ShiftDay
                var shiftDayIds = await _context.ShiftDays
                    .Where(d => d.RecruitmentPeriodId == id)
                    .Select(d => d.Id)
                    .ToListAsync();

                // ① 既存バックアップは初期提出状態として固定し、上書きしない
                var existingBackups = await _context.SubmitBackups
                    .Where(b => b.RecruitmentPeriodId == id)
                    .ToListAsync();

                var existingBackupKeys = existingBackups
                    .Select(b => GetShiftCellKey(b.UserId, b.ShiftDayId, b.ShiftType))
                    .ToHashSet();

                // ② 現在の提出済みシフトを取得
                var submissions = await _context.ShiftSubmissions
                    .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                    .ToListAsync();

                // ③ Backup 作成。既存キーは初期状態を守るため再作成しない。
                var backups = submissions
                    .Where(s => !existingBackupKeys.Contains(GetShiftCellKey(s.UserId, s.ShiftDayId, s.ShiftType)))
                    .Select(s => new SubmitBackup
                    {
                        RecruitmentPeriodId = id,
                        UserId = s.UserId,
                        ShiftDayId = s.ShiftDayId,
                        ShiftType = s.ShiftType,
                        ShiftStatus = s.ShiftStatus,
                        BackedUpAt = DateTime.UtcNow
                    })
                    .ToList();

                _context.SubmitBackups.AddRange(backups);
            }

            // ===== 募集状態トグル =====
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

        [HttpGet]///※１
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

        [HttpGet]///※１
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

        [HttpPost]///※１
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
                workload.RequiredWorkers = DailyWorkload.CalculateRequiredWorkers(entry.Value);
            }

            await _context.SaveChangesAsync();
            await _pdfService.GenerateShiftPdfAsync(periodId);
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

                int requiredWorkers = workload.RequiredWorkers > 0
                    ? workload.RequiredWorkers
                    : DailyWorkload.CalculateRequiredWorkers(workload.RequiredCount);

                int targetNormal = requiredWorkers / 2;
                int targetKey = requiredWorkers - targetNormal;

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
                    int stillNeeded = requiredWorkers - selected.Count;
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

    public class ShiftUpdateRequest
    {
        public List<ShiftUpdateModel> ShiftUpdates { get; set; } = new();
        public string? Reason { get; set; }
    }

    public class ShiftUpdateModel
    {
        public string UserId { get; set; }
        public string Date { get; set; }
        public int ShiftType { get; set; }
        public int? ShiftState { get; set; }
        public string ShiftStatus { get; set; }
        public int RecruitmentPeriodId { get; set; }
    }
}
