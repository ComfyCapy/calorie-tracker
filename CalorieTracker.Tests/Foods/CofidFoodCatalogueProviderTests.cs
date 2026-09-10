using CalorieTracker.Services;

namespace CalorieTracker.Tests.Foods;

public class CofidFoodCatalogueProviderTests
{
    [Theory]
    [InlineData("hamburger", "Burger, hamburger, takeaway")]
    [InlineData("burger", "Burger, beef")]
    [InlineData("rice", "Rice")]
    [InlineData("chicken breast", "Chicken breast")]
    [InlineData("sausage roll", "Sausage roll")]
    [InlineData("crumpet", "Crumpets")]
    [InlineData("Yorkshire pudding", "Yorkshire pudding")]
    public async Task Search_ReturnsRepresentativeUkFoods(
        string query,
        string expectedNameFragment)
    {
        var provider = new CofidFoodCatalogueProvider();

        var page = await provider.SearchAsync(query, 1, 20);

        Assert.NotEmpty(page.Foods);
        Assert.Contains(
            page.Foods,
            food => food.Name.Contains(
                expectedNameFragment,
                StringComparison.OrdinalIgnoreCase));
        Assert.All(page.Foods, food =>
        {
            Assert.Equal(FoodCatalogueProviders.Cofid, food.Provider);
            Assert.Equal(FoodSources.Cofid, food.Source);
        });
    }

    [Fact]
    public async Task Search_RanksExactNameBeforePrefixNameAndDescription()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("exact", "Rice", "Plain grain"),
            Food("prefix", "Rice, brown", "Plain grain"),
            Food("description", "Pudding", "Made with rice")
        ]);

        var page = await provider.SearchAsync("rice", 1, 20);

        Assert.Equal(
            ["exact", "prefix", "description"],
            page.Foods.Select(food => food.ExternalId));
    }

    [Fact]
    public async Task Search_IsDeterministicAcrossRepeatedRequests()
    {
        var provider = new CofidFoodCatalogueProvider();

        var first = await provider.SearchAsync("chicken breast", 1, 50);
        var second = await provider.SearchAsync("chicken breast", 1, 50);

        Assert.Equal(
            first.Foods.Select(food => food.ExternalId),
            second.Foods.Select(food => food.ExternalId));
    }

    [Fact]
    public async Task Search_MultiTokenNameOutranksDescriptionAndPaginationIsStable()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("b", "Chicken, breast, grilled", "Cooked meat"),
            Food("a", "Chicken breast steak", "Cooked meat"),
            Food("d", "Grilled poultry", "Chicken breast meat"),
            Food("c", "Chicken breast strips", "Cooked meat")
        ]);

        var firstPage = await provider.SearchAsync("chicken breast", 1, 2);
        var secondPage = await provider.SearchAsync("chicken breast", 2, 2);

        Assert.Equal(["a", "c"], firstPage.Foods.Select(food => food.ExternalId));
        Assert.Equal(["b", "d"], secondPage.Foods.Select(food => food.ExternalId));
        Assert.Equal(4, firstPage.TotalResults);
        Assert.Equal(2, firstPage.TotalPages);
    }

    [Fact]
    public async Task Search_ExtremePageNumberReturnsEmptyPageWithoutOverflow()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("apple", "Apple", "Fresh fruit")
        ]);

        var page = await provider.SearchAsync(
            "apple",
            int.MaxValue,
            20);

        Assert.Empty(page.Foods);
        Assert.Equal(int.MaxValue, page.PageNumber);
        Assert.Equal(1, page.TotalResults);
        Assert.Equal(1, page.TotalPages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Search_RejectsOutOfRangePageSizes(int pageSize)
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("apple", "Apple", "Fresh fruit")
        ]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            provider.SearchAsync("apple", 1, pageSize));
    }

    [Theory]
    [InlineData(",")]
    [InlineData("...")]
    [InlineData("-")]
    public async Task Search_PunctuationOnlyQueryReturnsNoResults(string query)
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("comma", "Apple, eating", "Fresh fruit")
        ]);

        var page = await provider.SearchAsync(query, 1, 20);

        Assert.Empty(page.Foods);
        Assert.Equal(0, page.TotalResults);
    }

    [Theory]
    [InlineData("semi-skimmed", "milk")]
    [InlineData("98-99% beef", "burger")]
    public async Task Search_TextWithPunctuationRemainsSearchable(
        string query,
        string expectedId)
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("milk", "Milk, semi-skimmed", "Dairy"),
            Food("burger", "Burger, 98-99% beef", "Meat")
        ]);

        var page = await provider.SearchAsync(query, 1, 20);

        Assert.Contains(page.Foods, food => food.ExternalId == expectedId);
    }

    [Fact]
    public async Task Search_UsesStableIdToBreakOtherwiseIdenticalTies()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("z", "Rice, boiled", "Cooked"),
            Food("a", "Rice, boiled", "Cooked")
        ]);

        var page = await provider.SearchAsync("rice", 1, 20);

        Assert.Equal(["a", "z"], page.Foods.Select(food => food.ExternalId));
    }

    [Fact]
    public async Task Search_GenericPrefixPrefersBaseFoodVariantToCompositeDish()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("composite", "Rice and beans", "Dish", sourceRow: 4),
            Food("variant", "Rice, brown, boiled", "Grain", sourceRow: 8)
        ]);

        var page = await provider.SearchAsync("rice", 1, 20);

        Assert.Equal("variant", page.Foods[0].ExternalId);
    }

    [Fact]
    public async Task Resolve_PreservesMassAndAlcoholicBeverageBases()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("bread", "Bread", "Loaf", servingUnit: "g"),
            Food("wine", "Wine", "Alcoholic beverage", servingUnit: "ml")
        ]);

        var bread = await provider.ResolveAsync("bread");
        var wine = await provider.ResolveAsync("wine");

        Assert.NotNull(bread);
        Assert.Equal(100, bread.ServingSize);
        Assert.Equal("g", bread.ServingUnit);
        Assert.NotNull(wine);
        Assert.Equal(100, wine.ServingSize);
        Assert.Equal("ml", wine.ServingUnit);
    }

    [Fact]
    public void InvalidOrUnknownIdsAreRejectedSafely()
    {
        var provider = new CofidFoodCatalogueProvider(
        [
            Food("known", "Known food", "Description")
        ]);

        Assert.False(provider.TryNormalizeExternalId("unknown", out _));
        Assert.False(provider.TryNormalizeExternalId("   ", out _));
        Assert.True(provider.TryNormalizeExternalId(" KNOWN ", out var id));
        Assert.Equal("known", id);
    }

    [Theory]
    [InlineData("blank-id")]
    [InlineData("blank-name")]
    [InlineData("blank-description")]
    [InlineData("blank-source-code")]
    [InlineData("invalid-source-row")]
    [InlineData("blank-group")]
    [InlineData("invalid-unit")]
    [InlineData("zero-serving")]
    [InlineData("negative-calories")]
    [InlineData("negative-protein")]
    [InlineData("negative-carbohydrates")]
    [InlineData("negative-fat")]
    [InlineData("fractional-calories")]
    public void Constructor_RejectsMalformedRecords(string scenario)
    {
        var food = Food("known", "Known food", "Description");

        switch (scenario)
        {
            case "blank-id":
                food.Id = " ";
                break;
            case "blank-name":
                food.Name = " ";
                break;
            case "blank-description":
                food.Description = " ";
                break;
            case "blank-source-code":
                food.SourceCode = " ";
                break;
            case "invalid-source-row":
                food.SourceRow = 0;
                break;
            case "blank-group":
                food.Group = " ";
                break;
            case "invalid-unit":
                food.ServingUnit = "oz";
                break;
            case "zero-serving":
                food.ServingSize = 0;
                break;
            case "negative-calories":
                food.Calories = -1;
                break;
            case "negative-protein":
                food.Protein = -1;
                break;
            case "negative-carbohydrates":
                food.Carbohydrates = -1;
                break;
            case "negative-fat":
                food.Fat = -1;
                break;
            case "fractional-calories":
                food.Calories = 84.25m;
                break;
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CofidFoodCatalogueProvider([food]));

        Assert.Contains("CoFID catalogue record", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsDuplicateIdsCaseInsensitively()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CofidFoodCatalogueProvider(
            [
                Food("duplicate", "First", "Description"),
                Food("DUPLICATE", "Second", "Description", sourceRow: 5)
            ]));

        Assert.Contains("duplicate ID", exception.Message);
    }

    [Fact]
    public async Task Constructor_AcceptsLegitimateZeroNutritionValues()
    {
        var food = Food("zero", "Zero food", "Description");
        food.Calories = 0;
        food.Protein = 0;
        food.Carbohydrates = 0;
        food.Fat = 0;
        var provider = new CofidFoodCatalogueProvider([food]);

        var result = await provider.ResolveAsync("zero");

        Assert.NotNull(result);
        Assert.Equal(0, result.Calories);
        Assert.Equal(0, result.Protein);
        Assert.Equal(0, result.Carbohydrates);
        Assert.Equal(0, result.Fat);
    }

    [Fact]
    public async Task EmbeddedCatalogue_PassesAllRecordInvariants()
    {
        var provider = new CofidFoodCatalogueProvider();

        var page = await provider.SearchAsync("crumpet", 1, 20);

        Assert.NotEmpty(page.Foods);
    }

    [Fact]
    public void SourceMapping_IsExplicitAndUnknownSourcesAreRejected()
    {
        Assert.Equal(
            FoodCatalogueProviders.Usda,
            FoodCatalogueProviders.ForSource(FoodSources.Usda));
        Assert.Equal(
            FoodCatalogueProviders.Cofid,
            FoodCatalogueProviders.ForSource(FoodSources.Cofid));
        Assert.Throws<InvalidOperationException>(() =>
            FoodCatalogueProviders.ForSource("Unknown"));
        Assert.Throws<InvalidOperationException>(() =>
            FoodCatalogueProviders.ForSource(null));
    }

    private static CofidFoodRecord Food(
        string id,
        string name,
        string description,
        string servingUnit = "g",
        int sourceRow = 4) =>
        new()
        {
            Id = id,
            SourceCode = "13-001",
            SourceRow = sourceRow,
            Name = name,
            Description = description,
            Group = servingUnit == "ml" ? "QE" : "A",
            Calories = 100,
            Protein = 5,
            Carbohydrates = 10,
            Fat = 4,
            ServingSize = 100,
            ServingUnit = servingUnit
        };
}
