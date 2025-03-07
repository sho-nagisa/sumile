using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

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
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 既存ユーザーの CustomId を取得して、最も小さい空いている値を決定する
            int newCustomId = 1;
            var existingIds = _userManager.Users
                                .Select(u => u.CustomId)
                                .OrderBy(id => id)
                                .ToList();
            foreach (var id in existingIds)
            {
                if (id == newCustomId)
                    newCustomId++;
                else
                    break;
            }

            var user = new ApplicationUser
            {
                // メールアドレスは使わず、ユーザー名に名前を設定
                UserName = model.Name,
                Name = model.Name,
                CustomId = newCustomId
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                // ログイン後はシフト一覧（ShiftController.Index）にリダイレクト
                return RedirectToAction("Index", "Shift");
            }

            foreach (var error in result.Errors)
            {
                // エラー内容をログ出力および ModelState に追加
                System.Console.WriteLine(error.Description);
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
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

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // ログイン成功時、シフト一覧に遷移するように変更
                return RedirectToAction("Index", "Shift");
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
            return RedirectToAction("Index", "Home");
        }
    }
}
