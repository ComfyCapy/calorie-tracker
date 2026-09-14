using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Foods;

[Authorize]
public sealed class SubmitModel(ApplicationDbContext db, CommunityFoodService community) : PageModel
{
    public Food Food { get; private set; } = null!;
    public CommunityFood? Submission { get; private set; }
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var userId = CommunityFoodService.UserId(User);
        if (!ModelState.IsValid || userId == null) return BadRequest();
        var food = await db.Foods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.Source == null && !x.IsDeleted, ct);
        if (food == null) return NotFound();
        Food = food;
        Submission = await db.CommunityFoods.AsNoTracking().SingleOrDefaultAsync(x => x.SourceFoodId == id && x.SubmitterId == userId, ct);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            var submission = await community.SubmitAsync(User, id, ct);
            if (submission == null) return NotFound();
            TempData["UiStatusMessage"] = "Your Community Food submission is " + submission.Status.ToString().ToLowerInvariant() + ".";
            return RedirectToPage(new { id });
        }
        catch (ArgumentException ex)
        {
            var page = await OnGetAsync(id, ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            return page;
        }
    }
}
