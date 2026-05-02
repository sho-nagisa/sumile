using Microsoft.AspNetCore.Authorization;
using sumile.Authorization;
using sumile.Controllers;
using Xunit;

namespace sumile.Tests;
// シフト交換に関するコントローラーの認可設定を検証するテストクラス
public class ExchangeControllerAuthorizationTests
{
    [Fact]
    // 対象の従業員だけがアクセスできることを検証するテスト
    public void ExchangeController_RequiresAuthenticatedUser()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(global::ExchangeController),
            typeof(AuthorizeAttribute));

        Assert.NotNull(attribute);
    }

    [Fact]
    // 管理者だけがアクセスできることを検証するテスト
    public void AdminController_RequiresAdminPolicy()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(AdminController),
            typeof(AuthorizeAttribute)) as AuthorizeAttribute;

        Assert.NotNull(attribute);
        Assert.Equal(AdminPolicy.Name, attribute.Policy);
    }

    [Theory]
    // 管理者だけがアクセスできることを検証するテスト
    [InlineData(nameof(global::ExchangeController.FinalizeExchange))]
    [InlineData(nameof(global::ExchangeController.RejectExchange))]
    public void ExchangeController_AdminActionsRequireAdminPolicy(string actionName)
    {
        var method = typeof(global::ExchangeController).GetMethod(actionName);
        var attribute = method?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault(a => a.Policy == AdminPolicy.Name);

        Assert.NotNull(attribute);
    }
}
