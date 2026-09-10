using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace CalorieTracker.Tests.TestSupport;

public sealed class FakeFoodSearchService : IFoodSearchService
{
    public Func<string, int, int, Task<FoodSearchPage>> SearchHandler { get; set; } =
        (_, page, pageSize) => Task.FromResult(new FoodSearchPage
        {
            PageNumber = page,
            PageSize = pageSize
        });

    public Func<string, Task<FoodSearchResult?>> GetHandler { get; set; } =
        _ => Task.FromResult<FoodSearchResult?>(null);

    public int SearchCallCount { get; private set; }
    public int GetCallCount { get; private set; }

    public Task<FoodSearchPage> SearchFoodsPageAsync(
        string searchTerm,
        int pageNumber,
        int pageSize)
    {
        SearchCallCount++;
        return SearchHandler(searchTerm, pageNumber, pageSize);
    }

    public Task<FoodSearchResult?> GetFoodAsync(string externalId)
    {
        GetCallCount++;
        return GetHandler(externalId);
    }
}

public static class TestFoodCatalogue
{
    public static FoodCatalogue Create(
        IFoodSearchService usdaService,
        params IFoodCatalogueProvider[] additionalProviders) =>
        new(
        [
            new UsdaFoodCatalogueProvider(usdaService),
            .. additionalProviders
        ]);
}

public sealed class FakeFoodCatalogueProvider : IFoodCatalogueProvider
{
    private readonly FoodSearchResult _food;

    public FakeFoodCatalogueProvider(
        string id,
        string source,
        string externalId,
        string name,
        decimal calories)
    {
        Id = id;
        Source = source;
        _food = new FoodSearchResult
        {
            ExternalId = externalId,
            Provider = id,
            Source = source,
            Name = name,
            Calories = calories,
            Protein = 5,
            Carbohydrates = 10,
            Fat = 4,
            ServingSize = 100,
            ServingUnit = "g"
        };
    }

    public string Id { get; }

    public string Source { get; }

    public string UnavailableTitle => $"The {Source} catalogue is unavailable.";

    public bool TryNormalizeExternalId(
        string? externalId,
        out string normalizedId)
    {
        var candidate = externalId?.Trim() ?? string.Empty;

        if (string.Equals(
                candidate,
                _food.ExternalId,
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedId = _food.ExternalId;
            return true;
        }

        normalizedId = string.Empty;
        return false;
    }

    public Task<FoodSearchPage> SearchAsync(
        string searchTerm,
        int pageNumber,
        int pageSize) =>
        Task.FromResult(new FoodSearchPage
        {
            Foods = [CopyFood()],
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalResults = 1,
            TotalPages = 1
        });

    public Task<FoodSearchResult?> ResolveAsync(string externalId) =>
        Task.FromResult<FoodSearchResult?>(
            string.Equals(
                externalId,
                _food.ExternalId,
                StringComparison.OrdinalIgnoreCase)
                ? CopyFood()
                : null);

    private FoodSearchResult CopyFood() => new()
    {
        ExternalId = _food.ExternalId,
        Provider = _food.Provider,
        Source = _food.Source,
        Name = _food.Name,
        Calories = _food.Calories,
        Protein = _food.Protein,
        Carbohydrates = _food.Carbohydrates,
        Fat = _food.Fat,
        ServingSize = _food.ServingSize,
        ServingUnit = _food.ServingUnit
    };
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<SentEmail> SentEmails { get; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        SentEmails.Add(new SentEmail(email, subject, htmlMessage));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(
    string Recipient,
    string Subject,
    string HtmlBody);
