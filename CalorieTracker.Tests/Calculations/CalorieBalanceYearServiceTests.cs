using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Calculations;

public class CalorieBalanceYearServiceTests
{
    [Fact]
    public async Task GetYear_NormalYearReturnsOrdered365DayCalendar()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var result = await Service(database).GetYearAsync("user-1", 2025);

        Assert.Equal(2025, result.Year);
        Assert.Equal(365, result.Days.Count);
        Assert.Equal(new DateOnly(2025, 1, 1), result.Days[0].Date);
        Assert.Equal(new DateOnly(2025, 12, 31), result.Days[^1].Date);
        Assert.True(result.Days.Zip(result.Days.Skip(1))
            .All(pair => pair.First.Date < pair.Second.Date));
    }

    [Fact]
    public async Task GetYear_LeapYearReturns366Days()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var result = await Service(database).GetYearAsync("user-1", 2024);

        Assert.Equal(366, result.Days.Count);
        Assert.Contains(result.Days, day =>
            day.Date == new DateOnly(2024, 2, 29));
        Assert.Equal(new DateOnly(2024, 12, 31), result.Days[^1].Date);
    }

    [Fact]
    public async Task GetYear_AggregatesDiarySnapshotsAndClassifiesAgainstMaintenance()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1", name: "Food one");
        var secondFood = TestData.Food("user-1", name: "Food two");
        food.Calories = 100;
        secondFood.Calories = 250;
        database.Context.Foods.AddRange(food, secondFood);
        await database.Context.SaveChangesAsync();

        var firstEntry = TestData.DiaryEntry("user-1", food, 100);
        firstEntry.Date = new DateTime(2025, 1, 15, 8, 0, 0);
        var secondEntry = TestData.DiaryEntry("user-1", secondFood, 100);
        secondEntry.Date = new DateTime(2025, 1, 15, 18, 0, 0);
        database.Context.DiaryEntries.AddRange(firstEntry, secondEntry);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2025, 1, 15),
                1000));
        await database.Context.SaveChangesAsync();

        // Mutable food data must not rewrite the diary's captured nutrition.
        food.Calories = 9999;
        await database.Context.SaveChangesAsync();
        var result = await Service(database).GetYearAsync("user-1", 2025);
        var day = result.Days.Single(item =>
            item.Date == new DateOnly(2025, 1, 15));

        Assert.True(day.HasDiaryData);
        Assert.Equal(350, day.CaloriesConsumed);
        Assert.Equal(1000, day.MaintenanceCalories);
        Assert.Equal(-650, day.Difference);
        Assert.Equal(-65, day.BalancePercentage);
        Assert.Equal(CalorieBalanceClassification.HeavyCut, day.Classification);
    }

    [Fact]
    public async Task GetYear_UsesMaintenanceSnapshotAndIgnoresCustomTarget()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var profile = new UserProfile
        {
            UserId = "user-1",
            DateOfBirth = DateTime.Today.AddYears(-30),
            HeightCm = 180,
            WeightKg = 80,
            CalculationSex = ProfileOptions.Male,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = ProfileOptions.Lose,
            WeeklyGoalKg = 1,
            CustomCalorieTarget = 500
        };
        database.Context.UserProfiles.Add(profile);
        var food = TestData.Food("user-1");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 100);
        entry.Date = new DateTime(2025, 2, 2);
        database.Context.DiaryEntries.Add(entry);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2025, 2, 2),
                1000));
        await database.Context.SaveChangesAsync();

        var day = (await Service(database).GetYearAsync("user-1", 2025))
            .Days.Single(item => item.Date == new DateOnly(2025, 2, 2));

        Assert.Equal(200, day.CaloriesConsumed);
        Assert.Equal(1000, day.MaintenanceCalories);
        Assert.Equal(-80, day.BalancePercentage);
    }

    [Fact]
    public async Task GetYear_RepresentsNoDataAndMissingMaintenanceStatesSeparately()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        database.Context.Foods.Add(food);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2025, 3, 1),
                1000));
        await database.Context.SaveChangesAsync();

        var entryWithoutMaintenance = TestData.DiaryEntry("user-1", food, 100);
        entryWithoutMaintenance.Date = new DateTime(2025, 3, 2);
        var entryWithMaintenance = TestData.DiaryEntry("user-1", food, 100);
        entryWithMaintenance.Date = new DateTime(2025, 3, 3);
        database.Context.DiaryEntries.AddRange(
            entryWithoutMaintenance,
            entryWithMaintenance);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2025, 3, 3),
                1000));
        await database.Context.SaveChangesAsync();

        var days = (await Service(database).GetYearAsync("user-1", 2025))
            .Days;
        var retainedSnapshotDay = days.Single(day =>
            day.Date == new DateOnly(2025, 3, 1));
        var missingMaintenanceDay = days.Single(day =>
            day.Date == new DateOnly(2025, 3, 2));
        var populatedDay = days.Single(day =>
            day.Date == new DateOnly(2025, 3, 3));

        Assert.False(retainedSnapshotDay.HasDiaryData);
        Assert.Null(retainedSnapshotDay.CaloriesConsumed);
        Assert.Null(retainedSnapshotDay.MaintenanceCalories);
        Assert.True(missingMaintenanceDay.HasDiaryData);
        Assert.Equal(200, missingMaintenanceDay.CaloriesConsumed);
        Assert.Null(missingMaintenanceDay.MaintenanceCalories);
        Assert.Null(missingMaintenanceDay.Classification);
        Assert.True(populatedDay.HasDiaryData);
        Assert.NotNull(populatedDay.Classification);
    }

    [Fact]
    public async Task GetYear_ExcludesOtherUsersAndPerformsNoWrites()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        var firstFood = TestData.Food("user-1", name: "First");
        var secondFood = TestData.Food("user-2", name: "Second");
        database.Context.Foods.AddRange(firstFood, secondFood);
        await database.Context.SaveChangesAsync();
        var firstEntry = TestData.DiaryEntry("user-1", firstFood, 100);
        firstEntry.Date = new DateTime(2025, 4, 1);
        var secondEntry = TestData.DiaryEntry("user-2", secondFood, 100);
        secondEntry.Date = new DateTime(2025, 4, 1);
        database.Context.DiaryEntries.AddRange(firstEntry, secondEntry);
        database.Context.DailyMaintenanceSnapshots.AddRange(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2025, 4, 1),
                1000),
            new DailyMaintenanceSnapshot(
                "user-2",
                new DateOnly(2025, 4, 1),
                2000));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var result = await Service(database).GetYearAsync("user-1", 2025);
        var day = result.Days.Single(item =>
            item.Date == new DateOnly(2025, 4, 1));

        Assert.Equal(200, day.CaloriesConsumed);
        Assert.Equal(1000, day.MaintenanceCalories);
        Assert.All(
            database.Context.ChangeTracker.Entries(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public async Task GetYear_FutureDatesRemainNoDataEvenIfDiaryRowsExist()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var futureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var entry = TestData.DiaryEntry("user-1", food, 100);
        entry.Date = futureDate.ToDateTime(TimeOnly.MinValue);
        database.Context.DiaryEntries.Add(entry);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot("user-1", futureDate, 1000));
        await database.Context.SaveChangesAsync();

        var result = await Service(database).GetYearAsync(
            "user-1",
            futureDate.Year);
        var futureDay = result.Days.Single(day =>
            day.Date == futureDate);

        Assert.False(futureDay.HasDiaryData);
        Assert.Null(futureDay.CaloriesConsumed);
        Assert.Null(futureDay.Classification);
    }

    private static CalorieBalanceYearService Service(TestDatabase database) =>
        new(database.Context);
}
