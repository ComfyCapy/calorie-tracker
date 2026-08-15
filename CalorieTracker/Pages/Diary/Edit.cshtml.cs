using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CalorieTracker.Pages.Diary
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
        public DiaryEntry DiaryEntry { get; set; } = new();

        public List<Food> FoodOptions { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var diaryEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == id &&
                    entry.UserId == userId);

            if (diaryEntry == null)
            {
                return NotFound();
            }

            DiaryEntry = diaryEntry;

            FoodOptions = await _context.Foods
                .OrderBy(food => food.Name)
                .ToListAsync();

            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                FoodOptions = await _context.Foods
                    .OrderBy(food => food.Name)
                    .ToListAsync();

                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == DiaryEntry.Id &&
                    entry.UserId == userId);

            if (existingEntry == null)
            {
                return NotFound();
            }

            existingEntry.Date = DiaryEntry.Date;
            existingEntry.MealType = DiaryEntry.MealType;
            existingEntry.FoodId = DiaryEntry.FoodId;
            existingEntry.Quantity = DiaryEntry.Quantity;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = existingEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}