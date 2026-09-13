namespace CalorieTracker.Services;

public sealed class ProgressionAchievementHooks
{
    private readonly ProgressionService _progressionService;
    private readonly ILogger<ProgressionAchievementHooks> _logger;

    public ProgressionAchievementHooks(
        ProgressionService progressionService,
        ILogger<ProgressionAchievementHooks> logger)
    {
        _progressionService = progressionService;
        _logger = logger;
    }

    public Task EvaluateDiaryAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        TryEvaluateAsync(
            "Diary",
            userId,
            async token => await _progressionService
                .EvaluateDiaryAchievementsAsync(userId, token),
            cancellationToken);

    public Task EvaluateFoodsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        TryEvaluateAsync(
            "Foods",
            userId,
            async token => await _progressionService
                .EvaluateFoodAchievementsAsync(userId, token),
            cancellationToken);

    public Task EvaluateSavedMealsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        TryEvaluateAsync(
            "SavedMeals",
            userId,
            async token => await _progressionService
                .EvaluateSavedMealAchievementsAsync(userId, token),
            cancellationToken);

    public Task EvaluateProfileAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        TryEvaluateAsync(
            "Profile",
            userId,
            async token => await _progressionService
                .EvaluateProfileAchievementsAsync(userId, token),
            cancellationToken);

    public Task EvaluateCustomisationAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        TryEvaluateAsync(
            "Customisation",
            userId,
            async token => await _progressionService
                .EvaluateCustomisationAchievementsAsync(userId, token),
            cancellationToken);

    private async Task TryEvaluateAsync(
        string category,
        string userId,
        Func<CancellationToken, Task> evaluate,
        CancellationToken cancellationToken)
    {
        try
        {
            await evaluate(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Optional {ProgressionCategory} achievement evaluation " +
                "failed for user {UserId}; the successful domain action " +
                "will continue.",
                category,
                userId);
        }
    }
}
