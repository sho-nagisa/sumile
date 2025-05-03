using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using DotNetEnv;  // .env読み込み用
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- 環境変数を `.env` から読み込む（ローカル開発用） ---
Env.Load();

// 環境変数 `DB_CONNECTION_STRING` を取得
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

// 接続文字列が設定されていない場合のエラーハンドリング
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING 環境変数が設定されていません。");
}

// 設定オブジェクトに接続文字列を追加
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// --- サービス登録 ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10); // ロック時間
    options.Lockout.MaxFailedAccessAttempts = 5; // 最大失敗回数
    options.Lockout.AllowedForNewUsers = true;   // 新規ユーザーにもロックアウトを適用
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ★ セッションの追加（UserType保存のため）
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // セッションの有効時間
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.MaxAge = TimeSpan.FromMinutes(30); // クライアント側にも有効期限を通知
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS のときのみ送信
    options.Cookie.SameSite = SameSiteMode.Strict; // クロスサイト送信を防止
});

// MVC 用のサービス登録
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ShiftPdfService>();
// カスタムサービスの DI 登録
builder.Services.AddScoped<IShiftService, ShiftService>();

var app = builder.Build();

// --- HTTP リクエストパイプラインの設定 ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    await next();
});

// ★ セッションの使用（Authenticationより前でも後でも可）
app.UseSession();

// 認証・認可のミドルウェア
app.UseAuthentication();
app.UseAuthorization();

// デフォルトのルーティング設定
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
