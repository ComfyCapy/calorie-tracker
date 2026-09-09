using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Theme;

public class ThemeTests
{
    private const string UserId = "theme-user";

    [Fact]
    public async Task CustomisationThemeOptionsUseBrowserStorageWithoutProfile()
    {
        using var factory = new IntegrationTestFactory();
        await ProvisionCapyAsync(factory);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Customisation");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-theme=\"light\"", html);
        Assert.Contains("data-theme=\"dark\"", html);
        Assert.Contains("data-theme=\"system\"", html);
        Assert.DoesNotContain(
            "class=\"capy-theme-option theme-option\" disabled",
            html);
        Assert.Contains("localStorage.getItem(\"theme\")", html);
        Assert.Contains("localStorage.setItem(\"theme\", savedTheme)", html);
        Assert.Contains("localStorage.setItem(\"theme\", selectedTheme)", html);
        Assert.Contains("prefers-color-scheme: dark", html);
    }

    [Fact]
    public async Task ThemeHandlerPersistsValidThemeForCurrentUser()
    {
        using var factory = new IntegrationTestFactory();
        await SeedProfileAsync(factory, ProfileOptions.SystemTheme);
        using var client = CreateClient(factory);

        var getResponse = await client.GetAsync("/Customisation");
        var token = ExtractAntiforgeryToken(
            await getResponse.Content.ReadAsStringAsync());

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/Profile/Index?handler=Theme")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["theme"] = ProfileOptions.DarkTheme
                })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            ProfileOptions.DarkTheme,
            (await context.UserProfiles.SingleAsync()).ThemePreference);
    }

    private static HttpClient CreateClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", UserId);
        return client;
    }

    private static async Task ProvisionCapyAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = $"{UserId}@example.test",
            NormalizedUserName = $"{UserId}@EXAMPLE.TEST",
            Email = $"{UserId}@example.test",
            NormalizedEmail = $"{UserId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await context.SaveChangesAsync();
        await new CapyProvisioningService(context).ProvisionAsync(UserId);
    }

    private static async Task SeedProfileAsync(
        IntegrationTestFactory factory,
        string theme)
    {
        await ProvisionCapyAsync(factory);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserProfiles.Add(new UserProfile
        {
            UserId = UserId,
            ThemePreference = theme,
            DateOfBirth = TestTime.Today.AddYears(-30).ToDateTime(TimeOnly.MinValue),
            HeightCm = 180,
            WeightKg = 80,
            CalculationSex = ProfileOptions.Male,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = ProfileOptions.Maintain
        });
        await context.SaveChangesAsync();
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
