using CalorieTracker.Models;
using CalorieTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using CalorieTracker.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace CalorieTracker.Controllers
{
    [ApiController]
    [Route("api/foods")]
    [Authorize]
    // These mutations use the Identity cookie, so browser requests also require an antiforgery token.
    [AutoValidateAntiforgeryToken]
    public class FoodsApiController : ControllerBase
    {
        private readonly IFoodSearchService _foodSearchService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ExternalFoodResolver _externalFoodResolver;

        public FoodsApiController(
            IFoodSearchService foodSearchService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ExternalFoodResolver externalFoodResolver)
        {
            _foodSearchService = foodSearchService;
            _context = context;
            _userManager = userManager;
            _externalFoodResolver = externalFoodResolver;
        }

        [HttpGet("search")]
        [EnableRateLimiting(RateLimitPolicies.FoodSearch)]
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

            FoodSearchPage results;

            try
            {
                results = await _foodSearchService.SearchFoodsPageAsync(
                    query,
                    page,
                    pageSize);
            }
            catch (Exception exception)
                when (exception is HttpRequestException or
                    TaskCanceledException or
                    JsonException or
                    NotSupportedException or
                    InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The USDA food database is temporarily unavailable.");
            }

            var externalIds = results.Foods
                .Select(food => food.ExternalId)
                .ToList();

            var favouriteExternalIds = (await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == FoodSources.Usda &&
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

            var resolution = await _externalFoodResolver
                .ResolveAsync(userId, externalId);

            if (resolution.Failure == ExternalFoodFailure.InvalidId)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The USDA food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Missing)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "The USDA food could not be found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The USDA food database is temporarily unavailable.");
            }

            var food = resolution.Food!;

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

            var resolution = await _externalFoodResolver
                .ResolveAsync(userId, externalId);

            if (resolution.Failure == ExternalFoodFailure.InvalidId)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The USDA food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Missing)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "The USDA food could not be found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The USDA food database is temporarily unavailable.");
            }

            var food = resolution.Food!;

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

            if (!ExternalFoodIds.TryNormalizeUsdaId(
                    externalId,
                    out var normalizedId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The USDA food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == FoodSources.Usda &&
                    food.ExternalId == normalizedId &&
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

    }
}
