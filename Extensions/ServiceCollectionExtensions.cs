using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sumile.Authorization;
using sumile.Data;
using sumile.Models;
using sumile.Services;

namespace sumile.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddApplicationDatabase(configuration);
            services.AddApplicationIdentity();
            services.AddApplicationAuthorizationPolicies();
            services.AddApplicationWebFeatures();
            services.AddDomainServices();

            return services;
        }

        private static IServiceCollection AddApplicationDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            return services;
        }

        private static IServiceCollection AddApplicationIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, IdentityRole>(ConfigureIdentityOptions)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            return services;
        }

        private static IServiceCollection AddApplicationAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AdminPolicy.Name, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new AdminRequirement());
                });
            });

            services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();

            return services;
        }

        private static IServiceCollection AddApplicationWebFeatures(this IServiceCollection services)
        {
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });

            services.AddControllersWithViews();

            return services;
        }

        private static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            services.AddScoped<ShiftPdfService>();
            services.AddScoped<IShiftService, ShiftService>();
            services.AddScoped<ShiftTableService>();
            services.AddScoped<ShiftStatusDisplayService>();
            services.AddScoped<ShiftSubmissionService>();
            services.AddScoped<ShiftPageService>();
            services.AddScoped<AutoShiftAssignmentService>();
            services.AddScoped<ShiftExchangeWorkflowService>();
            services.AddScoped<ExchangePageService>();
            services.AddScoped<AdminDashboardService>();
            services.AddScoped<AdminSubmissionPeriodService>();
            services.AddScoped<AdminShiftEditService>();

            return services;
        }

        private static void ConfigureIdentityOptions(IdentityOptions options)
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = false;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        }
    }
}
