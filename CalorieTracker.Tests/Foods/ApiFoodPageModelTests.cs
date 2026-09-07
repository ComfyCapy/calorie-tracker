using System.Net;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages.Foods;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Foods;

public class ApiFoodPageModelTests
{
    [Fact]
    public async Task MyFoods_CachedExternalCardLinksDirectlyToDiaryCreate()
    {
        using var factory = new IntegrationTestFactory();
        int foodId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            context.Users.Add(new ApplicationUser
            {
                Id = "user-1",
                UserName = "first",
                NormalizedUserName = "FIRST",
                Email = "first@example.test",
                NormalizedEmail = "FIRST@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var food = TestData.Food("user-1", name: "Cinnamon roll");
            food.Source = FoodSources.Usda;
            food.ExternalId = "123";
            food.IsFavourite = true;
            context.Foods.Add(food);
            await context.SaveChangesAsync();
            foodId = food.Id;
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "user-1");

        var response = await client.GetAsync(
            "/Foods/Index?returnToDiary=true&diaryDate=2026-09-07&diaryMeal=Lunch");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/Diary/Create?foodId={foodId}", html);
        Assert.Contains("date=2026-09-07", html);
        Assert.Contains("meal=Lunch", html);
        Assert.DoesNotContain("/Foods/ApiFood", html);
    }

    [Fact]
    public async Task Post_SavesResolvedFoodAndHandsOffDiaryContext()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var foodResult = TestData.UsdaResult(name: "Cinnamon roll");
        foodResult.Portions =
        [
            new FoodPortionCandidate("1 roll", 75)
        ];
        var service = new FakeFoodSearchService
        {
            GetHandler = _ => Task.FromResult<FoodSearchResult?>(foodResult)
        };
        var resolver = new ExternalFoodResolver(database.Context, service);
        var model = new ApiFoodModel(
            resolver,
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            ExternalId = "123",
            SearchTerm = "cinnamon",
            Date = new DateTime(2026, 9, 5),
            MealType = "Lunch"
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Diary/Create", redirect.PageName);
        var food = await database.Context.Foods
            .Include(candidate => candidate.Portions)
            .SingleAsync();
        Assert.Equal(food.Id, redirect.RouteValues!["foodId"]);
        Assert.Equal("2026-09-05", redirect.RouteValues["date"]);
        Assert.Equal("Lunch", redirect.RouteValues["meal"]);
        Assert.Equal(true, redirect.RouteValues["returnToFoodSearch"]);
        Assert.Equal("cinnamon", redirect.RouteValues["foodSearchTerm"]);
        Assert.Equal("1 roll", Assert.Single(food.Portions).Name);
        Assert.Empty(database.Context.DiaryEntries);
    }
}
