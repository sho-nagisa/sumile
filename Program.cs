using DotNetEnv;
using Microsoft.AspNetCore.DataProtection;
using PdfSharpCore.Fonts;
using sumile.Extensions;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionKeysPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "App_Data",
    "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING 環境変数が設定されていません。");
}

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
builder.Services.AddApplicationServices(builder.Configuration);

GlobalFontSettings.FontResolver = new CustomFontResolver();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
