using CalorieTracker.Models;
using CalorieTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalorieTracker.Controllers
{
    [ApiController]
    [Route("api/foods")]
    [Authorize]
    public class FoodsApiController : ControllerBase
    {
        private readonly IFoodSearchService _foodSearchService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FoodsApiController(
            IFoodSearchService foodSearchService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _foodSearchService = foodSearchService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("search")]
        public async Task<ActionResult<FoodSearchPage>> Search(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            page = Math.Max(page, 1);

            if (pageSize != 20 &&
                pageSize != 50 &&
                pageSize != 100)
            {
                pageSize = 20;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new FoodSearchPage
                {
                    PageNumber = page,
                    PageSize = pageSize
                });
            }

            var results =
                await _foodSearchService.SearchFoodsPageAsync(
                    query,
                    page,
                    pageSize);

            var externalIds = results.Foods
                .Select(food => food.ExternalId)
                .ToList();

            var favouriteExternalIds = (await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == "USDA" &&
                    food.ExternalId != null &&
                    externalIds.Contains(food.ExternalId) &&
                    food.IsFavourite &&
                    !food.IsDeleted)
                .Select(food => food.ExternalId!)
                .ToListAsync())
                .ToHashSet();

            foreach (var food in results.Foods)
            {
                food.IsFavourite =
                    favouriteExternalIds.Contains(food.ExternalId);
            }

            return Ok(results);
        }

        [HttpPost("select/{externalId}")]
        public async Task<IActionResult> Select(
            string externalId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                return BadRequest();
            }

            var result =
                await _foodSearchService.GetFoodAsync(
                    externalId);

            if (result == null)
            {
                return NotFound();
            }

            var food = await GetOrCreateFoodAsync(
                userId,
                result);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                foodId = food.Id
            });
        }

        [HttpPost("favourites/{externalId}")]
        public async Task<IActionResult> Favourite(
            string externalId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                return BadRequest();
            }

            var result = await _foodSearchService
                .GetFoodAsync(externalId);

            if (result == null)
            {
                return NotFound();
            }

            var food = await GetOrCreateFoodAsync(
                userId,
                result);

            food.IsFavourite = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                isFavourite = true
            });
        }

        [HttpDelete("favourites/{externalId}")]
        public async Task<IActionResult> Unfavourite(
            string externalId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == "USDA" &&
                    food.ExternalId == externalId &&
                    food.IsFavourite &&
                    !food.IsDeleted);

            if (food == null)
            {
                return NotFound();
            }

            food.IsFavourite = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                isFavourite = false
            });
        }

        private async Task<Food> GetOrCreateFoodAsync(
            string userId,
            FoodSearchResult result)
        {
            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == result.Source &&
                    food.ExternalId == result.ExternalId);

            if (food != null)
            {
                food.IsDeleted = false;

                return food;
            }

            food = new Food
            {
                UserId = userId,
                Source = result.Source,
                ExternalId = result.ExternalId,
                Name = result.Name,

                Calories =
                    (int)Math.Round(result.Calories),

                Protein = result.Protein,
                Carbohydrates = result.Carbohydrates,
                Fat = result.Fat,

                ServingSize = result.ServingSize,
                ServingUnit = result.ServingUnit,

                IsFavourite = false,
                IsDeleted = false
            };

            _context.Foods.Add(food);

            return food;
        }
    }
}
