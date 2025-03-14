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
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        // シフト提出期間を保持するstatic変数（必要に応じて利用）
        public static DateTime? SubmissionPeriodStart { get; set; }
        public static DateTime? SubmissionPeriodEnd { get; set; }

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        #region シフト一覧 (Index)

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // ========== 1. 最新の募集期間を取得 ==========
            var latestRecruitment = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            int days;
            DateTime startDate;

            if (latestRecruitment != null)
            {
                startDate = latestRecruitment.StartDate.Date;
                var endDate = latestRecruitment.EndDate.Date;

                // 「終了日 - 開始日 + 1」日分
                days = (endDate - startDate).Days + 1;
                // 万が一、終了日が開始日より前ならデフォルト10日
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

            // ========== 2. 日付リスト生成 ==========
            var dates = Enumerable.Range(0, days)
                .Select(i => startDate.AddDays(i))
                .ToList();

            // ========== 3. ユーザー情報を取得 ==========
            var users = await _userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.CustomId,
                    u.Name
                })
                .ToListAsync();

            // ========== 4. シフト提出情報を取得 (UTC に変換して保存済み) ==========
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            // ========== 5. ViewBag にデータを渡す ==========
            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.LoggedInUserId = _userManager.GetUserId(User);

            return View();
        }

        #endregion

        #region 募集期間・提出期間設定

        // 管理者用：シフト募集期間設定画面 (GET)
        [HttpGet]
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            var recruitmentPeriod = await _context.RecruitmentPeriods.FirstOrDefaultAsync();
            if (recruitmentPeriod == null)
            {
                recruitmentPeriod = new RecruitmentPeriod
                {
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(9)
                };
            }

            var model = new RecruitmentPeriodViewModel
            {
                StartDate = recruitmentPeriod.StartDate,
                EndDate = recruitmentPeriod.EndDate
            };
            return View(model);
        }

        // 管理者用：シフト募集期間設定の保存 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRecruitmentPeriod(RecruitmentPeriodViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // DateTime を UTC に変換して保存
            var startUtc = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);

            var recruitmentPeriod = await _context.RecruitmentPeriods.FirstOrDefaultAsync();
            if (recruitmentPeriod == null)
            {
                recruitmentPeriod = new RecruitmentPeriod
                {
                    StartDate = startUtc,
                    EndDate = endUtc
                };
                _context.RecruitmentPeriods.Add(recruitmentPeriod);
            }
            else
            {
                recruitmentPeriod.StartDate = startUtc;
                recruitmentPeriod.EndDate = endUtc;
                _context.RecruitmentPeriods.Update(recruitmentPeriod);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // 管理者用：シフト提出期間設定画面 (GET)
        [HttpGet]
        public IActionResult SetSubmissionPeriod()
        {
            var model = new SubmissionPeriodViewModel
            {
                StartDate = SubmissionPeriodStart ?? DateTime.Today,
                EndDate = SubmissionPeriodEnd ?? DateTime.Today.AddDays(9)
            };
            return View(model);
        }

        // 管理者用：シフト提出期間設定の保存 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetSubmissionPeriod(SubmissionPeriodViewModel model)
        {
            if (ModelState.IsValid)
            {
                SubmissionPeriodStart = model.StartDate;
                SubmissionPeriodEnd = model.EndDate;
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        #endregion

        #region シフト編集 (EditShifts / UpdateShifts)

        /// <summary>
        /// シフト編集画面 (管理者)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditShifts()
        {
            var latestRecruitment = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            int days;
            DateTime startDate;
            if (latestRecruitment != null)
            {
                startDate = latestRecruitment.StartDate.Date;
                var endDate = latestRecruitment.EndDate.Date;
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

            var dates = Enumerable.Range(0, days)
                .Select(i => startDate.AddDays(i))
                .ToList();

            var users = await _userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.CustomId,
                    u.Name
                })
                .ToListAsync();

            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;

            return View();
        }

        /// <summary>
        /// シフト更新処理 (JSON でやり取り)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateShifts([FromBody] List<ShiftUpdateModel> shiftUpdates)
        {
            try
            {
                foreach (var shift in shiftUpdates)
                {
                    // 送信された日付を UTC として扱う (Kind=Unspecifiedの場合を想定)
                    var dateUtc = DateTime.SpecifyKind(shift.Date.Date, DateTimeKind.Utc);

                    // DB上の既存データを検索（ユーザー、日付(同日)、シフト種類が同じもの）
                    var existing = await _context.ShiftSubmissions
                        .FirstOrDefaultAsync(s =>
                            s.UserId == shift.UserId &&
                            s.Date.Date == dateUtc.Date && // 日付のみ比較
                            s.ShiftType == shift.ShiftType
                        );

                    if (shift.IsSelected)
                    {
                        // 〇 (選択あり) → 既存がなければ新規作成
                        if (existing == null)
                        {
                            var newSubmission = new ShiftSubmission
                            {
                                UserId = shift.UserId,
                                Date = dateUtc,
                                ShiftType = shift.ShiftType,
                                IsSelected = true,
                                SubmittedAt = DateTime.UtcNow
                            };
                            _context.ShiftSubmissions.Add(newSubmission);
                        }
                    }
                    else
                    {
                        // × (選択解除) → 既存があれば削除
                        if (existing != null)
                        {
                            _context.ShiftSubmissions.Remove(existing);
                        }
                    }
                }

                // すべての変更をDBに保存
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // 例外発生時はJSONでエラーを返す
                return Json(new { success = false, error = ex.Message });
            }
        }

        #endregion
    }

    /// <summary>
    /// 管理者がシフト編集時に送信するデータ (JSON)
    /// </summary>
    public class ShiftUpdateModel
    {
        public string UserId { get; set; }
        public DateTime Date { get; set; }        // 例えば 2025-03-10T00:00:00Z
        public string ShiftType { get; set; }     // "Morning" or "Night"
        public bool IsSelected { get; set; }      // 〇→true, ×→false
    }
}
