using CalorieTracker.Data;
using CalorieTracker.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Admin;

public sealed class UsersModel(ApplicationDbContext db, UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    public List<(ApplicationUser User, IList<string> Roles)> Accounts { get; } = [];
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Query?.Length > 100) return BadRequest();
        var term = Query?.Trim().ToUpperInvariant() ?? "";
        var matches = await db.Users.Where(x => x.NormalizedUserName!.Contains(term) || x.NormalizedEmail!.Contains(term))
            .OrderBy(x => x.UserName).Take(30).ToListAsync(ct);
        foreach (var user in matches) Accounts.Add((user, await users.GetRolesAsync(user)));
        return Page();
    }
    public Task<IActionResult> OnPostGrantBetaAsync(string id) => SetBeta(id, true);
    public Task<IActionResult> OnPostRemoveBetaAsync(string id) => SetBeta(id, false);
    private async Task<IActionResult> SetBeta(string id, bool grant)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(id)) return BadRequest();
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        // Admin membership is exclusively operational. These handlers never accept a role name.
        if (await users.IsInRoleAsync(user, AccessRoles.Admin)) return BadRequest();
        if (await users.IsInRoleAsync(user, AccessRoles.Beta) != grant)
        {
            var result = grant ? await users.AddToRoleAsync(user, AccessRoles.Beta) : await users.RemoveFromRoleAsync(user, AccessRoles.Beta);
            if (!result.Succeeded) return Conflict();
        }
        TempData["UiStatusMessage"] = grant ? "Beta access granted." : "Beta access removed.";
        return RedirectToPage(new { Query });
    }
    private IActionResult Conflict() => StatusCode(409);
}
