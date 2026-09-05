using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class ApiFoodModel : PageModel
    {
        private readonly ExternalFoodResolver _externalFoodResolver;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private Food? _resolvedFood;

        public ApiFoodModel(
            ExternalFoodResolver externalFoodResolver,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _externalFoodResolver = externalFoodResolver;
            _context = context;
            _userManager = userManager;
        }

        public FoodSearchResult? Food { get; set; }
        public bool IsFavourite { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        [BindProperty]
        public string ExternalId { get; set; } = string.Empty;

        [BindProperty]
        public decimal Quantity { get; set; } = 100;

        [BindProperty]
        public string MealType { get; set; } = "Dinner";

        [BindProperty]
        public DateTime Date { get; set; } = DateTime.Today;

        public async Task<IActionResult> OnGetAsync(
            string id,
            DateTime? date,
            string? meal,
            string? searchTerm)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (date.HasValue &&
                (date.Value.Date < ValidationRules.MinimumDiaryDate ||
                 date.Value.Date > ValidationRules.MaximumDiaryDate))
            {
                return BadRequest();
            }

            if (!string.IsNullOrWhiteSpace(meal) &&
                !ValidationRules.MealTypes.Contains(meal))
            {
                return BadRequest();
            }

            ExternalId = id;

            var failure = await LoadFoodAsync(userId);

            if (failure != null)
            {
                return failure;
            }

            ExternalId = _resolvedFood!.ExternalId!;
            SearchTerm = searchTerm ?? string.Empty;
            Date = date?.Date ?? DateTime.Today;
            MealType = meal ?? "Dinner";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var failure = await LoadFoodAsync(userId);

            if (failure != null)
            {
                return failure;
            }

            ValidationRules.ValidateDiaryDate(
                Date,
                ModelState,
                nameof(Date));

            if (!ValidationRules.MealTypes.Contains(MealType))
            {
                ModelState.AddModelError(
                    nameof(MealType),
                    "Please select a valid meal.");
            }

            if (Quantity <= 0)
            {
                ModelState.AddModelError(
                    nameof(Quantity),
                    "Quantity must be greater than 0.");
            }

            if (!MeasurementUnits.TryToCanonical(
                    Quantity,
                    _resolvedFood!.ServingUnit,
                    out var canonicalQuantity,
                    out _,
                    out _))
            {
                ModelState.AddModelError(
                    nameof(Quantity),
                    "The quantity could not be converted.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var diaryEntry = new DiaryEntry
            {
                UserId = userId,
                Date = Date.Date,
                MealType = MealType,
                Food = _resolvedFood,
                Quantity = canonicalQuantity
            };

            diaryEntry.CaptureSnapshot(_resolvedFood, null);

            _context.DiaryEntries.Add(diaryEntry);
            await _context.SaveChangesAsync();
            TempData["UiStatusMessage"] = "Diary entry added.";

            return RedirectToPage(
                "/Diary/Index",
                new
                {
                    date = Date.ToString("yyyy-MM-dd")
                });
        }

        public async Task<IActionResult> OnPostFavouriteAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (!HasValidDiaryContext())
            {
                return BadRequest();
            }

            var failure = await LoadFoodAsync(userId);

            if (failure != null)
            {
                return failure;
            }

            _resolvedFood!.IsFavourite = true;
            await _context.SaveChangesAsync();

            return RedirectToPage(new
            {
                id = _resolvedFood.ExternalId,
                date = Date.ToString("yyyy-MM-dd"),
                meal = MealType,
                searchTerm = SearchTerm
            });
        }

        public async Task<IActionResult> OnPostUnfavouriteAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (!HasValidDiaryContext())
            {
                return BadRequest();
            }

            if (!ExternalFoodIds.TryNormalizeUsdaId(
                    ExternalId,
                    out var normalizedId))
            {
                return BadRequest();
            }

            var existingFood = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == FoodSources.Usda &&
                    food.ExternalId == normalizedId &&
                    food.IsFavourite &&
                    !food.IsDeleted);

            if (existingFood == null)
            {
                return NotFound();
            }

            existingFood.IsFavourite = false;
            await _context.SaveChangesAsync();

            return RedirectToPage(new
            {
                id = normalizedId,
                date = Date.ToString("yyyy-MM-dd"),
                meal = MealType,
                searchTerm = SearchTerm
            });
        }

        private async Task<IActionResult?> LoadFoodAsync(string userId)
        {
            var resolution = await _externalFoodResolver
                .ResolveAsync(userId, ExternalId);

            if (resolution.Failure == ExternalFoodFailure.InvalidId)
            {
                return BadRequest();
            }

            if (resolution.Failure == ExternalFoodFailure.Missing)
            {
                return NotFound();
            }

            if (resolution.Failure == ExternalFoodFailure.Unavailable)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            _resolvedFood = resolution.Food!;
            Food = resolution.Result;
            IsFavourite = _resolvedFood.IsFavourite;

            return null;
        }

        private bool HasValidDiaryContext()
        {
            return ModelState.IsValid &&
                Date.Date >= ValidationRules.MinimumDiaryDate &&
                Date.Date <= ValidationRules.MaximumDiaryDate &&
                ValidationRules.MealTypes.Contains(MealType);
        }
    }
}
