using CalorieTracker.Data;
using CalorieTracker.Models;
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
        [Range(
            0.01,
            100000,
            ErrorMessage = "Portion quantity must be greater than 0.")]
        public decimal? PortionQuantity { get; set; }

        public List<Food> FoodOptions { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(int id)
        {
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

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var existingEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == DiaryEntry.Id &&
                    entry.UserId == userId);

            if (existingEntry == null)
            {
                return NotFound();
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
                            portion.Id == SelectedPortionId);

                    if (portion == null)
                    {
                        ModelState.AddModelError(
                            nameof(SelectedPortionId),
                            "The selected portion is not valid for this food.");
                    }
                    else
                    {
                        DiaryEntry.Quantity =
                            portion.Amount *
                            PortionQuantity.Value;

                        ModelState.Remove(
                            "DiaryEntry.Quantity");
                    }
                }
            }
            else
            {
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
            }

            if (!ModelState.IsValid)
            {
                await LoadFoodOptionsAsync(
                    userId,
                    DiaryEntry.FoodId);

                return Page();
            }

            existingEntry.Date =
                DiaryEntry.Date;

            existingEntry.MealType =
                DiaryEntry.MealType;

            existingEntry.FoodId =
                DiaryEntry.FoodId;

            existingEntry.Quantity =
                DiaryEntry.Quantity;

            if (MeasurementMode == "Portion")
            {
                existingEntry.FoodPortionId =
                    SelectedPortionId;

                existingEntry.PortionQuantity =
                    PortionQuantity;
            }
            else
            {
                existingEntry.FoodPortionId = null;
                existingEntry.PortionQuantity = null;
            }

            await _context.SaveChangesAsync();

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