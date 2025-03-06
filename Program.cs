using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sumile.Data; // �� ������DbContext��namespace

var builder = WebApplication.CreateBuilder(args);

// DbContext�i���ɐݒ�ς݁j
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ������ǉ� (AddIdentity)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // �p�X���[�h�|���V�[���̐ݒ�
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

// �����厖
app.UseAuthentication(); // Identity ���g���ɂ͕K�{
app.UseAuthorization();

// MVC���[�g + Identity ���񋟂���RazorPages�̃}�b�v
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
app.MapRazorPages(); // Identity ��UI��Razor Pages

app.Run();
