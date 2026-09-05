using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages;

[AllowAnonymous]
public class FeedbackModel : PageModel
{
    private const string DeliveryFailureMessage =
        "Sorry, we couldn't send your feedback right now. Please try again later.";

    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeedbackModel> _logger;

    public FeedbackModel(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<FeedbackModel> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Please enter your feedback.")]
    [StringLength(
        FeedbackRules.MaximumLength,
        ErrorMessage = "Feedback must be 4,000 characters or fewer.")]
    public string FeedbackText { get; set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    public string? SubmissionError { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(FeedbackText))
        {
            ModelState.AddModelError(
                nameof(FeedbackText),
                "Please enter your feedback.");
        }
        else
        {
            FeedbackText = FeedbackText.Trim();

            if (FeedbackText.Length > FeedbackRules.MaximumLength)
            {
                ModelState.AddModelError(
                    nameof(FeedbackText),
                    "Feedback must be 4,000 characters or fewer.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var recipient = _configuration["Feedback:RecipientAddress"];

        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogError("Feedback recipient address is not configured.");
            SubmissionError = DeliveryFailureMessage;
            return Page();
        }

        var feedbackLines = FeedbackText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');

        var encodedFeedback = string.Join(
            "<br />",
            feedbackLines.Select(HtmlEncoder.Default.Encode));

        try
        {
            await _emailSender.SendEmailAsync(
                recipient,
                "CalorieTracker feedback",
                $"<p>{encodedFeedback}</p>");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to deliver anonymous feedback.");
            SubmissionError = DeliveryFailureMessage;
            return Page();
        }

        SuccessMessage = "Thank you — your feedback has been sent.";
        return RedirectToPage();
    }
}
