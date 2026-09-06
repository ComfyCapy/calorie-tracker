using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages
{
    public class StatusCodeModel : PageModel
    {
        public string Title { get; private set; } = "Request could not be completed";

        public string Message { get; private set; } =
            "Please check the request and try again.";

        public void OnGet(int code)
        {
            if (code == StatusCodes.Status400BadRequest)
            {
                Title = "The Capy stares in confusion.";
                Message = "Something about that request didn't look right. Try again or head back home.";
            }
            else if (code == StatusCodes.Status404NotFound)
            {
                Title = "The Capy you are looking for is in another sauna.";
                Message = "The requested item could not be found.";
            }
        }
    }
}
