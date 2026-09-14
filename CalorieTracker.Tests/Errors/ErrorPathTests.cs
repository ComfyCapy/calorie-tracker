using System.Net;
using CalorieTracker.Data;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Errors;

public sealed class ErrorPathTests
{
    private const string UnknownRoute =
        "/this-route-definitely-does-not-exist";

    [Fact]
    public async Task AnonymousDatabaseFailure_RendersSafeFriendlyError()
    {
        using var factory = new IntegrationTestFactory();
        await MakeApplicationDatabaseUnavailableAsync(factory);
        using var client = CreateClient(factory);

        var response = await client.GetAsync(
            "/Identity/Account/ConfirmEmail?userId=missing&code=dGVzdA");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertFriendlyError(html, authenticated: false);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task AuthenticatedDatabaseFailure_RendersWithoutApplicationShell()
    {
        using var factory = new IntegrationTestFactory();
        await MakeApplicationDatabaseUnavailableAsync(factory);
        using var client = CreateClient(factory, "error-user");

        var response = await client.GetAsync("/Diary");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertFriendlyError(html, authenticated: true);
        Assert.DoesNotContain("app-sidebar", html);
        Assert.DoesNotContain("account-nav", html);
        Assert.DoesNotContain("SqliteException", html);
        Assert.DoesNotContain("no such table", html);
        Assert.DoesNotContain("UserProfiles", html);
    }

    [Fact]
    public async Task UnknownRoute_PreservesNotFoundForAnonymousAndAuthenticatedUsers()
    {
        using var factory = new IntegrationTestFactory();
        await MakeApplicationDatabaseUnavailableAsync(factory);
        using var anonymousClient = CreateClient(factory);
        using var authenticatedClient = CreateClient(factory, "error-user");

        var anonymousResponse = await anonymousClient.GetAsync(UnknownRoute);
        var authenticatedResponse = await authenticatedClient.GetAsync(
            UnknownRoute);

        await AssertFriendlyNotFoundAsync(
            anonymousResponse,
            expectedRecoveryText: "Return home");
        await AssertFriendlyNotFoundAsync(
            authenticatedResponse,
            expectedRecoveryText: "Go to Dashboard");
    }

    [Fact]
    public async Task DirectFailurePages_RemainReachableWithoutDatabase()
    {
        using var factory = new IntegrationTestFactory();
        await MakeApplicationDatabaseUnavailableAsync(factory);
        using var anonymousClient = CreateClient(factory);
        using var authenticatedClient = CreateClient(factory, "error-user");

        var anonymousError = await anonymousClient.GetAsync("/Error");
        var authenticatedError = await authenticatedClient.GetAsync("/Error");
        var directStatus = await authenticatedClient.GetAsync(
            "/StatusCode/404");

        Assert.Equal(HttpStatusCode.OK, anonymousError.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticatedError.StatusCode);
        Assert.Equal(HttpStatusCode.OK, directStatus.StatusCode);
        Assert.Contains(
            "Something went wrong",
            await anonymousError.Content.ReadAsStringAsync());
        Assert.Contains(
            "Go to Dashboard",
            await authenticatedError.Content.ReadAsStringAsync());

        var statusHtml = await directStatus.Content.ReadAsStringAsync();
        Assert.Contains("Page not found", statusHtml);
        Assert.Contains("noindex,follow", statusHtml);
    }

    private static HttpClient CreateClient(
        IntegrationTestFactory factory,
        string? userId = null)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
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

    private static async Task MakeApplicationDatabaseUnavailableAsync(
        IntegrationTestFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await context.Database.GetDbConnection().CloseAsync();
    }

    private static void AssertFriendlyError(
        string html,
        bool authenticated)
    {
        Assert.Contains("class=\"failure-shell\"", html);
        Assert.Contains(
            "<h1 id=\"errorTitle\">Something went wrong</h1>",
            html);
        Assert.Contains("Request ID:", html);
        Assert.Contains("content=\"noindex,follow\"", html);
        Assert.Contains(
            authenticated ? "Go to Dashboard" : "Return home",
            html);
        Assert.DoesNotContain("StackTrace", html);
        Assert.DoesNotContain(" at Microsoft.", html);
    }

    private static async Task AssertFriendlyNotFoundAsync(
        HttpResponseMessage response,
        string expectedRecoveryText)
    {
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", html);
        Assert.Contains(
            "The Capy you are looking for is in another sauna.",
            html);
        Assert.Contains("broken anything", html);
        Assert.Contains("content=\"noindex,follow\"", html);
        Assert.Contains(expectedRecoveryText, html);
        Assert.DoesNotContain("app-sidebar", html);
    }
}
