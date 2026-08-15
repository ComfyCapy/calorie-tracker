using Microsoft.EntityFrameworkCore;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CalorieTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Food> Foods { get; set; }
        public DbSet<DiaryEntry> DiaryEntries { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
    }
}

