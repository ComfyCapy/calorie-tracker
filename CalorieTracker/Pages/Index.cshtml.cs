using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public UserProfile? UserProfile { get; set; }
        public UserCapyAppearance? CapyAppearance { get; set; }
        public decimal CaloriesConsumed { get; set; }
        public decimal ProteinConsumed { get; set; }
        public decimal CarbohydratesConsumed { get; set; }
        public decimal FatConsumed { get; set; }

        public decimal? CalorieTarget =>
            UserProfile?.EffectiveCalorieTarget;

        public decimal? CaloriesRemaining =>
            CalorieTarget - CaloriesConsumed;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task OnGetAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return;
            }

            UserProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile =>
                    profile.UserId == userId);
            CapyAppearance = await _context.UserCapyAppearances
            .Include(appearance => appearance.Background)
            .Include(appearance => appearance.Expression)
            .Include(appearance => appearance.Clothes)
            .Include(appearance => appearance.NeckAccessory)
            .Include(appearance => appearance.HatHair)
            .Include(appearance => appearance.FaceAccessory)
            .FirstOrDefaultAsync(appearance =>
             appearance.UserId == userId);
            var today = DateTime.Today;

            var todaysEntries = await _context.DiaryEntries
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Date.Date == today)
                .ToListAsync();

            foreach (var entry in todaysEntries)
            {
                CaloriesConsumed += entry.CaloriesConsumed;
                ProteinConsumed += entry.ProteinConsumed;
                CarbohydratesConsumed += entry.CarbohydratesConsumed;
                FatConsumed += entry.FatConsumed;
            }


        }
    }
}
