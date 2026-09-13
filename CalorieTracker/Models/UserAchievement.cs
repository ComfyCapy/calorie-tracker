namespace CalorieTracker.Models;

public sealed class UserAchievement
{
    public const int MaxAchievementKeyLength = 80;

    public string UserId { get; set; } = string.Empty;
    public string AchievementKey { get; set; } = string.Empty;
    public DateTime UnlockedAtUtc { get; set; }
}
