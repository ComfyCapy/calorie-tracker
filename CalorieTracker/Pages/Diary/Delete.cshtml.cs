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
        public DiaryEntry DiaryEntry { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var diaryEntry = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .FirstOrDefaultAsync(entry =>
                    entry.Id == id &&
                    entry.UserId == userId);

            if (diaryEntry == null)
            {
                return NotFound();
            }

            DiaryEntry = diaryEntry;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var diaryEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == DiaryEntry.Id &&
                    entry.UserId == userId);

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