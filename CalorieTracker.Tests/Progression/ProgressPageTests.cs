using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressPageTests
{
    private const string UserId = "progress-user";

    [Fact]
    public async Task ProgressPage_RequiresAuthentication()
    {
        using var factory = new IntegrationTestFactory();
        using var client = Client(factory);

        var response = await client.GetAsync("/Progress");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedProgressPage_LoadsWithOrderedActiveNavigation()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        using var client = Client(factory, UserId);

        var response = await client.GetAsync("/Progress");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Achievements", html);
        Assert.Contains("<h1 class=\"ct-page-title\">Achievements</h1>", html);
        Assert.Contains("Little steps add up.", html);
        Assert.Matches(
            "<a(?=[^>]*href=\"/Progress\")" +
            "(?=[^>]*aria-current=\"page\")[^>]*>",
            html);

        var expectedNavigation = new[]
        {
            "/", "/Diary", "/Foods", "/SavedMeals", "/Profile",
            "/Customisation", "/Progress"
        };
        var previousIndex = -1;

        foreach (var path in expectedNavigation)
        {
            var index = html.IndexOf(
                $"href=\"{path}\"",
                previousIndex + 1,
                StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Missing or unordered {path}.");
            previousIndex = index;
        }
    }

    [Fact]
    public async Task Get_ReconcilesBeforeSummaryAndDoesNotRecordActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddDiaryEvidenceAsync(factory);
        using var client = Client(factory, UserId, "UTC");

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/Progress"));

        Assert.Contains(
            "Nice — we found an achievement you’d already earned.",
            html);
        Assert.Contains(
            $"data-achievement-key=\"{AchievementDefinitions.DiaryFirstEntryKey}\"",
            html);
        Assert.Contains("data-achievement-state=\"unlocked\"", html);
        Assert.Contains("data-hide-unlocked", html);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.True(await context.UserAchievements.AnyAsync(item =>
            item.UserId == UserId &&
            item.AchievementKey ==
                AchievementDefinitions.DiaryFirstEntryKey));
        Assert.Equal(1, await context.UserProgressionStates
            .Where(item => item.UserId == UserId)
            .Select(item => item.AchievementBackfillVersion)
            .SingleAsync());
        Assert.Empty(await context.UserDailyActivities.ToArrayAsync());
        Assert.DoesNotContain(
            await context.UserXpEvents.ToArrayAsync(),
            item => item.EventKey.StartsWith("daily:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconciliationNoOp_DoesNotShowBackfillMessage()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        using var client = Client(factory, UserId);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/Progress"));

        Assert.DoesNotContain("Nice — we found", html);
    }

    [Fact]
    public async Task ReconciliationFailure_LogsAndStillRendersExistingSummary()
    {
        var logs = new ListLoggerProvider();
        using var factory = new IntegrationTestFactory(loggerProvider: logs);
        await AddUserAsync(factory);
        await AddDiaryEvidenceAsync(factory);
        await AddXpAsync(factory, 75);
        await CreateAchievementFailureTriggerAsync(factory);
        using var client = Client(factory, UserId, "UTC");

        var response = await client.GetAsync("/Progress");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Level 2", html);
        Assert.Contains("75 XP", html);
        Assert.Contains(
            $"data-achievement-key=\"{AchievementDefinitions.DiaryFirstEntryKey}\"",
            html);
        Assert.Contains("data-achievement-state=\"locked\"", html);
        Assert.DoesNotContain("Nice — we found", html);
        Assert.Contains(logs.Messages, message => message.Contains(
            "Optional Progress page reconciliation failed",
            StringComparison.Ordinal));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await context.UserDailyActivities.ToArrayAsync());
    }

    [Fact]
    public async Task LevelCard_RendersAuthoritativeXpAndNextLevelProgress()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddXpAsync(factory, 75);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains("Level 2", html);
        Assert.Contains("75 XP", html);
        Assert.Contains("33.33% to your next level", html);
        Assert.Contains("50 XP to go", html);
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=\"33.33\"", html);
        Assert.Contains("aria-valuemin=\"0\"", html);
        Assert.Contains("aria-valuemax=\"100\"", html);
        Assert.Contains("aria-label=\"Progress toward the next level\"", html);
    }

    [Fact]
    public async Task FinalLevel_RendersCompletedStateWithoutNextLevelCopy()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddXpAsync(factory, 1600);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains("Level 10", html);
        Assert.Contains("1600 XP", html);
        Assert.Contains("Highest level reached", html);
        Assert.DoesNotContain("to your next level", html);
        Assert.DoesNotContain("Level 11", html);
    }

    [Fact]
    public async Task ActivityCard_RendersCurrentLongestAndDistinctDays()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddActivitiesAsync(
            factory,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 4),
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 9));
        using var client = Client(factory, UserId, "UTC");

        var html = Compact(await client.GetStringAsync("/Progress"));

        Assert.Matches("Current streak</dt><dd><strong>3</strong><span>days", html);
        Assert.Matches("Longest streak</dt><dd><strong>4</strong><span>days", html);
        Assert.Matches("Active days</dt><dd><strong>7</strong><span>total", html);
    }

    [Fact]
    public async Task InactiveCurrentStreak_UsesNeutralLanguage()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddActivitiesAsync(
            factory,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 2));
        using var client = Client(factory, UserId, "UTC");

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains("No active streak right now.", html);
        Assert.DoesNotContain("lost your streak", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("start over", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No activity yet.", html);
    }

    [Fact]
    public async Task NeverActiveUser_GetsCalmEmptyState()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains(
            "No activity yet. Your active days will appear here when they happen.",
            html);
        Assert.Contains("No active streak right now.", html);
    }

    [Fact]
    public async Task Achievements_RenderAllDefinitionsInStableOrderAndState()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddKnownAndUnknownAchievementsAsync(factory);
        using var client = Client(factory, UserId);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/Progress"));
        var previousIndex = -1;

        foreach (var definition in AchievementDefinitions.All)
        {
            var marker = $"data-achievement-key=\"{definition.Key}\"";
            var index = html.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Unexpected order for {definition.Key}.");
            previousIndex = index;
            Assert.Contains(definition.DisplayName, html);
            Assert.Contains(definition.Lore, html);
            Assert.Contains(definition.HowToGet, html);
            Assert.Contains($"+{definition.XpReward} XP", html);
        }

        Assert.Equal(
            AchievementDefinitions.All.Count,
            Regex.Matches(html, "progress-achievement-how-to-label").Count);

        Assert.Equal(
            AchievementDefinitions.All.Count,
            Regex.Matches(html, "data-achievement-key=").Count);
        Assert.Matches(
            $"data-achievement-key=\"{Regex.Escape(AchievementDefinitions.DiaryFirstEntryKey)}\"[^>]*data-achievement-state=\"unlocked\"",
            html);
        Assert.Matches(
            $"data-achievement-key=\"{Regex.Escape(AchievementDefinitions.ProfileCompletedKey)}\"[^>]*data-achievement-state=\"locked\"",
            html);
        Assert.Matches(">\\s*Unlocked\\s*</span>", html);
        Assert.Matches(">\\s*Locked\\s*</span>", html);
        Assert.DoesNotContain("future.unknown", html);
    }

    [Fact]
    public void PlaceholderVisualMapping_IsStableAndHasSafeFallback()
    {
        foreach (var definition in AchievementDefinitions.All)
        {
            Assert.NotEqual(
                ProgressAchievementVisuals.FallbackSymbol,
                ProgressAchievementVisuals.SymbolFor(definition.Key));
        }

        Assert.Equal(
            ProgressAchievementVisuals.FallbackSymbol,
            ProgressAchievementVisuals.SymbolFor("future.unknown"));
    }

    [Fact]
    public async Task Markup_UsesSemanticStatesWithoutCollapsibleUi()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains("<section class=\"progress-achievements\"", html);
        Assert.Contains("<article class=\"progress-achievement-card", html);
        Assert.Contains("data-achievement-state=\"locked\"", html);
        Assert.DoesNotContain("data-bs-toggle=\"collapse\"", html);
        Assert.DoesNotContain("<details", html);
    }

    [Fact]
    public async Task Achievements_HideUnlockedControlIsAccessibleAndProgressive()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/Progress");

        Assert.Contains("data-achievements-section", html);
        Assert.Contains(
            "id=\"progress-achievement-grid\"",
            html);
        Assert.Contains("data-hide-unlocked", html);
        Assert.Contains("aria-pressed=\"false\"", html);
        Assert.Contains(
            "aria-controls=\"progress-achievement-grid\"",
            html);
        Assert.Contains("hidden", html);
        Assert.Contains(
            "data-achievements-empty",
            html);
        Assert.Contains("All achievements are unlocked.", html);
        Assert.Matches(
            "src=\"/js/progress-achievements\\.[^\"]+\\.mjs\"",
            html);
    }

    [Fact]
    public async Task Sidebar_ShowsAuthoritativeProgressionLevel()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory);
        await AddCurrentProgressionStateAsync(factory);
        await AddXpAsync(factory, 75);
        using var client = Client(factory, UserId);

        var html = await client.GetStringAsync("/About");

        Assert.Contains("class=\"account-nav-level\">Level 2</span>", html);
    }

    private static HttpClient Client(
        IntegrationTestFactory factory,
        string? userId = null,
        string? timeZoneId = null)
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

        if (timeZoneId != null)
        {
            client.DefaultRequestHeaders.Add(
                "Cookie",
                $"{UserLocalTimeProvider.TimeZoneCookieName}={timeZoneId}");
        }

        return client;
    }

    private static async Task AddUserAsync(IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "progress@example.test",
            NormalizedUserName = "PROGRESS@EXAMPLE.TEST",
            Email = "progress@example.test",
            NormalizedEmail = "PROGRESS@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddCurrentProgressionStateAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserProgressionStates.Add(new UserProgressionState
        {
            UserId = UserId,
            AchievementBackfillVersion = 1
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddXpAsync(
        IntegrationTestFactory factory,
        int amount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserXpEvents.Add(new UserXpEvent
        {
            UserId = UserId,
            EventKey = $"test:{amount}",
            Amount = amount,
            AwardedAtUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddActivitiesAsync(
        IntegrationTestFactory factory,
        params DateOnly[] dates)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserDailyActivities.AddRange(dates.Select(date =>
            new UserDailyActivity
            {
                UserId = UserId,
                LocalDate = date,
                RecordedAtUtc = date.ToDateTime(
                    new TimeOnly(12, 0),
                    DateTimeKind.Utc),
                TimeZoneId = "UTC"
            }));
        await context.SaveChangesAsync();
    }

    private static async Task AddDiaryEvidenceAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var food = TestData.Food(UserId);
        food.Source = FoodSources.Usda;
        food.ExternalId = "progress-page-food";
        var entry = TestData.DiaryEntry(UserId, food, 100);
        entry.Date = new DateTime(2026, 9, 1);
        entry.MealType = "Breakfast";
        context.DiaryEntries.Add(entry);
        await context.SaveChangesAsync();
    }

    private static async Task AddKnownAndUnknownAchievementsAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserAchievements.AddRange(
            new UserAchievement
            {
                UserId = UserId,
                AchievementKey = AchievementDefinitions.DiaryFirstEntryKey,
                UnlockedAtUtc = new DateTime(
                    2026,
                    8,
                    20,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc)
            },
            new UserAchievement
            {
                UserId = UserId,
                AchievementKey = "future.unknown",
                UnlockedAtUtc = new DateTime(
                    2026,
                    8,
                    21,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();
    }

    private static async Task CreateAchievementFailureTriggerAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER "FailProgressPageReconciliation"
            BEFORE INSERT ON "UserAchievements"
            BEGIN
                SELECT RAISE(
                    FAIL,
                    'forced Progress page reconciliation failure');
            END;
            """);
    }

    private static string Compact(string html) =>
        Regex.Replace(html, @">\s+<", "><");

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) =>
            new ListLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class ListLogger(ConcurrentQueue<string> messages)
            : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }
    }
}
