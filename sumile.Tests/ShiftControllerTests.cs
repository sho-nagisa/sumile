using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using sumile.Controllers;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using Xunit;

namespace sumile.Tests;

public class ShiftControllerTests
{
    [Fact]
    // 募集期間が締め切られている場合に、エラーメッセージが表示されることを検証するテスト
    public async Task SubmitShifts_WhenPeriodIsClosed_RedirectsWithoutCreatingSubmissions()
    {
        await using var context = TestDb.CreateContext();
        var user = new ApplicationUser
        {
            Id = "user-closed",
            UserName = "closed@example.com",
            Name = "Closed User",
            UserShiftRole = UserShiftRole.Normal
        };

        context.Users.Add(user);
        context.RecruitmentPeriods.Add(new RecruitmentPeriod
        {
            Id = 1,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 1),
            IsOpen = false
        });
        context.ShiftDays.Add(new ShiftDay
        {
            Id = 101,
            Date = new DateTime(2026, 5, 1),
            RecruitmentPeriodId = 1
        });
        await context.SaveChangesAsync();

        using var userManager = CreateUserManager(context);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) },
                "TestAuth"))
        };
        var controller = new ShiftController(
            userManager,
            new ShiftSubmissionService(context),
            new ShiftPageService(context, new ShiftTableService(context)))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var result = await controller.SubmitShifts("[]", 1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Submission", redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["periodId"]);
        Assert.Equal("この募集期間は締め切られているため提出できません。", controller.TempData["ErrorMessage"]);
        Assert.Empty(await context.ShiftSubmissions.ToListAsync());
    }
    // 他のテストケース用の型
    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
    {
        return new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
