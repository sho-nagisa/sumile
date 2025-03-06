using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;  // ViewModelたちの名前空間
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // [Authorize] 属性を使う場合

namespace sumile.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
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

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // 作成成功 → ログインさせて MyPage へ
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("MyPage", "Account");
            }

            foreach (var error in result.Errors)
            {
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
                // ログイン成功 → MyPage へ
                return RedirectToAction("MyPage");
            }

            // ログイン失敗
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

        // ========== ログイン後の画面（マイページ例） ==========
        [HttpGet]
        [Authorize] // ログイン中のみアクセス可能にする場合
        public IActionResult MyPage()
        {
            return View();
        }
    }
}
