using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.SavedMeals;

[Authorize]
public sealed class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DeleteModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public SavedMeal SavedMeal { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var meal = await FindOwnedAsync(id);
        if (meal == null)
        {
            return NotFound();
        }
        SavedMeal = meal;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var meal = await FindOwnedAsync(id);
        if (meal == null)
        {
            return NotFound();
        }
        _context.SavedMeals.Remove(meal);
        await _context.SaveChangesAsync();
        TempData["UiStatusMessage"] = $"Deleted {meal.Name}. Diary history was not changed.";
        return RedirectToPage("./Index");
    }

    private Task<SavedMeal?> FindOwnedAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        return _context.SavedMeals.FirstOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId);
    }
}
