using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public int PageSize { get; set; } = 10;

        public int FavouritesPage { get; set; } = 1;
        public int CustomPage { get; set; } = 1;
        public int RecentPage { get; set; } = 1;

        public int FavouritesTotalCount { get; set; }
        public int CustomTotalCount { get; set; }
        public int RecentTotalCount { get; set; }

        public int FavouritesTotalPages { get; set; }
        public int CustomTotalPages { get; set; }
        public int RecentTotalPages { get; set; }

        public async Task OnGetAsync(
            string searchTerm,
            int favouritesPage = 1,
            int customPage = 1,
            int recentPage = 1)
        {
            SearchTerm = searchTerm;

            FavouritesPage = Math.Max(1, favouritesPage);
            CustomPage = Math.Max(1, customPage);
            RecentPage = Math.Max(1, recentPage);

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                FavouriteFoods = [];
                CustomFoods = [];
                RecentFoods = [];
                return;
            }

            // Foods explicitly saved as favourites.
            var favouritesQuery = _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.IsFavourite &&
                    !food.IsDeleted);

            // Custom foods created by the user.
            var customQuery = _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == null &&
                    !food.IsDeleted);

            // Search applies to favourites, custom foods and recent foods.
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                favouritesQuery = favouritesQuery
                    .Where(food =>
                        food.Name.Contains(searchTerm));

                customQuery = customQuery
                    .Where(food =>
                        food.Name.Contains(searchTerm));
            }

            FavouritesTotalCount =
                await favouritesQuery.CountAsync();

            CustomTotalCount =
                await customQuery.CountAsync();

            FavouritesTotalPages =
                (int)Math.Ceiling(
                    FavouritesTotalCount /
                    (double)PageSize);

            CustomTotalPages =
                (int)Math.Ceiling(
                    CustomTotalCount /
                    (double)PageSize);

            if (FavouritesTotalPages > 0)
            {
                FavouritesPage =
                    Math.Min(
                        FavouritesPage,
                        FavouritesTotalPages);
            }

            if (CustomTotalPages > 0)
            {
                CustomPage =
                    Math.Min(
                        CustomPage,
                        CustomTotalPages);
            }

            FavouriteFoods = await favouritesQuery
                .OrderBy(food => food.Name)
                .Skip(
                    (FavouritesPage - 1) *
                    PageSize)
                .Take(PageSize)
                .ToListAsync();

            CustomFoods = await customQuery
                .OrderBy(food => food.Name)
                .Skip(
                    (CustomPage - 1) *
                    PageSize)
                .Take(PageSize)
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

            var recentFoodsQuery = recentEntries
                .Where(entry => entry.Food != null)
                .GroupBy(entry => entry.FoodId)
                .Select(group => group.First().Food!);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                recentFoodsQuery = recentFoodsQuery
                    .Where(food =>
                        food.Name.Contains(
                            searchTerm,
                            StringComparison.OrdinalIgnoreCase));
            }

            RecentTotalCount =
                recentFoodsQuery.Count();

            RecentTotalPages =
                (int)Math.Ceiling(
                    RecentTotalCount /
                    (double)PageSize);

            if (RecentTotalPages > 0)
            {
                RecentPage =
                    Math.Min(
                        RecentPage,
                        RecentTotalPages);
            }

            RecentFoods = recentFoodsQuery
                .Skip(
                    (RecentPage - 1) *
                    PageSize)
                .Take(PageSize)
                .ToList();
        }

        public async Task<IActionResult> OnPostFavouriteAsync(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            food.IsFavourite = true;

            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnfavouriteAsync(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.Id == id &&
                    food.UserId == userId &&
                    food.IsFavourite &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            food.IsFavourite = false;

            await _context.SaveChangesAsync();

            return RedirectToPage();
        }
    }
}