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

        /// <summary>
        /// 日付リストを生成する共通メソッド
        /// periodId が null なら「最新の募集期間」、指定ありなら該当Idの募集期間を探して日付リストを作成。
        /// 1件もなければデフォルト10日分。
        /// </summary>
        private async Task<List<DateTime>> GenerateDateListForRecruitment(int? periodId)
        {
            // 1. すべての募集期間を取得（新しい順）
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // 2. 指定された periodId のレコードを探す
            RecruitmentPeriod selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = allPeriods.FirstOrDefault(r => r.Id == periodId.Value);
            }
            // 3. 見つからなければ最新(先頭)
            if (selectedPeriod == null)
            {
                selectedPeriod = allPeriods.FirstOrDefault();
            }

            // 4. 日付の計算
            if (selectedPeriod == null)
            {
                // 募集期間テーブルが空 → デフォルト10日
                return Enumerable.Range(0, 10)
                    .Select(i => DateTime.Today.AddDays(i))
                    .ToList();
            }
            else
            {
                var startDate = selectedPeriod.StartDate.Date;
                var endDate = selectedPeriod.EndDate.Date;

                int days = (endDate - startDate).Days + 1;
                if (days < 1)
                {
                    days = 10;
                    startDate = DateTime.Today;
                }
                return Enumerable.Range(0, days)
                    .Select(i => startDate.AddDays(i))
                    .ToList();
            }
        }

        /// <summary>
        /// シフト一覧表示
        /// int? periodId を受け取り、該当の募集期間の日付リストを表示
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? periodId)
        {
            // ログインユーザー取得
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // CustomId が int 型なので、そのまま変換
            int customId = currentUser.CustomId;
            string customIdString = (customId < 0) ? "No user" : customId.ToString();
            // 名前がnull/空なら "No user"
            string userNameString = string.IsNullOrEmpty(currentUser.Name)
                ? "No user"
                : currentUser.Name;

            // 募集期間から日付リストを生成
            var dates = await GenerateDateListForRecruitment(periodId);

            // ユーザー情報を取得
            var users = await _userManager.Users
                .Select(u => new ShiftIndexViewModel.UserInfo
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name
                })
                .ToListAsync();

            // シフト提出情報を取得
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .Select(s => new ShiftIndexViewModel.SubmissionInfo
                {
                    UserId = s.UserId,
                    Date = s.Date,
                    ShiftType = s.ShiftType
                })
                .ToListAsync();

            // 募集期間リスト（プルダウン用）を取得
            var allRecs = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // ViewModelを作成して返す
            var model = new ShiftIndexViewModel
            {
                CurrentUserCustomId = customIdString,
                CurrentUserName = userNameString,
                Users = users,
                Dates = dates,
                Submissions = submissions,
                RecruitmentPeriods = allRecs,
                SelectedPeriodId = periodId
            };

            return View(model);
        }

        /// <summary>
        /// シフト提出フォーム
        /// Submission() でも同じ日付生成を共通メソッドにまとめる
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Submission(int? periodId)
        {
            // ログインユーザーをチェック
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int customId = currentUser.CustomId;
            string customIdString = (customId <= 0) ? "No user" : customId.ToString();
            string userNameString = string.IsNullOrEmpty(currentUser.Name)
                ? "No user"
                : currentUser.Name;

            ViewBag.CurrentUserCustomId = customIdString;
            ViewBag.CurrentUserName = userNameString;

            // 募集期間から日付リストを取得
            var dates = await GenerateDateListForRecruitment(periodId);

            ViewBag.Dates = dates;
            return View();
        }

        /// <summary>
        /// シフト提出フォーム（×→○のUIなど）で選択したシフトをJSONで受け取る
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitShifts([FromForm] string selectedShifts)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(selectedShifts))
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
                return RedirectToAction("Submission");
            }

            var shiftList = JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts);
            if (shiftList == null || shiftList.Count == 0)
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
                return RedirectToAction("Submission");
            }

            var submissions = new List<ShiftSubmission>();
            foreach (var shift in shiftList)
            {
                // DateTime を UTC に変換
                var parsedDate = DateTime.Parse(shift.Date);
                var dateUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                submissions.Add(new ShiftSubmission
                {
                    UserId = userId,
                    Date = dateUtc,
                    ShiftType = shift.ShiftType,
                    IsSelected = true,
                    SubmittedAt = DateTime.UtcNow
                });
            }

            if (submissions.Any())
            {
                _context.ShiftSubmissions.AddRange(submissions);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "シフトが提出されました。";
            }
            else
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// シフト提出一覧（ログインユーザー分）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SubmissioList(int? periodId)
        {
            // 1. ログインユーザーを取得
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 2. 募集期間一覧（プルダウン用）と「選択中の期間ID」を ViewBag に
            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            // 3. 日付リストを生成（該当 periodId が無ければ最新期間 or デフォルト10日）
            var dates = await GenerateDateListForRecruitment(periodId);
            ViewBag.Dates = dates;

            // 4. この画面は「ログイン中ユーザー1人分」だけ表示するので、Users には1件だけ入れる
            //    （ビュー側が「foreach(var user in users)」で回す想定のため、あえて1件のリストに）
            var users = new List<dynamic>()
            {
                new {
                    Id = currentUser.Id,
                    CustomId = currentUser.CustomId,
                    Name = currentUser.Name
                }
            };
            ViewBag.Users = users;

            // 5. シフト提出情報を期間内で絞って取得
            //    dates が空でなければ [最初の日付, 最後の日付] で Where する
            var submissions = new List<ShiftSubmission>();
            if (dates.Any())
            {
                var periodStart = dates.First();
                var periodEnd = dates.Last();

                submissions = await _context.ShiftSubmissions
                    .Where(s => s.UserId == currentUser.Id
                             && s.Date >= periodStart
                             && s.Date <= periodEnd)
                    .ToListAsync();
            }

            // 6. ビュー側で使うため ViewBag.Submissions に詰める
            ViewBag.Submissions = submissions;

            // 7. ビューを返す (今回のビューは「管理者用」風のデザイン例を流用)
            return View();
        }
        /// <summary>
        /// 特定募集期間内の提出シフト一覧
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SubmittedList(int? periodId)
        {
            // ログインユーザーを取得
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 募集期間一覧を取得（最新順）
            var recruitmentPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.RecruitmentPeriods = recruitmentPeriods;
            ViewBag.SelectedPeriodId = periodId;

            // 指定された募集期間で日付リストを生成（未指定なら最新募集期間、またはデフォルト10日分）
            var dates = await GenerateDateListForRecruitment(periodId);
            ViewBag.Dates = dates;

            // 募集期間の日付範囲でシフト提出情報を絞り込む
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
                // 日付リストが空の場合は空のリストを返す
                return View(new List<ShiftSubmission>());
            }
        }

        /// <summary>
        /// 新規シフト作成アクション
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Submission));
        }
    }

    /// <summary>
    /// JSONデータを受け取る用のViewModel
    /// </summary>
    public class ShiftSubmissionViewModel
    {
        public string Date { get; set; }
        public string ShiftType { get; set; }
    }
}
