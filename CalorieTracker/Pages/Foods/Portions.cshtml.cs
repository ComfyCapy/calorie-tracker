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
                    food.UserId == userId);

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
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId);

            if (food == null)
            {
                return NotFound();
            }

            NewPortion.FoodId = id;

            if (!ModelState.IsValid)
            {
                Food = food;

                Portions = await _context.FoodPortions
                    .Where(portion => portion.FoodId == id)
                    .OrderBy(portion => portion.Amount)
                    .ToListAsync();

                return Page();
            }

            _context.FoodPortions.Add(NewPortion);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
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
                    portion.Food!.UserId == userId);

            if (portion == null)
            {
                return NotFound();
            }

            _context.FoodPortions.Remove(portion);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
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
                    portion.Food!.UserId == userId);

            if (portion == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(name) || amount <= 0)
            {
                return RedirectToPage(new { id });
            }

            portion.Name = name.Trim();
            portion.Amount = amount;

            await _context.SaveChangesAsync();

            StatusMessage = "Portion updated! ✨";

            return RedirectToPage(new { id });
        }
    }
}