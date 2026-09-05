using System.Globalization;

namespace CalorieTracker.Services;

public static class ExternalFoodIds
{
    public static bool TryNormalizeUsdaId(
        string? externalId,
        out string normalizedId)
    {
        if (int.TryParse(
                externalId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id) &&
            id > 0)
        {
            normalizedId = id.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        normalizedId = string.Empty;
        return false;
    }
}
