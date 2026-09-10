using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Pages.SavedMeals;

[Authorize]
public sealed class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReusableMealService _reusableMealService;
    private readonly IUserLocalTimeProvider _userLocalTimeProvider;

    public EditModel(
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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        var meal = await _context.SavedMeals
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (meal == null)
        {
            return NotFound();
        }

        Input.Id = meal.Id;
        Input.Name = meal.Name;
        Input.SourceDate = _userLocalTimeProvider.Today.ToDateTime(TimeOnly.MinValue);
        Input.SourceMeal = "Breakfast";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id != Input.Id)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(
                "Input.Name",
                "Please enter a name for the saved meal.");
        }

        if (Input.ReplaceItems)
        {
            ValidationRules.ValidateDiaryDate(
                Input.SourceDate,
                ModelState,
                "Input.SourceDate");
            if (!ValidationRules.MealTypes.Contains(Input.SourceMeal))
            {
                ModelState.AddModelError("Input.SourceMeal", "Select a valid meal.");
            }
        }

        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }

        var meal = await _context.SavedMeals
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (meal == null)
        {
            return NotFound();
        }

        var entries = ModelState.IsValid && Input.ReplaceItems
            ? await _reusableMealService.LoadSourceMealAsync(
                userId,
                Input.SourceDate,
                Input.SourceMeal)
            : [];
        if (ModelState.IsValid && Input.ReplaceItems && entries.Count == 0)
        {
            ModelState.AddModelError(
                "Input.SourceDate",
                "That Diary meal has no entries to use.");
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        meal.Name = Input.Name.Trim();
        if (Input.ReplaceItems)
        {
            _context.SavedMealItems.RemoveRange(meal.Items);
            meal.Items = entries.Select(DiarySnapshotFactory.ToSavedMealItem).ToList();
        }
        await _context.SaveChangesAsync();
        TempData["UiStatusMessage"] = $"Updated {meal.Name}.";
        return RedirectToPage("./Details", new { id });
    }

    public sealed class InputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(SavedMeal.MaxNameLength)]
        public string Name { get; set; } = string.Empty;

        public bool ReplaceItems { get; set; }

        [DataType(DataType.Date)]
        public DateTime SourceDate { get; set; }

        public string SourceMeal { get; set; } = "Breakfast";
    }
}
