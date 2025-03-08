using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
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

        // 管理者用：提出シフト一覧をユーザーごとに表示する (Index)
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int days = 10; // 10日分のデータを表示
            var startDate = DateTime.Today;
            var dates = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();

            // **users を dynamic に変換する**
            var users = await _userManager.Users
                .Select(u => new
                {
                    Id = u.Id,
                    CustomId = u.CustomId,
                    Name = u.Name
                })
                .ToListAsync();

            // **明示的に dynamic にキャスト**
            var dynamicUsers = users.Select(u => (dynamic)u).ToList();

            // `submissions` が null にならないように取得
            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            // **ViewBag に dynamicUsers をセット**
            ViewBag.Users = dynamicUsers;
            ViewBag.Dates = dates ?? new List<DateTime>();
            ViewBag.Submissions = submissions ?? new List<ShiftSubmission>();

            return View();
        }




        // 管理者用：シフト募集期間設定画面 (GET)
        [HttpGet]
        public async Task<IActionResult> SetRecruitmentPeriod()
        {
            // DBから既存の募集期間設定を取得。なければデフォルト値を使用
            var recruitmentPeriod = await _context.RecruitmentPeriods.FirstOrDefaultAsync();
            if (recruitmentPeriod == null)
            {
                recruitmentPeriod = new RecruitmentPeriod
                {
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(9)  // デフォルトで10日間
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
            if (ModelState.IsValid)
            {
                // DateTime 値を UTC に変換してから保存する
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
                return RedirectToAction("Index");
            }
            return View(model);
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
                return RedirectToAction("Index");
            }
            return View(model);
        }
    }
}
