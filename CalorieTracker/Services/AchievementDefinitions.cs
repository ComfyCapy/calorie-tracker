using System.Collections.ObjectModel;

namespace CalorieTracker.Services;

public sealed record AchievementDefinition(
    string Key,
    string DisplayName,
    string Description,
    int XpReward);

public static class AchievementDefinitions
{
    public const string DiaryFirstEntryKey = "diary.first-entry";
    public const string ProfileCompletedKey = "profile.completed";
    public const string FoodsFirstCustomKey = "foods.first-custom";
    public const string SavedMealsFirstKey = "saved-meals.first";
    public const string CustomisationFirstEquipKey =
        "customisation.first-equip";
    public const string ActivityDistinctThreeKey = "activity.distinct-3";
    public const string ActivityDistinctSevenKey = "activity.distinct-7";
    public const string ActivityStreakSevenKey = "activity.streak-7";
    public const string DiaryDistinctDaysThirtyKey =
        "diary.distinct-days-30";
    public const string DiaryAllMealTypesKey = "diary.all-meal-types";

    public static IReadOnlyList<AchievementDefinition> All { get; } =
        new ReadOnlyCollection<AchievementDefinition>(
        [
            new(
                DiaryFirstEntryKey,
                "First Steps",
                "The first log is often the hardest.",
                25),
            new(
                ProfileCompletedKey,
                "Getting Comfy",
                "Your space is starting to feel like home.",
                25),
            new(
                FoodsFirstCustomKey,
                "Made It Mine",
                "A food saved your way.",
                30),
            new(
                SavedMealsFirstKey,
                "Meal Prep-ish",
                "Future you will appreciate this.",
                30),
            new(
                CustomisationFirstEquipKey,
                "Looking Good",
                "A little personal touch.",
                20),
            new(
                ActivityDistinctThreeKey,
                "Hello Again",
                "Three different days, nice and easy.",
                20),
            new(
                ActivityDistinctSevenKey,
                "Getting Settled",
                "Seven days spent making the app yours.",
                40),
            new(
                ActivityStreakSevenKey,
                "Comfy Week",
                "Seven local calendar days in a row.",
                50),
            new(
                DiaryDistinctDaysThirtyKey,
                "Regular",
                "A month's worth of Diary days.",
                100),
            new(
                DiaryAllMealTypesKey,
                "Full Plate",
                "Breakfast, lunch, dinner and something in between.",
                50)
        ]);
}
