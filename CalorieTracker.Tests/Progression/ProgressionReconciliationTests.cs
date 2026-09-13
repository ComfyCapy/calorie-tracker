using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionReconciliationTests
{
    private static readonly DateTimeOffset RecognitionTime =
        new(2026, 9, 13, 14, 27, 31, TimeSpan.Zero);

    [Fact]
    public async Task MissingState_ReconcilesFromZeroAndAdvancesWithoutAwards()
    {
        await using var database = await UserDatabaseAsync();

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.Equal(0, result.PreviousVersion);
        Assert.Equal(
            ProgressionService.CurrentAchievementBackfillVersion,
            result.CurrentVersion);
        Assert.True(result.WasReconciled);
        Assert.Empty(result.NewlyUnlockedAchievements);
        Assert.Equal(1, (await database.Context.UserProgressionStates
            .AsNoTracking()
            .SingleAsync()).AchievementBackfillVersion);
    }

    [Fact]
    public async Task RepeatedReconciliation_IsNoOpWithoutDuplicateWrites()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, null);
        database.Context.DiaryEntries.Add(DiaryEntry(
            "user-1", food.Id, new DateTime(2026, 9, 1), "Breakfast"));
        await database.Context.SaveChangesAsync();

        var first = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");
        var second = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.True(first.WasReconciled);
        Assert.False(second.WasReconciled);
        Assert.Equal(1, second.PreviousVersion);
        Assert.Empty(second.NewlyUnlockedAchievements);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Fact]
    public async Task AlreadyCurrentState_DoesNotEvaluateOrWrite()
    {
        await using var database = await UserDatabaseAsync();
        database.Context.UserProgressionStates.Add(new UserProgressionState
        {
            UserId = "user-1",
            AchievementBackfillVersion = 1
        });
        var food = await AddFoodAsync(database.Context, null);
        database.Context.DiaryEntries.Add(DiaryEntry(
            "user-1", food.Id, new DateTime(2026, 9, 1), "Breakfast"));
        await database.Context.SaveChangesAsync();

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.False(result.WasReconciled);
        Assert.Empty(await database.Context.UserAchievements.ToArrayAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToArrayAsync());
    }

    [Theory]
    [InlineData("diary-first", AchievementDefinitions.DiaryFirstEntryKey)]
    [InlineData(
        "diary-thirty",
        AchievementDefinitions.DiaryDistinctDaysThirtyKey)]
    [InlineData(
        "diary-meals",
        AchievementDefinitions.DiaryAllMealTypesKey)]
    [InlineData("profile", AchievementDefinitions.ProfileCompletedKey)]
    [InlineData("food", AchievementDefinitions.FoodsFirstCustomKey)]
    [InlineData("saved-meal", AchievementDefinitions.SavedMealsFirstKey)]
    [InlineData(
        "customisation",
        AchievementDefinitions.CustomisationFirstEquipKey)]
    public async Task HistoricalDurableEvidence_UnlocksExpectedAchievement(
        string scenario,
        string expectedKey)
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, scenario, "user-1");

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == expectedKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            expectedKey);
    }

    [Fact]
    public async Task MultipleEvidence_UnlocksTogetherAndPreservesExistingAward()
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-first", "user-1");
        await SeedScenarioAsync(database.Context, "profile", "user-1");
        await SeedScenarioAsync(database.Context, "saved-meal", "user-1");
        await SeedExistingAwardAsync(
            database.Context,
            "user-1",
            AchievementDefinitions.DiaryFirstEntryKey);

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.DoesNotContain(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.DiaryFirstEntryKey);
        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ProfileCompletedKey);
        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.SavedMealsFirstKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.DiaryFirstEntryKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.ProfileCompletedKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.SavedMealsFirstKey);
    }

    [Fact]
    public async Task DiaryHistory_DoesNotFabricateActivityOrActivityAwards()
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-thirty", "user-1");

        await Service(database.Context).ReconcileAchievementsAsync("user-1");

        Assert.Empty(await database.Context.UserDailyActivities.ToArrayAsync());
        Assert.DoesNotContain(
            await database.Context.UserXpEvents.ToArrayAsync(),
            item => item.EventKey.StartsWith("daily:", StringComparison.Ordinal));
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ActivityDistinctThreeKey));
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ActivityDistinctSevenKey));
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ActivityStreakSevenKey));
    }

    [Fact]
    public async Task ExistingRealActivity_UnlocksAwardsWithoutAddingRowsOrDailyXp()
    {
        await using var database = await UserDatabaseAsync();
        var dates = Enumerable.Range(0, 7)
            .Select(offset => new DateOnly(2026, 8, 1).AddDays(offset))
            .ToArray();
        database.Context.UserDailyActivities.AddRange(
            dates.Select(date => Activity("user-1", date)));
        await database.Context.SaveChangesAsync();

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityDistinctThreeKey);
        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityDistinctSevenKey);
        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityStreakSevenKey);
        Assert.Equal(dates, await database.Context.UserDailyActivities
            .OrderBy(item => item.LocalDate)
            .Select(item => item.LocalDate)
            .ToArrayAsync());
        Assert.DoesNotContain(
            await database.Context.UserXpEvents.ToArrayAsync(),
            item => item.EventKey.StartsWith("daily:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BackfilledAchievementAndXp_UseRecognitionTime()
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-first", "user-1");

        await Service(database.Context).ReconcileAchievementsAsync("user-1");

        var achievement = await database.Context.UserAchievements.SingleAsync();
        var xpEvent = await database.Context.UserXpEvents.SingleAsync();
        Assert.Equal(RecognitionTime.UtcDateTime, achievement.UnlockedAtUtc);
        Assert.Equal(RecognitionTime.UtcDateTime, xpEvent.AwardedAtUtc);
    }

    [Fact]
    public async Task Reconciliation_UsesOnlySuppliedUsersEvidence()
    {
        await using var database = await TwoUserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-thirty", "user-2");
        await SeedScenarioAsync(database.Context, "profile", "user-2");
        await SeedScenarioAsync(database.Context, "food", "user-2");
        await SeedScenarioAsync(database.Context, "saved-meal", "user-2");
        database.Context.UserDailyActivities.AddRange(
            Enumerable.Range(0, 7).Select(offset => Activity(
                "user-2",
                new DateOnly(2026, 8, 1).AddDays(offset))));
        await database.Context.SaveChangesAsync();

        var result = await Service(database.Context)
            .ReconcileAchievementsAsync("user-1");

        Assert.Empty(result.NewlyUnlockedAchievements);
        Assert.Empty(await database.Context.UserAchievements
            .Where(item => item.UserId == "user-1")
            .ToArrayAsync());
        Assert.Empty(await database.Context.UserXpEvents
            .Where(item => item.UserId == "user-1")
            .ToArrayAsync());
    }

    [Fact]
    public async Task XpFailure_RollsBackAchievementAndVersionAdvance()
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-first", "user-1");
        await SeedStateAsync(database.Context, "user-1", 0);
        await CreateFailureTriggerAsync(database.Context, "UserXpEvents");

        await Assert.ThrowsAsync<SqliteException>(() =>
            Service(database.Context).ReconcileAchievementsAsync("user-1"));

        Assert.Empty(await database.Context.UserAchievements.ToArrayAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToArrayAsync());
        Assert.Equal(0, await StateVersionAsync(database.Context, "user-1"));
    }

    [Fact]
    public async Task AchievementFailure_DoesNotMarkReconciliationComplete()
    {
        await using var database = await UserDatabaseAsync();
        await SeedScenarioAsync(database.Context, "diary-first", "user-1");
        await SeedStateAsync(database.Context, "user-1", 0);
        await CreateFailureTriggerAsync(database.Context, "UserAchievements");

        await Assert.ThrowsAsync<SqliteException>(() =>
            Service(database.Context).ReconcileAchievementsAsync("user-1"));

        Assert.Empty(await database.Context.UserAchievements.ToArrayAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToArrayAsync());
        Assert.Equal(0, await StateVersionAsync(database.Context, "user-1"));
    }

    [Fact]
    public async Task ConcurrentAttempts_CompleteWithOneStateAndOneAward()
    {
        await using var database = await FileReconciliationDatabase.CreateAsync();
        using var gate = new Barrier(2);

        var first = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context).ReconcileAchievementsAsync("user-1");
        });
        var second = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context).ReconcileAchievementsAsync("user-1");
        });

        var results = await Task.WhenAll(first, second);
        await using var verification = database.CreateContext();
        Assert.Single(results, result => result.WasReconciled);
        Assert.Single(results, result => !result.WasReconciled);
        Assert.Equal(1, await StateVersionAsync(verification, "user-1"));
        await AssertSingleAchievementAndXpAsync(
            verification,
            AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reconciliation_RejectsMissingUserId(string userId)
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(database.Context).ReconcileAchievementsAsync(userId));
    }

    private static ProgressionService Service(ApplicationDbContext context) =>
        new(
            context,
            new ProgressionLevelCalculator(),
            new ActivityStreakCalculator(),
            new FixedTimeProvider(RecognitionTime));

    private static async Task<TestDatabase> UserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        return database;
    }

    private static async Task<TestDatabase> TwoUserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "user-1");
        await database.AddUserAsync("user-2", "user-2");
        return database;
    }

    private static async Task SeedScenarioAsync(
        ApplicationDbContext context,
        string scenario,
        string userId)
    {
        switch (scenario)
        {
            case "diary-first":
            {
                var food = await AddFoodAsync(context, null);
                context.DiaryEntries.Add(DiaryEntry(
                    userId,
                    food.Id,
                    new DateTime(2026, 8, 1),
                    "Breakfast"));
                break;
            }
            case "diary-thirty":
            {
                var food = await AddFoodAsync(context, null);
                context.DiaryEntries.AddRange(Enumerable.Range(0, 30)
                    .Select(offset => DiaryEntry(
                        userId,
                        food.Id,
                        new DateTime(2026, 7, 1).AddDays(offset),
                        "Breakfast")));
                break;
            }
            case "diary-meals":
            {
                var food = await AddFoodAsync(context, null);
                context.DiaryEntries.AddRange(ValidationRules.MealTypes
                    .Select(mealType => DiaryEntry(
                        userId,
                        food.Id,
                        new DateTime(2026, 8, 1),
                        mealType)));
                break;
            }
            case "profile":
                context.UserProfiles.Add(CompleteProfile(userId));
                break;
            case "food":
                await AddFoodAsync(context, userId);
                break;
            case "saved-meal":
                context.SavedMeals.Add(new SavedMeal
                {
                    UserId = userId,
                    Name = "Historical meal"
                });
                break;
            case "customisation":
                context.UserCapyAppearances.Add(new UserCapyAppearance
                {
                    UserId = userId,
                    ExpressionId = 1,
                    BackgroundId = 13,
                    HatHairId = 2
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Food> AddFoodAsync(
        ApplicationDbContext context,
        string? userId)
    {
        var food = new Food
        {
            UserId = userId,
            Name = $"Food {Guid.NewGuid():N}",
            Calories = 100,
            Protein = 1,
            Carbohydrates = 2,
            Fat = 3
        };
        context.Foods.Add(food);
        await context.SaveChangesAsync();
        return food;
    }

    private static DiaryEntry DiaryEntry(
        string userId,
        int foodId,
        DateTime date,
        string mealType) => new()
        {
            UserId = userId,
            FoodId = foodId,
            Date = date,
            MealType = mealType,
            Quantity = 100,
            FoodNameSnapshot = "Historical food",
            ServingSizeSnapshot = 100,
            CanonicalServingSizeSnapshot = 100,
            CaloriesSnapshot = 100
        };

    private static UserProfile CompleteProfile(string userId) => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = new DateTime(1990, 1, 1),
        HeightCm = 180,
        WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.ModeratelyActive,
        Goal = ProfileOptions.Maintain
    };

    private static UserDailyActivity Activity(string userId, DateOnly date) =>
        new()
        {
            UserId = userId,
            LocalDate = date,
            RecordedAtUtc = RecognitionTime.UtcDateTime.AddMonths(-1),
            TimeZoneId = "UTC"
        };

    private static async Task SeedExistingAwardAsync(
        ApplicationDbContext context,
        string userId,
        string key)
    {
        var definition = AchievementDefinitions.All.Single(item =>
            item.Key == key);
        context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            AchievementKey = key,
            UnlockedAtUtc = RecognitionTime.UtcDateTime.AddDays(-1)
        });
        context.UserXpEvents.Add(new UserXpEvent
        {
            UserId = userId,
            EventKey = $"achievement:{key}",
            Amount = definition.XpReward,
            AwardedAtUtc = RecognitionTime.UtcDateTime.AddDays(-1)
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedStateAsync(
        ApplicationDbContext context,
        string userId,
        int version)
    {
        context.UserProgressionStates.Add(new UserProgressionState
        {
            UserId = userId,
            AchievementBackfillVersion = version
        });
        await context.SaveChangesAsync();
    }

    private static async Task CreateFailureTriggerAsync(
        ApplicationDbContext context,
        string tableName)
    {
        var sql = $"""
            CREATE TRIGGER "FailReconciliationInsert"
            BEFORE INSERT ON "{tableName}"
            BEGIN
                SELECT RAISE(FAIL, 'forced reconciliation failure');
            END;
            """;
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<int> StateVersionAsync(
        ApplicationDbContext context,
        string userId) => await context.UserProgressionStates
        .AsNoTracking()
        .Where(item => item.UserId == userId)
        .Select(item => item.AchievementBackfillVersion)
        .SingleAsync();

    private static async Task<bool> HasAchievementAsync(
        ApplicationDbContext context,
        string key) => await context.UserAchievements.AnyAsync(item =>
        item.UserId == "user-1" && item.AchievementKey == key);

    private static async Task AssertSingleAchievementAndXpAsync(
        ApplicationDbContext context,
        string key)
    {
        Assert.Equal(1, await context.UserAchievements.CountAsync(item =>
            item.UserId == "user-1" && item.AchievementKey == key));
        Assert.Equal(1, await context.UserXpEvents.CountAsync(item =>
            item.UserId == "user-1" &&
            item.EventKey == $"achievement:{key}"));
    }

    private sealed class FileReconciliationDatabase : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private FileReconciliationDatabase(
            string path,
            DbContextOptions<ApplicationDbContext> options)
        {
            _path = path;
            _options = options;
        }

        public static async Task<FileReconciliationDatabase> CreateAsync()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"comfy-capy-reconciliation-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(
                    $"Data Source={path};Default Timeout=30;Pooling=False")
                .Options;
            var database = new FileReconciliationDatabase(path, options);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(new ApplicationUser
            {
                Id = "user-1",
                UserName = "user-1",
                NormalizedUserName = "USER-1",
                Email = "user-1@example.test",
                NormalizedEmail = "USER-1@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var food = await AddFoodAsync(context, null);
            context.DiaryEntries.Add(DiaryEntry(
                "user-1",
                food.Id,
                new DateTime(2026, 8, 1),
                "Breakfast"));
            await context.SaveChangesAsync();
            return database;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            await Task.Yield();

            foreach (var path in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
