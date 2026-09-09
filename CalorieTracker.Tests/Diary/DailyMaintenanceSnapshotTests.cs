using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages.Profile;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DiaryCreateModel = CalorieTracker.Pages.Diary.CreateModel;
using DiaryDeleteModel = CalorieTracker.Pages.Diary.DeleteModel;
using DiaryEditModel = CalorieTracker.Pages.Diary.EditModel;

namespace CalorieTracker.Tests.Diary;

public class DailyMaintenanceSnapshotTests
{
    private static readonly DateTime SourceDate = new(2026, 6, 14);
    private static readonly DateTime DestinationDate = new(2026, 6, 15);

    [Fact]
    public async Task Create_FirstAndAdditionalEntriesCreateOneStableSnapshot()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");

        await CreateEntryAsync(database, "user-1", food, SourceDate, 100);
        var firstSnapshot = await database.Context.DailyMaintenanceSnapshots
            .SingleAsync();
        var maintenance = firstSnapshot.MaintenanceCalories;

        await CreateEntryAsync(database, "user-1", food, SourceDate, 200);

        var snapshots = await database.Context.DailyMaintenanceSnapshots
            .ToListAsync();
        Assert.Single(snapshots);
        Assert.Equal(maintenance, snapshots[0].MaintenanceCalories);
    }

    [Fact]
    public async Task Create_DefaultLocalDate_IsUsedForEntryAndSnapshot()
    {
        var localDate = new DateOnly(2026, 9, 9);
        await using var database = await CreateUserWithProfileAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");
        var model = new DiaryCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider(localDate));
        PageModelTestContext.Attach(model, "user-1");

        var getResult = await model.OnGetAsync(null, "Dinner", food.Id);
        model.DiaryEntry.Quantity = 100;
        var postResult = await model.OnPostAsync();

        Assert.IsType<PageResult>(getResult);
        Assert.IsType<RedirectToPageResult>(postResult);
        Assert.Equal(
            localDate,
            DateOnly.FromDateTime(model.DiaryEntry.Date));
        Assert.Equal(
            localDate,
            (await database.Context.DailyMaintenanceSnapshots.SingleAsync()).Date);
    }

    [Fact]
    public async Task Edit_FoodAndQuantityOnSameDateDoesNotChangeSnapshot()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        var originalFood = await AddFoodAsync(database, "user-1", "Original");
        var replacementFood = await AddFoodAsync(database, "user-1", "Replacement");
        var entry = await CreateEntryAsync(
            database,
            "user-1",
            originalFood,
            SourceDate,
            100);
        var maintenance = (await database.Context.DailyMaintenanceSnapshots
            .SingleAsync()).MaintenanceCalories;
        var model = CreateEditModel(
            database,
            "user-1",
            entry,
            replacementFood,
            SourceDate,
            250);

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        var snapshot = await database.Context.DailyMaintenanceSnapshots
            .SingleAsync();
        Assert.Equal(maintenance, snapshot.MaintenanceCalories);
        Assert.Equal(replacementFood.Id, entry.FoodId);
        Assert.Equal(250, entry.Quantity);
    }

    [Fact]
    public async Task Edit_MovingDatePreservesSourceAndCreatesDestinationSnapshot()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");
        var entry = await CreateEntryAsync(
            database,
            "user-1",
            food,
            SourceDate,
            100);
        var sourceMaintenance = (await database.Context
            .DailyMaintenanceSnapshots.SingleAsync()).MaintenanceCalories;
        var profile = await database.Context.UserProfiles.SingleAsync();
        profile.WeightKg = 100;
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(
            database,
            "user-1",
            entry,
            food,
            DestinationDate,
            100);

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        var snapshots = await database.Context.DailyMaintenanceSnapshots
            .OrderBy(snapshot => snapshot.Date)
            .ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(DateOnly.FromDateTime(SourceDate), snapshots[0].Date);
        Assert.Equal(sourceMaintenance, snapshots[0].MaintenanceCalories);
        Assert.Equal(DateOnly.FromDateTime(DestinationDate), snapshots[1].Date);
        Assert.NotEqual(sourceMaintenance, snapshots[1].MaintenanceCalories);
    }

    [Fact]
    public async Task DeleteLastEntryAndReAddPreservesOriginalSnapshot()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");
        var entry = await CreateEntryAsync(
            database,
            "user-1",
            food,
            SourceDate,
            100);
        var maintenance = (await database.Context.DailyMaintenanceSnapshots
            .SingleAsync()).MaintenanceCalories;
        var deleteModel = new DiaryDeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(deleteModel, "user-1");

        var deleteResult = await deleteModel.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(deleteResult);
        Assert.Empty(database.Context.DiaryEntries);
        Assert.Single(database.Context.DailyMaintenanceSnapshots);

        var profile = await database.Context.UserProfiles.SingleAsync();
        profile.WeightKg = 100;
        await database.Context.SaveChangesAsync();
        await CreateEntryAsync(database, "user-1", food, SourceDate, 100);

        var snapshot = await database.Context.DailyMaintenanceSnapshots
            .SingleAsync();
        Assert.Equal(maintenance, snapshot.MaintenanceCalories);
    }

    [Fact]
    public async Task ProfileSaveFillsOnlyMissingPopulatedDatesAndPreservesExistingSnapshot()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");
        var sourceEntry = TestData.DiaryEntry("user-1", food, 100);
        sourceEntry.Date = SourceDate;
        var destinationEntry = TestData.DiaryEntry("user-1", food, 100);
        destinationEntry.Date = DestinationDate;
        database.Context.DiaryEntries.AddRange(
            sourceEntry,
            destinationEntry);
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                DateOnly.FromDateTime(SourceDate),
                1111));
        await database.Context.SaveChangesAsync();
        var model = CreateProfileModel(database, "user-1", weightKg: 100);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var snapshots = await database.Context.DailyMaintenanceSnapshots
            .OrderBy(snapshot => snapshot.Date)
            .ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(1111, snapshots[0].MaintenanceCalories);
        Assert.NotEqual(1111, snapshots[1].MaintenanceCalories);
    }

    [Fact]
    public async Task Create_WithInvalidProfileDoesNotInventMaintenance()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserProfiles.Add(ValidProfile(
            "user-1",
            dateOfBirth: new DateTime(2010, 1, 1)));
        await database.Context.SaveChangesAsync();
        var food = await AddFoodAsync(database, "user-1");

        await CreateEntryAsync(database, "user-1", food, SourceDate, 100);

        Assert.Single(database.Context.DiaryEntries);
        Assert.Empty(database.Context.DailyMaintenanceSnapshots);
    }

    [Fact]
    public async Task Create_WithoutProfileDoesNotInventMaintenance()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = await AddFoodAsync(database, "user-1");

        await CreateEntryAsync(database, "user-1", food, SourceDate, 100);

        Assert.Single(database.Context.DiaryEntries);
        Assert.Empty(database.Context.DailyMaintenanceSnapshots);
    }

    [Fact]
    public async Task SameDateForTwoUsersCreatesSeparateOwnedSnapshots()
    {
        await using var database = await CreateUserWithProfileAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        database.Context.UserProfiles.Add(ValidProfile("user-2"));
        await database.Context.SaveChangesAsync();
        var firstFood = await AddFoodAsync(database, "user-1", "First");
        var secondFood = await AddFoodAsync(database, "user-2", "Second");

        await CreateEntryAsync(database, "user-1", firstFood, SourceDate, 100);
        await CreateEntryAsync(database, "user-2", secondFood, SourceDate, 100);

        var snapshots = await database.Context.DailyMaintenanceSnapshots
            .OrderBy(snapshot => snapshot.UserId)
            .ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal("user-1", snapshots[0].UserId);
        Assert.Equal("user-2", snapshots[1].UserId);
        Assert.All(snapshots, snapshot =>
            Assert.Equal(DateOnly.FromDateTime(SourceDate), snapshot.Date));
    }

    [Fact]
    public async Task SnapshotUsesHistoricalAgeAndIgnoresCustomCalorieTarget()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var profile = ValidProfile(
            "user-1",
            dateOfBirth: new DateTime(2000, 2, 29));
        profile.CustomCalorieTarget = 9999;
        profile.Goal = ProfileOptions.Gain;
        profile.WeeklyGoalKg = 1;
        database.Context.UserProfiles.Add(profile);
        await database.Context.SaveChangesAsync();
        var food = await AddFoodAsync(database, "user-1");
        var historicalDate = new DateTime(2025, 2, 28);

        await CreateEntryAsync(
            database,
            "user-1",
            food,
            historicalDate,
            100);

        var snapshot = await database.Context.DailyMaintenanceSnapshots
            .SingleAsync();
        Assert.Equal(2172m, snapshot.MaintenanceCalories);
        Assert.NotEqual(profile.CustomCalorieTarget, snapshot.MaintenanceCalories);
    }

    [Fact]
    public async Task ConcurrentSnapshotInsert_ReusesDatabaseWinnerAndSavesDiaryEntry()
    {
        const string connectionString =
            "Data Source=maintenance-snapshot-race;Mode=Memory;Cache=Shared";
        await using var firstConnection = new SqliteConnection(connectionString);
        await using var secondConnection = new SqliteConnection(connectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();

        var firstOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(firstConnection)
            .Options;
        var secondOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(secondConnection)
            .Options;
        await using var firstContext = new ApplicationDbContext(firstOptions);
        await using var secondContext = new ApplicationDbContext(secondOptions);
        await firstContext.Database.EnsureCreatedAsync();

        firstContext.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "user-1",
            NormalizedUserName = "USER-1",
            Email = "user-1@example.test",
            NormalizedEmail = "USER-1@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        var profile = ValidProfile("user-1");
        var food = TestData.Food("user-1");
        firstContext.AddRange(profile, food);
        await firstContext.SaveChangesAsync();

        var service = new DailyMaintenanceSnapshotService(firstContext);
        var date = DateOnly.FromDateTime(SourceDate);
        var entry = TestData.DiaryEntry("user-1", food, 100);
        entry.Date = SourceDate;
        firstContext.DiaryEntries.Add(entry);
        await service.EnsureSnapshotAsync("user-1", date, profile);

        secondContext.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot("user-1", date, 2222));
        await secondContext.SaveChangesAsync();

        await service.SaveChangesAsync();

        Assert.Single(await firstContext.DiaryEntries.ToListAsync());
        var snapshot = await firstContext.DailyMaintenanceSnapshots
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(2222, snapshot.MaintenanceCalories);
    }

    private static async Task<TestDatabase> CreateUserWithProfileAsync(
        string userId)
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync(userId);
        database.Context.UserProfiles.Add(ValidProfile(userId));
        await database.Context.SaveChangesAsync();
        return database;
    }

    private static UserProfile ValidProfile(
        string userId,
        DateTime? dateOfBirth = null,
        decimal weightKg = 80) => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = dateOfBirth ?? new DateTime(1990, 6, 15),
        HeightCm = 180,
        WeightKg = weightKg,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };

    private static async Task<Food> AddFoodAsync(
        TestDatabase database,
        string userId,
        string name = "Test food")
    {
        var food = TestData.Food(userId, name: name);
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        return food;
    }

    private static async Task<DiaryEntry> CreateEntryAsync(
        TestDatabase database,
        string userId,
        Food food,
        DateTime date,
        decimal quantity)
    {
        var model = new DiaryCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider())
        {
            DiaryEntry = new DiaryEntry
            {
                Date = date,
                MealType = "Dinner",
                FoodId = food.Id,
                Quantity = quantity
            },
            MeasurementMode = "Exact"
        };
        PageModelTestContext.Attach(model, userId);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        return model.DiaryEntry;
    }

    private static DiaryEditModel CreateEditModel(
        TestDatabase database,
        string userId,
        DiaryEntry entry,
        Food food,
        DateTime date,
        decimal quantity)
    {
        var model = new DiaryEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context))
        {
            DiaryEntry = new DiaryEntry
            {
                Date = date,
                MealType = entry.MealType,
                FoodId = food.Id,
                Quantity = quantity
            },
            MeasurementMode = "Exact"
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static IndexModel CreateProfileModel(
        TestDatabase database,
        string userId,
        decimal weightKg)
    {
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider())
        {
            UserProfile = ValidProfile(userId, weightKg: weightKg)
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }
}
