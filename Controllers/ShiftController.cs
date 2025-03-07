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
        /// シフト提出フォームを表示
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Submission()
        {
            // RecruitmentPeriodsテーブルから最新レコードを取得
            var latestRecruitment = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            List<DateTime> dates;
            if (latestRecruitment != null)
            {
                var start = latestRecruitment.StartDate.Date;
                var end = latestRecruitment.EndDate.Date;

                // 期間が正しいか簡単にチェック
                if (end < start)
                {
                    // もし終了日が開始日より前ならデフォルト10日分にフォールバック
                    dates = Enumerable.Range(0, 10)
                        .Select(i => DateTime.Today.AddDays(i))
                        .ToList();
                }
                else
                {
                    // 開始日～終了日までの日数
                    int dayCount = (end - start).Days + 1; // 終了日を含める
                    dates = Enumerable.Range(0, dayCount)
                        .Select(i => start.AddDays(i))
                        .ToList();
                }
            }
            else
            {
                // レコードが無い場合はデフォルト10日分
                dates = Enumerable.Range(0, 10)
                    .Select(i => DateTime.Today.AddDays(i))
                    .ToList();
            }

            // View ではこの List<DateTime> をモデルとして使ってテーブルを作る
            return View(dates);
        }

        /// <summary>
        /// シフト提出フォームから送信されたデータを受け取り、ShiftSubmissions テーブルに登録する
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitShifts(string[] selectedShifts)
        {
            if (selectedShifts == null || selectedShifts.Length == 0)
            {
                return RedirectToAction("Index");
            }

            var userId = _userManager.GetUserId(User);
            var submissions = new List<ShiftSubmission>();

            foreach (var shiftData in selectedShifts)
            {
                var parts = shiftData.Split('|');
                if (parts.Length < 3) continue;

                // parts[0] は "yyyy-MM-dd" 形式などを想定
                var parsedDate = DateTime.Parse(parts[0]);

                // ここでは、入力された日時をローカル時刻とみなし、UTC に変換する例
                var localDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Local);
                var dateUtc = localDate.ToUniversalTime();

                var shiftType = parts[1];
                var isSelected = bool.Parse(parts[2]);

                if (isSelected)
                {
                    submissions.Add(new ShiftSubmission
                    {
                        Date = dateUtc,          // UTC に変換済み
                        ShiftType = shiftType,
                        UserId = userId,
                        IsSelected = true,
                        SubmittedAt = DateTime.UtcNow // 現在のUTC時刻を使用
                    });
                }
            }

            if (submissions.Any())
            {
                _context.ShiftSubmissions.AddRange(submissions);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
        /// <summary>
        /// ログインユーザーが提出したシフトの一覧表示
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SubmissionList()
        {
            var userId = _userManager.GetUserId(User);
            var submissions = await _context.ShiftSubmissions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.ShiftType)
                .ToListAsync();

            return View(submissions);
        }

        /// <summary>
        /// シフト一覧表示
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index(int? dayCount)
        {
            int days = dayCount ?? 10;
            var users = await _userManager.Users.ToListAsync();
            var startDate = DateTime.Today;
            var dates = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();

            // 登録されたシフト提出情報を全件取得（絞り込みは適宜アプリに応じて調整）
            var submissions = await _context.ShiftSubmissions.ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;

            // ログインユーザーのIDを取得
            var userId = _userManager.GetUserId(User);
            ViewBag.LoggedInUserId = userId;

            return View();
        }

        /// <summary>
        /// 新規シフト作成アクション
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult Create()
        {
            // このアクションから Submission() のビューへリダイレクト
            return RedirectToAction("Submission");
        }
    }
}
