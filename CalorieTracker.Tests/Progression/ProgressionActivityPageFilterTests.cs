using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionActivityPageFilterTests
{
    private static readonly DateTimeOffset DefaultUtcNow =
        new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnonymousRazorPage_DoesNotRecordActivity()
    {
        using var factory = new IntegrationTestFactory();
        using var client = Client(factory, null, "UTC");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await ActivityCountAsync(factory));
    }

    [Fact]
    public async Task AuthenticatedMeaningfulRazorPage_RecordsActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = Assert.Single(await ActivitiesAsync(factory));
        Assert.Equal("user-1", activity.UserId);
        Assert.Equal(new DateOnly(2026, 9, 9), activity.LocalDate);
    }

    [Fact]
    public async Task MissingTimeZoneCookie_DoesNotRecordActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", null);

        await client.GetAsync("/");

        Assert.Equal(0, await ActivityCountAsync(factory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not/A_Time_Zone")]
    [InlineData("%E0%A4%A")]
    public async Task InvalidTimeZoneCookie_DoesNotRecordActivity(
        string cookieValue)
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(
            factory,
            "user-1",
            cookieValue,
            cookieIsEncoded: true);

        await client.GetAsync("/");

        Assert.Equal(0, await ActivityCountAsync(factory));
    }

    [Theory]
    [InlineData(
        "2026-09-08T23:35:00Z",
        "Europe/London",
        2026,
        9,
        9)]
    [InlineData(
        "2026-09-09T02:00:00Z",
        "America/New_York",
        2026,
        9,
        8)]
    public async Task ValidTimeZone_DerivesServerLocalDateAcrossUtcBoundary(
        string utcNow,
        string timeZoneId,
        int year,
        int month,
        int day)
    {
        using var factory = new IntegrationTestFactory(
            new FixedTimeProvider(DateTimeOffset.Parse(utcNow)));
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", timeZoneId);

        await client.GetAsync("/");

        var activity = Assert.Single(await ActivitiesAsync(factory));
        Assert.Equal(new DateOnly(year, month, day), activity.LocalDate);
        Assert.Equal(timeZoneId, activity.TimeZoneId);
    }

    [Fact]
    public async Task QueryDate_CannotChooseProgressionActivityDate()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync("/Diary?date=2099-12-31");

        var activity = Assert.Single(await ActivitiesAsync(factory));
        Assert.Equal(new DateOnly(2026, 9, 9), activity.LocalDate);
    }

    [Fact]
    public async Task RepeatedRequestsSameLocalDay_CreateOneActivityAndDailyXp()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync("/");
        await client.GetAsync("/Diary");
        await client.GetAsync("/Foods");

        Assert.Equal(1, await ActivityCountAsync(factory));
        Assert.Equal(1, await DailyXpCountAsync(factory));
    }

    [Fact]
    public async Task PersistentAuthenticatedSession_RecordsEachNewLocalDay()
    {
        var timeProvider = new MutableTimeProvider(DefaultUtcNow);
        using var factory = new IntegrationTestFactory(timeProvider);
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync("/");
        timeProvider.UtcNow = DefaultUtcNow.AddDays(1);
        await client.GetAsync("/Diary");

        Assert.Equal(
            [new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10)],
            (await ActivitiesAsync(factory))
                .Select(item => item.LocalDate)
                .Order()
                .ToArray());
        Assert.Equal(2, await DailyXpCountAsync(factory));
    }

    [Theory]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Privacy")]
    [InlineData("/Help")]
    [InlineData("/Feedback")]
    [InlineData("/StatusCode/404")]
    [InlineData("/Error")]
    [InlineData("/Progress")]
    [InlineData("/Foods/ApiFood?id=missing&provider=cofid")]
    [InlineData("/api/foods/search?query=")]
    [InlineData("/css/site.css")]
    public async Task ExcludedRequest_DoesNotRecordActivity(string path)
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync(path);

        Assert.Equal(0, await ActivityCountAsync(factory));
        Assert.Equal(0, await DailyXpCountAsync(factory));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Diary")]
    [InlineData("/Foods")]
    [InlineData("/SavedMeals")]
    [InlineData("/Profile")]
    [InlineData("/Customisation")]
    public async Task RepresentativeProductPage_RecordsActivity(string path)
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, await ActivityCountAsync(factory));
        Assert.Equal(1, await DailyXpCountAsync(factory));
    }

    [Fact]
    public async Task MeaningfulAuthenticatedRazorPagePost_RecordsActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        using var client = Client(factory, "user-1", "UTC");
        var form = await client.GetStringAsync("/Feedback");
        var token = Regex.Match(
            form,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] =
                    WebUtility.HtmlDecode(token.Groups[1].Value)
            });

        var response = await client.PostAsync(
            "/Customisation?handler=Provision",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await ActivityCountAsync(factory));
        Assert.Equal(1, await DailyXpCountAsync(factory));
    }

    [Fact]
    public async Task EligibleActivityPath_ReconcilesWithoutHistoricalActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        await AddHistoricalDiaryEntriesAsync(factory, "user-1", 30);
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync("/");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await context.UserProgressionStates
            .Where(item => item.UserId == "user-1")
            .Select(item => item.AchievementBackfillVersion)
            .SingleAsync());
        Assert.True(await context.UserAchievements.AnyAsync(item =>
            item.UserId == "user-1" &&
            item.AchievementKey ==
                AchievementDefinitions.DiaryDistinctDaysThirtyKey));
        var activity = Assert.Single(await context.UserDailyActivities
            .Where(item => item.UserId == "user-1")
            .ToArrayAsync());
        Assert.Equal(new DateOnly(2026, 9, 9), activity.LocalDate);
        Assert.Equal(1, await context.UserXpEvents.CountAsync(item =>
            item.UserId == "user-1" &&
            item.EventKey.StartsWith("daily:")));
    }

    [Fact]
    public async Task AlreadyCurrentReconciliation_IsHarmlessOnActivityPath()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        await AddHistoricalDiaryEntriesAsync(factory, "user-1", 1);
        await AddProgressionStateAsync(factory, "user-1", 1);
        using var client = Client(factory, "user-1", "UTC");

        await client.GetAsync("/");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await ActivityCountAsync(factory));
        Assert.False(await context.UserAchievements.AnyAsync(item =>
            item.UserId == "user-1" &&
            item.AchievementKey == AchievementDefinitions.DiaryFirstEntryKey));
        Assert.Equal(1, await context.UserProgressionStates.CountAsync(item =>
            item.UserId == "user-1" &&
            item.AchievementBackfillVersion == 1));
    }

    [Fact]
    public async Task ActivityFailure_IsLoggedAndDoesNotFailPage()
    {
        var logs = new ListLoggerProvider();
        using var factory = new IntegrationTestFactory(
            loggerProvider: logs);
        await AddUserAsync(factory, "user-1");
        await CreateFailureTriggerAsync(
            factory,
            "UserDailyActivities",
            "FailProgressionActivity");
        using var client = Client(factory, "user-1", "UTC");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await ActivityCountAsync(factory));
        Assert.Contains(logs.Messages, message =>
            message.Contains(
                "Optional progression activity failed",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconciliationFailure_IsLoggedAndDoesNotFailPage()
    {
        var logs = new ListLoggerProvider();
        using var factory = new IntegrationTestFactory(
            loggerProvider: logs);
        await AddUserAsync(factory, "user-1");
        await AddHistoricalDiaryEntriesAsync(factory, "user-1", 1);
        await CreateFailureTriggerAsync(
            factory,
            "UserAchievements",
            "FailProgressionReconciliation");
        using var client = Client(factory, "user-1", "UTC");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await ActivityCountAsync(factory));
        Assert.Contains(logs.Messages, message =>
            message.Contains(
                "Optional progression reconciliation failed",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task PageHandlerException_IsNotSwallowedByFilter()
    {
        await using var database = await TestDatabase.CreateAsync();
        var context = PageContext();
        var expected = new InvalidOperationException("handler failure");
        var filter = new ProgressionActivityPageFilter(
            new ProgressionService(
                database.Context,
                new ProgressionLevelCalculator(),
                new ActivityStreakCalculator(),
                new FixedTimeProvider(DefaultUtcNow)),
            new FixedTimeProvider(DefaultUtcNow),
            NullLogger<ProgressionActivityPageFilter>.Instance);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.OnPageHandlerExecutionAsync(
                context,
                () => Task.FromException<PageHandlerExecutedContext>(
                    expected)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task AuthenticatedUsers_RecordOnlyTheirOwnActivity()
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-a");
        await AddUserAsync(factory, "user-b");
        using var userA = Client(factory, "user-a", "UTC");
        using var userB = Client(factory, "user-b", "UTC");

        await userA.GetAsync("/");

        Assert.Equal(1, await ActivityCountAsync(factory, "user-a"));
        Assert.Equal(0, await ActivityCountAsync(factory, "user-b"));

        await userB.GetAsync("/");

        Assert.Equal(1, await ActivityCountAsync(factory, "user-a"));
        Assert.Equal(1, await ActivityCountAsync(factory, "user-b"));
    }

    private static HttpClient Client(
        IntegrationTestFactory factory,
        string? userId,
        string? timeZoneId,
        bool cookieIsEncoded = false)
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
            var cookieValue = cookieIsEncoded
                ? timeZoneId
                : Uri.EscapeDataString(timeZoneId);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Cookie",
                $"{UserLocalTimeProvider.TimeZoneCookieName}={cookieValue}");
        }

        return client;
    }

    private static async Task AddUserAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@example.test".ToUpperInvariant(),
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@example.test".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddHistoricalDiaryEntriesAsync(
        IntegrationTestFactory factory,
        string userId,
        int count)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var food = TestData.Food(userId);
        food.UserId = null;
        context.Foods.Add(food);
        await context.SaveChangesAsync();

        context.DiaryEntries.AddRange(Enumerable.Range(0, count).Select(offset =>
        {
            var entry = TestData.DiaryEntry(userId, food, 100);
            entry.Date = new DateTime(2026, 7, 1).AddDays(offset);
            return entry;
        }));
        await context.SaveChangesAsync();
    }

    private static async Task AddProgressionStateAsync(
        IntegrationTestFactory factory,
        string userId,
        int version)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.UserProgressionStates.Add(new UserProgressionState
        {
            UserId = userId,
            AchievementBackfillVersion = version
        });
        await context.SaveChangesAsync();
    }

    private static async Task CreateFailureTriggerAsync(
        IntegrationTestFactory factory,
        string tableName,
        string triggerName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var sql = (tableName, triggerName) switch
        {
            ("UserDailyActivities", "FailProgressionActivity") =>
                """
                CREATE TRIGGER "FailProgressionActivity"
                BEFORE INSERT ON "UserDailyActivities"
                BEGIN
                    SELECT RAISE(
                        FAIL,
                        'forced progression integration failure');
                END;
                """,
            ("UserAchievements", "FailProgressionReconciliation") =>
                """
                CREATE TRIGGER "FailProgressionReconciliation"
                BEFORE INSERT ON "UserAchievements"
                BEGIN
                    SELECT RAISE(
                        FAIL,
                        'forced progression integration failure');
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<UserDailyActivity[]> ActivitiesAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .UserDailyActivities
            .AsNoTracking()
            .ToArrayAsync();
    }

    private static async Task<int> ActivityCountAsync(
        IntegrationTestFactory factory,
        string? userId = null)
    {
        using var scope = factory.Services.CreateScope();
        var query = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .UserDailyActivities
            .AsNoTracking();
        return userId == null
            ? await query.CountAsync()
            : await query.CountAsync(item => item.UserId == userId);
    }

    private static async Task<int> DailyXpCountAsync(
        IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .UserXpEvents
            .AsNoTracking()
            .CountAsync(item => item.EventKey.StartsWith("daily:"));
    }

    private static PageHandlerExecutingContext PageContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie =
            $"{UserLocalTimeProvider.TimeZoneCookieName}=UTC";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));
        var descriptor = new CompiledPageActionDescriptor
        {
            ViewEnginePath = "/Test"
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            descriptor,
            new ModelStateDictionary());

        return new PageHandlerExecutingContext(
            new PageContext(actionContext),
            [],
            null,
            new Dictionary<string, object?>(),
            new object());
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

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
