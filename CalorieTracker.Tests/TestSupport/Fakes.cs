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
