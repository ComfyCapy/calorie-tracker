using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity;

namespace CalorieTracker.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;

        public UserProfile? UserProfile { get; set; }
    }
}