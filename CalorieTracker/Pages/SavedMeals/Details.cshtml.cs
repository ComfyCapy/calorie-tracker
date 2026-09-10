using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.SavedMeals;

[Authorize]
public sealed class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public SavedMeal SavedMeal { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        var meal = await _context.SavedMeals
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (meal == null)
        {
            return NotFound();
        }

        SavedMeal = meal;
        return Page();
    }
}
