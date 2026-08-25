using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        [Range(0.01, 100000, ErrorMessage = "Portion quantity must be greater than 0.")]
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

            FoodOptions = await _context.Foods
                .Include(food => food.Portions)
                .OrderBy(food => food.Name)
                .ToListAsync();

            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            // If we're editing using a portion, validate the selected portion
            // and convert it back into the food's base unit.
            if (MeasurementMode == "Portion")
            {
                if (SelectedPortionId == null)
                {
                    ModelState.AddModelError(
                        nameof(SelectedPortionId),
                        "Please select a portion.");
                }

                if (PortionQuantity == null || PortionQuantity <= 0)
                {
                    ModelState.AddModelError(
                        nameof(PortionQuantity),
                        "Portion quantity must be greater than 0.");
                }

                if (SelectedPortionId != null && PortionQuantity > 0)
                {
                    var portion = await _context.FoodPortions
                        .FirstOrDefaultAsync(portion =>
                            portion.Id == SelectedPortionId &&
                            portion.FoodId == DiaryEntry.FoodId);

                    if (portion == null)
                    {
                        ModelState.AddModelError(
                            nameof(SelectedPortionId),
                            "The selected portion is not valid for this food.");
                    }
                    else
                    {
                        // Convert e.g. 2 × 118 g portions into 236 g.
                        DiaryEntry.Quantity =
                            portion.Amount * PortionQuantity.Value;

                        // The exact quantity field is hidden in Portion mode,
                        // so ignore its original validation result.
                        ModelState.Remove("DiaryEntry.Quantity");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                FoodOptions = await _context.Foods
                    .Include(food => food.Portions)
                    .OrderBy(food => food.Name)
                    .ToListAsync();

                return Page();
            }

            var existingEntry = await _context.DiaryEntries
                .FirstOrDefaultAsync(entry =>
                    entry.Id == DiaryEntry.Id &&
                    entry.UserId == userId);

            if (existingEntry == null)
            {
                return NotFound();
            }

            existingEntry.Date = DiaryEntry.Date;
            existingEntry.MealType = DiaryEntry.MealType;
            existingEntry.FoodId = DiaryEntry.FoodId;
            existingEntry.Quantity = DiaryEntry.Quantity;

            if (MeasurementMode == "Portion")
            {
                existingEntry.FoodPortionId = SelectedPortionId;
                existingEntry.PortionQuantity = PortionQuantity;
            }
            else
            {
                existingEntry.FoodPortionId = null;
                existingEntry.PortionQuantity = null;
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = existingEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}