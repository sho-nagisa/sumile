using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Data;
using sumile.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace sumile.Controllers
{
    // 管理者だけアクセスできるようにする場合は、[Authorize(Roles="Administrator")]などを付与
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ユーザー一覧の表示
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // ユーザー昇格: UserType を "Veteran" に変更
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promote(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.UserType = "Veteran";
            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Index));
        }

        // ユーザー降格: UserType を "Normal" に変更 (等)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Demote(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.UserType = "Normal";
            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Index));
        }

        // シフト一覧 (管理者用)
        public async Task<IActionResult> ShiftList()
        {
            var shifts = await _context.Shifts.ToListAsync();
            return View(shifts);
        }

        // シフト手動編集フォーム
        [HttpGet]
        public async Task<IActionResult> EditShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            return View(shift);
        }

        // シフト手動編集の保存 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShift(Shift model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var shift = await _context.Shifts.FindAsync(model.Id);
            if (shift == null) return NotFound();

            // 必要な項目を更新
            shift.Date = model.Date;
            shift.ShiftType = model.ShiftType;
            shift.MaxCapacity = model.MaxCapacity;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ShiftList));
        }
    }
}
