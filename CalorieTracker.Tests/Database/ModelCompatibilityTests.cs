using CalorieTracker.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Database;

public class ModelCompatibilityTests
{
    [Fact]
    public async Task CurrentModel_MatchesReleasedMigrationSnapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        Assert.False(database.Context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task ModelSeedCatalogue_MatchesEveryMigratedCatalogueValue()
    {
        await using var modelDatabase = await TestDatabase.CreateAsync();
        await using var migratedDatabase = await TestDatabase.CreateMigratedAsync();
        var modelItems = await modelDatabase.Context.CapyItems.OrderBy(item => item.Id).ToListAsync();
        var migratedItems = await migratedDatabase.Context.CapyItems.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(51, modelItems.Count);
        Assert.Equal(migratedItems.Select(item => (item.Id, item.Name, item.Category, item.ImagePath,
                item.IsActive, item.IsDefault, item.IsStarter)),
            modelItems.Select(item => (item.Id, item.Name, item.Category, item.ImagePath,
                item.IsActive, item.IsDefault, item.IsStarter)));
    }
}
