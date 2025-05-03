using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // セッション用
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace sumile.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ========== ユーザー登録 ==========
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            Debug.WriteLine("Register メソッドが呼ばれました");

            if (!ModelState.IsValid)
            {
                Debug.WriteLine("ModelState が無効");
                return View(model);
            }

            // CustomId 自動採番
            int newCustomId = 1;
            var existingIds = _userManager.Users
                .Select(u => u.CustomId)
                .OrderBy(id => id)
                .ToList();

            foreach (var id in existingIds)
            {
                if (id == newCustomId) newCustomId++;
                else break;
            }

            var user = new ApplicationUser
            {
                UserName = newCustomId.ToString(),
                CustomId = newCustomId,
                Name = model.Name,
                UserType = "0" // 登録時は基本的に Normal 扱いにしておく
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    Debug.WriteLine($"エラー: {error.Code} - {error.Description}");
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // 自動ログイン＋セッション保存
            await _signInManager.SignInAsync(user, isPersistent: false);
            HttpContext.Session.SetString("UserType", user.UserType ?? "Normal");
            HttpContext.Session.SetString("UserId", user.Id);

            TempData["SuccessMessage"] = "登録が成功しました";
            return RedirectToAction("Index", "Shift");
        }

        // ========== ログイン ==========
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // CustomId で検索
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.CustomId == model.CustomId);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "ログインに失敗しました。");
                return View(model);
            }

            // ★ ロックアウト対応：失敗時にカウント、ロック状態も確認
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                HttpContext.Session.SetString("UserType", user.UserType ?? "Normal");
                HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

                return RedirectToAction("Index", "Shift");
            }
            else if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "アカウントがロックされています。しばらくしてから再試行してください。");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "ログインに失敗しました。");
            return View(model);
        }


        // ========== ログアウト ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear(); // ★ セッションもクリアしておく
            return RedirectToAction("Index", "Home");
        }
    }
}
