namespace CalorieTracker.Security;

public static class RateLimitPolicies
{
    // MVP limits: account creation 5/hour, login 30/5 minutes,
    // recovery and confirmation email requests 5/15 minutes, USDA search 60/minute.
    public const string IdentityOperations = "identity-operations";
    public const string FoodSearch = "food-search";
}
