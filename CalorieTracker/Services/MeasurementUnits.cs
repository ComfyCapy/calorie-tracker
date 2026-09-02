using System.Globalization;

namespace CalorieTracker.Services
{
    public enum MeasurementDimension
    {
        Mass,
        Volume
    }

    public static class MeasurementUnits
    {
        private sealed record UnitDefinition(
            string DisplayName,
            MeasurementDimension Dimension,
            decimal CanonicalFactor);

        private static readonly Dictionary<string, UnitDefinition> Units =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["g"] = new("g", MeasurementDimension.Mass, 1m),
                ["kg"] = new("kg", MeasurementDimension.Mass, 1000m),
                ["oz"] = new("oz", MeasurementDimension.Mass, 28.349523125m),
                ["lb"] = new("lb", MeasurementDimension.Mass, 453.59237m),
                ["ml"] = new("ml", MeasurementDimension.Volume, 1m),
                ["l"] = new("L", MeasurementDimension.Volume, 1000m),
                ["fl oz"] = new("fl oz", MeasurementDimension.Volume, 29.5735295625m)
            };

        public static IReadOnlyList<string> SupportedUnits { get; } =
            ["g", "kg", "oz", "lb", "ml", "L", "fl oz"];

        public static bool TryNormalize(
            string? unit,
            out string normalizedUnit,
            out MeasurementDimension dimension)
        {
            var key = unit?.Trim() ?? string.Empty;

            if (Units.TryGetValue(key, out var definition))
            {
                normalizedUnit = definition.DisplayName;
                dimension = definition.Dimension;
                return true;
            }

            normalizedUnit = string.Empty;
            dimension = default;
            return false;
        }

        public static bool TryToCanonical(
            decimal value,
            string? unit,
            out decimal canonicalValue,
            out string normalizedUnit,
            out MeasurementDimension dimension)
        {
            canonicalValue = 0;

            if (!TryNormalize(unit, out normalizedUnit, out dimension))
            {
                return false;
            }

            try
            {
                canonicalValue = checked(
                    value * Units[normalizedUnit].CanonicalFactor);

                return true;
            }
            catch (OverflowException)
            {
                canonicalValue = 0;
                return false;
            }
        }

        public static decimal FromCanonical(
            decimal canonicalValue,
            string unit)
        {
            if (!TryNormalize(unit, out var normalizedUnit, out _))
            {
                throw new ArgumentException(
                    $"Unsupported serving unit '{unit}'.",
                    nameof(unit));
            }

            return canonicalValue /
                Units[normalizedUnit].CanonicalFactor;
        }

        public static string CanonicalUnit(MeasurementDimension dimension) =>
            dimension == MeasurementDimension.Mass ? "g" : "ml";

        public static bool IsPositiveUsdaId(
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
}
