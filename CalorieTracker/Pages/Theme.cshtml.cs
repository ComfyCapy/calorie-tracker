using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages;

[Authorize]
public sealed class ThemeModel : PageModel
{
    public void OnGet()
    {
    }
}
