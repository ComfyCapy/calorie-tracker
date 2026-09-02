using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CalorieTracker.Services;

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
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    food.Source == null &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            Food = food;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)))
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingFood = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    food.Source == null &&
                    !food.IsDeleted);

            if (existingFood == null)
            {
                return NotFound();
            }

            ValidationRules.ValidateFood(
                Food,
                ModelState,
                nameof(Food),
                out var newDimension);

            if (MeasurementUnits.TryNormalize(
                    Food.ServingUnit,
                    out _,
                    out newDimension) &&
                MeasurementUnits.TryNormalize(
                    existingFood.ServingUnit,
                    out _,
                    out var existingDimension) &&
                existingDimension != newDimension)
            {
                var hasPortions = await _context.FoodPortions
                    .AnyAsync(portion =>
                        portion.FoodId == existingFood.Id);

                var hasDiaryHistory = await _context.DiaryEntries
                    .AnyAsync(entry =>
                        entry.FoodId == existingFood.Id);

                if (hasPortions || hasDiaryHistory)
                {
                    ModelState.AddModelError(
                        "Food.ServingUnit",
                        "A food with portions or diary history cannot change between mass and volume units.");
                }
            }

            if (!ModelState.IsValid)
            {
                Food.Id = existingFood.Id;
                return Page();
            }

            existingFood.Name = Food.Name;
            existingFood.Calories = Food.Calories;
            existingFood.Protein = Food.Protein;
            existingFood.Carbohydrates = Food.Carbohydrates;
            existingFood.Fat = Food.Fat;
            existingFood.ServingSize = Food.ServingSize;
            existingFood.ServingUnit = Food.ServingUnit;
            existingFood.CanonicalServingSize =
                Food.CanonicalServingSize;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
