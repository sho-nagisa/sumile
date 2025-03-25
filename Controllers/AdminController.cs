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
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        #region シフト一覧 (Index)
        /// <summary>
        /// 管理者用のシフト一覧サンプル
        /// プルダウンで募集期間(periodId)を選択し、ShiftSubmissionsを表示
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            // 1. すべての募集期間を取得 (新しいID順)
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // 2. 選択されたperiodIdに対応するレコードを検索
            RecruitmentPeriod selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = allPeriods.FirstOrDefault(r => r.Id == periodId.Value);
            }
            // 3. 見つからなければ最新(先頭)を選ぶ
            if (selectedPeriod == null)
            {
                selectedPeriod = allPeriods.FirstOrDefault();
            }

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
                // 募集期間テーブルが空 → デフォルト設定
                days = 10;
                startDate = DateTime.Today;
            }

            // 4. 日付リスト生成
            var dates = Enumerable.Range(0, days)
                .Select(i => startDate.AddDays(i))
                .ToList();

            // 5. ユーザー情報を取得
            var users = await _userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.CustomId,
                    u.Name
                })
                .ToListAsync();

            // 6. シフト提出情報を取得
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            // 7. ViewBag に渡す
            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;

            // プルダウン表示用に全募集期間リスト＆選択IDを渡す
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = (selectedPeriod == null) ? (int?)null : selectedPeriod.Id;

            return View();
        }
        #endregion

        #region 募集期間設定 (RecruitmentPeriod)
        /// <summary>
        /// 募集期間設定画面 (GET)
        /// ※ ここでは最新を読み込んで初期表示にする例
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            var latest = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            // レコードが存在しない場合は仮の初期値
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

        /// <summary>
        /// 募集期間設定を保存 (POST)
        /// ※ 新規レコードとして保存して履歴を残す例
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRecruitmentPeriod(RecruitmentPeriodViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 入力された日付を UTC として扱う
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
        #endregion

        #region 提出期間設定 (SubmissionPeriod)
        /// <summary>
        /// 提出期間設定画面 (GET)
        /// ※ SubmissionPeriodテーブルを用いて最新を読み込み
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSubmissionPeriod(SubmissionPeriodViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 入力された日付を UTC として扱う
            var startUtc = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);

            var newPeriod = new SubmissionPeriod
            {
                StartDate = startUtc,
                EndDate = endUtc
            };

            _context.SubmissionPeriods.Add(newPeriod);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 提出期間設定を保存 (POST)
        /// ※ 新規レコードとして履歴を残す例
        /// </summary>
        #endregion

        #region シフト編集 (EditShifts / UpdateShifts)
        /// <summary>
        /// シフト編集画面 (管理者)
        /// プルダウンで募集期間を切り替えられるように、int? periodId を追加
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditShifts(int? periodId)
        {
            // 1. すべての募集期間を取得
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // 2. 選択されたperiodIdに対応するレコードを検索
            RecruitmentPeriod selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = allPeriods.FirstOrDefault(r => r.Id == periodId.Value);
            }
            // 3. 見つからなければ最新
            if (selectedPeriod == null)
            {
                selectedPeriod = allPeriods.FirstOrDefault();
            }

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
                // レコードがなければデフォルト10日分
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

            // ViewBagに必要情報をセット
            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;

            // プルダウン用に全Periods & 選択中のID
            ViewBag.RecruitmentPeriods = allPeriods;
            ViewBag.SelectedPeriodId = (selectedPeriod == null) ? (int?)null : selectedPeriod.Id;

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
                    // shift.Date は string なので、DateTime にパースしてから Kind を UTC に指定
                    var parsedDate = DateTime.Parse(shift.Date);
                    var dateUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                    // DB上の既存データを検索（ユーザー、日付(同日)、シフト種類が同じもの）
                    var existing = await _context.ShiftSubmissions
                        .FirstOrDefaultAsync(s =>
                            s.UserId == shift.UserId &&
                            s.Date.Date == dateUtc.Date &&
                            s.ShiftType == shift.ShiftType
                        );

                    if (shift.IsSelected)
                    {
                        // 〇 (選択あり) → 既存なしなら新規
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

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // 例外発生時はJSONでエラー
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
        public string Date { get; set; }
        public string ShiftType { get; set; }
        public bool IsSelected { get; set; }
        public string ShiftStatus { get; set; } // "×","〇","△","" 等
    }
}
