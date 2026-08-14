using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Diary
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<DiaryEntry> Entries { get; set; } = [];
        public decimal TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbohydrates { get; set; }
        public decimal TotalFat { get; set; }
        public DateTime SelectedDate { get; set; }
        public DateTime PreviousDate => SelectedDate.AddDays(-1);
        public DateTime NextDate => SelectedDate.AddDays(1);
        public async Task OnGetAsync(DateTime? date)
        {
            SelectedDate = date?.Date ?? DateTime.Today;

            Entries = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .Where(entry => entry.Date.Date == SelectedDate)
                .ToListAsync();

            TotalCalories = Entries.Sum(entry => entry.CaloriesConsumed);
            TotalProtein = Entries.Sum(entry => entry.ProteinConsumed);
            TotalCarbohydrates = Entries.Sum(entry => entry.CarbohydratesConsumed);
            TotalFat = Entries.Sum(entry => entry.FatConsumed);
        }
    }
}