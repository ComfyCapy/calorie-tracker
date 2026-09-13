using CalorieTracker.Services;

namespace CalorieTracker.Tests.Progression;

public class AchievementDefinitionsTests
{
    private static readonly AchievementDefinition[] ExpectedDefinitions =
    [
        new(
            "diary.first-entry",
            "First Steps",
            "The first log is often the hardest.",
            25),
        new(
            "profile.completed",
            "Getting Comfy",
            "Your space is starting to feel like home.",
            25),
        new(
            "foods.first-custom",
            "Made It Mine",
            "A food saved your way.",
            30),
        new(
            "saved-meals.first",
            "Meal Prep-ish",
            "Future you will appreciate this.",
            30),
        new(
            "customisation.first-equip",
            "Looking Good",
            "A little personal touch.",
            20),
        new(
            "activity.distinct-3",
            "Hello Again",
            "Three different days, nice and easy.",
            20),
        new(
            "activity.distinct-7",
            "Getting Settled",
            "Seven days spent making the app yours.",
            40),
        new(
            "activity.streak-7",
            "Comfy Week",
            "Seven local calendar days in a row.",
            50),
        new(
            "diary.distinct-days-30",
            "Regular",
            "A month's worth of Diary days.",
            100),
        new(
            "diary.all-meal-types",
            "Full Plate",
            "Breakfast, lunch, dinner and something in between.",
            50)
    ];

    [Fact]
    public void All_ContainsEachApprovedDefinitionExactlyOnce()
    {
        Assert.Equal(ExpectedDefinitions, AchievementDefinitions.All);
    }

    [Fact]
    public void All_HasUniqueStableKeys()
    {
        Assert.Equal(
            AchievementDefinitions.All.Count,
            AchievementDefinitions.All
                .Select(definition => definition.Key)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void All_HasPositiveXpRewards()
    {
        Assert.All(
            AchievementDefinitions.All,
            definition => Assert.True(definition.XpReward > 0));
    }
}
