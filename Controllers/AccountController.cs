using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;  // ApplicationUser、ViewModel などの名前空間
using System.Threading.Tasks;
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

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email
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
                Console.WriteLine(error.Description);
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
                // ログイン成功 → シフト一覧へリダイレクト
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
