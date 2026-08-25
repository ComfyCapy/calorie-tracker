using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<Food> FavouriteFoods { get; set; } = [];

        public List<Food> CustomFoods { get; set; } = [];

        public List<Food> RecentFoods { get; set; } = [];

        public string SearchTerm { get; set; } = string.Empty;

        public async Task OnGetAsync(string searchTerm)
        {
            SearchTerm = searchTerm;

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                FavouriteFoods = [];
                CustomFoods = [];
                RecentFoods = [];
                return;
            }

            // Database foods explicitly saved as favourites.
            var favouritesQuery = _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source != null &&
                    food.IsFavourite &&
                    !food.IsDeleted);

            // Custom foods created by the user.
            var customQuery = _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == null &&
                    !food.IsDeleted);

            // Search applies to favourites and custom foods.
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                favouritesQuery = favouritesQuery
                    .Where(food =>
                        food.Name.Contains(searchTerm));

                customQuery = customQuery
                    .Where(food =>
                        food.Name.Contains(searchTerm));
            }

            FavouriteFoods = await favouritesQuery
                .OrderBy(food => food.Name)
                .ToListAsync();

            CustomFoods = await customQuery
                .OrderBy(food => food.Name)
                .ToListAsync();

            // Database foods the user has recently logged.
            var recentEntries = await _context.DiaryEntries
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Food != null &&
                    entry.Food.Source != null &&
                    !entry.Food.IsDeleted)
                .Include(entry => entry.Food)
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Id)
                .Take(100)
                .ToListAsync();

            RecentFoods = recentEntries
                .Where(entry => entry.Food != null)
                .GroupBy(entry => entry.FoodId)
                .Select(group => group.First().Food!)
                .Take(10)
                .ToList();
        }
    }
}