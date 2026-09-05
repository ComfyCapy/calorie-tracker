using CalorieTracker.Services;

namespace CalorieTracker.Tests.Measurements;

public class MeasurementUnitsTests
{
    [Theory]
    [InlineData("g", 2, 2, MeasurementDimension.Mass)]
    [InlineData("kg", 2, 2000, MeasurementDimension.Mass)]
    [InlineData("oz", 2, 56.69904625, MeasurementDimension.Mass)]
    [InlineData("lb", 2, 907.18474, MeasurementDimension.Mass)]
    [InlineData("ml", 2, 2, MeasurementDimension.Volume)]
    [InlineData("L", 2, 2000, MeasurementDimension.Volume)]
    [InlineData("fl oz", 2, 59.147059125, MeasurementDimension.Volume)]
    public void TryToCanonical_ConvertsEverySupportedUnit(
        string unit,
        double value,
        double expected,
        MeasurementDimension expectedDimension)
    {
        var converted = MeasurementUnits.TryToCanonical(
            (decimal)value,
            unit,
            out var canonicalValue,
            out _,
            out var dimension);

        Assert.True(converted);
        Assert.Equal((decimal)expected, canonicalValue, 8);
        Assert.Equal(expectedDimension, dimension);
    }

    [Theory]
    [InlineData("KG", "kg")]
    [InlineData(" l ", "L")]
    [InlineData("FL OZ", "fl oz")]
    public void TryNormalize_IsCaseInsensitiveAndPreservesDisplaySpelling(
        string input,
        string expectedDisplayUnit)
    {
        var normalized = MeasurementUnits.TryNormalize(
            input,
            out var displayUnit,
            out _);

        Assert.True(normalized);
        Assert.Equal(expectedDisplayUnit, displayUnit);
    }

    [Theory]
    [InlineData("kg", 2500, 2.5)]
    [InlineData("lb", 907.18474, 2)]
    [InlineData("L", 2500, 2.5)]
    [InlineData("fl oz", 59.147059125, 2)]
    public void FromCanonical_RoundTripsSupportedUnits(
        string unit,
        double canonicalValue,
        double expected)
    {
        var displayValue = MeasurementUnits.FromCanonical(
            (decimal)canonicalValue,
            unit);

        Assert.Equal((decimal)expected, displayValue, 8);
    }

    [Fact]
    public void UnsupportedUnit_IsRejected()
    {
        var converted = MeasurementUnits.TryToCanonical(
            1,
            "cup",
            out _,
            out _,
            out _);

        Assert.False(converted);
    }

    [Fact]
    public void MassAndVolumeUnits_HaveDifferentDimensions()
    {
        MeasurementUnits.TryNormalize("oz", out _, out var mass);
        MeasurementUnits.TryNormalize("fl oz", out _, out var volume);

        Assert.NotEqual(mass, volume);
    }

    [Fact]
    public void TryToCanonical_WhenMultiplicationOverflows_ReturnsFalse()
    {
        var converted = MeasurementUnits.TryToCanonical(
            decimal.MaxValue,
            "kg",
            out var canonicalValue,
            out _,
            out _);

        Assert.False(converted);
        Assert.Equal(0, canonicalValue);
    }
}
