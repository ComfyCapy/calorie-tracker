using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages.Foods
{
    public class  DeleteModel : PageModel 
    {
        private readonly ApplicationDbContext _context;
        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public Food Food { get; set; } = new Food();
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var food = await _context.Foods.FindAsync(id);
            if (food == null)
            {
                return NotFound();
            }
            Food = food;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var food = await _context.Foods.FindAsync(Food.Id);
            if (food == null)
            {
                return NotFound();
            }
            _context.Foods.Remove(food);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}