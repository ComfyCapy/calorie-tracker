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
    public class PortionsModel : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

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
                .OrderBy(portion => portion.Amount)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
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

            if (!ModelState.IsValid)
            {
                Food = food;

                Portions = food.Portions
                    .OrderBy(portion => portion.Amount)
                    .ToList();

                return Page();
            }

            NewPortion.Name = NewPortion.Name.Trim();

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
                    !portion.Food.IsDeleted);

            if (portion == null)
            {
                return NotFound();
            }

            _context.FoodPortions.Remove(portion);

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
                    !portion.Food.IsDeleted);

            if (portion == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage =
                    "Portion name cannot be empty.";

                return RedirectToPage(new
                {
                    id
                });
            }

            if (amount <= 0)
            {
                StatusMessage =
                    "Portion amount must be greater than 0.";

                return RedirectToPage(new
                {
                    id
                });
            }

            portion.Name = name.Trim();
            portion.Amount = amount;

            await _context.SaveChangesAsync();

            StatusMessage = "Portion updated! ✨";

            return RedirectToPage(new
            {
                id
            });
        }
    }
}