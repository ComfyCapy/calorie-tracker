using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Pages.Profile
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private const decimal CentimetresPerInch = 2.54m;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GoalTimelineCalculator _goalTimelineCalculator;
        private readonly DailyMaintenanceSnapshotService _snapshotService;
        private readonly IUserLocalTimeProvider _userLocalTimeProvider;
        private readonly ProgressionAchievementHooks _progressionHooks;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            GoalTimelineCalculator goalTimelineCalculator,
            DailyMaintenanceSnapshotService snapshotService,
            IUserLocalTimeProvider userLocalTimeProvider,
            ProgressionAchievementHooks progressionHooks)
        {
            _context = context;
            _userManager = userManager;
            _goalTimelineCalculator = goalTimelineCalculator;
            _snapshotService = snapshotService;
            _userLocalTimeProvider = userLocalTimeProvider;
            _progressionHooks = progressionHooks;
        }

        [BindProperty]
        public ProfileInput UserProfile { get; set; } = new();

        [BindProperty]
        public bool UseCustomCalorieTarget { get; set; }

        [BindProperty]
        [Range(
            1,
            9,
            ErrorMessage = "Please enter a height between 1 ft and 9 ft.")]
        public int? HeightFeet { get; set; }

        [BindProperty]
        [Range(
            0,
            11.99,
            ErrorMessage = "Please enter inches between 0 and 11.99.")]
        public decimal? HeightInches { get; set; }

        [BindProperty]
        [Range(
            44,
            1102,
            ErrorMessage = "Please enter a weight between 44 lb and 1,102 lb.")]
        public decimal? WeightLb { get; set; }

        [BindProperty]
        [Range(
            44,
            1102,
            ErrorMessage = "Please enter a goal weight between 44 lb and 1,102 lb.")]
        public decimal? GoalWeightLb { get; set; }

        [TempData]
        public string? ProfileStatusMessage { get; set; }

        public bool IsFirstTimeSetup { get; set; }
        public UserProfile? EstimatesProfile { get; set; }
        public DateOnly CurrentDate { get; private set; }
        public bool HasProfileEstimates =>
            EstimatesProfile?.HasUsableCalorieEstimatesOn(CurrentDate) == true;
        public GoalTimelineResult GoalTimeline { get; set; } =
            new(GoalTimelineStatus.IncompleteProfile, ProfileOptions.Metric);

        public async Task OnGetAsync()
        {
            CurrentDate = _userLocalTimeProvider.Today;
            var userId = _userManager.GetUserId(User);

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile == null)
            {
                IsFirstTimeSetup = true;
                return;
            }

            UserProfile = ProfileInput.FromEntity(profile);
            EstimatesProfile = profile;
            GoalTimeline = _goalTimelineCalculator.Calculate(profile, CurrentDate);

            UseCustomCalorieTarget =
                profile.CustomCalorieTarget.HasValue;

            if (profile.MeasurementSystem == ProfileOptions.Imperial)
            {
                var totalInches = profile.HeightCm / CentimetresPerInch;

                HeightFeet = (int)(totalInches / 12);
                HeightInches = totalInches - (HeightFeet.Value * 12);

                WeightLb = profile.WeightKg * ProfileOptions.PoundsPerKilogram;

                if (profile.GoalWeightKg.HasValue)
                {
                    GoalWeightLb =
                        profile.GoalWeightKg.Value * ProfileOptions.PoundsPerKilogram;
                }
            }
        }

        public async Task<IActionResult> OnPostAsync(
            CancellationToken cancellationToken = default)
        {
            CurrentDate = _userLocalTimeProvider.Today;
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            var processed = ProfileFormProcessor.Process(
                new(UserProfile.ToEntity(), UseCustomCalorieTarget, HeightFeet, HeightInches, WeightLb, GoalWeightLb),
                existingProfile, CurrentDate,
                ModelState.Where(field => field.Value?.Errors.Count > 0).Select(field => field.Key));
            UserProfile = ProfileInput.FromEntity(processed.CanonicalProfile);
            GoalWeightLb = processed.GoalWeightLb;
            foreach (var field in processed.ClearedFields)
                ModelState.Remove(field);
            foreach (var error in processed.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            if (!ModelState.IsValid)
            {
                IsFirstTimeSetup = existingProfile == null;
                EstimatesProfile = existingProfile;
                GoalTimeline = _goalTimelineCalculator.Calculate(existingProfile, CurrentDate);
                return Page();
            }

            UserProfile profileToSave;

            if (existingProfile == null)
            {
                profileToSave = UserProfile.ToEntity();
                profileToSave.UserId = userId;
                _context.UserProfiles.Add(profileToSave);
            }
            else
            {
                existingProfile.DateOfBirth = UserProfile.DateOfBirth;
                existingProfile.MeasurementSystem = UserProfile.MeasurementSystem;
                existingProfile.HeightCm = UserProfile.HeightCm;
                existingProfile.WeightKg = UserProfile.WeightKg;
                existingProfile.CalculationSex = UserProfile.CalculationSex;
                existingProfile.ActivityLevel = UserProfile.ActivityLevel;
                existingProfile.Goal = UserProfile.Goal;
                existingProfile.GoalWeightKg = UserProfile.GoalWeightKg;
                existingProfile.WeeklyGoalKg = UserProfile.WeeklyGoalKg;
                existingProfile.CustomCalorieTarget = UserProfile.CustomCalorieTarget;
                profileToSave = existingProfile;
            }

            await _snapshotService.FillMissingSnapshotsAsync(
                userId,
                profileToSave);
            await _snapshotService.SaveChangesAsync();
            await _progressionHooks.EvaluateProfileAsync(
                userId,
                cancellationToken);

            ProfileStatusMessage = "Profile saved successfully.";

            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostThemeAsync(string theme)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (theme != ProfileOptions.SystemTheme &&
                theme != ProfileOptions.LightTheme &&
                theme != ProfileOptions.DarkTheme)
            {
                return BadRequest();
            }

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile == null)
            {
                return new JsonResult(new { success = true, persisted = false });
            }

            profile.ThemePreference = theme;

            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }
    }
}
