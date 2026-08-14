using Microsoft.EntityFrameworkCore;
using CalorieTracker.Models;

namespace CalorieTracker.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Food> Foods { get; set; }
    }
}

