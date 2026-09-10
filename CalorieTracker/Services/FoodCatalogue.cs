using CalorieTracker.Models;

namespace CalorieTracker.Services;

public interface IFoodCatalogueProvider
{
    string Id { get; }

    string Source { get; }

    string UnavailableTitle { get; }

    bool TryNormalizeExternalId(
        string? externalId,
        out string normalizedId);

    Task<FoodSearchPage> SearchAsync(
        string searchTerm,
        int pageNumber,
        int pageSize);

    Task<FoodSearchResult?> ResolveAsync(string externalId);
}

public sealed class FoodCatalogue
{
    private readonly IReadOnlyDictionary<string, IFoodCatalogueProvider>
        _providers;

    public FoodCatalogue(IEnumerable<IFoodCatalogueProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetProvider(
        string? providerId,
        out IFoodCatalogueProvider provider)
    {
        var normalizedId = string.IsNullOrWhiteSpace(providerId)
            ? FoodCatalogueProviders.LegacyDefault
            : providerId.Trim();

        return _providers.TryGetValue(normalizedId, out provider!);
    }
}

public sealed class UsdaFoodCatalogueProvider : IFoodCatalogueProvider
{
    private readonly IFoodSearchService _foodSearchService;

    public UsdaFoodCatalogueProvider(IFoodSearchService foodSearchService)
    {
        _foodSearchService = foodSearchService;
    }

    public string Id => FoodCatalogueProviders.Usda;

    public string Source => FoodSources.Usda;

    public string UnavailableTitle =>
        "The USDA food database is temporarily unavailable.";

    public bool TryNormalizeExternalId(
        string? externalId,
        out string normalizedId) =>
        ExternalFoodIds.TryNormalizeUsdaId(externalId, out normalizedId);

    public async Task<FoodSearchPage> SearchAsync(
        string searchTerm,
        int pageNumber,
        int pageSize)
    {
        var page = await _foodSearchService.SearchFoodsPageAsync(
            searchTerm,
            pageNumber,
            pageSize);

        foreach (var food in page.Foods)
        {
            food.Provider = Id;
            food.Source = Source;
        }

        return page;
    }

    public async Task<FoodSearchResult?> ResolveAsync(string externalId)
    {
        var food = await _foodSearchService.GetFoodAsync(externalId);

        if (food != null)
        {
            food.Provider = Id;
            food.Source = Source;
        }

        return food;
    }
}
