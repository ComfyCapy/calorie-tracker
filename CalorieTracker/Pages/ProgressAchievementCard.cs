using CalorieTracker.Services;

namespace CalorieTracker.Pages;

public sealed record ProgressAchievementCard(
    string Key,
    string DisplayName,
    string Description,
    int XpReward,
    string Symbol,
    bool IsUnlocked,
    DateTime? UnlockedAtUtc);

public static class ProgressAchievementVisuals
{
    public const string FallbackSymbol = "•";

    public static string SymbolFor(string achievementKey) =>
        achievementKey switch
        {
            AchievementDefinitions.DiaryFirstEntryKey => "✓",
            AchievementDefinitions.ProfileCompletedKey => "⌂",
            AchievementDefinitions.FoodsFirstCustomKey => "✦",
            AchievementDefinitions.SavedMealsFirstKey => "▣",
            AchievementDefinitions.CustomisationFirstEquipKey => "★",
            AchievementDefinitions.ActivityDistinctThreeKey => "3",
            AchievementDefinitions.ActivityDistinctSevenKey => "7",
            AchievementDefinitions.ActivityStreakSevenKey => "♨",
            AchievementDefinitions.DiaryDistinctDaysThirtyKey => "30",
            AchievementDefinitions.DiaryAllMealTypesKey => "◉",
            _ => FallbackSymbol
        };
}
