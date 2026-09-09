using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CalorieTracker.Tests.Branding;

public class BrandingTests
{
    [Theory]
    [InlineData("/About", "About · Comfy Capy Calories")]
    [InlineData("/Help", "Help & FAQ · Comfy Capy Calories")]
    [InlineData("/Privacy", "Privacy · Comfy Capy Calories")]
    public async Task PublicInformationPages_AreAvailableAndUseProductTitles(
        string route,
        string expectedTitle)
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var titleMatch = Regex.Match(html, "<title>(.*?)</title>");
        Assert.True(titleMatch.Success);
        Assert.Equal(
            expectedTitle,
            WebUtility.HtmlDecode(titleMatch.Groups[1].Value));
        Assert.Contains("Comfy Capy Calories", html);
    }

    [Fact]
    public async Task SharedLayout_UsesProductBrandAndLocalCapyMark()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains(">Comfy Capy Calories</span>", html);
        Assert.Contains("/images/capy/expressions/Capy-Base.png", html);
        Assert.Contains("class=\"navbar-brand\"", html);
        Assert.DoesNotContain("<a class=\"navbar-brand\"", html);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("A Comfy Capy product", html);
        Assert.Contains("href=\"/About\"", html);
        Assert.Contains("href=\"/Help\"", html);
        Assert.Contains("id=\"developmentBanner\"", html);
        Assert.Contains(
            "sessionStorage.getItem(\"comfyCapy.developmentBannerDismissed\")",
            html);
        Assert.Contains(
            "document.documentElement.dataset.developmentBannerDismissed",
            html);
        Assert.Contains(
            "Comfy Capy Calories is in active development.",
            html);
        Assert.Contains("href=\"/Feedback\"", html);
        Assert.Contains("Send feedback →", html);
        Assert.Contains("data-development-banner-dismiss", html);
        Assert.Contains(
            "aria-label=\"Dismiss active development notice\"",
            html);
        Assert.Contains("development-banner.", html);

        var bootstrapIndex = html.IndexOf(
            "sessionStorage.getItem(\"comfyCapy.developmentBannerDismissed\")",
            StringComparison.Ordinal);
        var bannerIndex = html.IndexOf(
            "id=\"developmentBanner\"",
            StringComparison.Ordinal);

        Assert.True(bootstrapIndex >= 0);
        Assert.True(bannerIndex > bootstrapIndex);
        Assert.Single(Regex.Matches(html, @">\s*About\s*</a>"));
        Assert.Single(Regex.Matches(html, @">\s*Help\s*</a>"));

        var iconResponse = await client.GetAsync(
            "/images/capy/expressions/Capy-Base.png");
        Assert.Equal(HttpStatusCode.OK, iconResponse.StatusCode);
        Assert.Equal("image/png", iconResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginPage_KeepsIdentityFormAndUsesFriendlyProductCopy()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Identity/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Welcome back", html);
        Assert.Contains("id=\"account\"", html);
        Assert.Contains("name=\"Input.Username\"", html);
        Assert.Contains("name=\"Input.Password\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
    }

    [Fact]
    public async Task RegisterPage_OffersDirectLoginWithoutChangingRegistrationForm()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync(
            "/Identity/Account/Register");

        Assert.Contains("Make yourself at home", html);
        Assert.Contains(
            "Create an account to save your Diary and personalise your Capy.",
            html);
        Assert.Contains("Already have an account?", html);
        Assert.Contains("href=\"/Identity/Account/Login\">Log in</a>", html);
        Assert.Contains("id=\"registerForm\"", html);
    }

    [Fact]
    public async Task RegisterPage_UsesLocalValidationScriptsWithoutFallbackIntegrityMetadata()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync("/Identity/Account/Register");

        Assert.Contains(
            "/lib/jquery-validation/dist/jquery.validate.min.",
            html);
        Assert.Contains(
            "/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.",
            html);
        Assert.DoesNotContain(
            "cdnjs.cloudflare.com/ajax/libs/jquery-validation",
            html);
        Assert.DoesNotContain("integrity=\"sha384-DU2a51", html);
    }

    [Fact]
    public async Task FocusedCopyPages_KeepRequestedText()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var privacyHtml = await client.GetStringAsync("/Privacy");
        Assert.Contains(
            "How Comfy Capy Calories uses and protects your information.",
            privacyHtml);

        var resendHtml = await client.GetStringAsync(
            "/Identity/Account/ResendEmailConfirmation");
        Assert.Contains(
            "Enter your email and we'll send a fresh confirmation link.",
            resendHtml);

        var notFoundHtml = await client.GetStringAsync("/StatusCode/404");
        Assert.Contains(
            "The Capy you are looking for is in another sauna.",
            notFoundHtml);
        Assert.Contains("The requested item could not be found.", notFoundHtml);
        Assert.Contains("Return home", notFoundHtml);
    }

    [Fact]
    public async Task RegistrationEmail_UsesProductBrandAndKeepsConfirmationLink()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);
        var registerHtml = await client.GetStringAsync(
            "/Identity/Account/Register");
        var tokenMatch = Regex.Match(
            registerHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Username"] = "brand-test-user",
                ["Input.FirstName"] = "Capy",
                ["Input.Email"] = "brand@example.test",
                ["Input.Password"] = "Password1!",
                ["Input.ConfirmPassword"] = "Password1!",
                ["__RequestVerificationToken"] =
                    WebUtility.HtmlDecode(tokenMatch.Groups[1].Value)
            });

        var response = await client.PostAsync(
            "/Identity/Account/Register",
            content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var email = Assert.Single(factory.EmailSender.SentEmails);
        Assert.Equal(
            "Confirm your Comfy Capy Calories account",
            email.Subject);
        Assert.Contains(
            "Feel free to give that link a click to confirm your account and finish setting everything up.",
            email.HtmlBody);
        Assert.Contains("~ Comfy Capy", email.HtmlBody);
        Assert.Contains("/Identity/Account/ConfirmEmail", email.HtmlBody);
    }

    private static HttpClient CreateClient(IntegrationTestFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
}
