using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Identity;

public sealed class AccountDeletionTests
{
    private const string DeletedUserId = "delete-user";
    private const string SurvivingUserId = "surviving-user";
    private const string Password = "Deletion1!Password";

    [Fact]
    public async Task DeletePersonalData_FullyPopulatedUserIsDeletedWithoutAffectingAnotherUser()
    {
        using var factory = new IntegrationTestFactory();
        await SeedPopulatedUserAsync(factory, DeletedUserId, "delete");
        await SeedPopulatedUserAsync(factory, SurvivingUserId, "survive");
        int communityFoodId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var setup = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var source = await setup.Foods.FirstAsync(food => food.UserId == DeletedUserId);
            var community = new CommunityFood
            {
                SourceFoodId = source.Id,
                SubmitterId = DeletedUserId,
                ReviewerId = SurvivingUserId,
                SubmittedUtc = new DateTime(2026, 9, 13, 6, 0, 0, DateTimeKind.Utc),
                ReviewedUtc = new DateTime(2026, 9, 13, 7, 0, 0, DateTimeKind.Utc),
                Status = CommunityFoodStatus.Approved,
                Name = source.Name,
                Calories = source.Calories,
                Protein = source.Protein,
                Carbohydrates = source.Carbohydrates,
                Fat = source.Fat,
                ServingSize = source.ServingSize,
                CanonicalServingSize = source.CanonicalServingSize,
                ServingUnit = source.ServingUnit,
                ServingBasis = source.ServingBasis
            };
            setup.CommunityFoods.Add(community);
            setup.CommunityFoodVotes.AddRange(
                new CommunityFoodVote { CommunityFood = community, UserId = DeletedUserId, Value = 1 },
                new CommunityFoodVote { CommunityFood = community, UserId = SurvivingUserId, Value = -1 });
            await setup.SaveChangesAsync();
            communityFoodId = community.Id;
        }
        using var client = CreateClient(factory, DeletedUserId);

        var getResponse = await client.GetAsync(
            "/Identity/Account/Manage/DeletePersonalData");
        var html = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Password"] = Password,
                ["__RequestVerificationToken"] = ExtractAntiforgeryToken(html)
            });
        var postResponse = await client.PostAsync(
            "/Identity/Account/Manage/DeletePersonalData",
            content);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/", postResponse.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.Users
            .AnyAsync(user => user.Id == DeletedUserId));
        Assert.False(await context.UserProfiles
            .AnyAsync(profile => profile.UserId == DeletedUserId));
        Assert.False(await context.DiaryEntries
            .AnyAsync(entry => entry.UserId == DeletedUserId));
        Assert.False(await context.DailyMaintenanceSnapshots
            .AnyAsync(snapshot => snapshot.UserId == DeletedUserId));
        Assert.False(await context.Foods
            .AnyAsync(food => food.UserId == DeletedUserId));
        Assert.False(await context.SavedMeals
            .AnyAsync(meal => meal.UserId == DeletedUserId));
        Assert.False(await context.UserCapyItems
            .AnyAsync(item => item.UserId == DeletedUserId));
        Assert.False(await context.UserCapyAppearances
            .AnyAsync(appearance => appearance.UserId == DeletedUserId));
        Assert.False(await context.UserDailyActivities
            .AnyAsync(activity => activity.UserId == DeletedUserId));
        Assert.False(await context.UserAchievements
            .AnyAsync(achievement => achievement.UserId == DeletedUserId));
        Assert.False(await context.UserXpEvents
            .AnyAsync(xpEvent => xpEvent.UserId == DeletedUserId));
        Assert.False(await context.UserProgressionStates
            .AnyAsync(state => state.UserId == DeletedUserId));
        var retainedCommunityFood = await context.CommunityFoods.SingleAsync(food => food.Id == communityFoodId);
        Assert.Null(retainedCommunityFood.SourceFoodId);
        Assert.Null(retainedCommunityFood.SubmitterId);
        Assert.Equal(SurvivingUserId, retainedCommunityFood.ReviewerId);
        Assert.DoesNotContain(await context.CommunityFoodVotes.ToListAsync(), vote => vote.UserId == DeletedUserId);
        Assert.Contains(await context.CommunityFoodVotes.ToListAsync(), vote => vote.UserId == SurvivingUserId);

        Assert.True(await context.Users
            .AnyAsync(user => user.Id == SurvivingUserId));
        Assert.True(await context.UserProfiles
            .AnyAsync(profile => profile.UserId == SurvivingUserId));
        Assert.True(await context.DiaryEntries
            .AnyAsync(entry => entry.UserId == SurvivingUserId));
        Assert.True(await context.DailyMaintenanceSnapshots
            .AnyAsync(snapshot => snapshot.UserId == SurvivingUserId));
        Assert.True(await context.Foods
            .AnyAsync(food => food.UserId == SurvivingUserId));
        Assert.True(await context.SavedMeals
            .AnyAsync(meal => meal.UserId == SurvivingUserId));
        Assert.True(await context.FoodPortions
            .AnyAsync(portion =>
                portion.Food!.UserId == SurvivingUserId));
        Assert.Equal(1, await context.FoodPortions.CountAsync());
        Assert.True(await context.UserCapyItems
            .AnyAsync(item => item.UserId == SurvivingUserId));
        Assert.True(await context.UserCapyAppearances
            .AnyAsync(appearance =>
                appearance.UserId == SurvivingUserId));
        Assert.True(await context.UserDailyActivities
            .AnyAsync(activity => activity.UserId == SurvivingUserId));
        Assert.True(await context.UserAchievements
            .AnyAsync(achievement => achievement.UserId == SurvivingUserId));
        Assert.True(await context.UserXpEvents
            .AnyAsync(xpEvent => xpEvent.UserId == SurvivingUserId));
        Assert.True(await context.UserProgressionStates
            .AnyAsync(state => state.UserId == SurvivingUserId));
    }

    [Fact]
    public async Task DeletePersonalData_WhenIdentityDeleteFails_RollsBackOwnedDataCleanup()
    {
        using var factory = new IntegrationTestFactory();
        await SeedPopulatedUserAsync(factory, DeletedUserId, "delete");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER RejectTestUserDeletion
                BEFORE DELETE ON AspNetUsers
                WHEN OLD.Id = 'delete-user'
                BEGIN
                    SELECT RAISE(ABORT, 'test deletion failure');
                END;
                """);
        }

        using var client = CreateClient(factory, DeletedUserId);
        var html = await client.GetStringAsync(
            "/Identity/Account/Manage/DeletePersonalData");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Password"] = Password,
                ["__RequestVerificationToken"] = ExtractAntiforgeryToken(html)
            });

        using var response = await client.PostAsync(
            "/Identity/Account/Manage/DeletePersonalData",
            content);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.True(await verificationContext.Users
            .AnyAsync(user => user.Id == DeletedUserId));
        Assert.True(await verificationContext.DiaryEntries
            .AnyAsync(entry => entry.UserId == DeletedUserId));
        Assert.True(await verificationContext.Foods
            .AnyAsync(food => food.UserId == DeletedUserId));
        Assert.True(await verificationContext.FoodPortions
            .AnyAsync(portion => portion.Food!.UserId == DeletedUserId));
        Assert.True(await verificationContext.UserXpEvents
            .AnyAsync(xpEvent => xpEvent.UserId == DeletedUserId));
    }

    private static HttpClient CreateClient(
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

    private static async Task SeedPopulatedUserAsync(
        IntegrationTestFactory factory,
        string userId,
        string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userName}@example.test",
            Email = $"{userName}@example.test",
            EmailConfirmed = true,
            FirstName = userName
        };

        Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);

        var food = TestData.Food(userId, name: $"{userName} food");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "bowl",
            Amount = 150
        };
        var diaryEntry = TestData.DiaryEntry(
            userId,
            food,
            quantity: 150,
            portion,
            portionQuantity: 1);
        var savedMealItem = DiarySnapshotFactory.ToSavedMealItem(diaryEntry);
        savedMealItem.Food = food;
        savedMealItem.FoodPortion = portion;
        var savedMeal = new SavedMeal
        {
            UserId = userId,
            Name = $"{userName} saved meal",
            Items = [savedMealItem]
        };
        context.AddRange(
            food,
            portion,
            diaryEntry,
            savedMeal,
            new UserProfile
            {
                UserId = userId,
                MeasurementSystem = ProfileOptions.Metric,
                DateOfBirth = new DateTime(1990, 1, 1),
                HeightCm = 170,
                WeightKg = 70,
                CalculationSex = ProfileOptions.Female,
                ActivityLevel = ProfileOptions.ModeratelyActive,
                Goal = ProfileOptions.Maintain
            },
            new DailyMaintenanceSnapshot(
                userId,
                new DateOnly(2026, 9, 5),
                2000),
            new UserCapyItem
            {
                UserId = userId,
                CapyItemId = 1
            },
            new UserCapyAppearance
            {
                UserId = userId,
                ExpressionId = 1,
                BackgroundId = 13
            },
            new UserDailyActivity
            {
                UserId = userId,
                LocalDate = new DateOnly(2026, 9, 13),
                RecordedAtUtc = new DateTime(
                    2026,
                    9,
                    13,
                    5,
                    0,
                    0,
                    DateTimeKind.Utc),
                TimeZoneId = "Europe/London"
            },
            new UserAchievement
            {
                UserId = userId,
                AchievementKey = "diary.first-entry",
                UnlockedAtUtc = new DateTime(
                    2026,
                    9,
                    13,
                    5,
                    0,
                    0,
                    DateTimeKind.Utc)
            },
            new UserXpEvent
            {
                UserId = userId,
                EventKey = "achievement:diary.first-entry",
                Amount = 25,
                AwardedAtUtc = new DateTime(
                    2026,
                    9,
                    13,
                    5,
                    0,
                    0,
                    DateTimeKind.Utc)
            },
            new UserProgressionState
            {
                UserId = userId,
                AchievementBackfillVersion = 0
            });
        await context.SaveChangesAsync();
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
