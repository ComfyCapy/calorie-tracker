using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public sealed class ProgressionService
{
    public const int DailyActivityXp = 5;

    private const string DailyEventPrefix = "daily:";
    private const string AchievementEventPrefix = "achievement:";

    private static readonly IReadOnlyDictionary<string, AchievementDefinition>
        DefinitionsByKey = AchievementDefinitions.All.ToDictionary(
            definition => definition.Key,
            StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly ProgressionLevelCalculator _levelCalculator;
    private readonly ActivityStreakCalculator _streakCalculator;
    private readonly TimeProvider _timeProvider;

    public ProgressionService(
        ApplicationDbContext context,
        ProgressionLevelCalculator levelCalculator,
        ActivityStreakCalculator streakCalculator,
        TimeProvider timeProvider)
    {
        _context = context;
        _levelCalculator = levelCalculator;
        _streakCalculator = streakCalculator;
        _timeProvider = timeProvider;
    }

    public async Task<DailyActivityRecordResult> RecordDailyActivityAsync(
        string userId,
        DateOnly localDate,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateTimeZoneId(timeZoneId);

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
        var recordedAtUtc = UtcNow();
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT OR IGNORE INTO "UserDailyActivities"
                ("UserId", "LocalDate", "RecordedAtUtc", "TimeZoneId")
            VALUES
                ({userId}, {localDate}, {recordedAtUtc}, {timeZoneId});
            """,
            cancellationToken);

        if (inserted == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new DailyActivityRecordResult(false, []);
        }

        await InsertXpEventAsync(
            userId,
            DailyEventPrefix + localDate.ToString("yyyy-MM-dd"),
            DailyActivityXp,
            recordedAtUtc,
            cancellationToken);

        var activityKeys = await QualifiedActivityAchievementKeysAsync(
            userId,
            localDate,
            cancellationToken);
        var unlocked = await GrantAchievementKeysCoreAsync(
            userId,
            activityKeys,
            recordedAtUtc,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new DailyActivityRecordResult(true, unlocked);
    }

    public async Task<AchievementGrantResult> GrantAchievementAsync(
        string userId,
        string achievementKey,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var definition = Definition(achievementKey);

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
        var wasGranted = await GrantAchievementCoreAsync(
            userId,
            definition,
            UtcNow(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AchievementGrantResult(wasGranted, definition);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateDiaryAchievementsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var qualifiedKeys = new List<string>();
        var ownedEntries = _context.DiaryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId);

        if (await ownedEntries.AnyAsync(cancellationToken))
        {
            qualifiedKeys.Add(AchievementDefinitions.DiaryFirstEntryKey);
        }

        var distinctDayCount = await ownedEntries
            .Select(entry => entry.Date.Date)
            .Distinct()
            .CountAsync(cancellationToken);

        if (distinctDayCount >= 30)
        {
            qualifiedKeys.Add(
                AchievementDefinitions.DiaryDistinctDaysThirtyKey);
        }

        var mealTypes = await ownedEntries
            .Select(entry => entry.MealType)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (ValidationRules.MealTypes.IsSubsetOf(mealTypes))
        {
            qualifiedKeys.Add(AchievementDefinitions.DiaryAllMealTypesKey);
        }

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateProfileAchievementsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId,
                cancellationToken);
        string[] qualifiedKeys = profile != null &&
            IsStructurallyComplete(profile)
                ? [AchievementDefinitions.ProfileCompletedKey]
                : [];

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateFoodAchievementsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var hasOwnedCustomFood = await _context.Foods
            .AsNoTracking()
            .AnyAsync(
                food =>
                    food.UserId == userId &&
                    food.Source == null &&
                    food.ExternalId == null,
                cancellationToken);
        string[] qualifiedKeys = hasOwnedCustomFood
            ? [AchievementDefinitions.FoodsFirstCustomKey]
            : [];

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateSavedMealAchievementsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var hasOwnedSavedMeal = await _context.SavedMeals
            .AsNoTracking()
            .AnyAsync(
                meal => meal.UserId == userId,
                cancellationToken);
        string[] qualifiedKeys = hasOwnedSavedMeal
            ? [AchievementDefinitions.SavedMealsFirstKey]
            : [];

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateCustomisationAchievementsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var appearance = await _context.UserCapyAppearances
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId)
            .Select(candidate => new
            {
                candidate.ExpressionId,
                candidate.HatHairId,
                candidate.FaceAccessoryId,
                candidate.NeckAccessoryId,
                candidate.ClothesId,
                candidate.BackgroundId
            })
            .SingleOrDefaultAsync(cancellationToken);

        var qualifies = false;

        if (appearance != null)
        {
            qualifies =
                appearance.HatHairId.HasValue ||
                appearance.FaceAccessoryId.HasValue ||
                appearance.NeckAccessoryId.HasValue ||
                appearance.ClothesId.HasValue;

            if (!qualifies && appearance.ExpressionId.HasValue)
            {
                var defaultExpressionIds = await DefaultCapyItemIdsAsync(
                    CapyCategories.Expression,
                    cancellationToken);
                qualifies = defaultExpressionIds.Count > 0 &&
                    !defaultExpressionIds.Contains(
                        appearance.ExpressionId.Value);
            }

            if (!qualifies && appearance.BackgroundId.HasValue)
            {
                var defaultBackgroundIds = await DefaultCapyItemIdsAsync(
                    CapyCategories.Background,
                    cancellationToken);
                qualifies = defaultBackgroundIds.Count > 0 &&
                    !defaultBackgroundIds.Contains(
                        appearance.BackgroundId.Value);
            }
        }

        string[] qualifiedKeys = qualifies
            ? [AchievementDefinitions.CustomisationFirstEquipKey]
            : [];

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionEvaluationResult>
        EvaluateActivityAchievementsAsync(
            string userId,
            DateOnly localToday,
            CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var qualifiedKeys = await QualifiedActivityAchievementKeysAsync(
            userId,
            localToday,
            cancellationToken);

        return await GrantQualifiedAchievementsAsync(
            userId,
            qualifiedKeys,
            cancellationToken);
    }

    public async Task<ProgressionSummary> GetSummaryAsync(
        string userId,
        DateOnly localToday,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var totalXp = await _context.UserXpEvents
            .AsNoTracking()
            .Where(xpEvent => xpEvent.UserId == userId)
            .Select(xpEvent => (int?)xpEvent.Amount)
            .SumAsync(cancellationToken) ?? 0;
        var activityDates = await ActivityDatesAsync(
            userId,
            cancellationToken);
        var unlockedRows = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .OrderBy(achievement => achievement.UnlockedAtUtc)
            .ThenBy(achievement => achievement.AchievementKey)
            .ToArrayAsync(cancellationToken);
        var unlocked = unlockedRows
            .Select(row => new UnlockedAchievementSummary(
                row.AchievementKey,
                row.UnlockedAtUtc,
                DefinitionsByKey.GetValueOrDefault(row.AchievementKey)))
            .ToArray();

        return new ProgressionSummary(
            totalXp,
            _levelCalculator.Calculate(totalXp),
            _streakCalculator.Calculate(activityDates, localToday),
            unlocked);
    }

    private async Task<ProgressionEvaluationResult>
        GrantQualifiedAchievementsAsync(
            string userId,
            IReadOnlyCollection<string> achievementKeys,
            CancellationToken cancellationToken)
    {
        if (achievementKeys.Count == 0)
        {
            return new ProgressionEvaluationResult([]);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
        var unlocked = await GrantAchievementKeysCoreAsync(
            userId,
            achievementKeys,
            UtcNow(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProgressionEvaluationResult(unlocked);
    }

    private async Task<IReadOnlyList<AchievementDefinition>>
        GrantAchievementKeysCoreAsync(
            string userId,
            IEnumerable<string> achievementKeys,
            DateTime unlockedAtUtc,
            CancellationToken cancellationToken)
    {
        var unlocked = new List<AchievementDefinition>();

        foreach (var achievementKey in achievementKeys)
        {
            var definition = Definition(achievementKey);

            if (await GrantAchievementCoreAsync(
                    userId,
                    definition,
                    unlockedAtUtc,
                    cancellationToken))
            {
                unlocked.Add(definition);
            }
        }

        return unlocked;
    }

    private async Task<bool> GrantAchievementCoreAsync(
        string userId,
        AchievementDefinition definition,
        DateTime unlockedAtUtc,
        CancellationToken cancellationToken)
    {
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT OR IGNORE INTO "UserAchievements"
                ("UserId", "AchievementKey", "UnlockedAtUtc")
            VALUES
                ({userId}, {definition.Key}, {unlockedAtUtc});
            """,
            cancellationToken);

        if (inserted == 0)
        {
            return false;
        }

        await InsertXpEventAsync(
            userId,
            AchievementEventPrefix + definition.Key,
            definition.XpReward,
            unlockedAtUtc,
            cancellationToken);
        return true;
    }

    private async Task InsertXpEventAsync(
        string userId,
        string eventKey,
        int amount,
        DateTime awardedAtUtc,
        CancellationToken cancellationToken)
    {
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "UserXpEvents"
                ("UserId", "EventKey", "Amount", "AwardedAtUtc")
            VALUES
                ({userId}, {eventKey}, {amount}, {awardedAtUtc});
            """,
            cancellationToken);

        if (inserted != 1)
        {
            throw new InvalidOperationException(
                $"XP event '{eventKey}' was not inserted.");
        }
    }

    private async Task<IReadOnlyList<string>>
        QualifiedActivityAchievementKeysAsync(
            string userId,
            DateOnly localToday,
            CancellationToken cancellationToken)
    {
        var activityDates = await ActivityDatesAsync(
            userId,
            cancellationToken);
        var activity = _streakCalculator.Calculate(
            activityDates,
            localToday);
        var qualifiedKeys = new List<string>();

        if (activity.DistinctActiveDayCount >= 3)
        {
            qualifiedKeys.Add(AchievementDefinitions.ActivityDistinctThreeKey);
        }

        if (activity.DistinctActiveDayCount >= 7)
        {
            qualifiedKeys.Add(AchievementDefinitions.ActivityDistinctSevenKey);
        }

        if (activity.LongestStreak >= 7)
        {
            qualifiedKeys.Add(AchievementDefinitions.ActivityStreakSevenKey);
        }

        return qualifiedKeys;
    }

    private async Task<DateOnly[]> ActivityDatesAsync(
        string userId,
        CancellationToken cancellationToken) =>
        await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId)
            .Select(activity => activity.LocalDate)
            .ToArrayAsync(cancellationToken);

    private async Task<HashSet<int>> DefaultCapyItemIdsAsync(
        string category,
        CancellationToken cancellationToken) =>
        await _context.CapyItems
            .AsNoTracking()
            .Where(item => item.Category == category && item.IsDefault)
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);

    private static AchievementDefinition Definition(string achievementKey)
    {
        if (string.IsNullOrWhiteSpace(achievementKey))
        {
            throw new ArgumentException(
                "Achievement key is required.",
                nameof(achievementKey));
        }

        if (!DefinitionsByKey.TryGetValue(achievementKey, out var definition))
        {
            throw new ArgumentException(
                $"Unknown achievement key '{achievementKey}'.",
                nameof(achievementKey));
        }

        return definition;
    }

    private static bool IsStructurallyComplete(UserProfile profile)
    {
        var hasValidGoalFields = profile.Goal == ProfileOptions.Maintain ||
            (profile.Goal is ProfileOptions.Lose or ProfileOptions.Gain &&
             profile.GoalWeightKg is >= 20 and <= 500 &&
             profile.WeeklyGoalKg.HasValue &&
             ProfileOptions.WeeklyGoals.Contains(
                 profile.WeeklyGoalKg.Value));
        var hasValidCustomTarget = !profile.CustomCalorieTarget.HasValue ||
            profile.CustomCalorieTarget.Value is >= 500 and <= 10000;

        return profile.MeasurementSystem is
                ProfileOptions.Metric or ProfileOptions.Imperial &&
            profile.ThemePreference is
                ProfileOptions.SystemTheme or
                ProfileOptions.LightTheme or
                ProfileOptions.DarkTheme &&
            profile.DateOfBirth.HasValue &&
            profile.HeightCm is >= 50 and <= 300 &&
            profile.WeightKg is >= 20 and <= 500 &&
            profile.CalculationSex is
                ProfileOptions.Male or ProfileOptions.Female &&
            ProfileOptions.ActivityLevels.Contains(profile.ActivityLevel) &&
            ProfileOptions.Goals.Contains(profile.Goal) &&
            hasValidGoalFields &&
            hasValidCustomTarget;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }
    }

    private static void ValidateTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException(
                "Time zone ID is required.",
                nameof(timeZoneId));
        }

        if (timeZoneId.Length > UserDailyActivity.MaxTimeZoneIdLength)
        {
            throw new ArgumentException(
                $"Time zone ID must be {UserDailyActivity.MaxTimeZoneIdLength} " +
                "characters or fewer.",
                nameof(timeZoneId));
        }
    }
}
