using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Diary;

public sealed class DiaryCopyServiceTests
{
    [Fact]
    public async Task CopiesCompleteDayAsImmutableSnapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var food = TestData.Food("owner", name: "Original food");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var breakfast = TestData.DiaryEntry("owner", food, 75);
        breakfast.Date = new DateTime(2026, 9, 8);
        breakfast.MealType = "Breakfast";
        breakfast.IsApproximate = true;
        breakfast.ApproximationLabel = "Small";
        var dinner = TestData.DiaryEntry("owner", food, 125);
        dinner.Date = breakfast.Date;
        dinner.MealType = "Dinner";
        database.Context.DiaryEntries.AddRange(breakfast, dinner);
        await database.Context.SaveChangesAsync();

        food.Name = "Changed later";
        food.Calories = 999;
        await database.Context.SaveChangesAsync();
        var service = CreateService(database);
        var result = await service.CopyAsync(
            "owner",
            breakfast.Date,
            breakfast.Date.AddDays(1),
            false);

        Assert.Equal(DiaryCopyOutcome.Success, result.Outcome);
        Assert.Equal(2, result.EntriesCopied);
        var copies = database.Context.DiaryEntries
            .Where(entry => entry.Date == breakfast.Date.AddDays(1))
            .OrderBy(entry => entry.MealType)
            .ToList();
        Assert.Equal(2, copies.Count);
        Assert.All(copies, copy => Assert.Equal("Original food", copy.FoodNameSnapshot));
        Assert.All(copies, copy => Assert.Equal(200, copy.CaloriesSnapshot));
        Assert.True(copies.Single(copy => copy.MealType == "Breakfast").IsApproximate);
    }

    [Fact]
    public async Task PreviousDayCopyCannotReadAnotherUsersEntries()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        await database.AddUserAsync("other", "other");
        var food = TestData.Food("other");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        database.Context.DiaryEntries.Add(TestData.DiaryEntry("other", food, 100));
        await database.Context.SaveChangesAsync();

        var result = await CreateService(database).CopyAsync(
            "owner",
            new DateTime(2026, 9, 5),
            new DateTime(2026, 9, 6),
            false);

        Assert.Equal(DiaryCopyOutcome.NoSourceEntries, result.Outcome);
        Assert.Single(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task NonEmptyTargetRequiresExplicitAdditiveConfirmation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var food = TestData.Food("owner");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var source = TestData.DiaryEntry("owner", food, 100);
        var target = TestData.DiaryEntry("owner", food, 50);
        target.Date = source.Date.AddDays(1);
        database.Context.DiaryEntries.AddRange(source, target);
        await database.Context.SaveChangesAsync();

        var result = await CreateService(database).CopyAsync(
            "owner",
            source.Date,
            target.Date,
            false);

        Assert.Equal(DiaryCopyOutcome.TargetNotEmpty, result.Outcome);
        Assert.Equal(2, database.Context.DiaryEntries.Count());
    }

    [Fact]
    public async Task RepeatedCopyWithoutConfirmationDoesNotDuplicateEntries()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var food = TestData.Food("owner");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var source = TestData.DiaryEntry("owner", food, 100);
        database.Context.DiaryEntries.Add(source);
        await database.Context.SaveChangesAsync();
        var service = CreateService(database);
        var targetDate = source.Date.AddDays(1);

        var first = await service.CopyAsync(
            "owner", source.Date, targetDate, false);
        var repeated = await service.CopyAsync(
            "owner", source.Date, targetDate, false);

        Assert.Equal(DiaryCopyOutcome.Success, first.Outcome);
        Assert.Equal(DiaryCopyOutcome.TargetNotEmpty, repeated.Outcome);
        Assert.Equal(2, database.Context.DiaryEntries.Count());
    }

    private static DiaryCopyService CreateService(TestDatabase database) =>
        new(
            database.Context,
            new DailyMaintenanceSnapshotService(database.Context));
}
