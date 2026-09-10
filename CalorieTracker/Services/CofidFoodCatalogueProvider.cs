using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CalorieTracker.Models;

namespace CalorieTracker.Services;

public sealed class CofidFoodCatalogueProvider : IFoodCatalogueProvider
{
    private const string ResourceName =
        "CalorieTracker.Data.Catalogues.cofid-2021.json";
    private const string ExpectedSourceUrl =
        "https://www.gov.uk/government/publications/" +
        "composition-of-foods-integrated-dataset-cofid";

    private readonly IReadOnlyList<CofidFoodRecord> _foods;
    private readonly IReadOnlyDictionary<string, CofidFoodRecord> _foodsById;

    public CofidFoodCatalogueProvider()
        : this(LoadEmbeddedFoods())
    {
    }

    public CofidFoodCatalogueProvider(IEnumerable<CofidFoodRecord> foods)
    {
        _foods = ValidateFoods(foods);
        _foodsById = _foods.ToDictionary(
            food => food.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public string Id => FoodCatalogueProviders.Cofid;

    public string Source => FoodSources.Cofid;

    public string UnavailableTitle =>
        "The UK food database is temporarily unavailable.";

    public bool TryNormalizeExternalId(
        string? externalId,
        out string normalizedId)
    {
        var candidate = externalId?.Trim() ?? string.Empty;

        if (_foodsById.TryGetValue(candidate, out var food))
        {
            normalizedId = food.Id;
            return true;
        }

        normalizedId = string.Empty;
        return false;
    }

    public Task<FoodSearchPage> SearchAsync(
        string searchTerm,
        int pageNumber,
        int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var normalizedQuery = Normalize(searchTerm);
        var queryTokens = Tokens(normalizedQuery);

        if (queryTokens.Count == 0)
        {
            return Task.FromResult(EmptyPage(pageNumber, pageSize));
        }

        var ranked = _foods
            .Select(food => new
            {
                Food = food,
                Rank = Rank(food, normalizedQuery, queryTokens),
                PrefixContinuationRank = PrefixContinuationRank(
                    food.Name,
                    normalizedQuery)
            })
            .Where(match => match.Rank.HasValue)
            .OrderBy(match => match.Rank)
            .ThenBy(match => match.PrefixContinuationRank)
            .ThenBy(match => match.Food.SourceRow)
            .ThenBy(match => match.Food.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Food.Id, StringComparer.Ordinal)
            .ToList();
        var totalResults = ranked.Count;
        var totalPages = totalResults == 0
            ? 0
            : (int)Math.Ceiling(totalResults / (decimal)pageSize);
        var offset = ((long)pageNumber - 1) * pageSize;
        var foods = offset >= totalResults
            ? []
            : ranked
                .Skip((int)offset)
                .Take(pageSize)
                .Select(match => ToSearchResult(match.Food))
                .ToList();

        return Task.FromResult(new FoodSearchPage
        {
            Foods = foods,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalResults = totalResults,
            TotalPages = totalPages
        });
    }

    public Task<FoodSearchResult?> ResolveAsync(string externalId)
    {
        var food = _foodsById.GetValueOrDefault(externalId);
        return Task.FromResult(
            food == null ? null : ToSearchResult(food));
    }

    private static int? Rank(
        CofidFoodRecord food,
        string query,
        IReadOnlySet<string> queryTokens)
    {
        if (query.Length == 0)
        {
            return null;
        }

        var name = Normalize(food.Name);

        if (name == query)
        {
            return 0;
        }

        if (name.StartsWith(query, StringComparison.Ordinal) &&
            IsBoundary(name, query.Length))
        {
            return 1;
        }

        if (ContainsPhrase(name, query))
        {
            return 2;
        }

        var nameTokens = Tokens(name);
        if (queryTokens.Count > 0 && queryTokens.IsSubsetOf(nameTokens))
        {
            return 3;
        }

        if (name.Contains(query, StringComparison.Ordinal))
        {
            return 4;
        }

        var description = Normalize(food.Description);
        if (ContainsPhrase(description, query))
        {
            return 5;
        }

        var descriptionTokens = Tokens(description);
        return queryTokens.Count > 0 && queryTokens.IsSubsetOf(descriptionTokens)
            ? 6
            : null;
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        var index = text.IndexOf(phrase, StringComparison.Ordinal);

        while (index >= 0)
        {
            var startsAtBoundary = index == 0 ||
                !char.IsLetterOrDigit(text[index - 1]);
            var end = index + phrase.Length;
            var endsAtBoundary = end == text.Length ||
                !char.IsLetterOrDigit(text[end]);

            if (startsAtBoundary && endsAtBoundary)
            {
                return true;
            }

            index = text.IndexOf(
                phrase,
                index + 1,
                StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsBoundary(string text, int index) =>
        index >= text.Length || !char.IsLetterOrDigit(text[index]);

    private static int PrefixContinuationRank(
        string foodName,
        string query)
    {
        var name = Normalize(foodName);

        if (!name.StartsWith(query, StringComparison.Ordinal) ||
            name.Length == query.Length)
        {
            return 0;
        }

        // CoFID conventionally uses a comma for a base food followed by its variant.
        return name[query.Length] == ',' ? 0 : 1;
    }

    private static string Normalize(string value) =>
        string.Join(
            " ",
            value.Trim().ToLowerInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlySet<string> Tokens(string value) =>
        Regex.Matches(value, @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static FoodSearchPage EmptyPage(int pageNumber, int pageSize) =>
        new()
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

    private static FoodSearchResult ToSearchResult(CofidFoodRecord food) =>
        new()
        {
            ExternalId = food.Id,
            Provider = FoodCatalogueProviders.Cofid,
            Source = FoodSources.Cofid,
            Name = food.Name,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbohydrates = food.Carbohydrates,
            Fat = food.Fat,
            ServingSize = food.ServingSize,
            ServingUnit = food.ServingUnit
        };

    private static IReadOnlyList<CofidFoodRecord> LoadEmbeddedFoods()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "The embedded CoFID catalogue is unavailable.");
        var document = JsonSerializer.Deserialize<CofidCatalogueDocument>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException(
                "The embedded CoFID catalogue is invalid.");

        if (!string.Equals(
                document.Provider,
                FoodCatalogueProviders.Cofid,
                StringComparison.Ordinal) ||
            !string.Equals(
                document.Source,
                FoodSources.Cofid,
                StringComparison.Ordinal) ||
            document.Version != "2021" ||
            document.SourceUrl != ExpectedSourceUrl ||
            document.Foods == null ||
            document.Foods.Count == 0 ||
            document.Import == null ||
            document.Import.Worksheet != "1.3 Proximates" ||
            document.Import.FirstDataRow != 4 ||
            document.Import.ImportedRows != document.Foods.Count ||
            document.Import.SourceRows !=
                document.Import.ImportedRows + document.Import.ExcludedRows ||
            document.Import.TraceConversions < 0 ||
            document.Import.NValues < 0 ||
            document.Import.DuplicateSourceCodes < 0 ||
            document.Import.DuplicateSourceCodeRows < 0 ||
            document.Import.RoundedCalories < 0)
        {
            throw new InvalidOperationException(
                "The embedded CoFID catalogue metadata is invalid.");
        }

        return document.Foods;
    }

    private static IReadOnlyList<CofidFoodRecord> ValidateFoods(
        IEnumerable<CofidFoodRecord>? foods)
    {
        var records = foods?.ToList() ?? throw new InvalidOperationException(
            "The CoFID catalogue food collection is missing.");

        if (records.Count == 0)
        {
            throw new InvalidOperationException(
                "The CoFID catalogue contains no foods.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var food in records)
        {
            if (food == null)
            {
                throw new InvalidOperationException(
                    "The CoFID catalogue contains a null food record.");
            }

            var identity = food.SourceRow > 0
                ? $"source row {food.SourceRow}"
                : "an unknown source row";

            if (string.IsNullOrWhiteSpace(food.Id))
            {
                throw InvalidRecord(identity, "has a blank ID");
            }

            if (!ids.Add(food.Id))
            {
                throw InvalidRecord(identity, $"has duplicate ID '{food.Id}'");
            }

            if (string.IsNullOrWhiteSpace(food.SourceCode))
            {
                throw InvalidRecord(identity, "has a blank source code");
            }

            if (food.SourceRow < 4)
            {
                throw InvalidRecord(identity, "has an invalid source row");
            }

            if (string.IsNullOrWhiteSpace(food.Name))
            {
                throw InvalidRecord(identity, "has a blank food name");
            }

            if (string.IsNullOrWhiteSpace(food.Description))
            {
                throw InvalidRecord(identity, "has a blank description");
            }

            if (string.IsNullOrWhiteSpace(food.Group))
            {
                throw InvalidRecord(identity, "has a blank food group");
            }

            if (food.ServingSize <= 0)
            {
                throw InvalidRecord(identity, "has a non-positive serving size");
            }

            if (food.ServingUnit is not ("g" or "ml"))
            {
                throw InvalidRecord(
                    identity,
                    $"has unsupported serving unit '{food.ServingUnit}'");
            }

            if (food.Calories < 0 ||
                food.Protein < 0 ||
                food.Carbohydrates < 0 ||
                food.Fat < 0)
            {
                throw InvalidRecord(identity, "has negative nutrition values");
            }

            if (food.Calories != decimal.Truncate(food.Calories))
            {
                throw InvalidRecord(
                    identity,
                    "has calories that were not mapped to an integer");
            }
        }

        return records;
    }

    private static InvalidOperationException InvalidRecord(
        string identity,
        string problem) =>
        new($"The CoFID catalogue record at {identity} {problem}.");
}

public sealed class CofidCatalogueDocument
{
    public string Provider { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public CofidImportMetadata? Import { get; set; }

    public List<CofidFoodRecord> Foods { get; set; } = [];
}

public sealed class CofidImportMetadata
{
    public string Worksheet { get; set; } = string.Empty;

    public int FirstDataRow { get; set; }

    public int SourceRows { get; set; }

    public int ImportedRows { get; set; }

    public int ExcludedRows { get; set; }

    public int TraceConversions { get; set; }

    public int NValues { get; set; }

    public int DuplicateSourceCodes { get; set; }

    public int DuplicateSourceCodeRows { get; set; }

    public int RoundedCalories { get; set; }
}

public sealed class CofidFoodRecord
{
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source_code")]
    public string SourceCode { get; set; } = string.Empty;

    [JsonPropertyName("source_row")]
    public int SourceRow { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public decimal Calories { get; set; }

    public decimal Protein { get; set; }

    public decimal Carbohydrates { get; set; }

    public decimal Fat { get; set; }

    [JsonPropertyName("serving_size")]
    public decimal ServingSize { get; set; } = 100;

    [JsonPropertyName("serving_unit")]
    public string ServingUnit { get; set; } = "g";
}
