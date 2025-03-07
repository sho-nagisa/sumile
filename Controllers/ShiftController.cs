using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System.Threading.Tasks;

namespace sumile.Controllers
{
    public class ShiftController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShiftController(ApplicationDbContext context)
        {
            _context = context;
        }

        // シフト一覧の表示
        public async Task<IActionResult> Index()
        {
            var shifts = await _context.Shifts.ToListAsync();
            return View(shifts);
        }

        // シフト作成フォームの表示
        public IActionResult Create()
        {
            return View();
        }

        // シフト作成処理（POST）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shift shift)
        {
            if (ModelState.IsValid)
            {
                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(shift);
        }
    }
}
