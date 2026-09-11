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
        private readonly MacroTargetCalculator _macroTargetCalculator;
        private readonly IUserLocalTimeProvider _userLocalTimeProvider;
        public UserProfile? UserProfile { get; set; }
        public UserCapyAppearance? CapyAppearance { get; set; }
        public CalorieBalanceYear? CalorieBalanceYear { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? Year { get; set; }
        public int SelectedYear { get; private set; }
        public DateOnly CurrentDate { get; private set; }
        public int MinimumSupportedYear =>
            ValidationRules.MinimumDiaryDate.Year;
        public bool CanNavigatePrevious =>
            SelectedYear > MinimumSupportedYear;
        public bool CanNavigateNext =>
            SelectedYear < CurrentDate.Year;
        public GoalTimelineResult GoalTimeline { get; set; } =
            new(GoalTimelineStatus.IncompleteProfile, ProfileOptions.Metric);
        public decimal CaloriesConsumed { get; set; }
        public decimal ProteinConsumed { get; set; }
        public decimal CarbohydratesConsumed { get; set; }
        public decimal FatConsumed { get; set; }
        public MacroGoalProgress? MacroGoalProgress { get; set; }
        public string DashboardGreeting { get; private set; } = "Hello";

        public bool HasProfileEstimates =>
            UserProfile?.HasUsableCalorieEstimatesOn(CurrentDate) == true;

        public decimal? CalorieTarget =>
            HasProfileEstimates
                ? UserProfile!.CalculateEffectiveCalorieTarget(CurrentDate)
                : null;

        public decimal? CaloriesRemaining =>
            CalorieTarget - CaloriesConsumed;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            GoalTimelineCalculator goalTimelineCalculator,
            CalorieBalanceYearService calorieBalanceYearService,
            MacroTargetCalculator macroTargetCalculator,
            IUserLocalTimeProvider userLocalTimeProvider)
        {
            _context = context;
            _userManager = userManager;
            _goalTimelineCalculator = goalTimelineCalculator;
            _calorieBalanceYearService = calorieBalanceYearService;
            _macroTargetCalculator = macroTargetCalculator;
            _userLocalTimeProvider = userLocalTimeProvider;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            CurrentDate = _userLocalTimeProvider.Today;

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

            var currentYear = CurrentDate.Year;
            SelectedYear = Year ?? currentYear;

            if (SelectedYear < MinimumSupportedYear ||
                SelectedYear > currentYear)
            {
                return BadRequest();
            }

            var currentUser = await _userManager.FindByIdAsync(userId);
            var timeOfDayGreeting = _userLocalTimeProvider.LocalNow.Hour switch
            {
                < 4 => "Good evening",
                < 12 => "Good morning",
                < 18 => "Good afternoon",
                _ => "Good evening"
            };
            var greetingName = string.IsNullOrWhiteSpace(currentUser?.FirstName)
                ? currentUser?.UserName
                : currentUser.FirstName;
            DashboardGreeting = string.IsNullOrWhiteSpace(greetingName)
                ? timeOfDayGreeting
                : $"{timeOfDayGreeting}, {greetingName}";

            CalorieBalanceYear = await _calorieBalanceYearService
                .GetYearAsync(userId, SelectedYear, CurrentDate);

            UserProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile =>
                    profile.UserId == userId);
            GoalTimeline = _goalTimelineCalculator.Calculate(UserProfile, CurrentDate);
            CapyAppearance = await _context.UserCapyAppearances
            .Include(appearance => appearance.Background)
            .Include(appearance => appearance.Expression)
            .Include(appearance => appearance.Clothes)
            .Include(appearance => appearance.NeckAccessory)
            .Include(appearance => appearance.HatHair)
            .Include(appearance => appearance.FaceAccessory)
            .FirstOrDefaultAsync(appearance =>
             appearance.UserId == userId);
            var todayStart = CurrentDate.ToDateTime(TimeOnly.MinValue);
            var tomorrowStart = todayStart.AddDays(1);

            var todaysEntries = await _context.DiaryEntries
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Date >= todayStart &&
                    entry.Date < tomorrowStart)
                .ToListAsync();

            foreach (var entry in todaysEntries)
            {
                CaloriesConsumed += entry.CaloriesConsumed;
                ProteinConsumed += entry.ProteinConsumed;
                CarbohydratesConsumed += entry.CarbohydratesConsumed;
                FatConsumed += entry.FatConsumed;
            }

            MacroGoalProgress = _macroTargetCalculator
                .Calculate(UserProfile, CurrentDate)?
                .CreateProgress(
                    ProteinConsumed,
                    CarbohydratesConsumed,
                    FatConsumed);

            return Page();
        }
    }
}
