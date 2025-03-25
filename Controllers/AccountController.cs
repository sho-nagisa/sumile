using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;
using System.Threading.Tasks;
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

            // 空の RegisterViewModel インスタンスを渡す
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            Debug.WriteLine("Register メソッドが呼ばれました"); // ここに到達するか確認
            if (!ModelState.IsValid)
            {
                Debug.WriteLine("ModelState が無効");
                return View(model);
            }

            Debug.WriteLine("ModelState は有効");
            // CustomId を自動採番（現在の最小空き番号を検索）
            int newCustomId = 1;
            var existingIds = _userManager.Users
                                .Select(u => u.CustomId)
                                .OrderBy(id => id)
                                .ToList();
            Debug.WriteLine($"既存のカスタムID: {string.Join(", ", existingIds)}");
            foreach (var id in existingIds)
            {
                if (id == newCustomId)
                    newCustomId++;
                else
                    break;
            }
            Debug.WriteLine($"新規 CustomId: {newCustomId}");
            var user = new ApplicationUser
            {
                UserName = newCustomId.ToString(), // カスタムIDを文字列として UserName に設定
                CustomId = newCustomId,
                Name = model.Name  // 登録された名前を設定
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            Debug.WriteLine($"ユーザーオブジェクト作成: UserName={user.UserName}, CustomId={user.CustomId}, Name={user.Name}");
            if (!result.Succeeded)
            {
                Debug.WriteLine("ユーザー作成失敗！エラー内容:");
                foreach (var error in result.Errors)
                {
                    Debug.WriteLine($"エラー: {error.Code} - {error.Description}");
                }
                return View(model);
            }
            if (result.Succeeded)
            {
                Debug.WriteLine("ユーザー作成失敗！エラー内容:");
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["SuccessMessage"] = "登録が成功しました";
                return RedirectToAction("Index", "Shift");
            }
            Debug.WriteLine("登録成功 - Shift/Index にリダイレクト");

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            TempData["SuccessMessage"] = "登録が成功しました";
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

            // CustomId から User を取得
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.CustomId == model.CustomId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "ログインに失敗しました。");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
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
