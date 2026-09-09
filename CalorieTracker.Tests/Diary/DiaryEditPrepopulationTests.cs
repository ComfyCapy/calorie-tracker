using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DiaryEditModel = CalorieTracker.Pages.Diary.EditModel;

namespace CalorieTracker.Tests.Diary;

public class DiaryEditPrepopulationTests
{
    [Fact]
    public async Task EditGet_MeasuredEntryRendersFoodQuantityUnitDateAndMeal()
    {
        const string userId = "measured-edit-user";
        using var factory = new IntegrationTestFactory();
        var entryId = await SeedEntryAsync(
            factory,
            userId,
            MeasuredFood(userId),
            150m,
            "Lunch");
        using var client = AuthenticatedClient(factory, userId);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/Diary/Edit?id={entryId}"));

        Assert.Matches(
            new Regex(
                "<input(?=[^>]*id=\"foodSearch\")(?=[^>]*value=\"Chicken Breast\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.DoesNotContain(
            "id=\"measurementFields\" class=\"form-section\" hidden",
            html);
        Assert.DoesNotContain(
            "id=\"exactQuantitySection\" class=\"mb-3 mt-3\" style=\"display: none;\"",
            html);
        Assert.Matches(
            new Regex(
                "<input(?=[^>]*id=\"DiaryEntry_Quantity\")(?=[^>]*value=\"150(?:\\.0+)?\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.Matches(">g</span>", html);
        Assert.Contains(TestTime.Today.ToString("yyyy-MM-dd"), html);
        Assert.Matches(
            new Regex(
                "<option(?=[^>]*value=\"Lunch\")(?=[^>]*selected)[^>]*>\\s*Lunch\\s*</option>",
                RegexOptions.Singleline),
            html);
    }

    [Fact]
    public async Task EditGet_DirectPortionEntryRendersFoodCountAndPortionLabel()
    {
        const string userId = "direct-portion-edit-user";
        using var factory = new IntegrationTestFactory();
        var food = MeasuredFood(userId);
        food.Name = "Sandwich";
        food.ServingBasis = FoodServingBasis.Portion;
        food.ServingSize = 1;
        food.CanonicalServingSize = 1;
        food.PortionLabel = "sandwich";
        var entryId = await SeedEntryAsync(
            factory,
            userId,
            food,
            0.5m,
            "Dinner");
        using var client = AuthenticatedClient(factory, userId);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/Diary/Edit?id={entryId}"));

        Assert.Matches(
            new Regex(
                "<input(?=[^>]*id=\"foodSearch\")(?=[^>]*value=\"Sandwich\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.DoesNotContain(
            "id=\"measurementFields\" class=\"form-section\" hidden",
            html);
        Assert.Matches(
            new Regex(
                "<div(?=[^>]*id=\"measurementModeSection\")(?=[^>]*style=\"display: none;\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.Matches(
            new Regex(
                "<input(?=[^>]*id=\"DiaryEntry_Quantity\")(?=[^>]*value=\"0\\.5(?:0+)?\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.Contains(">sandwich</span>", html);
    }

    [Fact]
    public async Task EditGet_MeasuredPortionEntryRendersPortionCountAndSnapshotLabel()
    {
        const string userId = "measured-portion-edit-user";
        using var factory = new IntegrationTestFactory();
        int entryId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            AddUser(context, userId);
            var food = MeasuredFood(userId);
            var portion = new FoodPortion
            {
                Food = food,
                Name = "1 scoop",
                Amount = 30m
            };
            context.AddRange(food, portion);
            await context.SaveChangesAsync();
            var entry = TestData.DiaryEntry(
                userId,
                food,
                60m,
                portion,
                2m);
            context.DiaryEntries.Add(entry);
            await context.SaveChangesAsync();
            portion.Name = "Renamed scoop";
            await context.SaveChangesAsync();
            entryId = entry.Id;
        }

        using var client = AuthenticatedClient(factory, userId);
        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/Diary/Edit?id={entryId}"));

        Assert.DoesNotContain(
            "id=\"measurementFields\" class=\"form-section\" hidden",
            html);
        Assert.DoesNotContain(
            "id=\"portionSection\" class=\"mb-3 mt-3\" style=\"display: none;\"",
            html);
        Assert.Matches(
            new Regex(
                "<input(?=[^>]*id=\"PortionQuantity\")(?=[^>]*value=\"2(?:\\.0+)?\")[^>]*>",
                RegexOptions.Singleline),
            html);
        Assert.Contains("1 scoop", html);
        Assert.Contains("data-initial-portion-name=\"1 scoop\"", html);
        Assert.Contains("2 × 1 scoop", html);
    }

    [Fact]
    public async Task EditGet_AnotherUsersEntryReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        var food = MeasuredFood("user-2");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-2", food, 100m);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1");

        var result = await model.OnGetAsync(entry.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPost_ReplacingFoodCapturesServerAuthoritativeNutritionSnapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var original = MeasuredFood("user-1");
        var replacement = MeasuredFood("user-1");
        replacement.Name = "Replacement food";
        replacement.Calories = 321;
        replacement.Protein = 22m;
        replacement.Carbohydrates = 33m;
        replacement.Fat = 11m;
        database.Context.Foods.AddRange(original, replacement);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", original, 100m);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1");
        model.DiaryEntry = new DiaryEntry
        {
            Date = entry.Date,
            MealType = entry.MealType,
            FoodId = replacement.Id,
            Quantity = 100m,
            FoodNameSnapshot = "Client supplied name",
            CaloriesSnapshot = 9999,
            ProteinSnapshot = 9999,
            CarbohydratesSnapshot = 9999,
            FatSnapshot = 9999
        };

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(replacement.Id, entry.FoodId);
        Assert.Equal("Replacement food", entry.FoodNameSnapshot);
        Assert.Equal(321, entry.CaloriesSnapshot);
        Assert.Equal(22m, entry.ProteinSnapshot);
        Assert.Equal(33m, entry.CarbohydratesSnapshot);
        Assert.Equal(11m, entry.FatSnapshot);
    }

    private static async Task<int> SeedEntryAsync(
        IntegrationTestFactory factory,
        string userId,
        Food food,
        decimal quantity,
        string meal)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        AddUser(context, userId);
        context.Foods.Add(food);
        await context.SaveChangesAsync();
        var entry = TestData.DiaryEntry(userId, food, quantity);
        entry.Date = TestTime.Today.ToDateTime(TimeOnly.MinValue);
        entry.MealType = meal;
        context.DiaryEntries.Add(entry);
        await context.SaveChangesAsync();
        return entry.Id;
    }

    private static Food MeasuredFood(string userId) => new()
    {
        UserId = userId,
        Name = "Chicken Breast",
        Calories = 165,
        Protein = 31m,
        Carbohydrates = 0m,
        Fat = 3.6m,
        ServingSize = 100m,
        ServingBasis = FoodServingBasis.Measured,
        ServingUnit = "g",
        CanonicalServingSize = 100m
    };

    private static DiaryEditModel CreateEditModel(
        TestDatabase database,
        string userId)
    {
        var model = new DiaryEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context));
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static void AddUser(ApplicationDbContext context, string userId)
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
}
