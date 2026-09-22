using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Pages.Diary
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DailyMaintenanceSnapshotService _snapshotService;
        private readonly IUserLocalTimeProvider _userLocalTimeProvider;
        private readonly ProgressionAchievementHooks _progressionHooks;
        private readonly CommunityFoodService? _community;

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            DailyMaintenanceSnapshotService snapshotService,
            IUserLocalTimeProvider userLocalTimeProvider,
            ProgressionAchievementHooks progressionHooks,
            CommunityFoodService? community = null)
        {
            _context = context;
            _userManager = userManager;
            _snapshotService = snapshotService;
            _userLocalTimeProvider = userLocalTimeProvider;
            _progressionHooks = progressionHooks;
            _community = community;
        }

        [BindProperty]
        public DiaryEntry DiaryEntry { get; set; } = new();

        [BindProperty]
        public string MeasurementMode { get; set; } = "Exact";

        [BindProperty]
        public int? SelectedPortionId { get; set; }

        [BindProperty]
        public decimal? PortionQuantity { get; set; }

        [BindProperty]
        public int? ApproximationPortionId { get; set; }

        [BindProperty]
        public string ApproximationSize { get; set; } = "Medium";

        public List<Food> FoodOptions { get; set; } = [];
        [BindProperty(SupportsGet = true)]
        public bool ReturnToFoodSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FoodSearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FoodSearchProvider { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ReturnToFoodsIndex { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FoodSource { get; set; } = "mine";

        [BindProperty(SupportsGet = true)]
        public string? FoodSearchSource { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CommunityPage { get; set; } = 1;

        public List<CommunityFoodSearchResult> CommunityFoods { get; private set; } = [];

        public bool CommunityHasNext { get; private set; }


        public async Task<IActionResult> OnGetAsync(
            DateTime? date,
            string? meal,
            int? foodId,
            CancellationToken cancellationToken = default)
        {
            FoodSource = string.IsNullOrWhiteSpace(FoodSource)
                ? "mine"
                : FoodSource.Trim().ToLowerInvariant();

            if (FoodSource is not ("mine" or "database" or "community"))
            {
                return BadRequest();
            }

            FoodSearchTerm ??= string.Empty;
            if (FoodSearchTerm.Length > 100)
            {
                return BadRequest();
            }

            if (!string.IsNullOrWhiteSpace(FoodSearchProvider))
            {
                FoodSearchProvider = FoodSearchProvider
                    .Trim()
                    .ToLowerInvariant();

                if (FoodSearchProvider != FoodCatalogueProviders.Cofid &&
                    FoodSearchProvider != FoodCatalogueProviders.Usda)
                {
                    return BadRequest();
                }
            }

            if (ValidationRules.HasBindingError(ModelState, nameof(date)) ||
                ValidationRules.HasBindingError(ModelState, nameof(meal)) ||
                ValidationRules.HasBindingError(ModelState, nameof(foodId)) ||
                ValidationRules.HasBindingError(ModelState, nameof(FoodSource)) ||
                ValidationRules.HasBindingError(ModelState, nameof(FoodSearchTerm)) ||
                ValidationRules.HasBindingError(ModelState, nameof(CommunityPage)))
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            DiaryEntry.Date = date ??
                _userLocalTimeProvider.Today.ToDateTime(TimeOnly.MinValue);

            if (DiaryEntry.Date.Date < ValidationRules.MinimumDiaryDate ||
                DiaryEntry.Date.Date > ValidationRules.MaximumDiaryDate)
            {
                return BadRequest();
            }

            if (!string.IsNullOrWhiteSpace(meal))
            {
                if (!ValidationRules.MealTypes.Contains(meal))
                {
                    return BadRequest();
                }

                DiaryEntry.MealType = meal;
            }

            if (foodId.HasValue)
            {
                DiaryEntry.FoodId = foodId.Value;
            }

            await LoadFoodOptionsAsync(userId);

            if (FoodSource == "community")
            {
                if (_community == null)
                {
                    throw new InvalidOperationException(
                        "Community food search is not configured.");
                }

                CommunityPage = Math.Clamp(CommunityPage, 1, 100000);
                CommunityFoods = await _community
                    .Search(FoodSearchTerm, User)
                    .Skip((CommunityPage - 1) * 20)
                    .Take(21)
                    .ToListAsync(cancellationToken);
                CommunityHasNext = CommunityFoods.Count > 20;
                CommunityFoods = CommunityFoods.Take(20).ToList();
            }

            if (foodId.HasValue &&
                FoodOptions.All(food => food.Id != foodId.Value))
            {
                return NotFound();
            }

            var selectedFood = FoodOptions.FirstOrDefault(food =>
                food.Id == foodId);

            var availablePortions = selectedFood?.Portions
                .Where(portion => !portion.IsDeleted)
                .ToList() ?? [];

            if (availablePortions.Count > 0)
            {
                MeasurementMode = "Portion";
                PortionQuantity = 1;

                if (availablePortions.Count == 1)
                {
                    SelectedPortionId = availablePortions[0].Id;
                }

                ApproximationPortionId = availablePortions[0].Id;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSelectCommunityAsync(
            int communityFoodId,
            CancellationToken cancellationToken = default)
        {
            if (ValidationRules.HasBindingError(
                    ModelState,
                    nameof(communityFoodId)) ||
                communityFoodId <= 0 ||
                DiaryEntry.Date.Date < ValidationRules.MinimumDiaryDate ||
                DiaryEntry.Date.Date > ValidationRules.MaximumDiaryDate ||
                !ValidationRules.MealTypes.Contains(DiaryEntry.MealType) ||
                FoodSearchTerm?.Length > 100)
            {
                return BadRequest();
            }

            if (_community == null)
            {
                throw new InvalidOperationException(
                    "Community food selection is not configured.");
            }

            var food = await _community.SelectAsync(
                User,
                communityFoodId,
                cancellationToken);

            if (food == null)
            {
                return NotFound();
            }

            return RedirectToPage(new
            {
                foodId = food.Id,
                date = DiaryEntry.Date.ToString("yyyy-MM-dd"),
                meal = DiaryEntry.MealType,
                returnToFoodSearch = true,
                foodSearchSource = "community",
                foodSearchTerm = FoodSearchTerm
            });
        }

        public async Task<IActionResult> OnPostAsync(
            CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            ValidationRules.ValidateDiaryDate(
                DiaryEntry.Date,
                ModelState,
                "DiaryEntry.Date");

            if (!ValidationRules.MealTypes.Contains(
                    DiaryEntry.MealType))
            {
                ModelState.AddModelError(
                    "DiaryEntry.MealType",
                    "Please select a valid meal.");
            }

            if (!ValidationRules.MeasurementModes.Contains(
                    MeasurementMode))
            {
                ModelState.AddModelError(
                    nameof(MeasurementMode),
                    "Please select a valid measurement mode.");
            }

            var selectedFood = await _context.Foods
                .Include(food => food.Portions)
                .FirstOrDefaultAsync(food =>
                    food.Id == DiaryEntry.FoodId &&
                    food.UserId == userId &&
                    !food.IsDeleted);

            if (selectedFood == null)
            {
                ModelState.AddModelError(
                    "DiaryEntry.FoodId",
                    "Please select a valid food.");
            }

            // Foods without portions can only use exact amounts.
            if (selectedFood != null &&
                MeasurementMode == "Portion" &&
                selectedFood.Portions.All(portion => portion.IsDeleted))
            {
                MeasurementMode = "Exact";
            }

            var measurement = DiaryMeasurementResolver.Resolve(
                new(MeasurementMode, DiaryEntry.Quantity, SelectedPortionId,
                    PortionQuantity, ApproximationPortionId, ApproximationSize),
                selectedFood,
                selectedFood?.Portions.Where(portion => !portion.IsDeleted).ToList() ?? []);
            DiaryEntry.Quantity = measurement.Quantity;
            foreach (var field in measurement.DerivedFields)
                ModelState.Remove(field);
            foreach (var error in measurement.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            var selectedPortion = measurement.Portion;

            if (MeasurementMode == "Approximate")
            {
                if (measurement.HasEstimateBasis)
                {
                    DiaryEntry.FoodPortionId = selectedPortion?.Id;
                    DiaryEntry.PortionQuantity = selectedPortion != null
                        ? measurement.EstimateMultiplier : null;
                }
                DiaryEntry.IsApproximate = true;
                DiaryEntry.ApproximationLabel = ApproximationSize;
            }
            else if (MeasurementMode == "Portion")
            {
                if (selectedPortion != null)
                {
                    DiaryEntry.FoodPortionId = selectedPortion.Id;
                    DiaryEntry.PortionQuantity = PortionQuantity;
                    DiaryEntry.IsApproximate = false;
                    DiaryEntry.ApproximationLabel = null;
                }
            }
            else
            {
                DiaryEntry.FoodPortionId = null;
                DiaryEntry.PortionQuantity = null;
                DiaryEntry.IsApproximate = false;
                DiaryEntry.ApproximationLabel = null;
            }

            if (!ModelState.IsValid)
            {
                await LoadFoodOptionsAsync(userId);

                return Page();
            }

            DiaryEntry.UserId = userId;

            DiaryEntry.CaptureSnapshot(selectedFood!, selectedPortion);

            _context.DiaryEntries.Add(DiaryEntry);

            await _snapshotService.EnsureSnapshotAsync(
                userId,
                DateOnly.FromDateTime(DiaryEntry.Date));
            await _snapshotService.SaveChangesAsync();
            await _progressionHooks.EvaluateDiaryAsync(
                userId,
                cancellationToken);
            TempData["UiStatusMessage"] = "Diary entry added.";

            return RedirectToPage("./Index", new
            {
                date = DiaryEntry.Date
                    .ToString("yyyy-MM-dd")
            });
        }

        private async Task LoadFoodOptionsAsync(
            string userId)
        {
            // Favourites.
            var favouriteFoods = await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.IsFavourite &&
                    !food.IsDeleted)
                .Include(food => food.Portions)
                .OrderBy(food => food.Name)
                .ToListAsync();

            // Custom foods.
            var customFoods = await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == null &&
                    !food.IsDeleted)
                .Include(food => food.Portions)
                .OrderBy(food => food.Name)
                .ToListAsync();

            // Recently logged database foods.
            var recentFoods = (await RecentFoodQuery.LoadAsync(
                _context,
                userId,
                includePortions: true))
                .Take(10)
                .ToList();

            // Keep a food selected through the search page available,
            // even before it has been favourited or logged.
            var selectedFoods = await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Id == DiaryEntry.FoodId &&
                    !food.IsDeleted)
                .Include(food => food.Portions)
                .ToListAsync();

            // Build one searchable list:
            // favourites first, then custom foods, recent foods,
            // and any food selected through the search page.
            // GroupBy prevents the same food appearing twice.
            FoodOptions = favouriteFoods
                .Concat(customFoods)
                .Concat(recentFoods)
                .Concat(selectedFoods)
                .GroupBy(food => food.Id)
                .Select(group => group.First())
                .ToList();
        }
    }
}
