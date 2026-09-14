using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CommunityFoodService? _community;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CommunityFoodService? community = null)
        {
            _context = context;
            _userManager = userManager;
            _community = community;
        }

        public List<Food> FavouriteFoods { get; set; } = [];

        public List<Food> CustomFoods { get; set; } = [];

        public List<Food> RecentFoods { get; set; } = [];
        [BindProperty(SupportsGet = true)] public string? Source { get; set; } = "mine";
        [BindProperty(SupportsGet = true)] public int CommunityPage { get; set; } = 1;
        public List<CommunityFoodSearchResult> CommunityFoods { get; private set; } = [];
        public bool CommunityHasNext { get; private set; }

        [BindProperty(SupportsGet = true)]
        public bool ReturnToDiary { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DiaryDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DiaryMeal { get; set; }

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

        public async Task<IActionResult> OnGetAsync(
            string? searchTerm,
            int favouritesPage = 1,
            int customPage = 1,
            int recentPage = 1)
        {
            Source ??= "mine";
            if (!HasValidDiaryContext())
            {
                return BadRequest();
            }

            SearchTerm = searchTerm ?? string.Empty;
            if (SearchTerm.Length > 100 || Source is not ("mine" or "community" or "database")) return BadRequest();
            if (Source == "database") return RedirectToPage("Search", new { SearchTerm, ReturnToDiary, DiaryDate, DiaryMeal });
            if (Source == "community")
            {
                CommunityPage = Math.Clamp(CommunityPage, 1, 100000);
                CommunityFoods = await _community!.Search(SearchTerm, User).Skip((CommunityPage - 1) * 20).Take(21).ToListAsync();
                CommunityHasNext = CommunityFoods.Count > 20;
                CommunityFoods = CommunityFoods.Take(20).ToList();
                return Page();
            }

            FavouritesPage = Math.Max(1, favouritesPage);
            CustomPage = Math.Max(1, customPage);
            RecentPage = Math.Max(1, recentPage);

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                FavouriteFoods = [];
                CustomFoods = [];
                RecentFoods = [];
                return Page();
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
            var recentFoodsQuery = (await RecentFoodQuery.LoadAsync(
                _context,
                userId))
                .AsEnumerable();

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

            return Page();
        }

        public async Task<IActionResult> OnPostSelectCommunityAsync(int id, CancellationToken ct)
        {
            if (!HasValidDiaryContext()) return BadRequest();
            var food = await _community!.SelectAsync(User, id, ct);
            if (food == null) return NotFound();
            return RedirectToPage("/Diary/Create", new { foodId = food.Id,
                date = ReturnToDiary ? DiaryDate?.ToString("yyyy-MM-dd") : null,
                meal = ReturnToDiary ? DiaryMeal : null });
        }

        public async Task<IActionResult> OnPostVoteCommunityAsync(
            int id, int value, string? searchTerm, CancellationToken ct)
        {
            if (!HasValidDiaryContext() || searchTerm?.Length > 100) return BadRequest();
            try
            {
                var score = await _community!.VoteAsync(User, id, value, ct);
                if (score == null) return NotFound();
                TempData["UiStatusMessage"] = "Community vote updated.";
                return RedirectToPage(new { source = "community", searchTerm,
                    communityPage = CommunityPage, ReturnToDiary, DiaryDate, DiaryMeal });
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        public async Task<IActionResult> OnPostFavouriteAsync(
            int id,
            string? searchTerm,
            bool returnToDiary = false,
            DateTime? diaryDate = null,
            string? diaryMeal = null)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)))
            {
                return BadRequest();
            }

            if (!HasValidDiaryContext(diaryDate, diaryMeal))
            {
                return BadRequest();
            }

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

            return RedirectToPage(
                new
                {
                    searchTerm,
                    returnToDiary,
                    diaryDate = diaryDate?.ToString("yyyy-MM-dd"),
                    diaryMeal
                });
        }

        public async Task<IActionResult> OnPostUnfavouriteAsync(
            int id,
            string? searchTerm,
            bool returnToDiary = false,
            DateTime? diaryDate = null,
            string? diaryMeal = null)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)))
            {
                return BadRequest();
            }

            if (!HasValidDiaryContext(diaryDate, diaryMeal))
            {
                return BadRequest();
            }

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

            return RedirectToPage(
                new
                {
                    searchTerm,
                    returnToDiary,
                    diaryDate = diaryDate?.ToString("yyyy-MM-dd"),
                    diaryMeal
                });
        }

        private bool HasValidDiaryContext()
        {
            return ModelState.IsValid &&
                HasValidDiaryContext(DiaryDate, DiaryMeal);
        }

        private static bool HasValidDiaryContext(
            DateTime? diaryDate,
            string? diaryMeal)
        {
            return (!diaryDate.HasValue ||
                    (diaryDate.Value.Date >= ValidationRules.MinimumDiaryDate &&
                     diaryDate.Value.Date <= ValidationRules.MaximumDiaryDate)) &&
                (string.IsNullOrEmpty(diaryMeal) ||
                 ValidationRules.MealTypes.Contains(diaryMeal));
        }
    }
}
