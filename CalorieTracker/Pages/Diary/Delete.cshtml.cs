using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Diary
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public DiaryEntry DiaryEntry { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var diaryEntry = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .FirstOrDefaultAsync(entry => entry.Id == id);

            if (diaryEntry == null)
            {
                return NotFound();
            }

            DiaryEntry = diaryEntry;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var diaryEntry = await _context.DiaryEntries
                .FindAsync(DiaryEntry.Id);

            if (diaryEntry == null)
            {
                return NotFound();
            }

            var entryDate = diaryEntry.Date;

            _context.DiaryEntries.Remove(diaryEntry);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = entryDate.ToString("yyyy-MM-dd")
            });
        }
    }
}