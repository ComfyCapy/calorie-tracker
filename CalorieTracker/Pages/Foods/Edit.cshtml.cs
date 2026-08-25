using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public Food Food { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId);

            if (food == null)
            {
                return NotFound();
            }

            Food = food;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingFood = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == Food.Id &&
                    food.UserId == userId);

            if (existingFood == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            existingFood.Name = Food.Name;
            existingFood.Calories = Food.Calories;
            existingFood.Protein = Food.Protein;
            existingFood.Carbohydrates = Food.Carbohydrates;
            existingFood.Fat = Food.Fat;
            existingFood.ServingSize = Food.ServingSize;
            existingFood.ServingUnit = Food.ServingUnit;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}