using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.Services;

var builder = WebApplication.CreateBuilder(args);

// --- ���ϐ�����ڑ���������擾 ---
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

// Identity �̐ݒ�FApplicationUser �� IdentityRole ���g�p
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// MVC �p�̃T�[�r�X�o�^
builder.Services.AddControllersWithViews();

// �J�X�^���T�[�r�X�� DI �o�^
builder.Services.AddScoped<IShiftService, ShiftService>();

var app = builder.Build();

// --- HTTP ���N�G�X�g�p�C�v���C���̐ݒ� ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS �̎g�p�i�f�t�H���g�� 30 ���j
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// �F�؁E�F�̃~�h���E�F�A
app.UseAuthentication();
app.UseAuthorization();

// �f�t�H���g�̃��[�e�B���O�ݒ�
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
