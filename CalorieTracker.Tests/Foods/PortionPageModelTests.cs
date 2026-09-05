using CalorieTracker.Models;
using CalorieTracker.Pages.Foods;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Foods;

public class PortionPageModelTests
{
    [Fact]
    public async Task AddPortion_ConvertsPreferredDisplayAmountToCanonicalAmount()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food(
            "user-1",
            "oz",
            1,
            28.349523125m);
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = CreateModel(database, "user-1");
        model.NewPortion = new FoodPortion
        {
            Name = "double serving",
            Amount = 2
        };

        var result = await model.OnPostAsync(food.Id);

        Assert.IsType<RedirectToPageResult>(result);
        var portion = await database.Context.FoodPortions.SingleAsync();
        Assert.Equal(56.69904625m, portion.Amount, 8);
        Assert.Equal(food.Id, portion.FoodId);
    }

    [Fact]
    public async Task DeletePortion_AnotherUsersFoodReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var food = TestData.Food("user-2");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "serving",
            Amount = 50
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateModel(database, "user-1");

        var result = await model.OnPostDeleteAsync(food.Id, portion.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(portion.IsDeleted);
    }

    [Fact]
    public async Task EditPortion_WithMismatchedFoodIdReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1", name: "First");
        var otherFood = TestData.Food("user-1", name: "Second");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "serving",
            Amount = 50
        };
        database.Context.AddRange(food, otherFood, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateModel(database, "user-1");

        var result = await model.OnPostEditAsync(
            otherFood.Id,
            portion.Id,
            "changed",
            20);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("serving", portion.Name);
    }

    private static PortionsModel CreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new PortionsModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(model, userId);
        return model;
    }
}
