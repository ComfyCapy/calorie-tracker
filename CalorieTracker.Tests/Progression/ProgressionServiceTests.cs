using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionServiceTests
{
    private static readonly DateTimeOffset AuditTime =
        new(2026, 9, 13, 5, 17, 53, TimeSpan.Zero);

    [Fact]
    public async Task RecordDailyActivity_FirstCallCreatesActivityAndDailyXp()
    {
        await using var database = await UserDatabaseAsync();
        var date = new DateOnly(2026, 9, 13);

        var result = await Service(database.Context)
            .RecordDailyActivityAsync("user-1", date, "Europe/London");

        Assert.True(result.WasRecorded);
        Assert.Equal("Europe/London", (await database.Context.UserDailyActivities
            .SingleAsync()).TimeZoneId);
        var xpEvent = await database.Context.UserXpEvents.SingleAsync();
        Assert.Equal("daily:2026-09-13", xpEvent.EventKey);
        Assert.Equal(ProgressionService.DailyActivityXp, xpEvent.Amount);
    }

    [Fact]
    public async Task RecordDailyActivity_UsesUtcTimeProviderForAuditValues()
    {
        await using var database = await UserDatabaseAsync();

        await Service(database.Context).RecordDailyActivityAsync(
            "user-1",
            new DateOnly(2026, 9, 13),
            "Pacific/Auckland");

        var activity = await database.Context.UserDailyActivities.SingleAsync();
        var xpEvent = await database.Context.UserXpEvents.SingleAsync();
        Assert.Equal(AuditTime.UtcDateTime, activity.RecordedAtUtc);
        Assert.Equal(AuditTime.UtcDateTime, xpEvent.AwardedAtUtc);
    }

    [Fact]
    public async Task RecordDailyActivity_RepeatedDayIsHarmlessAndNextDayRecords()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);
        var firstDate = new DateOnly(2026, 9, 13);

        var first = await service.RecordDailyActivityAsync(
            "user-1", firstDate, "UTC");
        var repeated = await service.RecordDailyActivityAsync(
            "user-1", firstDate, "UTC");
        var next = await service.RecordDailyActivityAsync(
            "user-1", firstDate.AddDays(1), "UTC");

        Assert.True(first.WasRecorded);
        Assert.False(repeated.WasRecorded);
        Assert.Empty(repeated.NewlyUnlockedAchievements);
        Assert.True(next.WasRecorded);
        Assert.Equal(2, await database.Context.UserDailyActivities.CountAsync());
        Assert.Equal(2, await database.Context.UserXpEvents.CountAsync(
            item => item.EventKey.StartsWith("daily:")));
    }

    [Fact]
    public async Task RecordDailyActivity_DifferentUsersMayRecordSameDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "user-1");
        await database.AddUserAsync("user-2", "user-2");
        var service = Service(database.Context);
        var date = new DateOnly(2026, 9, 13);

        await service.RecordDailyActivityAsync("user-1", date, "UTC");
        await service.RecordDailyActivityAsync("user-2", date, "UTC");

        Assert.Equal(2, await database.Context.UserDailyActivities.CountAsync());
        Assert.Equal(2, await database.Context.UserXpEvents.CountAsync());
    }

    [Theory]
    [InlineData(null, "UTC")]
    [InlineData("", "UTC")]
    [InlineData(" ", "UTC")]
    [InlineData("user-1", null)]
    [InlineData("user-1", "")]
    [InlineData("user-1", " ")]
    public async Task RecordDailyActivity_RejectsMissingRequiredArguments(
        string? userId,
        string? timeZoneId)
    {
        await using var database = await UserDatabaseAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(database.Context).RecordDailyActivityAsync(
                userId!,
                new DateOnly(2026, 9, 13),
                timeZoneId!));
    }

    [Fact]
    public async Task RecordDailyActivity_RejectsOversizedTimeZoneId()
    {
        await using var database = await UserDatabaseAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(database.Context).RecordDailyActivityAsync(
                "user-1",
                new DateOnly(2026, 9, 13),
                new string('x', UserDailyActivity.MaxTimeZoneIdLength + 1)));
    }

    [Fact]
    public async Task RecordDailyActivity_XpFailureRollsBackActivity()
    {
        await using var database = await UserDatabaseAsync();
        await CreateFailingInsertTriggerAsync(
            database.Context,
            "UserXpEvents",
            "FailDailyXp");

        await Assert.ThrowsAsync<SqliteException>(() =>
            Service(database.Context).RecordDailyActivityAsync(
                "user-1",
                new DateOnly(2026, 9, 13),
                "UTC"));

        Assert.Empty(await database.Context.UserDailyActivities.ToListAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToListAsync());
    }

    [Fact]
    public async Task RecordDailyActivity_AchievementXpFailureRollsBackWholeDay()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);
        var start = new DateOnly(2026, 9, 1);
        await service.RecordDailyActivityAsync("user-1", start, "UTC");
        await service.RecordDailyActivityAsync(
            "user-1", start.AddDays(2), "UTC");
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER "FailAchievementXpDuringActivity"
            BEFORE INSERT ON "UserXpEvents"
            WHEN NEW."EventKey" LIKE 'achievement:%'
            BEGIN
                SELECT RAISE(FAIL, 'forced progression test failure');
            END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() =>
            service.RecordDailyActivityAsync(
                "user-1", start.AddDays(4), "UTC"));

        Assert.Equal(2, await database.Context.UserDailyActivities.CountAsync());
        Assert.Equal(2, await database.Context.UserXpEvents.CountAsync());
        Assert.Empty(await database.Context.UserAchievements.ToListAsync());
    }

    [Fact]
    public async Task ThirdDistinctActivityDayUnlocksHelloAgainExactlyOnce()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);
        var start = new DateOnly(2026, 9, 1);

        await service.RecordDailyActivityAsync("user-1", start, "UTC");
        await service.RecordDailyActivityAsync("user-1", start.AddDays(2), "UTC");
        var result = await service.RecordDailyActivityAsync(
            "user-1", start.AddDays(4), "UTC");
        await service.EvaluateActivityAchievementsAsync(
            "user-1", start.AddDays(4));

        Assert.Contains(
            result.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityDistinctThreeKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.ActivityDistinctThreeKey);
    }

    [Fact]
    public async Task SevenConsecutiveActivityDaysUnlockDistinctSevenAndComfyWeek()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);
        var start = new DateOnly(2026, 9, 1);
        DailyActivityRecordResult? seventh = null;

        for (var offset = 0; offset < 7; offset++)
        {
            seventh = await service.RecordDailyActivityAsync(
                "user-1", start.AddDays(offset), "UTC");
        }

        Assert.NotNull(seventh);
        Assert.Contains(
            seventh.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityDistinctSevenKey);
        Assert.Contains(
            seventh.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityStreakSevenKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.ActivityDistinctSevenKey);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.ActivityStreakSevenKey);
    }

    [Fact]
    public async Task SevenNonConsecutiveActivityDaysDoNotUnlockComfyWeek()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);
        var start = new DateOnly(2026, 9, 1);

        for (var offset = 0; offset < 14; offset += 2)
        {
            await service.RecordDailyActivityAsync(
                "user-1", start.AddDays(offset), "UTC");
        }

        Assert.True(await database.Context.UserAchievements.AnyAsync(item =>
            item.AchievementKey ==
                AchievementDefinitions.ActivityDistinctSevenKey));
        Assert.False(await database.Context.UserAchievements.AnyAsync(item =>
            item.AchievementKey ==
                AchievementDefinitions.ActivityStreakSevenKey));
    }

    [Fact]
    public async Task HistoricalSevenDayStreakStillQualifies()
    {
        await using var database = await UserDatabaseAsync();
        var start = new DateOnly(2026, 8, 1);
        database.Context.UserDailyActivities.AddRange(
            Enumerable.Range(0, 7).Select(offset =>
                Activity("user-1", start.AddDays(offset))));
        await database.Context.SaveChangesAsync();

        var first = await Service(database.Context)
            .EvaluateActivityAchievementsAsync(
                "user-1",
                new DateOnly(2026, 9, 13));
        var repeated = await Service(database.Context)
            .EvaluateActivityAchievementsAsync(
                "user-1",
                new DateOnly(2026, 9, 13));

        Assert.Contains(
            first.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.ActivityStreakSevenKey);
        Assert.Empty(repeated.NewlyUnlockedAchievements);
        await AssertSingleAchievementAndXpAsync(
            database.Context,
            AchievementDefinitions.ActivityStreakSevenKey);
    }

    [Fact]
    public async Task GrantAchievement_DuplicateIsHarmlessAndRewardComesFromDefinition()
    {
        await using var database = await UserDatabaseAsync();
        var service = Service(database.Context);

        var first = await service.GrantAchievementAsync(
            "user-1",
            AchievementDefinitions.DiaryFirstEntryKey);
        var repeated = await service.GrantAchievementAsync(
            "user-1",
            AchievementDefinitions.DiaryFirstEntryKey);

        Assert.True(first.WasGranted);
        Assert.False(repeated.WasGranted);
        var definition = AchievementDefinitions.All.Single(item =>
            item.Key == AchievementDefinitions.DiaryFirstEntryKey);
        var achievement = await database.Context.UserAchievements.SingleAsync();
        var xpEvent = await database.Context.UserXpEvents.SingleAsync();
        Assert.Equal(definition.XpReward, xpEvent.Amount);
        Assert.Equal(
            $"achievement:{definition.Key}",
            xpEvent.EventKey);
        Assert.Equal(AuditTime.UtcDateTime, achievement.UnlockedAtUtc);
        Assert.Equal(AuditTime.UtcDateTime, xpEvent.AwardedAtUtc);
        Assert.Equal(1, await database.Context.UserAchievements.CountAsync());
    }

    [Fact]
    public async Task GrantAchievement_RejectsUnknownKeyWithoutWriting()
    {
        await using var database = await UserDatabaseAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(database.Context).GrantAchievementAsync(
                "user-1",
                "unknown.achievement"));

        Assert.Empty(await database.Context.UserAchievements.ToListAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToListAsync());
    }

    [Fact]
    public async Task GrantAchievement_XpFailureRollsBackAchievement()
    {
        await using var database = await UserDatabaseAsync();
        await CreateFailingInsertTriggerAsync(
            database.Context,
            "UserXpEvents",
            "FailAchievementXp");

        await Assert.ThrowsAsync<SqliteException>(() =>
            Service(database.Context).GrantAchievementAsync(
                "user-1",
                AchievementDefinitions.DiaryFirstEntryKey));

        Assert.Empty(await database.Context.UserAchievements.ToListAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToListAsync());
    }

    [Fact]
    public async Task GrantAchievement_AchievementFailureCreatesNoXp()
    {
        await using var database = await UserDatabaseAsync();
        await CreateFailingInsertTriggerAsync(
            database.Context,
            "UserAchievements",
            "FailAchievement");

        await Assert.ThrowsAsync<SqliteException>(() =>
            Service(database.Context).GrantAchievementAsync(
                "user-1",
                AchievementDefinitions.DiaryFirstEntryKey));

        Assert.Empty(await database.Context.UserAchievements.ToListAsync());
        Assert.Empty(await database.Context.UserXpEvents.ToListAsync());
    }

    [Fact]
    public async Task Diary_FirstOwnedEntryQualifiesButAnotherUsersEntryDoesNot()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, null);
        database.Context.DiaryEntries.Add(DiaryEntry(
            "user-2", food.Id, new DateTime(2026, 9, 1), "Breakfast"));
        await database.Context.SaveChangesAsync();

        var notOwned = await Service(database.Context)
            .EvaluateDiaryAchievementsAsync("user-1");
        database.Context.DiaryEntries.Add(DiaryEntry(
            "user-1", food.Id, new DateTime(2026, 9, 2), "Lunch"));
        await database.Context.SaveChangesAsync();
        var owned = await Service(database.Context)
            .EvaluateDiaryAchievementsAsync("user-1");

        Assert.Empty(notOwned.NewlyUnlockedAchievements);
        Assert.Contains(
            owned.NewlyUnlockedAchievements,
            item => item.Key == AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Fact]
    public async Task Diary_ThirtyDistinctDatesQualifyAndDuplicateDateCountsOnce()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, null);
        var start = new DateTime(2026, 8, 1);
        database.Context.DiaryEntries.AddRange(
            Enumerable.Range(0, 29).Select(offset =>
                DiaryEntry("user-1", food.Id, start.AddDays(offset), "Breakfast")));
        database.Context.DiaryEntries.Add(
            DiaryEntry("user-1", food.Id, start.AddHours(12), "Lunch"));
        await database.Context.SaveChangesAsync();

        await Service(database.Context).EvaluateDiaryAchievementsAsync("user-1");
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.DiaryDistinctDaysThirtyKey));

        database.Context.DiaryEntries.Add(
            DiaryEntry("user-1", food.Id, start.AddDays(29), "Dinner"));
        await database.Context.SaveChangesAsync();
        await Service(database.Context).EvaluateDiaryAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.DiaryDistinctDaysThirtyKey));
    }

    [Fact]
    public async Task Diary_AllFourMealTypesQualifyAndMissingOneDoesNot()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, null);
        var meals = new[] { "Breakfast", "Lunch", "Dinner" };
        database.Context.DiaryEntries.AddRange(meals.Select((meal, index) =>
            DiaryEntry(
                "user-1", food.Id, new DateTime(2026, 9, index + 1), meal)));
        await database.Context.SaveChangesAsync();

        await Service(database.Context).EvaluateDiaryAchievementsAsync("user-1");
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.DiaryAllMealTypesKey));

        database.Context.DiaryEntries.Add(DiaryEntry(
            "user-1", food.Id, new DateTime(2026, 9, 4), "Snack"));
        await database.Context.SaveChangesAsync();
        await Service(database.Context).EvaluateDiaryAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.DiaryAllMealTypesKey));
    }

    [Fact]
    public async Task Foods_OnlyOwnedManualFoodQualifiesIncludingSoftDeletedHistory()
    {
        await using var database = await TwoUserDatabaseAsync();
        await AddFoodAsync(database.Context, "user-2");
        await AddFoodAsync(
            database.Context,
            "user-1",
            source: FoodSources.Usda,
            externalId: "provider-1");

        await Service(database.Context).EvaluateFoodAchievementsAsync("user-1");
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.FoodsFirstCustomKey));

        await AddFoodAsync(database.Context, "user-1", isDeleted: true);
        await Service(database.Context).EvaluateFoodAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.FoodsFirstCustomKey));
    }

    [Fact]
    public async Task Foods_ActiveOwnedManualFoodQualifies()
    {
        await using var database = await UserDatabaseAsync();
        await AddFoodAsync(database.Context, "user-1");

        await Service(database.Context).EvaluateFoodAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.FoodsFirstCustomKey));
    }

    [Fact]
    public async Task SavedMeals_OnlyOwnedMealQualifies()
    {
        await using var database = await TwoUserDatabaseAsync();
        database.Context.SavedMeals.Add(new SavedMeal
        {
            UserId = "user-2",
            Name = "Other meal"
        });
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateSavedMealAchievementsAsync("user-1");
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.SavedMealsFirstKey));

        database.Context.SavedMeals.Add(new SavedMeal
        {
            UserId = "user-1",
            Name = "My meal"
        });
        await database.Context.SaveChangesAsync();
        await Service(database.Context)
            .EvaluateSavedMealAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.SavedMealsFirstKey));
    }

    [Fact]
    public async Task Profile_StructurallyCompleteQualifiesAndIncompleteDoesNot()
    {
        await using var database = await TwoUserDatabaseAsync();
        var incomplete = CompleteProfile("user-1");
        incomplete.ActivityLevel = string.Empty;
        database.Context.UserProfiles.AddRange(
            incomplete,
            CompleteProfile("user-2"));
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateProfileAchievementsAsync("user-1");
        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ProfileCompletedKey));

        incomplete.ActivityLevel = ProfileOptions.Sedentary;
        await database.Context.SaveChangesAsync();
        await Service(database.Context)
            .EvaluateProfileAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ProfileCompletedKey));
    }

    [Fact]
    public async Task Profile_TargetDirectionAndHealthOutcomeDoNotControlQualification()
    {
        await using var database = await UserDatabaseAsync();
        var profile = CompleteProfile("user-1");
        profile.HeightCm = 300;
        profile.WeightKg = 20;
        profile.Goal = ProfileOptions.Lose;
        profile.GoalWeightKg = 100;
        profile.WeeklyGoalKg = 0.5m;
        database.Context.UserProfiles.Add(profile);
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateProfileAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.ProfileCompletedKey));
    }

    [Fact]
    public async Task Customisation_ProvisionedDefaultsAloneDoNotQualify()
    {
        await using var database = await TwoUserDatabaseAsync();
        database.Context.UserCapyAppearances.AddRange(
            DefaultAppearance("user-1"),
            new UserCapyAppearance
            {
                UserId = "user-2",
                ExpressionId = 1,
                BackgroundId = 13,
                HatHairId = 2
            });
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateCustomisationAchievementsAsync("user-1");

        Assert.False(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.CustomisationFirstEquipKey));
    }

    [Fact]
    public async Task Customisation_OptionalEquippedItemQualifies()
    {
        await using var database = await UserDatabaseAsync();
        var appearance = DefaultAppearance("user-1");
        appearance.HatHairId = 2;
        database.Context.UserCapyAppearances.Add(appearance);
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateCustomisationAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.CustomisationFirstEquipKey));
    }

    [Fact]
    public async Task Customisation_ChangedExpressionQualifies()
    {
        await using var database = await UserDatabaseAsync();
        database.Context.CapyItems.Add(new CapyItem
        {
            Id = 100,
            Name = "Alternate expression",
            Category = CapyCategories.Expression,
            ImagePath = "/test.png",
            IsActive = true
        });
        var appearance = DefaultAppearance("user-1");
        appearance.ExpressionId = 100;
        database.Context.UserCapyAppearances.Add(appearance);
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateCustomisationAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.CustomisationFirstEquipKey));
    }

    [Fact]
    public async Task Customisation_ChangedBackgroundQualifies()
    {
        await using var database = await UserDatabaseAsync();
        var appearance = DefaultAppearance("user-1");
        appearance.BackgroundId = 8;
        database.Context.UserCapyAppearances.Add(appearance);
        await database.Context.SaveChangesAsync();

        await Service(database.Context)
            .EvaluateCustomisationAchievementsAsync("user-1");

        Assert.True(await HasAchievementAsync(
            database.Context,
            AchievementDefinitions.CustomisationFirstEquipKey));
    }

    [Fact]
    public async Task Summary_EmptyUserReturnsLevelOneAndZeroActivity()
    {
        await using var database = await UserDatabaseAsync();

        var summary = await Service(database.Context).GetSummaryAsync(
            "user-1",
            new DateOnly(2026, 9, 13));

        Assert.Equal(0, summary.TotalXp);
        Assert.Equal(1, summary.Level.CurrentLevel);
        Assert.Equal(0, summary.Level.TotalXp);
        Assert.Equal(0, summary.Activity.DistinctActiveDayCount);
        Assert.Empty(summary.UnlockedAchievements);
    }

    [Fact]
    public async Task Summary_DerivesLedgerLevelActivityAndAchievementMetadata()
    {
        await using var database = await UserDatabaseAsync();
        database.Context.UserXpEvents.AddRange(
            XpEvent("user-1", "daily:2026-09-11", 60),
            XpEvent("user-1", "bonus:test", 70));
        database.Context.UserDailyActivities.AddRange(
            Activity("user-1", new DateOnly(2026, 9, 10)),
            Activity("user-1", new DateOnly(2026, 9, 11)),
            Activity("user-1", new DateOnly(2026, 9, 12)));
        database.Context.UserAchievements.Add(new UserAchievement
        {
            UserId = "user-1",
            AchievementKey = AchievementDefinitions.DiaryFirstEntryKey,
            UnlockedAtUtc = AuditTime.UtcDateTime
        });
        await database.Context.SaveChangesAsync();

        var summary = await Service(database.Context).GetSummaryAsync(
            "user-1",
            new DateOnly(2026, 9, 13));
        var expectedLevel = new ProgressionLevelCalculator().Calculate(130);
        var expectedActivity = new ActivityStreakCalculator().Calculate(
            [
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 11),
                new DateOnly(2026, 9, 12)
            ],
            new DateOnly(2026, 9, 13));

        Assert.Equal(130, summary.TotalXp);
        Assert.Equal(expectedLevel, summary.Level);
        Assert.Equal(expectedActivity, summary.Activity);
        var unlocked = Assert.Single(summary.UnlockedAchievements);
        Assert.Equal(AchievementDefinitions.DiaryFirstEntryKey, unlocked.Key);
        Assert.Equal("First Steps", unlocked.Definition!.DisplayName);
    }

    [Fact]
    public async Task Summary_UnknownPersistedKeyDoesNotCrash()
    {
        await using var database = await UserDatabaseAsync();
        database.Context.UserAchievements.Add(new UserAchievement
        {
            UserId = "user-1",
            AchievementKey = "legacy.unknown",
            UnlockedAtUtc = AuditTime.UtcDateTime
        });
        await database.Context.SaveChangesAsync();

        var summary = await Service(database.Context).GetSummaryAsync(
            "user-1",
            new DateOnly(2026, 9, 13));

        var unlocked = Assert.Single(summary.UnlockedAchievements);
        Assert.Equal("legacy.unknown", unlocked.Key);
        Assert.Null(unlocked.Definition);
    }

    [Fact]
    public async Task Summary_DoesNotLeakAnotherUsersProgression()
    {
        await using var database = await TwoUserDatabaseAsync();
        database.Context.UserXpEvents.Add(XpEvent("user-2", "daily:other", 100));
        database.Context.UserDailyActivities.Add(Activity(
            "user-2", new DateOnly(2026, 9, 13)));
        database.Context.UserAchievements.Add(new UserAchievement
        {
            UserId = "user-2",
            AchievementKey = AchievementDefinitions.DiaryFirstEntryKey,
            UnlockedAtUtc = AuditTime.UtcDateTime
        });
        await database.Context.SaveChangesAsync();

        var summary = await Service(database.Context).GetSummaryAsync(
            "user-1",
            new DateOnly(2026, 9, 13));

        Assert.Equal(0, summary.TotalXp);
        Assert.Equal(0, summary.Activity.DistinctActiveDayCount);
        Assert.Empty(summary.UnlockedAchievements);
    }

    [Fact]
    public async Task ConcurrentDailyActivityAttemptsPersistExactlyOneActivityAndXp()
    {
        await using var database = await FileProgressionDatabase.CreateAsync();
        var date = new DateOnly(2026, 9, 13);
        using var gate = new Barrier(2);

        var first = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context).RecordDailyActivityAsync(
                "user-1", date, "UTC");
        });
        var second = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context).RecordDailyActivityAsync(
                "user-1", date, "UTC");
        });

        var results = await Task.WhenAll(first, second);
        await using var verification = database.CreateContext();
        Assert.Single(results, result => result.WasRecorded);
        Assert.Equal(1, await verification.UserDailyActivities.CountAsync());
        Assert.Equal(1, await verification.UserXpEvents.CountAsync(item =>
            item.EventKey == "daily:2026-09-13"));
    }

    [Fact]
    public async Task ConcurrentAchievementEvaluationPersistsExactlyOneAchievementAndXp()
    {
        await using var database = await FileProgressionDatabase.CreateAsync(
            seedDiaryEntry: true);
        using var gate = new Barrier(2);

        var first = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context)
                .EvaluateDiaryAchievementsAsync("user-1");
        });
        var second = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            gate.SignalAndWait();
            return await Service(context)
                .EvaluateDiaryAchievementsAsync("user-1");
        });

        await Task.WhenAll(first, second);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.UserAchievements.CountAsync(item =>
            item.AchievementKey ==
                AchievementDefinitions.DiaryFirstEntryKey));
        Assert.Equal(1, await verification.UserXpEvents.CountAsync(item =>
            item.EventKey ==
                "achievement:diary.first-entry"));
    }

    private static ProgressionService Service(ApplicationDbContext context) =>
        new(
            context,
            new ProgressionLevelCalculator(),
            new ActivityStreakCalculator(),
            new FixedTimeProvider(AuditTime));

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

    private static UserDailyActivity Activity(string userId, DateOnly date) =>
        new()
        {
            UserId = userId,
            LocalDate = date,
            RecordedAtUtc = AuditTime.UtcDateTime,
            TimeZoneId = "UTC"
        };

    private static UserXpEvent XpEvent(
        string userId,
        string eventKey,
        int amount) => new()
        {
            UserId = userId,
            EventKey = eventKey,
            Amount = amount,
            AwardedAtUtc = AuditTime.UtcDateTime
        };

    private static async Task<Food> AddFoodAsync(
        ApplicationDbContext context,
        string? userId,
        string? source = null,
        string? externalId = null,
        bool isDeleted = false)
    {
        var food = new Food
        {
            UserId = userId,
            Source = source,
            ExternalId = externalId,
            IsDeleted = isDeleted,
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
            FoodNameSnapshot = "Test food",
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

    private static UserCapyAppearance DefaultAppearance(string userId) =>
        new()
        {
            UserId = userId,
            ExpressionId = 1,
            BackgroundId = 13
        };

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

    private static async Task CreateFailingInsertTriggerAsync(
        ApplicationDbContext context,
        string tableName,
        string triggerName)
    {
        var sql = (tableName, triggerName) switch
        {
            ("UserXpEvents", "FailDailyXp") =>
                """
                CREATE TRIGGER "FailDailyXp"
                BEFORE INSERT ON "UserXpEvents"
                BEGIN
                    SELECT RAISE(FAIL, 'forced progression test failure');
                END;
                """,
            ("UserXpEvents", "FailAchievementXp") =>
                """
                CREATE TRIGGER "FailAchievementXp"
                BEFORE INSERT ON "UserXpEvents"
                BEGIN
                    SELECT RAISE(FAIL, 'forced progression test failure');
                END;
                """,
            ("UserAchievements", "FailAchievement") =>
                """
                CREATE TRIGGER "FailAchievement"
                BEFORE INSERT ON "UserAchievements"
                BEGIN
                    SELECT RAISE(FAIL, 'forced progression test failure');
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(triggerName))
        };

        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private sealed class FileProgressionDatabase : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private FileProgressionDatabase(
            string path,
            DbContextOptions<ApplicationDbContext> options)
        {
            _path = path;
            _options = options;
        }

        public static async Task<FileProgressionDatabase> CreateAsync(
            bool seedDiaryEntry = false)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"comfy-capy-progression-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(
                    $"Data Source={path};Default Timeout=30;Pooling=False")
                .Options;
            var database = new FileProgressionDatabase(path, options);
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

            if (seedDiaryEntry)
            {
                var food = new Food
                {
                    Name = "Concurrency food",
                    Calories = 100,
                    Protein = 1,
                    Carbohydrates = 2,
                    Fat = 3
                };
                context.Foods.Add(food);
                await context.SaveChangesAsync();
                context.DiaryEntries.Add(DiaryEntry(
                    "user-1",
                    food.Id,
                    new DateTime(2026, 9, 13),
                    "Breakfast"));
            }

            await context.SaveChangesAsync();
            return database;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            await Task.Yield();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            var walPath = _path + "-wal";
            var shmPath = _path + "-shm";

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }
        }
    }
}
