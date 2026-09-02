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
                Title = "Invalid request";
                Message = "One or more values in the request were invalid.";
            }
            else if (code == StatusCodes.Status404NotFound)
            {
                Title = "Not found";
                Message = "The requested item could not be found.";
            }
        }
    }
}
