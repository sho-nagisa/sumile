using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using DotNetEnv;  // 追加

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
    options.Password.RequiredLength = 6;  // 8文字以上
    options.Password.RequireUppercase = false;  // 大文字不要
    options.Password.RequireLowercase = true;   // 小文字必要
    options.Password.RequireDigit = true;       // 数字必要
    options.Password.RequireNonAlphanumeric = false; // 特殊文字不要
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();


// MVC 用のサービス登録
builder.Services.AddControllersWithViews();

// カスタムサービスの DI 登録
builder.Services.AddScoped<IShiftService, ShiftService>();

var app = builder.Build();

// --- HTTP リクエストパイプラインの設定 ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS の使用（デフォルトは 30 日）
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 認証・認可のミドルウェア
app.UseAuthentication();
app.UseAuthorization();

// デフォルトのルーティング設定
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
