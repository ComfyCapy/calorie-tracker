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
            if (UserProfile.DateOfBirth.HasValue &&
                UserProfile.DateOfBirth.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    "UserProfile.DateOfBirth",
                    "Date of birth cannot be in the future.");
            }

            if (UserProfile.MeasurementSystem == "Imperial")
            {
                if (HeightFeet.HasValue && HeightInches.HasValue)
                {
                    var totalInches =
                        (HeightFeet.Value * 12m) + HeightInches.Value;

                    UserProfile.HeightCm = totalInches * 2.54m;

                    ModelState.Remove("UserProfile.HeightCm");
                }

                if (WeightLb.HasValue)
                {
                    UserProfile.WeightKg =
                        WeightLb.Value / 2.2046226218m;

                    ModelState.Remove("UserProfile.WeightKg");
                }

                if (GoalWeightLb.HasValue)
                {
                    UserProfile.GoalWeightKg =
                        GoalWeightLb.Value / 2.2046226218m;

                    ModelState.Remove("UserProfile.GoalWeightKg");
                }
                else
                {
                    UserProfile.GoalWeightKg = null;
                    ModelState.Remove("UserProfile.GoalWeightKg");
                }
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

            if (!UseCustomCalorieTarget)
            {
                UserProfile.CustomCalorieTarget = null;

                ModelState.Remove(
                    "UserProfile.CustomCalorieTarget");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (UserProfile.Goal == "Maintain")
            {
                UserProfile.GoalWeightKg = null;
                UserProfile.WeeklyGoalKg = null;
                GoalWeightLb = null;
            }

            var existingProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

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