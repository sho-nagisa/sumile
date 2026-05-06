using DotNetEnv;
using PdfSharpCore.Fonts;
using sumile.Extensions;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING 環境変数が設定されていません。");
}

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
builder.Services.AddApplicationServices(builder.Configuration);

GlobalFontSettings.FontResolver = new CustomFontResolver();

const string ContentSecurityPolicy =
    "default-src 'self'; " +
    "base-uri 'self'; " +
    "connect-src 'self'; " +
    "font-src 'self'; " +
    "form-action 'self'; " +
    "frame-ancestors 'none'; " +
    "img-src 'self' data:; " +
    "object-src 'self'; " +
    "script-src 'self' 'unsafe-inline'; " +
    "style-src 'self' 'unsafe-inline'";
const string PermissionsPolicy =
    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["X-XSS-Protection"] = "0";
        headers["Referrer-Policy"] = "no-referrer";
        return Task.CompletedTask;
    });

    await next();
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/shift_pdfs") &&
        string.Equals(Path.GetExtension(context.Request.Path.Value), ".pdf", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
