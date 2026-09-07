using System.Net;
using System.Text;
using CalorieTracker.Services;
using Microsoft.Extensions.Configuration;

namespace CalorieTracker.Tests.Api;

public class UsdaFoodServiceTests
{
    [Fact]
    public async Task Search_MapsUsdaNutrientsWithoutCallingLiveService()
    {
        const string responseJson = """
            {
              "foods": [
                {
                  "fdcId": 123,
                  "description": "Test apple",
                  "foodNutrients": [
                    { "nutrientId": 1008, "value": 52 },
                    { "nutrientId": 1003, "value": 0.3 },
                    { "nutrientId": 1005, "value": 14 },
                    { "nutrientId": 1004, "value": 0.2 }
                  ]
                }
              ],
              "currentPage": 2,
              "totalHits": 21,
              "totalPages": 3
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var page = await service.SearchFoodsPageAsync("apple", 2, 10);

        var food = Assert.Single(page.Foods);
        Assert.Equal("123", food.ExternalId);
        Assert.Equal("Test apple", food.Name);
        Assert.Equal(52, food.Calories);
        Assert.Equal(0.3m, food.Protein);
        Assert.Equal(14, food.Carbohydrates);
        Assert.Equal(0.2m, food.Fat);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(21, page.TotalResults);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetFood_MapsDetailResponseAndEnergyFallback()
    {
        const string responseJson = """
            {
              "fdcId": 456,
              "description": "Test cheese",
              "foodNutrients": [
                { "nutrient": { "id": 2047 }, "amount": 120 },
                { "nutrient": { "id": 1003 }, "amount": 7 },
                { "nutrient": { "id": 1005 }, "amount": 1 },
                { "nutrient": { "id": 1004 }, "amount": 9 }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("00456");

        Assert.NotNull(food);
        Assert.Equal("456", food.ExternalId);
        Assert.Equal(120, food.Calories);
        Assert.Equal(7, food.Protein);
        Assert.Equal(1, food.Carbohydrates);
        Assert.Equal(9, food.Fat);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetFood_MapsOnlyValidMeasuredPortions()
    {
        const string responseJson = """
            {
              "fdcId": 789,
              "description": "Banana, raw",
              "dataType": "Foundation",
              "foodNutrients": [
                { "nutrient": { "id": 1008 }, "amount": 300 }
              ],
              "foodPortions": [
                {
                  "gramWeight": 75,
                  "amount": 1,
                  "measureUnit": { "name": "Banana" },
                  "modifier": "Peeled"
                },
                {
                  "gramWeight": 75.0,
                  "amount": 1,
                  "measureUnit": { "name": "banana" },
                  "modifier": "peeled"
                },
                {
                  "gramWeight": 30,
                  "amount": 1,
                  "modifier": "slice"
                },
                {
                  "gramWeight": 28,
                  "amount": 2,
                  "measureUnit": { "name": "tbsp" }
                },
                {
                  "gramWeight": 20,
                  "portionDescription": "   "
                },
                {
                  "gramWeight": 20,
                  "portionDescription": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                {
                  "gramWeight": 0,
                  "portionDescription": "1 packet"
                },
                {
                  "gramWeight": -10,
                  "portionDescription": "1 bar"
                },
                {
                  "gramWeight": "NaN",
                  "portionDescription": "1 invalid serving"
                },
                {
                  "gramWeight": 1e999,
                  "portionDescription": "1 enormous serving"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("789");

        Assert.NotNull(food);
        Assert.Collection(
            food.Portions,
            portion =>
            {
                Assert.Equal("1 Banana, Peeled", portion.Name);
                Assert.Equal(75, portion.GramWeight);
            },
            portion =>
            {
                Assert.Equal("1 slice", portion.Name);
                Assert.Equal(30, portion.GramWeight);
            },
            portion =>
            {
                Assert.Equal("2 tbsp", portion.Name);
                Assert.Equal(28, portion.GramWeight);
            });
    }

    [Fact]
    public async Task GetFood_FnddsUsesHouseholdDescriptionAndExcludesUnspecifiedPortion()
    {
        const string responseJson = """
            {
              "fdcId": 790,
              "description": "Apple, candied",
              "dataType": "Survey (FNDDS)",
              "foodNutrients": [
                { "nutrient": { "id": 1008 }, "amount": 300 }
              ],
              "foodPortions": [
                {
                  "gramWeight": 320,
                  "portionDescription": "Quantity not specified",
                  "modifier": "90000"
                },
                {
                  "gramWeight": 150,
                  "portionDescription": "  1   apple, any size "
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("790");

        Assert.NotNull(food);
        var portion = Assert.Single(food.Portions);
        Assert.Equal("1 apple, any size", portion.Name);
        Assert.Equal(150, portion.GramWeight);
    }

    [Fact]
    public async Task GetFood_MissingPortionsReturnsNoCandidates()
    {
        const string responseJson = """
            {
              "fdcId": 790,
              "description": "Food without measured servings",
              "foodNutrients": []
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("790");

        Assert.NotNull(food);
        Assert.Empty(food.Portions);
    }

    [Fact]
    public async Task Search_BlankTermReturnsEmptyWithoutHttpRequestOrApiKey()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called"));
        var service = new UsdaFoodService(
            new HttpClient(handler),
            new ConfigurationBuilder().Build());

        var page = await service.SearchFoodsPageAsync("   ", 1, 20);

        Assert.Empty(page.Foods);
        Assert.Equal(0, handler.CallCount);
    }

    private static UsdaFoodService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FoodDataCentral:ApiKey"] = "test-key"
            })
            .Build();

        return new UsdaFoodService(new HttpClient(handler), configuration);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(
        HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
