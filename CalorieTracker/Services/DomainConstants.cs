namespace CalorieTracker.Services;

public static class ProfileOptions
{
    public const string Metric = "Metric";
    public const string Imperial = "Imperial";

    public const string SystemTheme = "System";
    public const string LightTheme = "Light";
    public const string DarkTheme = "Dark";

    public const string Male = "Male";
    public const string Female = "Female";

    public const string Sedentary = "Sedentary";
    public const string LightlyActive = "LightlyActive";
    public const string ModeratelyActive = "ModeratelyActive";
    public const string VeryActive = "VeryActive";
    public const string ExtraActive = "ExtraActive";

    public const string Lose = "Lose";
    public const string Maintain = "Maintain";
    public const string Gain = "Gain";

    public static readonly IReadOnlySet<string> ActivityLevels =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Sedentary,
            LightlyActive,
            ModeratelyActive,
            VeryActive,
            ExtraActive
        };

    public static readonly IReadOnlySet<string> Goals =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Lose,
            Maintain,
            Gain
        };

    public static readonly IReadOnlySet<decimal> WeeklyGoals =
        new HashSet<decimal>
        {
            0.25m,
            0.5m,
            0.75m,
            1.0m
        };
}

public static class CapyCategories
{
    public const string Background = "Background";
    public const string Expression = "Expression";
    public const string Clothes = "Clothes";
    public const string NeckAccessory = "NeckAccessory";
    public const string HatHair = "HatHair";
    public const string FaceAccessory = "FaceAccessory";
}

public static class FoodSources
{
    public const string Usda = "USDA";
}
