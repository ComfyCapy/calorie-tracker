using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CalorieTracker.Pages.Profile
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

        [BindProperty]
        public UserProfile UserProfile { get; set; } = new();

        [BindProperty]
        public int? HeightFeet { get; set; }

        [BindProperty]
        public decimal? HeightInches { get; set; }

        [BindProperty]
        public decimal? WeightLb { get; set; }

        [BindProperty]
        public decimal? GoalWeightLb { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile != null)
            {
                UserProfile = profile;

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
        }

        public async Task<IActionResult> OnPostAsync()
        {
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

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
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

            StatusMessage = "Profile saved successfully.";

            return RedirectToPage();
        }
    }
}