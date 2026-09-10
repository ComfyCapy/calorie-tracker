using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.SavedMeals;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReusableMealService _reusableMealService;
    private readonly IUserLocalTimeProvider _userLocalTimeProvider;

    public IndexModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ReusableMealService reusableMealService,
        IUserLocalTimeProvider userLocalTimeProvider)
    {
        _context = context;
        _userManager = userManager;
        _reusableMealService = reusableMealService;
        _userLocalTimeProvider = userLocalTimeProvider;
    }

    public List<SavedMeal> SavedMeals { get; private set; } = [];
    public DateTime DefaultDate { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }

        DefaultDate = _userLocalTimeProvider.Today.ToDateTime(TimeOnly.MinValue);
        SavedMeals = await _context.SavedMeals
            .AsNoTracking()
            .Include(meal => meal.Items)
            .Where(meal => meal.UserId == userId)
            .OrderBy(meal => meal.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(
        int id,
        DateTime date,
        string mealType)
    {
        if (!ModelState.IsValid ||
            date.Date < ValidationRules.MinimumDiaryDate ||
            date.Date > ValidationRules.MaximumDiaryDate ||
            !ValidationRules.MealTypes.Contains(mealType))
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }

        var count = await _reusableMealService.AddSavedMealToDiaryAsync(
            userId,
            id,
            date.Date,
            mealType);

        if (count == null)
        {
            return NotFound();
        }

        TempData["UiStatusMessage"] =
            $"Added {count} saved-meal {(count == 1 ? "item" : "items")} to {mealType}.";

        return RedirectToPage("/Diary/Index", new
        {
            date = date.ToString("yyyy-MM-dd")
        });
    }
}
