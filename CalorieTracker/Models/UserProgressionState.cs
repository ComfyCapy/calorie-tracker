namespace CalorieTracker.Models;

public sealed class UserProgressionState
{
    public string UserId { get; set; } = string.Empty;
    public int AchievementBackfillVersion { get; set; }
}
