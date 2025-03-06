using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sumile.Data; // ← 自分のDbContextのnamespace

var builder = WebApplication.CreateBuilder(args);

// DbContext（既に設定済み）
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ここを追加 (AddIdentity)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // パスワードポリシー等の設定
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC + Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ここ大事
app.UseAuthentication(); // Identity を使うには必須
app.UseAuthorization();

// MVCルート + Identity が提供するRazorPagesのマップ
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
app.MapRazorPages(); // Identity のUIはRazor Pages

app.Run();
