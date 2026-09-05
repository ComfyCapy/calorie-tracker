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

        public EditModel(
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

        public int? OriginalPortionId { get; set; }

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
            OriginalPortionId = diaryEntry.FoodPortionId;

            if (diaryEntry.Food != null &&
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

            if (diaryEntry.FoodPortionId.HasValue &&
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

            OriginalPortionId = existingEntry.FoodPortionId;

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
                    (!food.IsDeleted ||
                     food.Id == existingEntry.FoodId));

            if (selectedFood == null)
            {
                ModelState.AddModelError(
                    "DiaryEntry.FoodId",
                    "Please select a valid food.");
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
                            (!portion.IsDeleted ||
                             (selectedFood.Id == existingEntry.FoodId &&
                              portion.Id == existingEntry.FoodPortionId)));

                    if (portion == null)
                    {
                        ModelState.AddModelError(
                            nameof(SelectedPortionId),
                            "The selected portion is not valid for this food.");
                    }
                    else
                    {
                        selectedPortion = portion;

                        var canonicalAmount =
                            selectedFood.Id == existingEntry.FoodId &&
                            portion.Id == existingEntry.FoodPortionId &&
                            existingEntry.PortionQuantity > 0
                                ? existingEntry.Quantity /
                                  existingEntry.PortionQuantity.Value
                                : portion.Amount;

                        try
                        {
                            DiaryEntry.Quantity = checked(
                                canonicalAmount *
                                PortionQuantity.Value);
                        }
                        catch (OverflowException)
                        {
                            ModelState.AddModelError(
                                nameof(PortionQuantity),
                                "The resulting quantity is too large.");
                        }

                        ModelState.Remove(
                            "DiaryEntry.Quantity");
                    }
                }
            }
            else
            {
                decimal canonicalQuantity = 0;

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
                DiaryEntry.Id = existingEntry.Id;
                await LoadFoodOptionsAsync(
                    userId,
                    DiaryEntry.FoodId);

                return Page();
            }

            var foodChanged =
                existingEntry.FoodId != DiaryEntry.FoodId;

            var portionChanged =
                existingEntry.FoodPortionId != SelectedPortionId;

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
                ValidationRules.CaptureSnapshot(
                    existingEntry,
                    selectedFood!,
                    selectedPortion);
            }

            if (MeasurementMode == "Portion")
            {
                existingEntry.FoodPortionId =
                    SelectedPortionId;

                existingEntry.PortionQuantity =
                    PortionQuantity;

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
            }

            await _context.SaveChangesAsync();
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
    }
}
