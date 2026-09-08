using CalorieTracker.Services;

namespace CalorieTracker.Models;

public sealed record CalorieBalanceYear(
    int Year,
    IReadOnlyList<CalorieBalanceDay> Days);

public sealed record CalorieBalanceDay(
    DateOnly Date,
    bool HasDiaryData,
    decimal? CaloriesConsumed,
    decimal? MaintenanceCalories,
    decimal? Difference,
    decimal? BalancePercentage,
    CalorieBalanceClassification? Classification);
