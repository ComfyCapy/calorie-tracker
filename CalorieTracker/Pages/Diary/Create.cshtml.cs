using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Diary
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public DiaryEntry DiaryEntry { get; set; } = new();

        public List<Food> FoodOptions { get; set; } = [];

        public async Task OnGetAsync(DateTime? date, string? meal)
        {
            if (date.HasValue)
            {
                DiaryEntry.Date = date.Value;
            }

            if (!string.IsNullOrWhiteSpace(meal))
            {
                DiaryEntry.MealType = meal;
            }

            FoodOptions = await _context.Foods
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                FoodOptions = await _context.Foods
                    .OrderBy(f => f.Name)
                    .ToListAsync();

                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            DiaryEntry.UserId = userId;

            _context.DiaryEntries.Add(DiaryEntry);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = DiaryEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}