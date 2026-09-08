using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CalorieTracker.Services;

namespace CalorieTracker.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GoalTimelineCalculator _goalTimelineCalculator;
        private readonly CalorieBalanceYearService _calorieBalanceYearService;
        public UserProfile? UserProfile { get; set; }
        public UserCapyAppearance? CapyAppearance { get; set; }
        public CalorieBalanceYear? CalorieBalanceYear { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? Year { get; set; }
        public int SelectedYear { get; private set; }
        public int MinimumSupportedYear =>
            ValidationRules.MinimumDiaryDate.Year;
        public bool CanNavigatePrevious =>
            SelectedYear > MinimumSupportedYear;
        public bool CanNavigateNext =>
            SelectedYear < DateTime.Today.Year;
        public GoalTimelineResult GoalTimeline { get; set; } =
            new(GoalTimelineStatus.IncompleteProfile, ProfileOptions.Metric);
        public decimal CaloriesConsumed { get; set; }
        public decimal ProteinConsumed { get; set; }
        public decimal CarbohydratesConsumed { get; set; }
        public decimal FatConsumed { get; set; }

        public bool HasProfileEstimates =>
            UserProfile?.HasUsableCalorieEstimates == true;

        public decimal? CalorieTarget =>
            HasProfileEstimates
                ? UserProfile!.EffectiveCalorieTarget
                : null;

        public decimal? CaloriesRemaining =>
            CalorieTarget - CaloriesConsumed;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            GoalTimelineCalculator goalTimelineCalculator,
            CalorieBalanceYearService calorieBalanceYearService)
        {
            _context = context;
            _userManager = userManager;
            _goalTimelineCalculator = goalTimelineCalculator;
            _calorieBalanceYearService = calorieBalanceYearService;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Page();
            }

            if (ValidationRules.HasBindingError(ModelState, nameof(Year)))
            {
                return BadRequest();
            }

            var currentYear = DateTime.Today.Year;
            SelectedYear = Year ?? currentYear;

            if (SelectedYear < MinimumSupportedYear ||
                SelectedYear > currentYear)
            {
                return BadRequest();
            }

            CalorieBalanceYear = await _calorieBalanceYearService
                .GetYearAsync(userId, SelectedYear);

            UserProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile =>
                    profile.UserId == userId);
            GoalTimeline = _goalTimelineCalculator.Calculate(UserProfile);
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

            return Page();
        }
    }
}
