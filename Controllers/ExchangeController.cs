using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace sumile.Controllers
{
    public class ExchangeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExchangeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // シフト交換リクエストの一覧
        public async Task<IActionResult> Index()
        {
            // ログインユーザーが関係する交換リクエストのみ表示したい場合など、
            // 条件付きで絞り込みを行うことを想定
            var userId = _userManager.GetUserId(User); // ログイン中のユーザーID
            var exchangeRequests = await _context.ShiftExchanges
                .Include(e => e.ShiftAssignment)
                    .ThenInclude(sa => sa.Shift)
                .Include(e => e.RequestedByUser)
                .Include(e => e.AcceptedByUser)
                .Where(e => e.RequestedByUserId == userId || e.AcceptedByUserId == userId || e.Status == "Pending")
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(exchangeRequests);
        }

        // 新規シフト交換リクエスト作成フォームの表示
        [HttpGet]
        public IActionResult Create(int shiftAssignmentId)
        {
            // shiftAssignmentId を受け取り、どのシフトを交換に出すか指定
            // 必要に応じてバリデーションを挟む
            var model = new ShiftExchange
            {
                ShiftAssignmentId = shiftAssignmentId
            };
            return View(model);
        }

        // 新規シフト交換リクエスト作成（POST）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShiftExchange model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            model.RequestedByUserId = userId;
            model.Status = "Pending";
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            _context.ShiftExchanges.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // シフト交換リクエストを承諾する
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var exchange = await _context.ShiftExchanges
                .Include(e => e.ShiftAssignment)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exchange == null)
            {
                return NotFound();
            }

            // ログインユーザーを"承諾者"として設定し、ステータスを更新
            var userId = _userManager.GetUserId(User);
            exchange.AcceptedByUserId = userId;
            exchange.Status = "Accepted";
            exchange.UpdatedAt = DateTime.Now;

            // ここで実際に ShiftAssignment の持ち主を変更して
            // シフト交換を確定させるロジックなどを入れる場合がある
            // 例:
            // exchange.ShiftAssignment.UserId = userId;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // シフト交換リクエストを拒否する
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var exchange = await _context.ShiftExchanges
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exchange == null)
            {
                return NotFound();
            }

            exchange.Status = "Rejected";
            exchange.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
