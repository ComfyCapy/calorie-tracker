using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Diary
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<DiaryEntry> Entries { get; set; } = [];

        public decimal TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbohydrates { get; set; }
        public decimal TotalFat { get; set; }

        public decimal DailyCalorieTarget { get; set; }
        public decimal CaloriesRemaining { get; set; }
        public decimal CalorieProgressPercent { get; set; }

        public DateTime SelectedDate { get; set; }

        public DateTime? PreviousDate =>
            SelectedDate > ValidationRules.MinimumDiaryDate
                ? SelectedDate.AddDays(-1)
                : null;

        public DateTime? NextDate =>
            SelectedDate < ValidationRules.MaximumDiaryDate
                ? SelectedDate.AddDays(1)
                : null;

        public async Task<IActionResult> OnGetAsync(DateTime? date)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);
            SelectedDate = date?.Date ?? DateTime.Today;

            if (SelectedDate < ValidationRules.MinimumDiaryDate ||
                SelectedDate > ValidationRules.MaximumDiaryDate)
            {
                return BadRequest();
            }

            Entries = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .Include(entry => entry.FoodPortion)
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Date.Date == SelectedDate)
                .ToListAsync();

            TotalCalories = Entries.Sum(entry => entry.CaloriesConsumed);
            TotalProtein = Entries.Sum(entry => entry.ProteinConsumed);
            TotalCarbohydrates = Entries.Sum(entry => entry.CarbohydratesConsumed);
            TotalFat = Entries.Sum(entry => entry.FatConsumed);

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile != null)
            {
                DailyCalorieTarget = profile.EffectiveCalorieTarget;
                CaloriesRemaining = DailyCalorieTarget - TotalCalories;

                if (DailyCalorieTarget > 0)
                {
                    CalorieProgressPercent =
                        (TotalCalories / DailyCalorieTarget) * 100;

                    CalorieProgressPercent =
                        Math.Min(CalorieProgressPercent, 100);
                }
            }

            return Page();
        }
    }
}
