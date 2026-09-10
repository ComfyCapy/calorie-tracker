using System.Net;
using System.Text;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.Extensions.Configuration;

namespace CalorieTracker.Tests.Api;

public class UsdaFoodServiceTests
{
    [Fact]
    public async Task Search_VolumeMetadataDoesNotRebaseCardsOrFetchDetails()
    {
        const string json = """
            {
              "foods": [
                { "fdcId": 1, "description": "Beer", "dataType": "Survey (FNDDS)",
                  "foodNutrients": [{ "nutrientId": 1008, "value": 43 }],
                  "foodMeasures": [{ "disseminationText": "1 fl oz", "gramWeight": 30 }] },
                { "fdcId": 2, "description": "Milk", "dataType": "Foundation",
                  "foodNutrients": [{ "nutrientId": 1008, "value": 61 }],
                  "foodMeasures": [] }
              ],
              "currentPage": 1, "totalHits": 2, "totalPages": 1
            }
            """;
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/fdc/v1/foods/search", request.RequestUri!.AbsolutePath);
            return JsonResponse(json);
        });
        var page = await CreateService(handler).SearchFoodsPageAsync("drinks", 1, 20);
        Assert.Equal(2, page.Foods.Count);
        Assert.All(page.Foods, food =>
        {
            Assert.Equal(100, food.ServingSize);
            Assert.Equal("g", food.ServingUnit);
            Assert.Empty(food.Portions);
        });
        Assert.Equal(43, page.Foods[0].Calories);
        Assert.Equal(61, page.Foods[1].Calories);
        Assert.Equal(1, handler.CallCount);
    }

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
    public async Task GetFood_FnddsBeerMapsAuthoritativeVolumePortionsAndKeepsHundredGramBasis()
    {
        const string responseJson = """
            {
              "fdcId": 2710616,
              "description": "Beer, regular",
              "dataType": "Survey (FNDDS)",
              "foodNutrients": [
                { "nutrient": { "id": 1008 }, "amount": 43 },
                { "nutrient": { "id": 1003 }, "amount": 0.46 },
                { "nutrient": { "id": 1005 }, "amount": 3.55 },
                { "nutrient": { "id": 1004 }, "amount": 0 }
              ],
              "foodPortions": [
                {
                  "gramWeight": 360,
                  "portionDescription": "Quantity not specified"
                },
                {
                  "gramWeight": 30,
                  "portionDescription": "1 fl oz"
                },
                {
                  "gramWeight": 360,
                  "portionDescription": "1 can or bottle (12 fl oz)"
                },
                {
                  "gramWeight": 480,
                  "portionDescription": "1 can or bottle (16 fl oz)"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("2710616");

        Assert.NotNull(food);
        Assert.Equal(100, food.ServingSize);
        Assert.Equal("g", food.ServingUnit);
        Assert.Equal(43, food.Calories);
        Assert.Collection(
            food.Portions,
            portion => Assert.Equal(
                new FoodPortionCandidate("1 fl oz", 30),
                portion),
            portion => Assert.Equal(
                new FoodPortionCandidate(
                    "1 can or bottle (12 fl oz)",
                    360),
                portion),
            portion => Assert.Equal(
                new FoodPortionCandidate(
                    "1 can or bottle (16 fl oz)",
                    480),
                portion));
    }

    [Fact]
    public async Task GetFood_SrLegacyBeerMapsAmountModifierVolumePortions()
    {
        const string responseJson = """
            {
              "fdcId": 168746,
              "description": "Alcoholic beverage, beer, regular",
              "dataType": "SR Legacy",
              "foodNutrients": [],
              "foodPortions": [
                {
                  "gramWeight": 356,
                  "amount": 1,
                  "modifier": "can"
                },
                {
                  "gramWeight": 29.7,
                  "amount": 1,
                  "modifier": "fl oz"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("168746");

        Assert.NotNull(food);
        Assert.Collection(
            food.Portions,
            portion => Assert.Equal(
                new FoodPortionCandidate("1 can", 356),
                portion),
            portion => Assert.Equal(
                new FoodPortionCandidate("1 fl oz", 29.7m),
                portion));
    }

    [Theory]
    [InlineData("Orange juice", "1 fl oz", 31)]
    [InlineData("Milk, whole", "1 cup", 244)]
    [InlineData("Milk, whole", "1 fl oz", 30.5)]
    public async Task GetFood_FnddsLiquidsRetainSuppliedHouseholdPortions(
        string description,
        string portionDescription,
        decimal gramWeight)
    {
        var responseJson = $$"""
            {
              "fdcId": 900,
              "description": "{{description}}",
              "dataType": "Survey (FNDDS)",
              "foodNutrients": [],
              "foodPortions": [
                {
                  "gramWeight": {{gramWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                  "portionDescription": "{{portionDescription}}"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("900");

        Assert.NotNull(food);
        var portion = Assert.Single(food.Portions);
        Assert.Equal(portionDescription, portion.Name);
        Assert.Equal(gramWeight, portion.GramWeight);
    }

    [Fact]
    public async Task GetFood_FoundationLiquidWithoutPortionsRemainsGramOnly()
    {
        const string responseJson = """
            {
              "fdcId": 790,
              "description": "Oat milk",
              "dataType": "Foundation",
              "foodNutrients": []
            }
            """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(responseJson));
        var service = CreateService(handler);

        var food = await service.GetFoodAsync("790");

        Assert.NotNull(food);
        Assert.Empty(food.Portions);
        Assert.Equal(100, food.ServingSize);
        Assert.Equal("g", food.ServingUnit);
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
