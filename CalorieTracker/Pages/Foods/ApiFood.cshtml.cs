using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class ApiFoodModel : PageModel
    {
        private readonly IFoodSearchService _foodSearchService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApiFoodModel(
            IFoodSearchService foodSearchService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _foodSearchService = foodSearchService;
            _context = context;
            _userManager = userManager;
        }

        public FoodSearchResult? Food { get; set; }

        [BindProperty]
        public bool AddToFavourites { get; set; }

        [BindProperty]
        public string ExternalId { get; set; } = string.Empty;

        [BindProperty]
        [Range(
            0.01,
            100000,
            ErrorMessage = "Quantity must be greater than 0.")]
        public decimal Quantity { get; set; } = 100;

        [BindProperty]
        public string MealType { get; set; } = "Dinner";

        [BindProperty]
        public DateTime Date { get; set; } = DateTime.Today;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            Food = await _foodSearchService.GetFoodAsync(id);

            if (Food == null)
            {
                return NotFound();
            }

            ExternalId = id;

            var existingFood = await FindExistingFoodAsync(
                userId,
                Food);

            AddToFavourites =
                existingFood?.IsFavourite ?? false;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            if (!await LoadFoodAsync())
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var databaseFood =
                await GetOrCreateFoodAsync(
                    userId,
                    Food!);

            databaseFood.IsFavourite =
                AddToFavourites;

            var diaryEntry = new DiaryEntry
            {
                UserId = userId,
                Date = Date,
                MealType = MealType,
                FoodId = databaseFood.Id,
                Quantity = Quantity
            };

            _context.DiaryEntries.Add(diaryEntry);

            await _context.SaveChangesAsync();

            return RedirectToPage(
                "/Diary/Index",
                new
                {
                    date = Date.ToString("yyyy-MM-dd")
                });
        }

        private async Task<bool> LoadFoodAsync()
        {
            if (string.IsNullOrWhiteSpace(ExternalId))
            {
                return false;
            }

            Food = await _foodSearchService
                .GetFoodAsync(ExternalId);

            return Food != null;
        }

        private async Task<Food?> FindExistingFoodAsync(
            string userId,
            FoodSearchResult food)
        {
            return await _context.Foods
                .FirstOrDefaultAsync(existingFood =>
                    existingFood.UserId == userId &&
                    existingFood.Source == food.Source &&
                    existingFood.ExternalId == food.ExternalId);
        }

        private async Task<Food> GetOrCreateFoodAsync(
            string userId,
            FoodSearchResult food)
        {
            var existingFood =
                await FindExistingFoodAsync(
                    userId,
                    food);

            if (existingFood != null)
            {
                // If this database food was previously hidden,
                // using it again restores it.
                existingFood.IsDeleted = false;

                return existingFood;
            }

            var databaseFood = new Food
            {
                UserId = userId,
                Source = food.Source,
                ExternalId = food.ExternalId,

                Name = food.Name,

                Calories =
                    (int)Math.Round(food.Calories),

                Protein = food.Protein,
                Carbohydrates = food.Carbohydrates,
                Fat = food.Fat,

                ServingSize = food.ServingSize,
                ServingUnit = food.ServingUnit,

                IsFavourite = false,
                IsDeleted = false
            };

            _context.Foods.Add(databaseFood);

            await _context.SaveChangesAsync();

            return databaseFood;
        }
    }
}