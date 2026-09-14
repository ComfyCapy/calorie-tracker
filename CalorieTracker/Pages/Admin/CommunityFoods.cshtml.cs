using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Admin;

public sealed class CommunityFoodsModel(ApplicationDbContext db, CommunityFoodService community) : PageModel
{
    [BindProperty(SupportsGet = true)] public CommunityFoodStatus Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public List<CommunityFood> Submissions { get; private set; } = [];
    public bool HasNext { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(Status)) return BadRequest();
        PageNumber = Math.Clamp(PageNumber, 1, 100000);
        Submissions = await db.CommunityFoods.AsNoTracking().Where(x => x.Status == Status)
            .OrderBy(x => x.Id).Skip((PageNumber - 1) * 20).Take(21).ToListAsync(ct);
        HasNext = Submissions.Count > 20;
        Submissions = Submissions.Take(20).ToList();
        return Page();
    }
    public Task<IActionResult> OnPostApproveAsync(int id, string? note, CancellationToken ct) => Review(id, true, note, ct);
    public Task<IActionResult> OnPostRejectAsync(int id, string? note, CancellationToken ct) => Review(id, false, note, ct);
    public async Task<IActionResult> OnPostRenameAsync(int id, string? name, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            if (!await community.RenameAsync(User, id, name, ct)) return NotFound();
            TempData["UiStatusMessage"] = "Community food renamed.";
            return RedirectToPage(new { Status, PageNumber });
        }
        catch (ArgumentException ex)
        {
            TempData["UiStatusMessage"] = ex.Message;
            return RedirectToPage(new { Status, PageNumber });
        }
    }
    private async Task<IActionResult> Review(int id, bool approve, string? note, CancellationToken ct)
    {
        if (!ModelState.IsValid || note?.Length > 500) return BadRequest();
        if (!await community.ReviewAsync(User, id, approve, note, ct))
        {
            TempData["UiStatusMessage"] = "This submission was already reviewed or is no longer available.";
            return RedirectToPage();
        }
        TempData["UiStatusMessage"] = approve ? "Community food approved." : "Submission rejected.";
        return RedirectToPage();
    }
}
