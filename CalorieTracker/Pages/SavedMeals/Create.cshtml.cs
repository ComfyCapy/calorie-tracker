using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Pages.SavedMeals;

[Authorize]
public sealed class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReusableMealService _reusableMealService;
    private readonly IUserLocalTimeProvider _userLocalTimeProvider;

    public CreateModel(
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

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet(DateTime? sourceDate, string? sourceMeal)
    {
        Input.SourceDate = sourceDate?.Date ??
            _userLocalTimeProvider.Today.ToDateTime(TimeOnly.MinValue);
        Input.SourceMeal = sourceMeal ?? "Breakfast";

        if (Input.SourceDate < ValidationRules.MinimumDiaryDate ||
            Input.SourceDate > ValidationRules.MaximumDiaryDate ||
            !ValidationRules.MealTypes.Contains(Input.SourceMeal))
        {
            return BadRequest();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateInput();
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }

        var entries = ModelState.IsValid
            ? await _reusableMealService.LoadSourceMealAsync(
                userId,
                Input.SourceDate,
                Input.SourceMeal)
            : [];

        if (ModelState.IsValid && entries.Count == 0)
        {
            ModelState.AddModelError(
                nameof(Input.SourceDate),
                "That Diary meal has no entries to save.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var meal = new SavedMeal
        {
            UserId = userId,
            Name = Input.Name.Trim(),
            Items = entries.Select(DiarySnapshotFactory.ToSavedMealItem).ToList()
        };
        _context.SavedMeals.Add(meal);
        await _context.SaveChangesAsync();
        TempData["UiStatusMessage"] = $"Saved {meal.Name}.";

        return RedirectToPage("./Details", new { id = meal.Id });
    }

    private void ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(
                "Input.Name",
                "Please enter a name for the saved meal.");
        }

        ValidationRules.ValidateDiaryDate(
            Input.SourceDate,
            ModelState,
            "Input.SourceDate");

        if (!ValidationRules.MealTypes.Contains(Input.SourceMeal))
        {
            ModelState.AddModelError("Input.SourceMeal", "Select a valid meal.");
        }
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(SavedMeal.MaxNameLength)]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime SourceDate { get; set; }

        public string SourceMeal { get; set; } = "Breakfast";
    }
}
