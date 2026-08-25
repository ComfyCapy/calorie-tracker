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
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
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