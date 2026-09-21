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
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DailyMaintenanceSnapshotService _snapshotService;

        public EditModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            DailyMaintenanceSnapshotService snapshotService)
        {
            _context = context;
            _userManager = userManager;
            _snapshotService = snapshotService;
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

        public DiaryEntry OriginalEntry { get; private set; } = null!;
        public int? OriginalPortionId { get; set; }
        public decimal? OriginalPortionAmount { get; private set; }
        public string SelectedFoodName { get; set; } = string.Empty;
        public string SelectedServingUnit { get; set; } = string.Empty;
        public string? SelectedPortionName { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
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

            var diaryEntry = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .Include(entry => entry.FoodPortion)
                .FirstOrDefaultAsync(entry =>
                    entry.Id == id &&
                    entry.UserId == userId);

            if (diaryEntry == null)
            {
                return NotFound();
            }

            DiaryEntry = diaryEntry;
            OriginalEntry = diaryEntry;
            OriginalPortionId = diaryEntry.FoodPortionId;
            OriginalPortionAmount = diaryEntry.PortionQuantity > 0
                ? diaryEntry.Quantity / diaryEntry.PortionQuantity.Value
                : null;
            SetSelectedFoodDisplay(
                diaryEntry,
                diaryEntry.Food,
                diaryEntry.FoodPortion);

            if (diaryEntry.Food?.ServingBasis == FoodServingBasis.Measured &&
                MeasurementUnits.TryToCanonical(
                    1,
                    diaryEntry.Food.ServingUnit,
                    out _,
                    out _,
                    out _))
            {
                DiaryEntry.Quantity =
                    MeasurementUnits.FromCanonical(
                        diaryEntry.Quantity,
                        diaryEntry.Food.ServingUnit);
            }

            if (diaryEntry.IsApproximate)
            {
                MeasurementMode = "Approximate";
                ApproximationSize = ApproximatePortions.TryGetMultiplier(
                    diaryEntry.ApproximationLabel,
                    out _)
                    ? diaryEntry.ApproximationLabel!
                    : "Medium";
                ApproximationPortionId = diaryEntry.FoodPortionId;
            }
            else if (diaryEntry.FoodPortionId.HasValue &&
                diaryEntry.PortionQuantity.HasValue)
            {
                MeasurementMode = "Portion";
                SelectedPortionId = diaryEntry.FoodPortionId;
                PortionQuantity = diaryEntry.PortionQuantity;
            }
            else
            {
                MeasurementMode = "Exact";
            }

            await LoadFoodOptionsAsync(
                userId,
                diaryEntry.FoodId);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (ValidationRules.HasBindingError(ModelState, nameof(id)))
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == id &&
                    entry.UserId == userId);

            if (existingEntry == null)
            {
                return NotFound();
            }

            OriginalEntry = existingEntry;
            OriginalPortionId = existingEntry.FoodPortionId;
            OriginalPortionAmount = existingEntry.PortionQuantity > 0
                ? existingEntry.Quantity / existingEntry.PortionQuantity.Value
                : null;

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

            // Keep the original soft-deleted food available so its historical entry can still be edited.
            var selectedFood = await _context.Foods
                .Include(food => food.Portions)
                .FirstOrDefaultAsync(food =>
                    food.Id == DiaryEntry.FoodId &&
                    food.UserId == userId &&
                    (!food.IsDeleted ||
                     food.Id == existingEntry.FoodId));

            if (selectedFood == null)
            {
                ModelState.AddModelError(
                    "DiaryEntry.FoodId",
                    "Please select a valid food.");
            }

            // Only the original deleted portion is eligible, and only on its original food.
            var availablePortions = selectedFood?.Portions
                .Where(portion => !portion.IsDeleted ||
                    (selectedFood.Id == existingEntry.FoodId && portion.Id == existingEntry.FoodPortionId))
                .ToList() ?? [];

            decimal PortionAmount(FoodPortion portion) =>
                selectedFood!.Id == existingEntry.FoodId &&
                portion.Id == existingEntry.FoodPortionId &&
                existingEntry.PortionQuantity > 0
                    ? existingEntry.Quantity / existingEntry.PortionQuantity.Value
                    : portion.Amount;

            decimal EstimateAmount(FoodPortion? portion) =>
                selectedFood!.Id == existingEntry.FoodId &&
                (portion == null || portion.Id == existingEntry.FoodPortionId) &&
                existingEntry.IsApproximate &&
                ApproximatePortions.TryGetMultiplier(existingEntry.ApproximationLabel, out var originalMultiplier)
                    ? existingEntry.Quantity / originalMultiplier
                    : portion?.Amount ?? selectedFood.CanonicalServingSize;

            var measurement = DiaryMeasurementResolver.Resolve(
                new(MeasurementMode, DiaryEntry.Quantity, SelectedPortionId,
                    PortionQuantity, ApproximationPortionId, ApproximationSize),
                selectedFood, availablePortions, PortionAmount, EstimateAmount);
            DiaryEntry.Quantity = measurement.Quantity;
            foreach (var field in measurement.DerivedFields)
                ModelState.Remove(field);
            foreach (var error in measurement.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            var selectedPortion = measurement.Portion;

            if (!ModelState.IsValid)
            {
                DiaryEntry.Id = existingEntry.Id;
                SetSelectedFoodDisplay(
                    existingEntry,
                    selectedFood,
                    selectedPortion);
                await LoadFoodOptionsAsync(
                    userId,
                    DiaryEntry.FoodId);

                return Page();
            }

            // Only a new food gets a new snapshot; changing date/meal/quantity must preserve history.
            var foodChanged =
                existingEntry.FoodId != DiaryEntry.FoodId;

            var selectedSnapshotPortionId = MeasurementMode == "Approximate"
                ? ApproximationPortionId
                : SelectedPortionId;

            var portionChanged =
                existingEntry.FoodPortionId != selectedSnapshotPortionId;

            existingEntry.Date =
                DiaryEntry.Date;

            existingEntry.MealType =
                DiaryEntry.MealType;

            existingEntry.FoodId =
                DiaryEntry.FoodId;

            existingEntry.Quantity =
                DiaryEntry.Quantity;

            if (foodChanged)
            {
                existingEntry.CaptureSnapshot(selectedFood!, selectedPortion);
            }

            if (MeasurementMode == "Approximate")
            {
                ApproximatePortions.TryGetMultiplier(
                    ApproximationSize,
                    out var multiplier);
                existingEntry.FoodPortionId = ApproximationPortionId;
                existingEntry.PortionQuantity = selectedPortion != null
                    ? multiplier
                    : null;
                existingEntry.IsApproximate = true;
                existingEntry.ApproximationLabel = ApproximationSize;

                if (foodChanged ||
                    portionChanged ||
                    string.IsNullOrWhiteSpace(existingEntry.PortionNameSnapshot))
                {
                    existingEntry.PortionNameSnapshot = selectedPortion?.Name;
                }
            }
            else if (MeasurementMode == "Portion")
            {
                // Retain the original portion label unless the selection changed or legacy data lacks it.
                existingEntry.FoodPortionId =
                    SelectedPortionId;

                existingEntry.PortionQuantity =
                    PortionQuantity;
                existingEntry.IsApproximate = false;
                existingEntry.ApproximationLabel = null;

                if (foodChanged ||
                    portionChanged ||
                    string.IsNullOrWhiteSpace(
                        existingEntry.PortionNameSnapshot))
                {
                    existingEntry.PortionNameSnapshot =
                        selectedPortion!.Name;
                }
            }
            else
            {
                existingEntry.FoodPortionId = null;
                existingEntry.PortionQuantity = null;
                existingEntry.PortionNameSnapshot = null;
                existingEntry.IsApproximate = false;
                existingEntry.ApproximationLabel = null;
            }

            await _snapshotService.EnsureSnapshotAsync(
                userId,
                DateOnly.FromDateTime(existingEntry.Date));
            await _snapshotService.SaveChangesAsync();
            TempData["UiStatusMessage"] = "Diary entry updated.";

            return RedirectToPage("./Index", new
            {
                date = existingEntry.Date
                    .ToString("yyyy-MM-dd")
            });
        }

        private async Task LoadFoodOptionsAsync(
            string userId,
            int currentFoodId)
        {
            FoodOptions = await _context.Foods
                .Include(food => food.Portions)
                .Where(food =>
                    food.UserId == userId &&
                    (!food.IsDeleted ||
                     food.Id == currentFoodId))
                .OrderBy(food => food.Name)
                .ToListAsync();
        }

        private void SetSelectedFoodDisplay(
            DiaryEntry existingEntry,
            Food? selectedFood,
            FoodPortion? selectedPortion)
        {
            var isOriginalFood =
                selectedFood?.Id == existingEntry.FoodId;

            SelectedFoodName = isOriginalFood &&
                !string.IsNullOrWhiteSpace(existingEntry.FoodNameSnapshot)
                    ? existingEntry.FoodNameSnapshot
                    : selectedFood?.Name ?? string.Empty;

            SelectedServingUnit = isOriginalFood
                ? existingEntry.ServingBasisSnapshot == FoodServingBasis.Portion
                    ? existingEntry.PortionLabelSnapshot ??
                      selectedFood?.DisplayServingUnit ??
                      "portion"
                    : existingEntry.ServingUnitSnapshot
                : selectedFood?.DisplayServingUnit ?? string.Empty;

            SelectedPortionName = isOriginalFood &&
                !string.IsNullOrWhiteSpace(existingEntry.PortionNameSnapshot)
                    ? existingEntry.PortionNameSnapshot
                    : selectedPortion?.Name;
        }
    }
}
