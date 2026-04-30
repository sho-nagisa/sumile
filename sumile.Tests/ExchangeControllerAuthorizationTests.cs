using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace sumile.Tests;

public class ExchangeControllerAuthorizationTests
{
    [Fact]
    public void ExchangeController_RequiresAuthenticatedUser()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(global::ExchangeController),
            typeof(AuthorizeAttribute));

        Assert.NotNull(attribute);
    }
}
