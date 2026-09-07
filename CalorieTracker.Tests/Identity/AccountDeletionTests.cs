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
        Assert.False(await context.Foods
            .AnyAsync(food => food.UserId == DeletedUserId));
        Assert.False(await context.UserCapyItems
            .AnyAsync(item => item.UserId == DeletedUserId));
        Assert.False(await context.UserCapyAppearances
            .AnyAsync(appearance => appearance.UserId == DeletedUserId));

        Assert.True(await context.Users
            .AnyAsync(user => user.Id == SurvivingUserId));
        Assert.True(await context.UserProfiles
            .AnyAsync(profile => profile.UserId == SurvivingUserId));
        Assert.True(await context.DiaryEntries
            .AnyAsync(entry => entry.UserId == SurvivingUserId));
        Assert.True(await context.Foods
            .AnyAsync(food => food.UserId == SurvivingUserId));
        Assert.True(await context.FoodPortions
            .AnyAsync(portion =>
                portion.Food!.UserId == SurvivingUserId));
        Assert.Equal(1, await context.FoodPortions.CountAsync());
        Assert.True(await context.UserCapyItems
            .AnyAsync(item => item.UserId == SurvivingUserId));
        Assert.True(await context.UserCapyAppearances
            .AnyAsync(appearance =>
                appearance.UserId == SurvivingUserId));
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

        context.AddRange(
            food,
            portion,
            diaryEntry,
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
