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
        [Range(0.01, 100000, ErrorMessage = "Portion quantity must be greater than 0.")]
        public decimal? PortionQuantity { get; set; }

        public List<Food> FoodOptions { get; set; } = [];

        public async Task OnGetAsync(DateTime? date, string? meal)
        {
            if (date.HasValue)
            {
                DiaryEntry.Date = date.Value;
            }

            if (!string.IsNullOrWhiteSpace(meal))
            {
                DiaryEntry.MealType = meal;
            }

            FoodOptions = await _context.Foods
                .Include(f => f.Portions)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            // Portion mode needs to be converted into the food's base unit
            // before the diary entry is saved.
            if (MeasurementMode == "Portion")
            {
                if (SelectedPortionId == null)
                {
                    ModelState.AddModelError(
                        nameof(SelectedPortionId),
                        "Please select a portion.");
                }
                else
                {
                    DiaryEntry.FoodPortionId = null;
                    DiaryEntry.PortionQuantity = null;
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
                        DiaryEntry.FoodPortionId = portion.Id;
                        DiaryEntry.PortionQuantity = PortionQuantity;

                        DiaryEntry.Quantity =
                            portion.Amount * PortionQuantity.Value;

                        // Quantity was initially bound as 0 because the exact
                        // quantity input is hidden in Portion mode.
                        ModelState.Remove("DiaryEntry.Quantity");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                FoodOptions = await _context.Foods
                    .Include(f => f.Portions)
                    .OrderBy(f => f.Name)
                    .ToListAsync();

                return Page();
            }

            DiaryEntry.UserId = userId;

            _context.DiaryEntries.Add(DiaryEntry);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index", new
            {
                date = DiaryEntry.Date.ToString("yyyy-MM-dd")
            });
        }
    }
}
