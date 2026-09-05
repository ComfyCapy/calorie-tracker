using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Areas.Identity.Pages.Account.Manage;
using CalorieTracker.Data;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Identity;

public class AccountSecurityTests
{
    private const string UserId = "identity-user";
    private const string Email = "test+qr@example.test";
    private const string AuthenticatorKey = "JBSWY3DPEHPK3PXP";

    [Fact]
    public async Task EnableAuthenticator_AnonymousRequestIsUnauthorized()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(
            "/Identity/Account/Manage/EnableAuthenticator");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnableAuthenticator_ExposesEncodedUriToLocalQrScripts()
    {
        using var factory = new IntegrationTestFactory();
        await SeedIdentityUserAsync(factory);
        using var client = CreateClient(factory, UserId);

        var response = await client.GetAsync(
            "/Identity/Account/Manage/EnableAuthenticator");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"qrCode\"", html);
        Assert.Contains("/lib/qrcodejs/qrcode.min.", html);
        Assert.Contains("/js/qr.", html);
        Assert.Contains("jbsw y3dp ehpk 3pxp", html);
        Assert.DoesNotContain(
            "Learn how to enable QR code generation",
            html,
            StringComparison.OrdinalIgnoreCase);

        var uriMatch = Regex.Match(
            html,
            "id=\"qrCodeData\" data-url=\"([^\"]+)\"");
        Assert.True(uriMatch.Success);
        var authenticatorUri = WebUtility.HtmlDecode(uriMatch.Groups[1].Value);

        Assert.StartsWith(
            "otpauth://totp/CalorieTracker:test%2Bqr@example.test?",
            authenticatorUri);
        Assert.Contains($"secret={AuthenticatorKey}", authenticatorUri);
        Assert.Contains("issuer=CalorieTracker", authenticatorUri);

        var qrScriptSources = Regex.Matches(
                html,
                "<script[^>]+src=\"([^\"]+)\"",
                RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .Where(source => source.Contains("qrcodejs") ||
                source.Contains("/js/qr."))
            .ToList();
        Assert.Equal(2, qrScriptSources.Count);
        Assert.All(qrScriptSources, source =>
            Assert.False(Uri.TryCreate(source, UriKind.Absolute, out _)));

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/lib/qrcodejs/qrcode.min.js")).StatusCode);
        var qrScript = await client.GetStringAsync("/js/qr.js");
        Assert.Contains("qrCodeDataElement?.dataset.url", qrScript);
        Assert.Contains("new QRCode", qrScript);
    }

    [Fact]
    public async Task EnableAuthenticator_InvalidCodeDoesNotEnableTwoFactor()
    {
        using var factory = new IntegrationTestFactory();
        await SeedIdentityUserAsync(factory);
        using var client = CreateClient(factory, UserId);
        var getResponse = await client.GetAsync(
            "/Identity/Account/Manage/EnableAuthenticator");
        var html = await getResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(html);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Code"] = "000000",
                ["__RequestVerificationToken"] = antiforgeryToken
            });
        var response = await client.PostAsync(
            "/Identity/Account/Manage/EnableAuthenticator",
            content);
        var responseHtml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Verification code is invalid.", responseHtml);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(UserId);
        Assert.NotNull(user);
        Assert.False(await userManager.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task ManageProfile_DoesNotDisplayOrUpdateExistingPhoneNumber()
    {
        using var factory = new IntegrationTestFactory();
        await SeedIdentityUserAsync(factory, phoneNumber: "+44 7700 900123");
        using var client = CreateClient(factory, UserId);

        var getResponse = await client.GetAsync("/Identity/Account/Manage");
        var html = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(Email, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Phone number", html, StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(IndexModel).GetProperty("Input"));

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.PhoneNumber"] = "+44 7700 900999",
                ["__RequestVerificationToken"] = ExtractAntiforgeryToken(html)
            });
        var postResponse = await client.PostAsync(
            "/Identity/Account/Manage",
            content);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == UserId);
        Assert.Equal("+44 7700 900123", user.PhoneNumber);
    }

    private static HttpClient CreateClient(
        IntegrationTestFactory factory,
        string? userId = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        if (userId != null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", userId);
        }

        return client;
    }

    private static async Task SeedIdentityUserAsync(
        IntegrationTestFactory factory,
        string? phoneNumber = null)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            PhoneNumber = phoneNumber
        };

        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        var userStore = scope.ServiceProvider
            .GetRequiredService<IUserStore<ApplicationUser>>();
        var authenticatorStore = Assert.IsAssignableFrom<
            IUserAuthenticatorKeyStore<ApplicationUser>>(userStore);
        await authenticatorStore.SetAuthenticatorKeyAsync(
            user,
            AuthenticatorKey,
            CancellationToken.None);
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
