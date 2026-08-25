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
        [Range(
            0.01,
            100000,
            ErrorMessage = "Portion quantity must be greater than 0.")]
        public decimal? PortionQuantity { get; set; }

        public List<Food> FoodOptions { get; set; } = [];

        public async Task OnGetAsync(
            DateTime? date,
            string? meal)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return;
            }

            DiaryEntry.Date = date ?? DateTime.Today;

            if (!string.IsNullOrWhiteSpace(meal))
            {
                DiaryEntry.MealType = meal;
            }

            await LoadFoodOptionsAsync(userId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
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
                selectedFood.Portions.Count == 0)
            {
                MeasurementMode = "Exact";
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
                        DiaryEntry.FoodPortionId =
                            portion.Id;

                        DiaryEntry.PortionQuantity =
                            PortionQuantity;

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
                DiaryEntry.FoodPortionId = null;
                DiaryEntry.PortionQuantity = null;

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
                await LoadFoodOptionsAsync(userId);

                return Page();
            }

            DiaryEntry.UserId = userId;

            _context.DiaryEntries.Add(DiaryEntry);

            await _context.SaveChangesAsync();

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
            var recentEntries = await _context.DiaryEntries
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Food != null &&
                    entry.Food.Source != null &&
                    !entry.Food.IsDeleted)
                .Include(entry => entry.Food!)
                    .ThenInclude(food => food.Portions)
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Id)
                .Take(100)
                .ToListAsync();

            var recentFoods = recentEntries
                .Where(entry => entry.Food != null)
                .GroupBy(entry => entry.FoodId)
                .Select(group => group.First().Food!)
                .Take(10)
                .ToList();

            // Build one searchable list:
            // favourites first, then custom foods, then recent foods.
            // GroupBy prevents the same food appearing twice.
            FoodOptions = favouriteFoods
                .Concat(customFoods)
                .Concat(recentFoods)
                .GroupBy(food => food.Id)
                .Select(group => group.First())
                .ToList();
        }
    }
}