using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages.Admin;

public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    public int Pending { get; private set; }
    public int Approved { get; private set; }
    public int Rejected { get; private set; }
    public int Users { get; private set; }
    public async Task OnGetAsync(CancellationToken ct)
    {
        Pending = await db.CommunityFoods.CountAsync(x => x.Status == CommunityFoodStatus.Pending, ct);
        Approved = await db.CommunityFoods.CountAsync(x => x.Status == CommunityFoodStatus.Approved, ct);
        Rejected = await db.CommunityFoods.CountAsync(x => x.Status == CommunityFoodStatus.Rejected, ct);
        Users = await db.Users.CountAsync(ct);
    }
}
