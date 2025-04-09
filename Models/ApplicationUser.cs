using Microsoft.AspNetCore.Identity;

namespace sumile.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }
        public string UserType { get; set; } = "Normal";
        public int CustomId { get; set; }
        public UserShiftRole UserShiftRole { get; set; }
        public int Gender {  get; set; }
    }
}
