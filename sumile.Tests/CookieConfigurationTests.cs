using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using sumile.Extensions;
using Xunit;

namespace sumile.Tests;

public class CookieConfigurationTests
{
    [Fact]
    public void ApplicationCookie_IsExplicitlyHardened()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=sumile;Username=test;Password=test"
            })
            .Build();

        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var options = monitor.Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-sumile.auth", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.True(options.Cookie.IsEssential);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal("/Account/Login", options.LoginPath.ToString());
        Assert.Equal("/Account/Logout", options.LogoutPath.ToString());
        Assert.Equal("/Account/AccessDenied", options.AccessDeniedPath.ToString());
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }
}
