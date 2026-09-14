using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages
{
    public class StatusCodeModel : PageModel
    {
        public int Code { get; private set; }

        public string Title { get; private set; } = "Request could not be completed";

        public string Message { get; private set; } =
            "Please check the request and try again.";

        public string? Guidance { get; private set; }

        public void OnGet(int code)
        {
            Code = code;

            if (code == StatusCodes.Status400BadRequest)
            {
                Title = "The Capy stares in confusion.";
                Message = "Something about that request didn't look right. Try again or head back home.";
            }
            else if (code == StatusCodes.Status404NotFound)
            {
                Title = "The Capy you are looking for is in another sauna.";
                Message = "The requested item could not be found.";
                Guidance =
                    "You haven't broken anything. The page may have moved, " +
                    "or the address may not be quite right.";
            }
        }
    }
}
