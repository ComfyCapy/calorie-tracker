using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Diary
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public DiaryEntry DiaryEntry { get; set; } = new();

        public List<Food> FoodOptions { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var diaryEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry => entry.Id == id);

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

            _context.DiaryEntries.Update(DiaryEntry);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = DiaryEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}