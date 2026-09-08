using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Calculations;

public class CalorieBalanceDashboardTests
{
    [Fact]
    public async Task DashboardModel_LoadsAuthenticatedUsersCurrentCalendarYear()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new CalorieBalanceYearService(database.Context));
        PageModelTestContext.Attach(model, "user-1");

        await model.OnGetAsync();

        Assert.NotNull(model.CalorieBalanceYear);
        Assert.Equal(DateTime.Today.Year, model.CalorieBalanceYear.Year);
        Assert.Equal(
            DateTime.IsLeapYear(DateTime.Today.Year) ? 366 : 365,
            model.CalorieBalanceYear.Days.Count);
        Assert.Equal(DateTime.Today.Year, model.SelectedYear);
        Assert.False(model.CanNavigateNext);
    }

    [Fact]
    public async Task AnonymousHome_RemainsLandingPageWithoutYearMap()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await client.GetStringAsync("/");

        Assert.Contains("home-landing", html);
        Assert.Contains("Track your food without the faff.", html);
        Assert.DoesNotContain("dashboard-year-card", html);
    }

    [Fact]
    public async Task Dashboard_RendersCalendarGeometryMonthsAndAuthoritativeLegend()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "calendar-user";
        await SeedCalendarAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);
        var year = DateTime.Today.Year;

        var html = await client.GetStringAsync("/");
        var decodedHtml = WebUtility.HtmlDecode(html);

        Assert.Contains($"data-calorie-balance-year=\"{year}\"", html);
        Assert.Contains($"throughout {year}", html);
        Assert.Contains("--calorie-year-grid-columns:", html);
        Assert.Contains("calorie-year-tooltip-layer", html);
        Assert.Contains($"data-date=\"{year}-01-01\"", html);
        Assert.Contains($"data-date=\"{year}-12-31\"", html);

        var firstDate = new DateOnly(year, 1, 1);
        var lastDate = new DateOnly(year, 12, 31);
        var firstOffset = MondayFirstOffset(firstDate);
        AssertDayGeometry(html, firstDate, 2, firstOffset + 2);
        AssertDayGeometry(
            html,
            lastDate,
            (firstOffset + lastDate.DayOfYear - 1) / 7 + 2,
            MondayFirstOffset(lastDate) + 2);
        AssertMonthLabelsAlignWithFirstDays(html, year, firstOffset);

        var weekdayLabels = Regex.Matches(
                decodedHtml,
                @"<span class=""calorie-year-weekday""[^>]*>\s*(?<label>\w+)\s*</span>",
                RegexOptions.Singleline)
            .Select(match => match.Groups["label"].Value)
            .ToArray();
        Assert.Equal(
            new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
            weekdayLabels);

        foreach (var month in new[]
                 {
                     "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                     "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
                 })
        {
            Assert.Matches(
                new Regex(
                    $"calorie-year-month[^>]*>\\s*{month}\\s*</span>",
                    RegexOptions.Singleline),
                html);
        }

        foreach (var definition in
                 CalorieBalanceClassifier.LegendDefinitions)
        {
            Assert.Contains(definition.DisplayName, decodedHtml);
            Assert.Contains(definition.PercentageRange, decodedHtml);
        }

        Assert.Contains("No Data", decodedHtml);
        Assert.Contains("No maintenance data", decodedHtml);
        Assert.Contains("data-state=\"heavycut\"", html);
        Assert.Contains("data-state=\"lightcut\"", html);
        Assert.Contains("data-state=\"maintenance\"", html);
        Assert.Contains("data-state=\"lightgain\"", html);
        Assert.Contains("data-state=\"heavygain\"", html);
    }

    [Fact]
    public async Task Dashboard_PopulatedDaysAreAccessibleClassifiedDiaryLinks()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "populated-user";
        var dates = await SeedCalendarAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        var populatedDate = dates.Populated.ToString("yyyy-MM-dd");
        var populatedDateText = dates.Populated.ToString("d MMMM yyyy");

        var populatedLink = Regex.Match(
            html,
            $"<a[^>]*data-date=\"{populatedDate}\"[^>]*>",
            RegexOptions.Singleline).Value;
        Assert.NotEmpty(populatedLink);
        Assert.Contains(
            $"href=\"/Diary?date={populatedDate}\"",
            populatedLink);
        Assert.Contains("data-state=\"heavycut\"", populatedLink);
        Assert.Contains($"aria-label=\"{populatedDateText}. Logged: 800 kcal.", html);
        Assert.Contains("Maintenance: 1000 kcal.", html);
        Assert.Contains("Balance: -200 kcal (-20%). Heavy Cut.", html);
    }

    [Fact]
    public async Task Dashboard_NoDataAndMissingMaintenanceRemainDistinctAndUserScoped()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "state-user";
        var dates = await SeedCalendarAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        var retainedDate = dates.Retained.ToString("yyyy-MM-dd");
        var missingDate = dates.MissingMaintenance.ToString("yyyy-MM-dd");
        var otherDate = dates.OtherUserOnly.ToString("yyyy-MM-dd");

        Assert.Matches(
            $"<span[^>]*data-date=\"{retainedDate}\"[^>]*data-state=\"no-data\"",
            html);
        Assert.DoesNotMatch(
            $"<a[^>]*data-date=\"{retainedDate}\"",
            html);

        Assert.Matches(
            $"<a[^>]*data-date=\"{missingDate}\"[^>]*data-state=\"unclassified\"",
            html);
        Assert.Contains(
            "No maintenance data is available.",
            html);

        Assert.Matches(
            $"<span[^>]*data-date=\"{otherDate}\"[^>]*data-state=\"no-data\"",
            html);
        Assert.DoesNotMatch(
            $"<a[^>]*data-date=\"{otherDate}\"",
            html);
    }

    [Fact]
    public async Task Dashboard_ValidHistoricalYearRendersLeapCalendarLinksAndNavigation()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "history-user";
        var historicalYear = PreviousLeapYear(DateTime.Today.Year);
        var historicalDate = new DateOnly(historicalYear, 2, 29);
        await SeedHistoricalDayAsync(factory, userId, historicalDate);
        using var client = AuthenticatedClient(factory, userId);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/?year={historicalYear}"));

        Assert.Contains(
            $"data-calorie-balance-year=\"{historicalYear}\"",
            html);
        Assert.Contains($"throughout {historicalYear}", html);
        Assert.Contains(
            $"data-date=\"{historicalYear}-02-29\"",
            html);
        Assert.Contains(
            $"href=\"/Diary?date={historicalYear}-02-29\"",
            html);
        Assert.Contains(
            $"href=\"/?year={historicalYear + 1}\"",
            html);
        Assert.Contains(
            $"href=\"/?year={historicalYear - 1}\"",
            html);
    }

    [Fact]
    public async Task Dashboard_CurrentYearDiaryCanNavigateToEmptyPreviousYear()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "current-navigation-user";
        await SeedCalendarAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);

        var currentYear = DateTime.Today.Year;
        var html = await client.GetStringAsync($"/?year={currentYear - 1}");

        Assert.Contains(
            $"data-calorie-balance-year=\"{currentYear - 1}\"",
            html);
        Assert.Equal(
            DateTime.IsLeapYear(currentYear - 1) ? 366 : 365,
            Regex.Matches(html, "data-state=\"no-data\"").Count);
        var navigation = Regex.Match(
            html,
            "<nav class=\"calorie-year-navigation\".*?</nav>",
            RegexOptions.Singleline).Value;
        var nextLink = Regex.Match(
            navigation,
            $"<a[^>]*aria-label=\"View {currentYear}\"[^>]*>",
            RegexOptions.Singleline).Value;
        Assert.NotEmpty(nextLink);
        Assert.Contains($"href=\"/?year={currentYear}\"", nextLink);
        Assert.DoesNotContain(
            $"href=\"/?year={currentYear + 1}\"",
            navigation);

        var nextResponse = await client.GetAsync($"/?year={currentYear}");
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
        var nextHtml = await nextResponse.Content.ReadAsStringAsync();
        Assert.Contains(
            $"data-calorie-balance-year=\"{currentYear}\"",
            nextHtml);
    }

    [Fact]
    public async Task Dashboard_UserWithoutDiaryCanNavigateAcrossEmptyHistoricalYears()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "empty-navigation-user";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var currentYear = DateTime.Today.Year;
        var firstEmptyYear = currentYear - 1;
        var secondEmptyYear = currentYear - 2;

        foreach (var year in new[] { firstEmptyYear, secondEmptyYear })
        {
            var html = await client.GetStringAsync($"/?year={year}");

            Assert.Contains(
                $"data-calorie-balance-year=\"{year}\"",
                html);
            Assert.Equal(
                DateTime.IsLeapYear(year) ? 366 : 365,
                Regex.Matches(html, "data-state=\"no-data\"").Count);
            Assert.Contains(
                $"href=\"/?year={year + 1}\"",
                html);
            if (year > ValidationRules.MinimumDiaryDate.Year)
            {
                Assert.Contains(
                    $"href=\"/?year={year - 1}\"",
                    html);
            }
            Assert.Contains("Mon", WebUtility.HtmlDecode(html));
            Assert.Contains("Sun", WebUtility.HtmlDecode(html));
        }

        var currentHtml = await client.GetStringAsync("/");
        Assert.Contains(
            $"href=\"/?year={firstEmptyYear}\"",
            currentHtml);
        Assert.DoesNotContain(
            $"href=\"/?year={currentYear + 1}\"",
            currentHtml);
    }

    [Fact]
    public async Task Dashboard_PreviousNavigationStopsAtMinimumSupportedYear()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new CalorieBalanceYearService(database.Context))
        {
            Year = ValidationRules.MinimumDiaryDate.Year
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(ValidationRules.MinimumDiaryDate.Year, model.SelectedYear);
        Assert.False(model.CanNavigatePrevious);
        Assert.True(model.CanNavigateNext);
    }

    [Fact]
    public async Task Dashboard_MinimumYearRendersNextButNoPreviousLink()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "minimum-rendered-navigation-user";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var minimumYear = ValidationRules.MinimumDiaryDate.Year;
        var html = await client.GetStringAsync($"/?year={minimumYear}");
        var navigation = Regex.Match(
            html,
            "<nav class=\"calorie-year-navigation\".*?</nav>",
            RegexOptions.Singleline).Value;

        Assert.DoesNotContain(
            $"href=\"/?year={minimumYear - 1}\"",
            navigation);
        Assert.Contains(
            $"href=\"/?year={minimumYear + 1}\"",
            navigation);
    }

    [Fact]
    public async Task Dashboard_YearBelowMinimumIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new CalorieBalanceYearService(database.Context))
        {
            Year = ValidationRules.MinimumDiaryDate.Year - 1
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnGetAsync();

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Dashboard_EmptyHistoricalYearNavigationPerformsNoWrites()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "empty-read-only-history-user";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        int entriesBefore;
        int snapshotsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            entriesBefore = await context.DiaryEntries.CountAsync();
            snapshotsBefore = await context.DailyMaintenanceSnapshots.CountAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var response = await client.GetAsync(
            $"/?year={DateTime.Today.Year - 1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Equal(entriesBefore, await verificationContext.DiaryEntries.CountAsync());
        Assert.Equal(
            snapshotsBefore,
            await verificationContext.DailyMaintenanceSnapshots.CountAsync());
    }

    [Theory]
    [InlineData("not-a-year")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    public async Task Dashboard_MalformedOrExtremeYearIsRejectedSafely(string year)
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "safe-year-input-user";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var response = await client.GetAsync($"/?year={year}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_CurrentYearHasNoFutureNavigation()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "current-year-navigation-user";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var html = await client.GetStringAsync("/");

        Assert.Contains(
            $"<strong aria-current=\"page\">{DateTime.Today.Year}</strong>",
            html);
        Assert.DoesNotContain(
            $"href=\"/?year={DateTime.Today.Year + 1}\"",
            html);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2101)]
    public async Task Dashboard_UnsupportedYearIsRejected(int year)
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "range-user";
        using var client = AuthenticatedClient(factory, userId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/?year={year}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_NextCalendarYearIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new CalorieBalanceYearService(database.Context))
        {
            Year = DateTime.Today.Year + 1
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnGetAsync();

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(model.CalorieBalanceYear);
    }

    [Fact]
    public async Task Dashboard_OtherUsersHistoryDoesNotAffectEmptyYearNavigation()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "no-history-user";
        var historicalYear = DateTime.Today.Year - 2;
        await SeedHistoricalDayAsync(
            factory,
            "other-history-user",
            new DateOnly(historicalYear, 1, 2));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var response = await client.GetAsync($"/?year={historicalYear}");
        var currentHtml = await client.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var historicalHtml = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"data-calorie-balance-year=\"{historicalYear}\"",
            historicalHtml);
        Assert.Equal(
            DateTime.IsLeapYear(historicalYear) ? 366 : 365,
            Regex.Matches(historicalHtml, "data-state=\"no-data\"").Count);
        Assert.DoesNotContain(
            $"href=\"/?year={DateTime.Today.Year + 1}\"",
            currentHtml);
    }

    [Fact]
    public async Task Dashboard_ViewingPopulatedHistoricalYearPerformsNoWrites()
    {
        using var factory = new IntegrationTestFactory();
        const string userId = "read-only-history-user";
        var historicalYear = DateTime.Today.Year - 1;
        await SeedHistoricalDayAsync(
            factory,
            userId,
            new DateOnly(historicalYear, 8, 4));
        using var client = AuthenticatedClient(factory, userId);

        int entriesBefore;
        int snapshotsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            entriesBefore = await context.DiaryEntries.CountAsync();
            snapshotsBefore = await context.DailyMaintenanceSnapshots.CountAsync();
        }

        var response = await client.GetAsync($"/?year={historicalYear}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.Equal(entriesBefore, await verificationContext.DiaryEntries.CountAsync());
        Assert.Equal(
            snapshotsBefore,
            await verificationContext.DailyMaintenanceSnapshots.CountAsync());
    }

    private static async Task<CalendarDates> SeedCalendarAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        var year = DateTime.Today.Year;
        var dates = new CalendarDates(
            new DateOnly(year, 1, 15),
            new DateOnly(year, 2, 16),
            new DateOnly(year, 3, 17),
            new DateOnly(year, 4, 18));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        AddUser(context, userId);
        AddUser(context, "other-user");
        var food = TestData.Food(userId, name: "Own food");
        var otherFood = TestData.Food("other-user", name: "Other food");
        context.Foods.AddRange(food, otherFood);
        await context.SaveChangesAsync();

        var classifiedEntries = new[]
        {
            (Date: dates.Populated, Quantity: 400m),
            (Date: dates.Populated.AddDays(1), Quantity: 450m),
            (Date: dates.Populated.AddDays(2), Quantity: 500m),
            (Date: dates.Populated.AddDays(3), Quantity: 550m),
            (Date: dates.Populated.AddDays(4), Quantity: 600m)
        };

        foreach (var classifiedEntry in classifiedEntries)
        {
            var entry = TestData.DiaryEntry(
                userId,
                food,
                classifiedEntry.Quantity);
            entry.Date = classifiedEntry.Date
                .ToDateTime(TimeOnly.MinValue);
            context.DiaryEntries.Add(entry);
            context.DailyMaintenanceSnapshots.Add(
                new DailyMaintenanceSnapshot(
                    userId,
                    classifiedEntry.Date,
                    1000));
        }

        var missingEntry = TestData.DiaryEntry(userId, food, 100);
        missingEntry.Date = dates.MissingMaintenance
            .ToDateTime(TimeOnly.MinValue);
        var otherEntry = TestData.DiaryEntry("other-user", otherFood, 100);
        otherEntry.Date = dates.OtherUserOnly
            .ToDateTime(TimeOnly.MinValue);
        context.DiaryEntries.AddRange(missingEntry, otherEntry);
        context.DailyMaintenanceSnapshots.AddRange(
            new DailyMaintenanceSnapshot(userId, dates.Retained, 1000),
            new DailyMaintenanceSnapshot(
                "other-user",
                dates.OtherUserOnly,
                1000));
        await context.SaveChangesAsync();
        return dates;
    }

    private static async Task SeedHistoricalDayAsync(
        IntegrationTestFactory factory,
        string userId,
        DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        AddUser(context, userId);
        var food = TestData.Food(userId, name: "Historical food");
        context.Foods.Add(food);
        await context.SaveChangesAsync();

        var entry = TestData.DiaryEntry(userId, food, 100);
        entry.Date = date.ToDateTime(TimeOnly.MinValue);
        context.DiaryEntries.Add(entry);
        context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(userId, date, 2000));
        await context.SaveChangesAsync();
    }

    private static int PreviousLeapYear(int currentYear)
    {
        var year = currentYear - 1;
        while (!DateTime.IsLeapYear(year))
        {
            year--;
        }

        return year;
    }

    private static void AddUser(
        ApplicationDbContext context,
        string userId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    private static void AssertDayGeometry(
        string html,
        DateOnly date,
        int week,
        int weekday)
    {
        var element = Regex.Match(
            html,
            $"<(?:a|span)[^>]*data-date=\"{date:yyyy-MM-dd}\"[^>]*>",
            RegexOptions.Singleline).Value;

        Assert.NotEmpty(element);
        Assert.Contains(
            $"--calorie-year-week: {week}; " +
            $"--calorie-year-weekday: {weekday};",
            element);
    }

    private static int MondayFirstOffset(DateOnly date) =>
        ((int)date.DayOfWeek + 6) % 7;

    private static void AssertMonthLabelsAlignWithFirstDays(
        string html,
        int year,
        int firstDayOffset)
    {
        var boundaryWeeks = Enumerable.Range(2, 11)
            .Select(month =>
            {
                var monthDate = new DateOnly(year, month, 1);
                return (firstDayOffset + monthDate.DayOfYear - 1) / 7 + 1;
            })
            .Where(week => week > 1)
            .Distinct()
            .ToHashSet();

        int VisualWeekColumn(int week) =>
            week + 1 + boundaryWeeks.Count(boundaryWeek => boundaryWeek <= week);

        for (var month = 1; month <= 12; month++)
        {
            var date = new DateOnly(year, month, 1);
            var week = (firstDayOffset + date.DayOfYear - 1) / 7 + 1;
            var column = VisualWeekColumn(week);
            var monthLabel = Regex.Match(
                html,
                $"<span[^>]*class=\"calorie-year-month\"[^>]*data-month=\"{month}\"[^>]*>",
                RegexOptions.Singleline).Value;
            var dateCell = Regex.Match(
                html,
                $"<(?:a|span)[^>]*data-date=\"{date:yyyy-MM-dd}\"[^>]*>",
                RegexOptions.Singleline).Value;

            Assert.NotEmpty(monthLabel);
            Assert.NotEmpty(dateCell);
            Assert.Contains($"--calorie-year-month-column: {column};", monthLabel);
            Assert.Contains($"--calorie-year-column: {column};", dateCell);
        }
    }

    private static HttpClient AuthenticatedClient(
        IntegrationTestFactory factory,
        string userId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        return client;
    }

    private sealed record CalendarDates(
        DateOnly Populated,
        DateOnly MissingMaintenance,
        DateOnly Retained,
        DateOnly OtherUserOnly);
}
