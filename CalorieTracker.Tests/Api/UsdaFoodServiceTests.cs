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
