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
    public class PortionsModel : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

        public int? EditedPortionId { get; set; }
        public string? EditedName { get; set; }
        public string? EditedAmount { get; set; }

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PortionsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Food Food { get; set; } = new();

        public List<FoodPortion> Portions { get; set; } = [];

        [BindProperty]
        public FoodPortion NewPortion { get; set; } = new();

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
                .Include(food => food.Portions)
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            Food = food;

            Portions = food.Portions
                .Where(portion => !portion.IsDeleted)
                .OrderBy(portion => portion.Amount)
                .ToList();

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

            var food = await _context.Foods
                .Include(food => food.Portions)
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            NewPortion.FoodId = food.Id;

            decimal canonicalAmount = 0;

            if (string.IsNullOrWhiteSpace(NewPortion.Name))
            {
                ModelState.AddModelError(
                    "NewPortion.Name",
                    "Please enter a portion name.");
            }

            if (NewPortion.Amount <= 0)
            {
                ModelState.AddModelError(
                    "NewPortion.Amount",
                    "Amount must be greater than 0.");
            }

            if (!MeasurementUnits.TryToCanonical(
                    NewPortion.Amount,
                    food.ServingUnit,
                    out canonicalAmount,
                    out _,
                    out _))
            {
                ModelState.AddModelError(
                    "NewPortion.Amount",
                    "The portion amount could not be converted.");
            }

            if (!ModelState.IsValid)
            {
                Food = food;

                Portions = food.Portions
                    .Where(portion => !portion.IsDeleted)
                    .OrderBy(portion => portion.Amount)
                    .ToList();

                return Page();
            }

            NewPortion.Name = NewPortion.Name.Trim();
            NewPortion.Amount = canonicalAmount;
            NewPortion.IsDeleted = false;

            _context.FoodPortions.Add(NewPortion);

            await _context.SaveChangesAsync();

            StatusMessage = "Portion added! ✨";

            return RedirectToPage(new
            {
                id = food.Id
            });
        }

        public async Task<IActionResult> OnPostDeleteAsync(
            int id,
            int portionId)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)) ||
                ValidationRules.HasBindingError(ModelState, nameof(portionId)))
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var portion = await _context.FoodPortions
                .Include(portion => portion.Food)
                .FirstOrDefaultAsync(portion =>
                    portion.Id == portionId &&
                    portion.FoodId == id &&
                    portion.Food != null &&
                    portion.Food.UserId == userId &&
                    !portion.IsDeleted &&
                    !portion.Food.IsDeleted);

            if (portion == null)
            {
                return NotFound();
            }

            portion.IsDeleted = true;

            await _context.SaveChangesAsync();

            StatusMessage = "Portion deleted.";

            return RedirectToPage(new
            {
                id
            });
        }

        public async Task<IActionResult> OnPostEditAsync(
            int id,
            int portionId,
            string name,
            decimal amount)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)) ||
                ValidationRules.HasBindingError(ModelState, nameof(portionId)))
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var portion = await _context.FoodPortions
                .Include(portion => portion.Food)
                .FirstOrDefaultAsync(portion =>
                    portion.Id == portionId &&
                    portion.FoodId == id &&
                    portion.Food != null &&
                    portion.Food.UserId == userId &&
                    !portion.IsDeleted &&
                    !portion.Food.IsDeleted);

            if (portion == null)
            {
                return NotFound();
            }

            // Only the row fields belong to this handler; NewPortion is the separate add form.
            ModelState.Remove("NewPortion.Name");
            ModelState.Remove("NewPortion.Amount");
            if (string.IsNullOrWhiteSpace(name))
                ModelState.AddModelError(nameof(name), "Portion name cannot be empty.");
            else if (name.Trim().Length > 50)
                ModelState.AddModelError(nameof(name), "Portion name must be 50 characters or fewer.");

            if (amount <= 0)
                ModelState.AddModelError(nameof(amount), "Portion amount must be greater than 0.");

            if (!MeasurementUnits.TryToCanonical(
                    amount, portion.Food!.ServingUnit,
                    out var canonicalAmount, out _, out _))
                ModelState.AddModelError(nameof(amount), "The portion amount could not be converted.");

            if (!ModelState.IsValid)
            {
                Food = portion.Food!;
                Portions = await _context.FoodPortions
                    .Where(item => item.FoodId == Food.Id && !item.IsDeleted)
                    .OrderBy(item => item.Amount)
                    .ToListAsync();
                EditedPortionId = portion.Id;
                EditedName = ModelState[nameof(name)]?.AttemptedValue ?? name;
                EditedAmount = ModelState[nameof(amount)]?.AttemptedValue;
                return Page();
            }

            portion.Name = name.Trim();
            portion.Amount = canonicalAmount;

            await _context.SaveChangesAsync();

            StatusMessage = "Portion updated! ✨";

            return RedirectToPage(new
            {
                id
            });
        }
    }
}
