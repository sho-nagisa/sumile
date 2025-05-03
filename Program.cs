using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;
using DotNetEnv;  // .env�ǂݍ��ݗp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- ���ϐ��� `.env` ����ǂݍ��ށi���[�J���J���p�j ---
Env.Load();

// ���ϐ� `DB_CONNECTION_STRING` ���擾
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

// �ڑ������񂪐ݒ肳��Ă��Ȃ��ꍇ�̃G���[�n���h�����O
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING ���ϐ����ݒ肳��Ă��܂���B");
}

// �ݒ�I�u�W�F�N�g�ɐڑ��������ǉ�
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// --- �T�[�r�X�o�^ ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10); // ���b�N����
    options.Lockout.MaxFailedAccessAttempts = 5; // �ő厸�s��
    options.Lockout.AllowedForNewUsers = true;   // �V�K���[�U�[�ɂ����b�N�A�E�g��K�p
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// �� �Z�b�V�����̒ǉ��iUserType�ۑ��̂��߁j
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // �Z�b�V�����̗L������
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.MaxAge = TimeSpan.FromMinutes(30); // �N���C�A���g���ɂ��L��������ʒm
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS �̂Ƃ��̂ݑ��M
    options.Cookie.SameSite = SameSiteMode.Strict; // �N���X�T�C�g���M��h�~
});

// MVC �p�̃T�[�r�X�o�^
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ShiftPdfService>();
// �J�X�^���T�[�r�X�� DI �o�^
builder.Services.AddScoped<IShiftService, ShiftService>();

var app = builder.Build();

// --- HTTP ���N�G�X�g�p�C�v���C���̐ݒ� ---
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

// �� �Z�b�V�����̎g�p�iAuthentication���O�ł���ł��j
app.UseSession();

// �F�؁E�F�̃~�h���E�F�A
app.UseAuthentication();
app.UseAuthorization();

// �f�t�H���g�̃��[�e�B���O�ݒ�
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
