using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using sumile.Models;

namespace sumile.Authorization
{
    public static class AdminPolicy
    {
        public const string Name = "AdminOnly";
    }

    public sealed class AdminRequirement : IAuthorizationRequirement
    {
    }

    public sealed class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminAuthorizationHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var user = await _userManager.GetUserAsync(context.User);
            if (user?.IsAdmin == true)
            {
                context.Succeed(requirement);
            }
        }
    }
}
