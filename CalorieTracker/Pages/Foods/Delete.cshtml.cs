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
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DeleteModel(
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

            food.IsDeleted = true;

            await _context.SaveChangesAsync();
            TempData["UiStatusMessage"] = "Food deleted.";

            return RedirectToPage("./Index");
        }
    }
}
