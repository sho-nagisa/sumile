using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sumile.Data; // ������DbContext�̖��O���

var builder = WebApplication.CreateBuilder(args);

// DbContext �̓o�^�iNpgsql���g�p�j
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity �̓o�^�i�p�X���[�h�|���V�[���ɘa�j
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // �p�X���[�h�̐ݒ���ɘa�i�J���E�e�X�g�p�̐ݒ�ł��j
    options.Password.RequiredLength = 4;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC �� Razor Pages �̒ǉ�
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

app.UseAuthentication(); // Identity �𗘗p���邽�ߕK�{
app.UseAuthorization();

// MVC���[�g + RazorPages (Identity UI �p)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();