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
        private readonly FoodCatalogue _foodCatalogue;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ExternalFoodResolver _externalFoodResolver;

        public FoodsApiController(
            FoodCatalogue foodCatalogue,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ExternalFoodResolver externalFoodResolver)
        {
            _foodCatalogue = foodCatalogue;
            _context = context;
            _userManager = userManager;
            _externalFoodResolver = externalFoodResolver;
        }

        [HttpGet("search")]
        [EnableRateLimiting(RateLimitPolicies.FoodSearch)]
        public async Task<ActionResult<FoodSearchPage>> Search(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery(Name = "provider")] string? providerId = null)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!_foodCatalogue.TryGetProvider(
                    providerId,
                    out var catalogueProvider))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The food database selection is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
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
                results = await catalogueProvider.SearchAsync(
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
                    title: catalogueProvider.UnavailableTitle);
            }

            var externalIds = results.Foods
                .Select(food => food.ExternalId)
                .ToList();

            var favouriteExternalIds = (await _context.Foods
                .Where(food =>
                    food.UserId == userId &&
                    food.Source == catalogueProvider.Source &&
                    food.ExternalId != null &&
                    externalIds.Contains(food.ExternalId) &&
                    food.IsFavourite &&
                    !food.IsDeleted)
                .Select(food => food.ExternalId!)
                .ToListAsync())
                .ToHashSet();

            foreach (var food in results.Foods)
            {
                food.Provider = catalogueProvider.Id;
                food.Source = catalogueProvider.Source;
                food.IsFavourite =
                    favouriteExternalIds.Contains(food.ExternalId);
            }

            return Ok(results);
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> Suggestions([FromQuery] string? query)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            var term = query?.Trim() ?? string.Empty;

            if (term.Length < 2)
            {
                return Ok(Array.Empty<object>());
            }

            if (term.Length > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Search terms must be 100 characters or fewer.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var escapedTerm = term
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var matchPattern = $"%{escapedTerm}%";

            var frequentIds = (await FrequentlyLoggedFoodQuery.LoadAsync(
                _context,
                userId,
                limit: 10))
                .Select(food => food.Id)
                .ToHashSet();
            var recentIds = (await RecentFoodQuery.LoadAsync(
                _context,
                userId))
                .Take(20)
                .Select(food => food.Id)
                .ToHashSet();
            var reusableIds = frequentIds
                .Concat(recentIds)
                .ToHashSet();

            var candidates = await _context.Foods
                .AsNoTracking()
                .Where(food =>
                    food.UserId == userId &&
                    !food.IsDeleted &&
                    EF.Functions.Like(food.Name, matchPattern, "\\") &&
                    (food.IsFavourite ||
                     food.Source == null ||
                     reusableIds.Contains(food.Id)))
                .OrderByDescending(food => food.IsFavourite)
                .ThenBy(food => food.Name)
                .ThenBy(food => food.Id)
                .Take(50)
                .ToListAsync();

            var suggestions = candidates
                .Select(food => new
                {
                    Food = food,
                    Rank = food.IsFavourite
                        ? 0
                        : frequentIds.Contains(food.Id)
                            ? 1
                            : food.Source == null
                                ? 2
                                : 3,
                    Category = food.IsFavourite
                        ? "Favourite"
                        : food.Source == null
                            ? "Custom"
                            : null
                })
                .OrderBy(item => item.Rank)
                .ThenBy(item => item.Food.Name)
                .Take(8)
                .Select(item => new
                {
                    foodId = item.Food.Id,
                    name = item.Food.Name,
                    category = item.Category,
                    serving = $"{item.Food.ServingSize:0.##} {item.Food.DisplayServingUnit}"
                });

            return Ok(suggestions);
        }

        [HttpPost("select/{externalId}")]
        [EnableRateLimiting(RateLimitPolicies.FoodSearch)]
        public async Task<IActionResult> Select(
            string externalId,
            [FromQuery(Name = "provider")] string? providerId = null)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!_foodCatalogue.TryGetProvider(
                    providerId,
                    out var catalogueProvider))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The food database selection is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var resolution = await _externalFoodResolver
                .ResolveAsync(userId, catalogueProvider.Id, externalId);

            if (resolution.Failure == ExternalFoodFailure.InvalidId)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = $"The {catalogueProvider.Source} food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Missing)
            {
                return NotFound(new ProblemDetails
                {
                    Title = $"The {catalogueProvider.Source} food could not be found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: catalogueProvider.UnavailableTitle);
            }

            var food = resolution.Food!;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                foodId = food.Id
            });
        }

        [HttpPost("favourites/{externalId}")]
        [EnableRateLimiting(RateLimitPolicies.FoodSearch)]
        public async Task<IActionResult> Favourite(
            string externalId,
            [FromQuery(Name = "provider")] string? providerId = null)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!_foodCatalogue.TryGetProvider(
                    providerId,
                    out var catalogueProvider))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The food database selection is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var resolution = await _externalFoodResolver
                .ResolveAsync(userId, catalogueProvider.Id, externalId);

            if (resolution.Failure == ExternalFoodFailure.InvalidId)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = $"The {catalogueProvider.Source} food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Missing)
            {
                return NotFound(new ProblemDetails
                {
                    Title = $"The {catalogueProvider.Source} food could not be found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (resolution.Failure == ExternalFoodFailure.Unavailable)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: catalogueProvider.UnavailableTitle);
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
            string externalId,
            [FromQuery(Name = "provider")] string? providerId = null)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Unauthorized();
            }

            if (!_foodCatalogue.TryGetProvider(
                    providerId,
                    out var catalogueProvider))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "The food database selection is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (!catalogueProvider.TryNormalizeExternalId(
                    externalId,
                    out var normalizedId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = $"The {catalogueProvider.Source} food ID is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var food = await _context.Foods
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == catalogueProvider.Source &&
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
