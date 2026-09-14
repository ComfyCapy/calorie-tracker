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
            "Log your first food in the Diary.",
            25),
        new(
            "profile.completed",
            "Getting Comfy",
            "Your space is starting to feel like home.",
            "Complete your profile.",
            25),
        new(
            "foods.first-custom",
            "Made It Mine",
            "A food saved your way.",
            "Create your first custom food.",
            30),
        new(
            "saved-meals.first",
            "Meal Prep-ish",
            "Future you will appreciate this.",
            "Create your first saved meal.",
            30),
        new(
            "customisation.first-equip",
            "Looking Good",
            "A little personal touch.",
            "Equip your first customisation.",
            20),
        new(
            "activity.distinct-3",
            "Hello Again",
            "Three different days, nice and easy.",
            "Use Comfy Capy on 3 different days.",
            20),
        new(
            "activity.distinct-7",
            "Getting Settled",
            "Seven days spent making the app yours.",
            "Use Comfy Capy on 7 different days.",
            40),
        new(
            "activity.streak-7",
            "Comfy Week",
            "Seven local calendar days in a row.",
            "Use Comfy Capy for 7 days in a row.",
            50),
        new(
            "diary.distinct-days-30",
            "Regular",
            "A month's worth of Diary days.",
            "Log food in the Diary on 30 different days.",
            100),
        new(
            "diary.all-meal-types",
            "Full Plate",
            "Breakfast, lunch, dinner and something in between.",
            "Log food in the Diary for Breakfast, Lunch, Dinner, and Snack.",
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

    [Fact]
    public void All_HaveNonEmptyLoreAndHowToGetMetadata()
    {
        Assert.All(
            AchievementDefinitions.All,
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Lore));
                Assert.False(string.IsNullOrWhiteSpace(definition.HowToGet));
            });
    }
}
