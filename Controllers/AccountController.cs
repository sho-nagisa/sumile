using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using sumile.Authorization;

namespace sumile.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // ========== ユーザー登録 ==========
        [HttpGet]
        [Authorize(Policy = AdminPolicy.Name)]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [Authorize(Policy = AdminPolicy.Name)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
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
                Name = model.Name
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning(
                        "User registration failed with identity error {ErrorCode}: {ErrorDescription}",
                        error.Code,
                        error.Description);
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            TempData["SuccessMessage"] = $"従業員を登録しました。ログインID: {newCustomId}";
            return RedirectToAction(nameof(Register));
        }

        // ========== ログイン ==========
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
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
                user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                HttpContext.Session.SetString("UserId", user.Id);

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
