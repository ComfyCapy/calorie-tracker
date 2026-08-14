using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Foods
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public List<Food> Foods { get; set; } = [];
        public string SearchTerm { get; set; } = string.Empty;
        public async Task OnGetAsync(string searchTerm)
        {
            SearchTerm = searchTerm;
            var query = _context.Foods.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(food => food.Name.Contains(searchTerm));
            }
            Foods = await query.ToListAsync();
        }
    }
}