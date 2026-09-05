using CalorieTracker.Data;
using CalorieTracker.Models;
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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public UserProfile UserProfile { get; set; } = new();

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

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile == null)
            {
                IsFirstTimeSetup = true;
                return;
            }

            UserProfile = profile;
            EstimatesProfile = profile;

            UseCustomCalorieTarget =
                profile.CustomCalorieTarget.HasValue;

            if (profile.MeasurementSystem == "Imperial")
            {
                var totalInches = profile.HeightCm / 2.54m;

                HeightFeet = (int)(totalInches / 12);
                HeightInches = totalInches - (HeightFeet.Value * 12);

                WeightLb = profile.WeightKg * 2.2046226218m;

                if (profile.GoalWeightKg.HasValue)
                {
                    GoalWeightLb =
                        profile.GoalWeightKg.Value * 2.2046226218m;
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (UserProfile.MeasurementSystem != "Metric" &&
                UserProfile.MeasurementSystem != "Imperial")
            {
                ModelState.AddModelError(
                    "UserProfile.MeasurementSystem",
                    "Please select a valid measurement system.");
            }

            if (UserProfile.ThemePreference != "System" &&
                UserProfile.ThemePreference != "Light" &&
                UserProfile.ThemePreference != "Dark")
            {
                ModelState.AddModelError(
                    "UserProfile.ThemePreference",
                    "Please select a valid theme.");
            }

            if (UserProfile.CalculationSex != "Male" &&
                UserProfile.CalculationSex != "Female")
            {
                ModelState.AddModelError(
                    "UserProfile.CalculationSex",
                    "Please select a valid calculation sex.");
            }

            if (UserProfile.ActivityLevel != "Sedentary" &&
                UserProfile.ActivityLevel != "LightlyActive" &&
                UserProfile.ActivityLevel != "ModeratelyActive" &&
                UserProfile.ActivityLevel != "VeryActive" &&
                UserProfile.ActivityLevel != "ExtraActive")
            {
                ModelState.AddModelError(
                    "UserProfile.ActivityLevel",
                    "Please select a valid activity level.");
            }

            if (UserProfile.Goal != "Lose" &&
                UserProfile.Goal != "Maintain" &&
                UserProfile.Goal != "Gain")
            {
                ModelState.AddModelError(
                    "UserProfile.Goal",
                    "Please select a valid goal.");
            }

            if (UserProfile.DateOfBirth.HasValue &&
                UserProfile.DateOfBirth.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    "UserProfile.DateOfBirth",
                    "Date of birth cannot be in the future.");
            }

            if (UserProfile.DateOfBirth.HasValue &&
                (UserProfile.Age < 18 || UserProfile.Age > 120))
            {
                ModelState.AddModelError(
                    "UserProfile.DateOfBirth",
                    "You must be between 18 and 120 years old.");
            }

            if (UserProfile.MeasurementSystem == "Imperial")
            {
                if (!HeightFeet.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(HeightFeet),
                        "Please enter your height in feet.");
                }

                if (!HeightInches.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(HeightInches),
                        "Please enter your remaining height in inches.");
                }

                if (HeightFeet.HasValue && HeightInches.HasValue)
                {
                    var totalInches =
                        (HeightFeet.Value * 12m) + HeightInches.Value;

                    UserProfile.HeightCm = totalInches * 2.54m;

                    ModelState.Remove("UserProfile.HeightCm");

                    if (UserProfile.HeightCm < 50 ||
                        UserProfile.HeightCm > 300)
                    {
                        ModelState.AddModelError(
                            nameof(HeightFeet),
                            "Height must convert to between 50 cm and 300 cm.");
                    }
                }

                if (!WeightLb.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(WeightLb),
                        "Please enter your current weight.");
                }
                else
                {
                    UserProfile.WeightKg =
                        WeightLb.Value / 2.2046226218m;

                    ModelState.Remove("UserProfile.WeightKg");

                    if (UserProfile.WeightKg < 20 ||
                        UserProfile.WeightKg > 500)
                    {
                        ModelState.AddModelError(
                            nameof(WeightLb),
                            "Weight must convert to between 20 kg and 500 kg.");
                    }
                }

                if (GoalWeightLb.HasValue)
                {
                    UserProfile.GoalWeightKg =
                        GoalWeightLb.Value / 2.2046226218m;

                    ModelState.Remove("UserProfile.GoalWeightKg");

                    if (UserProfile.GoalWeightKg < 20 ||
                        UserProfile.GoalWeightKg > 500)
                    {
                        ModelState.AddModelError(
                            nameof(GoalWeightLb),
                            "Goal weight must convert to between 20 kg and 500 kg.");
                    }
                }
                else
                {
                    UserProfile.GoalWeightKg = null;
                    ModelState.Remove("UserProfile.GoalWeightKg");
                }
            }

            if ((UserProfile.Goal == "Lose" ||
                 UserProfile.Goal == "Gain") &&
                !UserProfile.GoalWeightKg.HasValue)
            {
                var fieldName = UserProfile.MeasurementSystem == "Imperial"
                    ? nameof(GoalWeightLb)
                    : "UserProfile.GoalWeightKg";

                ModelState.AddModelError(
                    fieldName,
                    "Please enter a goal weight.");
            }

            if ((UserProfile.Goal == "Lose" ||
                 UserProfile.Goal == "Gain") &&
                !UserProfile.WeeklyGoalKg.HasValue)
            {
                ModelState.AddModelError(
                    "UserProfile.WeeklyGoalKg",
                    "Please select a weekly weight change.");
            }

            if (UserProfile.WeeklyGoalKg.HasValue &&
                UserProfile.WeeklyGoalKg.Value != 0.25m &&
                UserProfile.WeeklyGoalKg.Value != 0.5m &&
                UserProfile.WeeklyGoalKg.Value != 0.75m &&
                UserProfile.WeeklyGoalKg.Value != 1.0m)
            {
                ModelState.AddModelError(
                    "UserProfile.WeeklyGoalKg",
                    "Please select a valid weekly weight change.");
            }

            if (UserProfile.Goal == "Lose" &&
            UserProfile.GoalWeightKg.HasValue &&
            UserProfile.GoalWeightKg.Value >= UserProfile.WeightKg)
            {
                var fieldName = UserProfile.MeasurementSystem == "Imperial"
                    ? nameof(GoalWeightLb)
                    : "UserProfile.GoalWeightKg";

                ModelState.AddModelError(
                    fieldName,
                    "Your goal weight must be lower than your current weight.");
            }

            if (UserProfile.Goal == "Gain" &&
                UserProfile.GoalWeightKg.HasValue &&
                UserProfile.GoalWeightKg.Value <= UserProfile.WeightKg)
            {
                var fieldName = UserProfile.MeasurementSystem == "Imperial"
                    ? nameof(GoalWeightLb)
                    : "UserProfile.GoalWeightKg";

                ModelState.AddModelError(
                    fieldName,
                    "Your goal weight must be higher than your current weight.");
            }

            if (UseCustomCalorieTarget &&
                !UserProfile.CustomCalorieTarget.HasValue)
            {
                ModelState.AddModelError(
                    "UserProfile.CustomCalorieTarget",
                    "Please enter a custom calorie target.");
            }
            else if (!UseCustomCalorieTarget)
            {
                UserProfile.CustomCalorieTarget = null;

                ModelState.Remove(
                    "UserProfile.CustomCalorieTarget");
            }

            if (UserProfile.Goal == "Maintain")
            {
                UserProfile.GoalWeightKg = null;
                UserProfile.WeeklyGoalKg = null;
                GoalWeightLb = null;
                ModelState.Remove("UserProfile.GoalWeightKg");
                ModelState.Remove("UserProfile.WeeklyGoalKg");
                ModelState.Remove(nameof(GoalWeightLb));
            }

            if (ModelState.IsValid &&
                !UseCustomCalorieTarget &&
                UserProfile.DailyCalorieTarget <= 0)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "These profile values do not produce a valid calculated calorie target.");
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (!ModelState.IsValid)
            {
                IsFirstTimeSetup = existingProfile == null;
                EstimatesProfile = existingProfile;
                return Page();
            }

            if (existingProfile == null)
            {
                UserProfile.UserId = userId;
                _context.UserProfiles.Add(UserProfile);
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
            }

            await _context.SaveChangesAsync();

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

            if (theme != "System" &&
                theme != "Light" &&
                theme != "Dark")
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
