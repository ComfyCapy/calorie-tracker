using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public sealed class DailyMaintenanceSnapshotService
{
    private readonly ApplicationDbContext _context;

    public DailyMaintenanceSnapshotService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureSnapshotAsync(
        string userId,
        DateOnly date,
        UserProfile? profile = null,
        CancellationToken cancellationToken = default)
    {
        if (_context.DailyMaintenanceSnapshots.Local.Any(snapshot =>
                snapshot.UserId == userId &&
                snapshot.Date == date) ||
            await _context.DailyMaintenanceSnapshots.AnyAsync(snapshot =>
                    snapshot.UserId == userId &&
                    snapshot.Date == date,
                cancellationToken))
        {
            return;
        }

        profile ??= await _context.UserProfiles
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId,
                cancellationToken);

        if (profile?.TryCalculateMaintenance(
                date,
                out var maintenanceCalories) != true)
        {
            return;
        }

        _context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                userId,
                date,
                maintenanceCalories));
    }

    public async Task FillMissingSnapshotsAsync(
        string userId,
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        var diaryDates = await _context.DiaryEntries
            .Where(entry => entry.UserId == userId)
            .Select(entry => entry.Date.Date)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (diaryDates.Count == 0)
        {
            return;
        }

        var existingDates = (await _context.DailyMaintenanceSnapshots
                .Where(snapshot => snapshot.UserId == userId)
                .Select(snapshot => snapshot.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var trackedSnapshot in
                 _context.DailyMaintenanceSnapshots.Local)
        {
            if (trackedSnapshot.UserId == userId)
            {
                existingDates.Add(trackedSnapshot.Date);
            }
        }

        foreach (var diaryDate in diaryDates)
        {
            var date = DateOnly.FromDateTime(diaryDate);

            if (existingDates.Contains(date) ||
                !profile.TryCalculateMaintenance(
                    date,
                    out var maintenanceCalories))
            {
                continue;
            }

            _context.DailyMaintenanceSnapshots.Add(
                new DailyMaintenanceSnapshot(
                    userId,
                    date,
                    maintenanceCalories));
            existingDates.Add(date);
        }
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception)
                when (DetachSnapshotInsertRace(exception))
            {
                // A concurrent request committed the same user/date snapshot.
                // The failed SaveChanges transaction was rolled back, so retry
                // the remaining diary/profile changes without the duplicate.
            }
        }
    }

    private static bool DetachSnapshotInsertRace(
        DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException
            {
                SqliteErrorCode: 19,
                SqliteExtendedErrorCode: 1555 or 2067
            })
        {
            return false;
        }

        var snapshotEntries = exception.Entries
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity is DailyMaintenanceSnapshot)
            .ToList();

        if (snapshotEntries.Count == 0)
        {
            return false;
        }

        foreach (var entry in snapshotEntries)
        {
            entry.State = EntityState.Detached;
        }

        return true;
    }
}
