using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DiaryCreateModel = CalorieTracker.Pages.Diary.CreateModel;
using FoodCreateModel = CalorieTracker.Pages.Foods.CreateModel;
using ProfileModel = CalorieTracker.Pages.Profile.IndexModel;
using SavedMealCreateModel = CalorieTracker.Pages.SavedMeals.CreateModel;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionDomainHookTests
{
    private static readonly DateTimeOffset AuditTime =
        new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DiaryCreate_FirstEntryUnlocksFirstSteps()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        var model = DiaryModel(database, "user-1", food);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Fact]
    public async Task DiaryCreate_ThirtiethDistinctDayUnlocksRegular()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        database.Context.DiaryEntries.AddRange(Enumerable.Range(1, 29)
            .Select(day => Entry(
                "user-1",
                food,
                new DateTime(2026, 8, day),
                "Breakfast")));
        await database.Context.SaveChangesAsync();
        var model = DiaryModel(
            database,
            "user-1",
            food,
            new DateTime(2026, 8, 30));

        await model.OnPostAsync();

        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryDistinctDaysThirtyKey);
    }

    [Fact]
    public async Task DiaryCreate_MissingFourthMealTypeUnlocksFullPlate()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        database.Context.DiaryEntries.AddRange(
            Entry("user-1", food, new DateTime(2026, 9, 1), "Breakfast"),
            Entry("user-1", food, new DateTime(2026, 9, 1), "Lunch"),
            Entry("user-1", food, new DateTime(2026, 9, 1), "Dinner"));
        await database.Context.SaveChangesAsync();
        var model = DiaryModel(
            database,
            "user-1",
            food,
            new DateTime(2026, 9, 1),
            "Snack");

        await model.OnPostAsync();

        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryAllMealTypesKey);
    }

    [Fact]
    public async Task RepeatedDiaryCreates_DoNotDuplicateAchievementOrXp()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");

        await DiaryModel(database, "user-1", food).OnPostAsync();
        await DiaryModel(
            database,
            "user-1",
            food,
            new DateTime(2026, 9, 2)).OnPostAsync();

        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Fact]
    public async Task InvalidDiaryCreate_DoesNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        var model = DiaryModel(database, "user-1", food);
        model.DiaryEntry.Quantity = 0;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task ForeignFoodDiaryCreate_DoesNotAwardWrongUser()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-2");
        var model = DiaryModel(database, "user-1", food);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task DiaryCopy_ThirtiethDistinctDayEvaluatesDiaryAchievements()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        database.Context.DiaryEntries.AddRange(Enumerable.Range(1, 29)
            .Select(day => Entry(
                "user-1",
                food,
                new DateTime(2026, 8, day),
                "Breakfast")));
        await database.Context.SaveChangesAsync();
        var service = new DiaryCopyService(
            database.Context,
            new DailyMaintenanceSnapshotService(database.Context),
            Hooks(database.Context));

        var result = await service.CopyAsync(
            "user-1",
            new DateTime(2026, 8, 29),
            new DateTime(2026, 8, 30),
            false);

        Assert.Equal(DiaryCopyOutcome.Success, result.Outcome);
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryDistinctDaysThirtyKey);
    }

    [Fact]
    public async Task ReusableMealAdd_FirstDiaryEvidenceEvaluatesDiaryAchievements()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        var savedMeal = new SavedMeal
        {
            UserId = "user-1",
            Name = "Stored meal",
            Items =
            [
                DiarySnapshotFactory.ToSavedMealItem(Entry(
                    "user-1",
                    food,
                    new DateTime(2026, 8, 1),
                    "Lunch"))
            ]
        };
        database.Context.SavedMeals.Add(savedMeal);
        await database.Context.SaveChangesAsync();
        var service = new ReusableMealService(
            database.Context,
            new DailyMaintenanceSnapshotService(database.Context),
            Hooks(database.Context));

        var count = await service.AddSavedMealToDiaryAsync(
            "user-1",
            savedMeal.Id,
            new DateTime(2026, 9, 1),
            "Lunch");

        Assert.Equal(1, count);
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.DiaryFirstEntryKey);
    }

    [Fact]
    public async Task CustomFoodCreate_UnlocksMadeItMine()
    {
        await using var database = await UserDatabaseAsync();
        var model = FoodModel(database, "user-1", ValidFood());

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.FoodsFirstCustomKey);
    }

    [Fact]
    public async Task InvalidCustomFoodCreate_DoesNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var food = ValidFood();
        food.Name = string.Empty;
        var model = FoodModel(database, "user-1", food);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.Foods);
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task ProviderAndOtherUserFoods_DoNotQualifyCurrentUser()
    {
        await using var database = await TwoUserDatabaseAsync();
        var providerFood = ValidFood();
        providerFood.UserId = "user-1";
        providerFood.Source = FoodSources.Usda;
        providerFood.ExternalId = "123";
        var otherFood = ValidFood();
        otherFood.UserId = "user-2";
        database.Context.Foods.AddRange(providerFood, otherFood);
        await database.Context.SaveChangesAsync();

        await Hooks(database.Context).EvaluateFoodsAsync("user-1");

        Assert.False(await HasAchievementAsync(
            database.Context,
            "user-1",
            AchievementDefinitions.FoodsFirstCustomKey));
    }

    [Fact]
    public async Task SavedMealCreate_UnlocksOnceAndInvalidSaveDoesNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        database.Context.DiaryEntries.Add(Entry(
            "user-1",
            food,
            new DateTime(2026, 9, 1),
            "Breakfast"));
        await database.Context.SaveChangesAsync();

        var first = SavedMealModel(database, "user-1", "First");
        var second = SavedMealModel(database, "user-1", "Second");
        var invalid = SavedMealModel(
            database,
            "user-1",
            "Missing",
            new DateTime(2026, 9, 2));

        Assert.IsType<RedirectToPageResult>(await first.OnPostAsync());
        Assert.IsType<RedirectToPageResult>(await second.OnPostAsync());
        Assert.IsType<PageResult>(await invalid.OnPostAsync());
        Assert.Equal(2, await database.Context.SavedMeals.CountAsync());
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.SavedMealsFirstKey);
    }

    [Fact]
    public async Task CompleteProfileSave_UnlocksGettingComfy()
    {
        await using var database = await UserDatabaseAsync();
        var model = ProfilePage(database, "user-1", CompleteProfile());

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.ProfileCompletedKey);
    }

    [Fact]
    public async Task InvalidOrIncompleteProfileMutation_DoesNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var invalidProfile = CompleteProfile();
        invalidProfile.ActivityLevel = string.Empty;
        var invalid = ProfilePage(database, "user-1", invalidProfile);

        Assert.IsType<PageResult>(await invalid.OnPostAsync());

        var incompleteProfile = CompleteProfile();
        incompleteProfile.UserId = "user-1";
        incompleteProfile.Goal = string.Empty;
        database.Context.UserProfiles.Add(incompleteProfile);
        await database.Context.SaveChangesAsync();
        var theme = ProfilePage(database, "user-1", new UserProfile());
        Assert.IsType<JsonResult>(await theme.OnPostThemeAsync(
            ProfileOptions.DarkTheme));
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task DeliberateCustomisationEquip_UnlocksOnce()
    {
        await using var database = await UserDatabaseAsync();
        var provisioning = new CapyProvisioningService(database.Context);
        await provisioning.ProvisionAsync("user-1");
        var model = CustomisationPage(database, provisioning, "user-1");

        Assert.IsType<JsonResult>(await model.OnPostEquipAsync(
            2,
            CapyCategories.HatHair));
        Assert.IsType<JsonResult>(await model.OnPostEquipAsync(
            3,
            CapyCategories.HatHair));

        await AssertSingleAwardAsync(
            database.Context,
            AchievementDefinitions.CustomisationFirstEquipKey);
    }

    [Fact]
    public async Task CustomisationViewAndProvisionedDefaults_DoNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var provisioning = new CapyProvisioningService(database.Context);
        var model = CustomisationPage(database, provisioning, "user-1");

        await model.OnGetAsync();
        Assert.Empty(database.Context.UserAchievements);
        Assert.IsType<JsonResult>(await model.OnPostProvisionAsync());
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task FailedOrUnauthorizedCustomisation_DoesNotAward()
    {
        await using var database = await UserDatabaseAsync();
        var provisioning = new CapyProvisioningService(database.Context);
        await provisioning.ProvisionAsync("user-1");
        var forbidden = CustomisationPage(database, provisioning, "user-1");
        var anonymous = CustomisationPage(database, provisioning, null);

        Assert.IsType<ForbidResult>(await forbidden.OnPostEquipAsync(
            14,
            CapyCategories.HatHair));
        Assert.IsType<UnauthorizedResult>(await anonymous.OnPostEquipAsync(
            2,
            CapyCategories.HatHair));
        Assert.Empty(database.Context.UserAchievements);
    }

    [Theory]
    [InlineData("Diary")]
    [InlineData("Foods")]
    [InlineData("SavedMeals")]
    [InlineData("Profile")]
    [InlineData("Customisation")]
    public async Task SharedHook_LogsAndFailsOpenForEveryCategory(
        string category)
    {
        await using var database = await UserDatabaseAsync();
        await SeedQualifyingEvidenceAsync(database.Context, category);
        await CreateFailureTriggerAsync(
            database.Context,
            "UserAchievements",
            "FailAchievementHook");
        var logger = new RecordingLogger<ProgressionAchievementHooks>();
        var hooks = Hooks(database.Context, logger);

        await EvaluateAsync(hooks, category, "user-1");

        Assert.Contains(logger.Messages, message =>
            message.Contains(category, StringComparison.Ordinal) &&
            message.Contains("user-1", StringComparison.Ordinal));
        Assert.Empty(database.Context.UserAchievements);
    }

    [Fact]
    public async Task Hook_DoesNotSwallowRequestCancellation()
    {
        await using var database = await UserDatabaseAsync();
        var food = await AddFoodAsync(database.Context, "user-1");
        database.Context.DiaryEntries.Add(Entry(
            "user-1",
            food,
            new DateTime(2026, 9, 1),
            "Breakfast"));
        await database.Context.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Hooks(database.Context).EvaluateDiaryAsync(
                "user-1",
                cancellation.Token));
    }

    [Fact]
    public async Task FoodProgressionFailure_PreservesCoreDataAndRedirect()
    {
        await using var database = await UserDatabaseAsync();
        await CreateFailureTriggerAsync(
            database.Context,
            "UserAchievements",
            "FailFoodAchievement");
        var logger = new RecordingLogger<ProgressionAchievementHooks>();
        var model = FoodModel(
            database,
            "user-1",
            ValidFood(),
            Hooks(database.Context, logger));

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Single(database.Context.Foods);
        Assert.Contains(logger.Messages, message =>
            message.Contains("Foods", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreFoodPersistenceFailure_IsNotSwallowedByHook()
    {
        await using var database = await UserDatabaseAsync();
        await CreateFailureTriggerAsync(
            database.Context,
            "Foods",
            "FailCoreFood");
        var model = FoodModel(database, "user-1", ValidFood());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => model.OnPostAsync());
        Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Empty(database.Context.UserAchievements);
    }

    [Theory]
    [InlineData("Diary", AchievementDefinitions.DiaryFirstEntryKey)]
    [InlineData("Foods", AchievementDefinitions.FoodsFirstCustomKey)]
    [InlineData("SavedMeals", AchievementDefinitions.SavedMealsFirstKey)]
    [InlineData("Profile", AchievementDefinitions.ProfileCompletedKey)]
    [InlineData(
        "Customisation",
        AchievementDefinitions.CustomisationFirstEquipKey)]
    public async Task AuthenticatedRazorPagePost_InvokesDomainHook(
        string category,
        string achievementKey)
    {
        using var factory = new IntegrationTestFactory();
        await AddUserAsync(factory, "user-1");
        var state = await PrepareRequestAsync(factory, category, "user-1");
        using var client = AuthenticatedClient(factory, "user-1");
        var token = await AntiforgeryTokenAsync(client);
        var form = state.Form;
        form["__RequestVerificationToken"] = token;

        var response = await client.PostAsync(
            state.Path,
            new FormUrlEncodedContent(form));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == state.ExpectedStatus,
            $"Expected {state.ExpectedStatus} but received " +
            $"{response.StatusCode}. Response: {responseBody}");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.True(await HasAchievementAsync(
            context,
            "user-1",
            achievementKey));
    }

    private static DiaryCreateModel DiaryModel(
        TestDatabase database,
        string userId,
        Food food,
        DateTime? date = null,
        string mealType = "Breakfast")
    {
        var model = new DiaryCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider(),
            Hooks(database.Context))
        {
            DiaryEntry = new DiaryEntry
            {
                UserId = "posted-user",
                FoodId = food.Id,
                Date = date ?? new DateTime(2026, 9, 1),
                MealType = mealType,
                Quantity = 100
            },
            MeasurementMode = "Exact"
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static FoodCreateModel FoodModel(
        TestDatabase database,
        string userId,
        Food food,
        ProgressionAchievementHooks? hooks = null)
    {
        var model = new FoodCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            hooks ?? Hooks(database.Context))
        {
            Food = food
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static SavedMealCreateModel SavedMealModel(
        TestDatabase database,
        string userId,
        string name,
        DateTime? sourceDate = null)
    {
        var hooks = Hooks(database.Context);
        var model = new SavedMealCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new ReusableMealService(
                database.Context,
                new DailyMaintenanceSnapshotService(database.Context),
                hooks),
            new TestUserLocalTimeProvider(),
            hooks)
        {
            Input = new SavedMealCreateModel.InputModel
            {
                Name = name,
                SourceDate = sourceDate ?? new DateTime(2026, 9, 1),
                SourceMeal = "Breakfast"
            }
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static ProfileModel ProfilePage(
        TestDatabase database,
        string userId,
        UserProfile profile)
    {
        var model = new ProfileModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider(),
            Hooks(database.Context))
        {
            UserProfile = profile
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static CustomisationModel CustomisationPage(
        TestDatabase database,
        CapyProvisioningService provisioning,
        string? userId)
    {
        var model = new CustomisationModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            provisioning,
            Hooks(database.Context),
            new CapyWardrobeService(database.Context, provisioning));
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static ProgressionAchievementHooks Hooks(
        ApplicationDbContext context,
        ILogger<ProgressionAchievementHooks>? logger = null)
    {
        var timeProvider = new FixedTimeProvider(AuditTime);
        return new ProgressionAchievementHooks(
            new ProgressionService(
                context,
                new ProgressionLevelCalculator(),
                new ActivityStreakCalculator(),
                timeProvider),
            logger ?? new RecordingLogger<ProgressionAchievementHooks>());
    }

    private static Task EvaluateAsync(
        ProgressionAchievementHooks hooks,
        string category,
        string userId) => category switch
        {
            "Diary" => hooks.EvaluateDiaryAsync(userId),
            "Foods" => hooks.EvaluateFoodsAsync(userId),
            "SavedMeals" => hooks.EvaluateSavedMealsAsync(userId),
            "Profile" => hooks.EvaluateProfileAsync(userId),
            "Customisation" => hooks.EvaluateCustomisationAsync(userId),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    private static async Task SeedQualifyingEvidenceAsync(
        ApplicationDbContext context,
        string category)
    {
        switch (category)
        {
            case "Diary":
            {
                var food = await AddFoodAsync(context, "user-1");
                context.DiaryEntries.Add(Entry(
                    "user-1",
                    food,
                    new DateTime(2026, 9, 1),
                    "Breakfast"));
                break;
            }
            case "Foods":
                await AddFoodAsync(context, "user-1");
                break;
            case "SavedMeals":
                context.SavedMeals.Add(new SavedMeal
                {
                    UserId = "user-1",
                    Name = "Meal"
                });
                break;
            case "Profile":
            {
                var profile = CompleteProfile();
                profile.UserId = "user-1";
                context.UserProfiles.Add(profile);
                break;
            }
            case "Customisation":
                context.UserCapyAppearances.Add(new UserCapyAppearance
                {
                    UserId = "user-1",
                    ExpressionId = 1,
                    BackgroundId = 13,
                    HatHairId = 2
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(category));
        }

        await context.SaveChangesAsync();
    }

    private static async Task<RequestState> PrepareRequestAsync(
        IntegrationTestFactory factory,
        string category,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        switch (category)
        {
            case "Diary":
            {
                var food = await AddFoodAsync(context, userId);
                return new(
                    "/Diary/Create",
                    HttpStatusCode.Redirect,
                    new Dictionary<string, string>
                    {
                        ["DiaryEntry.Date"] = "2026-09-01",
                        ["DiaryEntry.MealType"] = "Breakfast",
                        ["DiaryEntry.FoodId"] = food.Id.ToString(),
                        ["DiaryEntry.Quantity"] = "100",
                        ["MeasurementMode"] = "Exact",
                        ["ApproximationSize"] = "Medium"
                    });
            }
            case "Foods":
                return new(
                    "/Foods/Create",
                    HttpStatusCode.Redirect,
                    new Dictionary<string, string>
                    {
                        ["Food.Name"] = "Manual food",
                        ["Food.Calories"] = "100",
                        ["Food.Protein"] = "1",
                        ["Food.Carbohydrates"] = "2",
                        ["Food.Fat"] = "3",
                        ["Food.ServingBasis"] = "Measured",
                        ["Food.ServingSize"] = "100",
                        ["Food.ServingUnit"] = "g"
                    });
            case "SavedMeals":
            {
                var food = await AddFoodAsync(context, userId);
                context.DiaryEntries.Add(Entry(
                    userId,
                    food,
                    new DateTime(2026, 9, 1),
                    "Breakfast"));
                await context.SaveChangesAsync();
                return new(
                    "/SavedMeals/Create",
                    HttpStatusCode.Redirect,
                    new Dictionary<string, string>
                    {
                        ["Input.Name"] = "Breakfast set",
                        ["Input.SourceDate"] = "2026-09-01",
                        ["Input.SourceMeal"] = "Breakfast"
                    });
            }
            case "Profile":
                return new(
                    "/Profile",
                    HttpStatusCode.Redirect,
                    new Dictionary<string, string>
                    {
                        ["UserProfile.MeasurementSystem"] =
                            ProfileOptions.Metric,
                        ["UserProfile.ThemePreference"] =
                            ProfileOptions.SystemTheme,
                        ["UserProfile.DateOfBirth"] = "1990-01-01",
                        ["UserProfile.HeightCm"] = "180",
                        ["UserProfile.WeightKg"] = "80",
                        ["UserProfile.CalculationSex"] = ProfileOptions.Male,
                        ["UserProfile.ActivityLevel"] =
                            ProfileOptions.Sedentary,
                        ["UserProfile.Goal"] = ProfileOptions.Maintain,
                        ["UseCustomCalorieTarget"] = "false"
                    });
            case "Customisation":
                return new(
                    "/Customisation?handler=Equip",
                    HttpStatusCode.OK,
                    new Dictionary<string, string>
                    {
                        ["itemId"] = "2",
                        ["category"] = CapyCategories.HatHair
                    });
            default:
                throw new ArgumentOutOfRangeException(nameof(category));
        }
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpClient client)
    {
        var form = await client.GetStringAsync("/Feedback");
        var token = Regex.Match(
            form,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return WebUtility.HtmlDecode(token.Groups[1].Value);
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

    private static async Task AddUserAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.Users.Add(User(userId));
        await context.SaveChangesAsync();
    }

    private static async Task<TestDatabase> UserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        return database;
    }

    private static async Task<TestDatabase> TwoUserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        return database;
    }

    private static ApplicationUser User(string userId) => new()
    {
        Id = userId,
        UserName = $"{userId}@example.test",
        NormalizedUserName = $"{userId}@example.test".ToUpperInvariant(),
        Email = $"{userId}@example.test",
        NormalizedEmail = $"{userId}@example.test".ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task<Food> AddFoodAsync(
        ApplicationDbContext context,
        string userId)
    {
        var food = ValidFood();
        food.UserId = userId;
        context.Foods.Add(food);
        await context.SaveChangesAsync();
        return food;
    }

    private static Food ValidFood() => new()
    {
        Name = $"Food {Guid.NewGuid():N}",
        Calories = 100,
        Protein = 1,
        Carbohydrates = 2,
        Fat = 3,
        ServingSize = 100,
        CanonicalServingSize = 100,
        ServingUnit = "g"
    };

    private static DiaryEntry Entry(
        string userId,
        Food food,
        DateTime date,
        string mealType)
    {
        var entry = TestData.DiaryEntry(userId, food, 100);
        entry.Date = date;
        entry.MealType = mealType;
        return entry;
    }

    private static UserProfile CompleteProfile() => new()
    {
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = new DateTime(1990, 1, 1),
        HeightCm = 180,
        WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };

    private static async Task<bool> HasAchievementAsync(
        ApplicationDbContext context,
        string userId,
        string key) => await context.UserAchievements.AnyAsync(item =>
        item.UserId == userId && item.AchievementKey == key);

    private static async Task AssertSingleAwardAsync(
        ApplicationDbContext context,
        string key)
    {
        Assert.Equal(1, await context.UserAchievements.CountAsync(item =>
            item.UserId == "user-1" && item.AchievementKey == key));
        Assert.Equal(1, await context.UserXpEvents.CountAsync(item =>
            item.UserId == "user-1" &&
            item.EventKey == $"achievement:{key}"));
    }

    private static async Task CreateFailureTriggerAsync(
        ApplicationDbContext context,
        string tableName,
        string triggerName)
    {
        var sql = (tableName, triggerName) switch
        {
            ("UserAchievements", "FailAchievementHook") =>
                FailureTrigger("FailAchievementHook", "UserAchievements"),
            ("UserAchievements", "FailFoodAchievement") =>
                FailureTrigger("FailFoodAchievement", "UserAchievements"),
            ("Foods", "FailCoreFood") =>
                FailureTrigger("FailCoreFood", "Foods"),
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static string FailureTrigger(string name, string table) =>
        $"""
        CREATE TRIGGER "{name}"
        BEFORE INSERT ON "{table}"
        BEGIN
            SELECT RAISE(FAIL, 'forced domain hook test failure');
        END;
        """;

    private sealed record RequestState(
        string Path,
        HttpStatusCode ExpectedStatus,
        Dictionary<string, string> Form);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Enqueue(formatter(state, exception));
    }
}
