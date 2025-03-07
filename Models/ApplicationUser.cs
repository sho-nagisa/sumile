using Microsoft.AspNetCore.Identity;

namespace sumile.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }
        public string UserType { get; set; } = "Normal";
    }
}
