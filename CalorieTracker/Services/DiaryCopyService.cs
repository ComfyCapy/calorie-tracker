using CalorieTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public enum DiaryCopyOutcome
{
    Success,
    Invalid,
    NoSourceEntries,
    TargetNotEmpty
}

public sealed record DiaryCopyResult(
    DiaryCopyOutcome Outcome,
    int EntriesCopied = 0);

public sealed class DiaryCopyService
{
    private readonly ApplicationDbContext _context;
    private readonly DailyMaintenanceSnapshotService _snapshotService;

    public DiaryCopyService(
        ApplicationDbContext context,
        DailyMaintenanceSnapshotService snapshotService)
    {
        _context = context;
        _snapshotService = snapshotService;
    }

    public async Task<DiaryCopyResult> CopyAsync(
        string userId,
        DateTime sourceDate,
        DateTime targetDate,
        bool confirmAdditive)
    {
        sourceDate = sourceDate.Date;
        targetDate = targetDate.Date;

        if (sourceDate < ValidationRules.MinimumDiaryDate ||
            sourceDate > ValidationRules.MaximumDiaryDate ||
            targetDate < ValidationRules.MinimumDiaryDate ||
            targetDate > ValidationRules.MaximumDiaryDate ||
            sourceDate == targetDate)
        {
            return new(DiaryCopyOutcome.Invalid);
        }

        var sourceEntries = await _context.DiaryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.UserId == userId &&
                entry.Date.Date == sourceDate)
            .OrderBy(entry => entry.Id)
            .ToListAsync();

        if (sourceEntries.Count == 0)
        {
            return new(DiaryCopyOutcome.NoSourceEntries);
        }

        var targetHasEntries = await _context.DiaryEntries.AnyAsync(entry =>
            entry.UserId == userId &&
            entry.Date.Date == targetDate);

        if (!confirmAdditive && targetHasEntries)
        {
            return new(DiaryCopyOutcome.TargetNotEmpty);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync();

        var copies = sourceEntries
            .Select(entry => DiarySnapshotFactory.CopyEntry(
                entry,
                userId,
                targetDate))
            .ToList();

        _context.DiaryEntries.AddRange(copies);
        await _snapshotService.EnsureSnapshotAsync(
            userId,
            DateOnly.FromDateTime(targetDate));
        await _snapshotService.SaveChangesAsync();
        await transaction.CommitAsync();

        return new(DiaryCopyOutcome.Success, copies.Count);
    }
}
