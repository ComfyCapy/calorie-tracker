using CalorieTracker.Data;

namespace CalorieTracker.Models;

public sealed class DailyMaintenanceSnapshot
{
    private DailyMaintenanceSnapshot()
    {
    }

    public DailyMaintenanceSnapshot(
        string userId,
        DateOnly date,
        decimal maintenanceCalories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (maintenanceCalories <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maintenanceCalories),
                "Maintenance calories must be greater than zero.");
        }

        UserId = userId;
        Date = date;
        MaintenanceCalories = maintenanceCalories;
    }

    public string UserId { get; private set; } = string.Empty;
    public ApplicationUser? User { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal MaintenanceCalories { get; private set; }
}
