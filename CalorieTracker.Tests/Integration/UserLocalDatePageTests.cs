using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Integration;

public class UserLocalDatePageTests
{
    [Fact]
    public async Task TimeZoneBootstrap_DoesNotReloadPostResponses()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var form = await client.GetStringAsync("/Feedback");
        var token = Regex.Match(
            form,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["FeedbackText"] = string.Empty,
                ["__RequestVerificationToken"] =
                    WebUtility.HtmlDecode(token.Groups[1].Value)
            });

        var response = await client.PostAsync("/Feedback", content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-time-zone-reload=\"false\"", html);
    }

    [Fact]
    public async Task Dashboard_LondonAfterMidnight_UsesNewLocalDayEverywhere()
    {
        const string userId = "london-midnight-user";
        using var factory = new IntegrationTestFactory(new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 8, 23, 35, 0, TimeSpan.Zero)));
        await SeedUserWithEntriesAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId, "Europe/London");

        var html = await client.GetStringAsync("/");

        Assert.Contains("Wednesday, 9 September", html);
        Assert.Contains("data-time-zone-reload=\"true\"", html);
        Assert.Contains("data-calorie-balance-year=\"2026\"", html);
        Assert.Matches(
            "<span class=\"dashboard-calorie-current\">\\s*123",
            html);
        Assert.Contains("/Diary/Create?date=2026-09-09", html);
        Assert.Contains("/Diary?date=2026-09-09", html);
    }

    [Fact]
    public async Task DiaryAndQuickAdd_NewYorkBeforeMidnight_DefaultToPreviousLocalDay()
    {
        const string userId = "new-york-midnight-user";
        using var factory = new IntegrationTestFactory(new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 9, 2, 0, 0, TimeSpan.Zero)));
        await SeedUserWithEntriesAsync(factory, userId);
        using var client = AuthenticatedClient(
            factory,
            userId,
            "America/New_York");

        var diary = await client.GetStringAsync("/Diary");
        var quickAdd = await client.GetStringAsync("/Diary/Create");

        Assert.Contains("Tuesday, 8 September 2026", diary);
        Assert.Contains("aria-current=\"date\"", diary);
        Assert.Contains("value=\"2026-09-08\"", diary);
        Assert.Contains("value=\"2026-09-08\"", quickAdd);
    }

    [Fact]
    public async Task Dashboard_YearRollover_UsesUsersCurrentYear()
    {
        const string userId = "year-rollover-user";
        using var factory = new IntegrationTestFactory(new FixedTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.Zero)));
        await SeedUserWithEntriesAsync(factory, userId);
        using var client = AuthenticatedClient(
            factory,
            userId,
            "America/New_York");

        var html = await client.GetStringAsync("/");
        var futureYear = await client.GetAsync("/?year=2026");

        Assert.Contains("Wednesday, 31 December", html);
        Assert.Contains("data-calorie-balance-year=\"2025\"", html);
        Assert.Contains("<strong aria-current=\"page\">2025</strong>", html);
        Assert.Equal(HttpStatusCode.BadRequest, futureYear.StatusCode);
    }

    private static async Task SeedUserWithEntriesAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        var food = TestData.Food(userId);
        food.Calories = 100;
        context.Foods.Add(food);
        await context.SaveChangesAsync();

        var septemberEighth = TestData.DiaryEntry(userId, food, 456);
        septemberEighth.Date = new DateTime(2026, 9, 8);
        var septemberNinth = TestData.DiaryEntry(userId, food, 123);
        septemberNinth.Date = new DateTime(2026, 9, 9);
        context.DiaryEntries.AddRange(septemberEighth, septemberNinth);
        await context.SaveChangesAsync();
    }

    private static HttpClient AuthenticatedClient(
        IntegrationTestFactory factory,
        string userId,
        string timeZoneId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            $"{UserLocalTimeProvider.TimeZoneCookieName}=" +
            Uri.EscapeDataString(timeZoneId));
        return client;
    }
}
