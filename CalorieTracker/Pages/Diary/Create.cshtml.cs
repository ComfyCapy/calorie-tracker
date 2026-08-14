using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Diary
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
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
            _context.DiaryEntries.Add(DiaryEntry);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index", new
            {
                date = DiaryEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}