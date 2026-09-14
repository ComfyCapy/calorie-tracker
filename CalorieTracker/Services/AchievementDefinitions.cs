using System.Collections.ObjectModel;

namespace CalorieTracker.Services;

public sealed record AchievementDefinition(
    string Key,
    string DisplayName,
    string Lore,
    string HowToGet,
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
                "Log your first food in the Diary.",
                25),
            new(
                ProfileCompletedKey,
                "Getting Comfy",
                "Your space is starting to feel like home.",
                "Complete your profile.",
                25),
            new(
                FoodsFirstCustomKey,
                "Made It Mine",
                "A food saved your way.",
                "Create your first custom food.",
                30),
            new(
                SavedMealsFirstKey,
                "Meal Prep-ish",
                "Future you will appreciate this.",
                "Create your first saved meal.",
                30),
            new(
                CustomisationFirstEquipKey,
                "Looking Good",
                "A little personal touch.",
                "Equip your first customisation.",
                20),
            new(
                ActivityDistinctThreeKey,
                "Hello Again",
                "Three different days, nice and easy.",
                "Use Comfy Capy on 3 different days.",
                20),
            new(
                ActivityDistinctSevenKey,
                "Getting Settled",
                "Seven days spent making the app yours.",
                "Use Comfy Capy on 7 different days.",
                40),
            new(
                ActivityStreakSevenKey,
                "Comfy Week",
                "Seven local calendar days in a row.",
                "Use Comfy Capy for 7 days in a row.",
                50),
            new(
                DiaryDistinctDaysThirtyKey,
                "Regular",
                "A month's worth of Diary days.",
                "Log food in the Diary on 30 different days.",
                100),
            new(
                DiaryAllMealTypesKey,
                "Full Plate",
                "Breakfast, lunch, dinner and something in between.",
                "Log food in the Diary for Breakfast, Lunch, Dinner, and Snack.",
                50)
        ]);
}
