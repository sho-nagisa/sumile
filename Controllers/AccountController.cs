using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sumile.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using sumile.Authorization;
using sumile.Data;
using System.Data;
using System.Security.Cryptography;
using sumile.ViewModels;

namespace sumile.Controllers
{
    public class AccountController : Controller
    {
        private const int MaxCustomIdRegistrationAttempts = 5;
        private const long CustomIdAllocationLockKey = 2026051001L;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
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

            var registration = await RegisterWithUniqueCustomIdAsync(model);

            if (!registration.IdentityResult.Succeeded)
            {
                foreach (var error in registration.IdentityResult.Errors)
                {
                    _logger.LogWarning(
                        "User registration failed with identity error {ErrorCode}: {ErrorDescription}",
                        error.Code,
                        error.Description);
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            TempData["SuccessMessage"] = $"従業員を登録しました。ログインID: {registration.CustomId}";
            return RedirectToAction(nameof(Register));
        }

        private async Task<CustomIdRegistrationResult> RegisterWithUniqueCustomIdAsync(RegisterViewModel model)
        {
            for (var attempt = 1; attempt <= MaxCustomIdRegistrationAttempts; attempt++)
            {
                try
                {
                    var registration = _context.Database.IsRelational()
                        ? await RegisterWithRelationalTransactionAsync(model)
                        : await CreateUserWithNextCustomIdAsync(model);

                    if (!registration.IdentityResult.Succeeded &&
                        IsRetryableIdentityConflict(registration.IdentityResult) &&
                        attempt < MaxCustomIdRegistrationAttempts)
                    {
                        _logger.LogWarning(
                            "CustomId allocation conflicted on attempt {Attempt}; retrying registration.",
                            attempt);
                        _context.ChangeTracker.Clear();
                        continue;
                    }

                    return registration;
                }
                catch (Exception ex) when (IsRetryableRegistrationException(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "CustomId allocation failed with a retryable database error on attempt {Attempt}.",
                        attempt);
                    _context.ChangeTracker.Clear();
                }
            }

            return CustomIdRegistrationResult.Failed(
                "登録が同時に実行されたため、ログインIDを確定できませんでした。もう一度登録してください。");
        }

        private async Task<CustomIdRegistrationResult> RegisterWithRelationalTransactionAsync(RegisterViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            await AcquireCustomIdAllocationLockAsync();
            var registration = await CreateUserWithNextCustomIdAsync(model);
            if (!registration.IdentityResult.Succeeded)
            {
                return registration;
            }

            await transaction.CommitAsync();
            return registration;
        }

        private async Task AcquireCustomIdAllocationLockAsync()
        {
            if (_context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({CustomIdAllocationLockKey})");
            }
        }

        private async Task<CustomIdRegistrationResult> CreateUserWithNextCustomIdAsync(RegisterViewModel model)
        {
            var newCustomId = await GetNextAvailableCustomIdAsync();

            var user = new ApplicationUser
            {
                UserName = newCustomId.ToString(),
                CustomId = newCustomId,
                Name = model.Name,
                UserType = "0" // 登録時は基本的に Normal 扱いにしておく
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            return new CustomIdRegistrationResult(result, newCustomId);
        }

        private async Task<int> GetNextAvailableCustomIdAsync()
        {
            var existingIds = await _context.Users
                .Where(u => u.CustomId > 0)
                .Select(u => u.CustomId)
                .OrderBy(id => id)
                .ToListAsync();

            var newCustomId = 1;
            foreach (var id in existingIds)
            {
                if (id == newCustomId)
                {
                    newCustomId++;
                    continue;
                }

                if (id > newCustomId)
                {
                    break;
                }
            }

            return newCustomId;
        }

        private static bool IsRetryableIdentityConflict(IdentityResult result)
        {
            return result.Errors.Any(error => error.Code == "DuplicateUserName");
        }

        private static bool IsRetryableRegistrationException(Exception exception)
        {
            var postgresException = FindPostgresException(exception);
            return postgresException?.SqlState is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected;
        }

        private static PostgresException? FindPostgresException(Exception exception)
        {
            var current = exception;
            while (current != null)
            {
                if (current is PostgresException postgresException)
                {
                    return postgresException;
                }

                current = current.InnerException;
            }

            return null;
        }

        private sealed record CustomIdRegistrationResult(IdentityResult IdentityResult, int? CustomId)
        {
            public static CustomIdRegistrationResult Failed(string message)
            {
                return new CustomIdRegistrationResult(
                    IdentityResult.Failed(new IdentityError
                    {
                        Code = "CustomIdAllocationConflict",
                        Description = message
                    }),
                    null);
            }
        }

        // ========== マイページ ==========
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyPage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(ToShiftImportSettingsViewModel(user));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyPage(ShiftImportSettingsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                model.CustomId = user.CustomId;
                model.Name = user.Name;
                model.ShiftPdfSearchName = user.ShiftPdfSearchName;
                model.ShiftImportApiKey = user.ShiftImportApiKey;
                return View(model);
            }

            user.ShiftPdfSearchName = string.IsNullOrWhiteSpace(model.ShiftPdfSearchName)
                ? null
                : model.ShiftPdfSearchName.Trim();
            user.ShiftPdfStaffRowNumber = model.ShiftPdfStaffRowNumber;
            if (string.IsNullOrWhiteSpace(user.ShiftImportApiKey))
            {
                user.ShiftImportApiKey = await GenerateUniqueShiftImportApiKeyAsync();
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.CustomId = user.CustomId;
                model.Name = user.Name;
                model.ShiftPdfSearchName = user.ShiftPdfSearchName;
                model.ShiftImportApiKey = user.ShiftImportApiKey;
                return View(model);
            }

            TempData["SuccessMessage"] = "PDF取込設定を保存しました。";
            return RedirectToAction(nameof(MyPage));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateShiftImportApiKey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            user.ShiftImportApiKey = await GenerateUniqueShiftImportApiKeyAsync();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "ショートカット用キーを再発行できませんでした。";
                return RedirectToAction(nameof(MyPage));
            }

            TempData["SuccessMessage"] = "ショートカット用キーを再発行しました。";
            return RedirectToAction(nameof(MyPage));
        }

        private static ShiftImportSettingsViewModel ToShiftImportSettingsViewModel(ApplicationUser user)
        {
            return new ShiftImportSettingsViewModel
            {
                CustomId = user.CustomId,
                Name = user.Name,
                ShiftPdfSearchName = user.ShiftPdfSearchName,
                ShiftPdfStaffRowNumber = user.ShiftPdfStaffRowNumber,
                ShiftImportApiKey = user.ShiftImportApiKey
            };
        }

        private async Task<string> GenerateUniqueShiftImportApiKeyAsync()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var key = GenerateShiftImportApiKey();
                var exists = await _userManager.Users.AnyAsync(user => user.ShiftImportApiKey == key);
                if (!exists)
                {
                    return key;
                }
            }

            throw new InvalidOperationException("ショートカット用キーを生成できませんでした。");
        }

        private static string GenerateShiftImportApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ========== ログイン ==========
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
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
                HttpContext.Session.SetString("UserType", user.UserType ?? "Normal");
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
