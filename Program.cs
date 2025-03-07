using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;

var builder = WebApplication.CreateBuilder(args);

// --- サービス登録 ---

// appsettings.json に設定した DefaultConnection を利用（例: PostgreSQL）
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity の設定：ApplicationUser と IdentityRole を使用
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
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
