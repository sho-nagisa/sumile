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

            return View(dates);
        }

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

                var parsedDate = DateTime.Parse(parts[0]);
                var localDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Local);
                var dateUtc = localDate.ToUniversalTime();
                var shiftType = parts[1];
                var isSelected = bool.Parse(parts[2]);

                if (isSelected)
                {
                    submissions.Add(new ShiftSubmission
                    {
                        Date = dateUtc,
                        ShiftType = shiftType,
                        UserId = userId,
                        IsSelected = true,
                        SubmittedAt = DateTime.UtcNow
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

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index(int? dayCount)
        {
            int days = dayCount ?? 10;
            var startDate = DateTime.Today;
            var dates = Enumerable.Range(0, days).Select(i => startDate.AddDays(i)).ToList();

            // **修正: ユーザー情報に `CustomId` を明示的に含める**
            var users = await _userManager.Users
                .Select(u => new
                {
                    Id = u.Id,
                    CustomId = u.CustomId, // ← **ここが重要！**
                    Name = u.Name
                })
                .ToListAsync();

            var submissions = await _context.ShiftSubmissions
                .Include(s => s.User)
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.Dates = dates;
            ViewBag.Submissions = submissions;
            ViewBag.LoggedInUserId = _userManager.GetUserId(User);

            return View();
        }

        [HttpGet]
        [Authorize]
        public IActionResult Create()
        {
            return RedirectToAction("Submission");
        }
    }
}
