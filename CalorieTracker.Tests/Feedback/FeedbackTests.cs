using System.Net;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieTracker.Tests.Feedback;

public class FeedbackTests
{
    [Fact]
    public async Task FeedbackPage_AnonymousGetIsAllowed()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Feedback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FeedbackPage_AuthenticatedGetIsAlsoAllowed()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-User", "user-1");

        var response = await client.GetAsync("/Feedback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidAnonymousFeedback_UsesConfiguredRecipientAndSafeEmailBody()
    {
        var emailSender = new FakeEmailSender();
        var model = CreateModel(emailSender);
        model.FeedbackText = "  Helpful <script>alert('x')</script>\r\nThank you.  ";

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var email = Assert.Single(emailSender.SentEmails);
        Assert.Equal("owner@example.test", email.Recipient);
        Assert.Equal("Comfy Capy Calories feedback", email.Subject);
        Assert.Contains("Helpful &lt;script&gt;", email.HtmlBody);
        Assert.Contains("<br />Thank you.", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.NotNull(model.SuccessMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public async Task EmptyOrWhitespaceFeedback_IsRejected(string feedback)
    {
        var emailSender = new FakeEmailSender();
        var model = CreateModel(emailSender);
        model.FeedbackText = feedback;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task FeedbackAboveMaximumLength_IsRejected()
    {
        var emailSender = new FakeEmailSender();
        var model = CreateModel(emailSender);
        model.FeedbackText = new string('a', FeedbackRules.MaximumLength + 1);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task EmailDeliveryFailure_ReturnsFriendlyPageWithoutProviderDetails()
    {
        var emailSender = new FakeEmailSender
        {
            ExceptionToThrow = new InvalidOperationException("provider secret")
        };
        var model = CreateModel(emailSender);
        model.FeedbackText = "Useful feedback";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.SubmissionError);
        Assert.DoesNotContain("provider", model.SubmissionError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner@example.test", model.SubmissionError);
    }

    [Fact]
    public async Task MissingRecipient_ReturnsFriendlyFailureWithoutSendingEmail()
    {
        var emailSender = new FakeEmailSender();
        var model = CreateModel(emailSender, recipient: null);
        model.FeedbackText = "Useful feedback";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.SubmissionError);
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task AnonymousPostWithoutAntiforgeryToken_IsRejected()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FeedbackText"] = "Useful feedback"
        });

        var response = await client.PostAsync("/Feedback", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    [Fact]
    public async Task AnonymousFeedbackPost_IsLimitedToFiveAttemptsPerWindow()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["FeedbackText"] = "Missing token"
                });
            var response = await client.PostAsync("/Feedback", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var finalContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["FeedbackText"] = "Rate limited"
            });
        var limitedResponse = await client.PostAsync("/Feedback", finalContent);

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    private static FeedbackModel CreateModel(
        FakeEmailSender emailSender,
        string? recipient = "owner@example.test")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:RecipientAddress"] = recipient
            })
            .Build();
        var model = new FeedbackModel(
            emailSender,
            configuration,
            NullLogger<FeedbackModel>.Instance);
        PageModelTestContext.Attach(model);
        return model;
    }

    private static HttpClient CreateClient(IntegrationTestFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
}
