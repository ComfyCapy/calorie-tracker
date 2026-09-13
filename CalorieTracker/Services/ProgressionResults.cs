namespace CalorieTracker.Services;

public sealed record DailyActivityRecordResult(
    bool WasRecorded,
    IReadOnlyList<AchievementDefinition> NewlyUnlockedAchievements);

public sealed record AchievementGrantResult(
    bool WasGranted,
    AchievementDefinition Achievement);

public sealed record ProgressionEvaluationResult(
    IReadOnlyList<AchievementDefinition> NewlyUnlockedAchievements);

public sealed record AchievementReconciliationResult(
    int PreviousVersion,
    int CurrentVersion,
    bool WasReconciled,
    IReadOnlyList<AchievementDefinition> NewlyUnlockedAchievements);

public sealed record UnlockedAchievementSummary(
    string Key,
    DateTime UnlockedAtUtc,
    AchievementDefinition? Definition);

public sealed record ProgressionSummary(
    int TotalXp,
    ProgressionLevelResult Level,
    ActivityStreakResult Activity,
    IReadOnlyList<UnlockedAchievementSummary> UnlockedAchievements);
