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

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public DiaryEntry DiaryEntry { get; set; } = new();

        [BindProperty]
        public string MeasurementMode { get; set; } = "Exact";

        [BindProperty]
        public int? SelectedPortionId { get; set; }

        [BindProperty]
        public decimal? PortionQuantity { get; set; }

        public List<Food> FoodOptions { get; set; } = [];
        [BindProperty(SupportsGet = true)]
        public bool ReturnToFoodSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FoodSearchTerm { get; set; }
        [BindProperty(SupportsGet = true)]
        public bool ReturnToFoodsIndex { get; set; }


        public async Task<IActionResult> OnGetAsync(
            DateTime? date,
            string? meal,
            int? foodId)
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

            DiaryEntry.Date = date ?? DateTime.Today;

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

            if (foodId.HasValue &&
                FoodOptions.All(food => food.Id != foodId.Value))
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
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
                selectedFood.Portions.All(portion => portion.IsDeleted))
            {
                MeasurementMode = "Exact";
            }

            FoodPortion? selectedPortion = null;

            if (MeasurementMode == "Portion")
            {
                if (SelectedPortionId == null)
                {
                    ModelState.AddModelError(
                        nameof(SelectedPortionId),
                        "Please select a portion.");
                }

                if (PortionQuantity == null ||
                    PortionQuantity <= 0)
                {
                    ModelState.AddModelError(
                        nameof(PortionQuantity),
                        "Portion quantity must be greater than 0.");
                }

                if (SelectedPortionId != null &&
                    PortionQuantity > 0 &&
                    selectedFood != null)
                {
                    var portion = selectedFood.Portions
                        .FirstOrDefault(portion =>
                            portion.Id == SelectedPortionId &&
                            !portion.IsDeleted);

                    if (portion == null)
                    {
                        ModelState.AddModelError(
                            nameof(SelectedPortionId),
                            "The selected portion is not valid for this food.");
                    }
                    else
                    {
                        selectedPortion = portion;
                        DiaryEntry.FoodPortionId =
                            portion.Id;

                        DiaryEntry.PortionQuantity =
                            PortionQuantity;

                        try
                        {
                            DiaryEntry.Quantity = checked(
                                portion.Amount *
                                PortionQuantity.Value);
                        }
                        catch (OverflowException)
                        {
                            ModelState.AddModelError(
                                nameof(PortionQuantity),
                                "The resulting quantity is too large.");
                        }

                        // Quantity is derived from the owned portion, not trusted from the posted field.
                        ModelState.Remove(
                            "DiaryEntry.Quantity");
                    }
                }
            }
            else
            {
                decimal canonicalQuantity = 0;

                DiaryEntry.FoodPortionId = null;
                DiaryEntry.PortionQuantity = null;

                // Exact mode owns quantity; discard stale portion fields from the same form post.
                ModelState.Remove(
                    nameof(SelectedPortionId));

                ModelState.Remove(
                    nameof(PortionQuantity));

                if (DiaryEntry.Quantity <= 0)
                {
                    ModelState.AddModelError(
                        "DiaryEntry.Quantity",
                        "Quantity must be greater than 0.");
                }

                else if (selectedFood != null &&
                    !MeasurementUnits.TryToCanonical(
                        DiaryEntry.Quantity,
                        selectedFood.ServingUnit,
                        out canonicalQuantity,
                        out _,
                        out _))
                {
                    ModelState.AddModelError(
                        "DiaryEntry.Quantity",
                        "The quantity could not be converted.");
                }
                else if (selectedFood != null)
                {
                    DiaryEntry.Quantity = canonicalQuantity;
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadFoodOptionsAsync(userId);

                return Page();
            }

            DiaryEntry.UserId = userId;

            DiaryEntry.CaptureSnapshot(selectedFood!, selectedPortion);

            _context.DiaryEntries.Add(DiaryEntry);

            await _context.SaveChangesAsync();
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
