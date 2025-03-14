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
            var latestRecruitment = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            List<DateTime> dates;
            if (latestRecruitment != null)
            {
                var start = latestRecruitment.StartDate.Date;
                var end = latestRecruitment.EndDate.Date;

                if (end < start)
                {
                    dates = Enumerable.Range(0, 10)
                        .Select(i => DateTime.Today.AddDays(i))
                        .ToList();
                }
                else
                {
                    int dayCount = (end - start).Days + 1;
                    dates = Enumerable.Range(0, dayCount)
                        .Select(i => start.AddDays(i))
                        .ToList();
                }
            }
            else
            {
                dates = Enumerable.Range(0, 10)
                    .Select(i => DateTime.Today.AddDays(i))
                    .ToList();
            }

            // ViewBag に日付リストを渡す
            ViewBag.Dates = dates;
            return View();
        }

        /// <summary>
        /// シフト提出フォーム（×→○のUIなど）で選択したシフトをJSONで受け取る例
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitShifts([FromForm] string selectedShifts)
        {
            // 例: JavaScriptで JSON.stringify([{ date: "2025-03-10", shiftType: "Morning" }, ...])
            // として hidden input にセット → ここで受け取る
            if (string.IsNullOrEmpty(selectedShifts))
            {
                TempData["ErrorMessage"] = "シフトを選択してください。";
                return RedirectToAction("Submission");
            }

            var userId = _userManager.GetUserId(User);
            var submissions = new List<ShiftSubmission>();

            // JSON をデシリアライズ
            var shiftList = JsonConvert.DeserializeObject<List<ShiftSubmissionViewModel>>(selectedShifts);

            foreach (var shift in shiftList)
            {
                // **DateTime を UTC に変換**
                var parsedDate = DateTime.Parse(shift.Date); // 2025-03-10
                // Kind が Unspecified の場合が多いので、まず UTC として扱う:
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

            return RedirectToAction("Index");
        }

        /// <summary>
        /// シフト提出一覧（ログインユーザー分）
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
        public async Task<IActionResult> Index()
        {
            // 1. 最新の募集期間を取得
            var latestRecruitment = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            // 2. 日数と開始日を決定
            int days;
            DateTime startDate;

            if (latestRecruitment != null)
            {
                // 募集期間がある場合は、その開始日～終了日を表示期間とする
                startDate = latestRecruitment.StartDate.Date;
                var endDate = latestRecruitment.EndDate.Date;

                // 「終了日 - 開始日 + 1」日分
                days = (endDate - startDate).Days + 1;

                // 万が一、終了日が開始日より前なら、デフォルト 10 日間にフォールバック
                if (days < 1)
                {
                    days = 10;
                    startDate = DateTime.Today;
                }
            }
            else
            {
                // 募集期間が無い場合、デフォルト 10 日
                days = 10;
                startDate = DateTime.Today;
            }

            // 3. 日付リストを生成
            var dates = Enumerable.Range(0, days)
                .Select(i => startDate.AddDays(i))
                .ToList();

            // 4. ユーザー情報を取得
            var users = await _userManager.Users
                .Select(u => new
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name
                })
                .ToListAsync();

            // 5. シフト提出情報を取得
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            // 6. ViewBag に格納
            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.LoggedInUserId = _userManager.GetUserId(User);

            return View();
        }

        /// <summary>
        /// 新規シフト作成アクション
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult Create()
        {
            return RedirectToAction("Submission");
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
