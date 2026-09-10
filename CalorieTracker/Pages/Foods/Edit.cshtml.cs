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

        public bool ServingBasisIsLocked { get; private set; }

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
            ServingBasisIsLocked = await HasServingHistoryAsync(food.Id);

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

            var hasServingHistory = await HasServingHistoryAsync(existingFood.Id);
            ServingBasisIsLocked = hasServingHistory;

            var servingBasisChanged =
                Food.ServingBasis != existingFood.ServingBasis;
            var servingBasisChangeBlocked =
                hasServingHistory && servingBasisChanged;

            if (!servingBasisChanged &&
                Food.ServingBasis == FoodServingBasis.Measured &&
                MeasurementUnits.TryNormalize(
                    Food.ServingUnit,
                    out _,
                    out newDimension) &&
                MeasurementUnits.TryNormalize(
                    existingFood.ServingUnit,
                    out _,
                    out var existingDimension) &&
                existingDimension != newDimension)
            {
                // A dimension change would reinterpret stored portions and diary quantities.
                if (hasServingHistory)
                {
                    ModelState.AddModelError(
                        "Food.ServingUnit",
                        "A food with portions or diary history cannot change between mass and volume units.");
                }
            }

            if (servingBasisChangeBlocked)
            {
                Food.ServingBasis = existingFood.ServingBasis;
                ModelState.Remove("Food.ServingBasis");
            }

            if (!ModelState.IsValid || servingBasisChangeBlocked)
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
            existingFood.ServingBasis = Food.ServingBasis;
            existingFood.PortionLabel = Food.PortionLabel;
            existingFood.ServingUnit = Food.ServingUnit;
            existingFood.CanonicalServingSize =
                Food.CanonicalServingSize;

            await _context.SaveChangesAsync();
            TempData["UiStatusMessage"] = "Food updated.";

            return RedirectToPage("./Index");
        }

        private async Task<bool> HasServingHistoryAsync(int foodId) =>
            await _context.FoodPortions.AnyAsync(portion =>
                portion.FoodId == foodId) ||
            await _context.DiaryEntries.AnyAsync(entry =>
                entry.FoodId == foodId);
    }
}
