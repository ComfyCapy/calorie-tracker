using CalorieTracker.Data;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages;

[Authorize]
public sealed class ProgressModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ProgressionService _progressionService;
    private readonly IUserLocalTimeProvider _userLocalTimeProvider;
    private readonly ILogger<ProgressModel> _logger;

    public ProgressModel(
        UserManager<ApplicationUser> userManager,
        ProgressionService progressionService,
        IUserLocalTimeProvider userLocalTimeProvider,
        ILogger<ProgressModel> logger)
    {
        _userManager = userManager;
        _progressionService = progressionService;
        _userLocalTimeProvider = userLocalTimeProvider;
        _logger = logger;
    }

    public ProgressionSummary Summary { get; private set; } = null!;

    public IReadOnlyList<ProgressAchievementCard> Achievements
        { get; private set; } = [];

    public string? ReconciliationMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }

        AchievementReconciliationResult? reconciliation = null;

        try
        {
            reconciliation = await _progressionService
                .ReconcileAchievementsAsync(userId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Optional Progress page reconciliation failed for user " +
                "{UserId}; the existing progression summary will still " +
                "be loaded.",
                userId);
        }

        Summary = await _progressionService.GetSummaryAsync(
            userId,
            _userLocalTimeProvider.Today,
            cancellationToken);
        Achievements = CreateAchievementCards(Summary);

        if (reconciliation?.NewlyUnlockedAchievements.Count > 0)
        {
            var count = reconciliation.NewlyUnlockedAchievements.Count;
            ReconciliationMessage = count == 1
                ? "Nice — we found an achievement you’d already earned."
                : $"Nice — we found {count} achievements you’d already earned.";
        }

        return Page();
    }

    private static IReadOnlyList<ProgressAchievementCard>
        CreateAchievementCards(ProgressionSummary summary)
    {
        var unlockedByKey = summary.UnlockedAchievements
            .Where(item => item.Definition != null)
            .ToDictionary(item => item.Key, StringComparer.Ordinal);

        return AchievementDefinitions.All
            .Select(definition =>
            {
                unlockedByKey.TryGetValue(definition.Key, out var unlocked);

                return new ProgressAchievementCard(
                    definition.Key,
                    definition.DisplayName,
                    definition.Lore,
                    definition.HowToGet,
                    definition.XpReward,
                    ProgressAchievementVisuals.SymbolFor(definition.Key),
                    unlocked != null,
                    unlocked?.UnlockedAtUtc);
            })
            .ToArray();
    }
}
