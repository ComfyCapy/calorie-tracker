using CalorieTracker.Models;
using CalorieTracker.Pages.Profile;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Calculations;

public sealed class ProfileFormProcessorTests
{
    [Fact]
    public void Process_DoesNotMutateSubmittedOrExistingProfile_AndApplyDoesNotCopyIdentity()
    {
        var submitted = ValidProfile();
        submitted.UserId = "untrusted";
        submitted.Id = 99;
        submitted.GoalWeightKg = 65;
        submitted.WeeklyGoalKg = 0.5m;
        submitted.CustomCalorieTarget = 1000;
        var existing = ValidProfile();
        existing.UserId = "owner";
        existing.Id = 1;
        var result = ProfileFormProcessor.Process(new(submitted, false, null, null, null, null),
            existing, TestTime.Today, []);
        Assert.Empty(result.Errors);
        Assert.Equal(65, submitted.GoalWeightKg);
        Assert.Equal(1000, submitted.CustomCalorieTarget);
        Assert.Null(existing.GoalWeightKg);
        result.ApplyTo(existing);
        Assert.Equal("owner", existing.UserId);
        Assert.Equal(1, existing.Id);
        Assert.Null(existing.GoalWeightKg);
        Assert.Null(existing.WeeklyGoalKg);
        Assert.Null(existing.CustomCalorieTarget);
    }

    [Theory]
    [InlineData("UserProfile.WeightKg", false)]
    [InlineData("userprofile.weightkg", false)]
    [InlineData("HeightFeet", true)]
    public void CalculatedTarget_CheckHonorsRemainingBindingErrors(string invalidField, bool retained)
    {
        var submitted = ValidProfile();
        submitted.MeasurementSystem = ProfileOptions.Imperial;
        submitted.DateOfBirth = TestTime.Today.AddYears(-120).ToDateTime(TimeOnly.MinValue);
        var result = ProfileFormProcessor.Process(
            new(submitted, false, 2, 0, 20 * ProfileOptions.PoundsPerKilogram, null),
            null, TestTime.Today, [invalidField]);
        Assert.Equal(!retained, result.Errors.Any(error => error.Field == string.Empty));
        Assert.Contains("UserProfile.WeightKg", result.ClearedFields);
    }

    [Theory]
    [InlineData(44, true)]
    [InlineData(45, false)]
    [InlineData(1102, false)]
    public void ImperialWeight_UsesCanonicalBoundaryNotRoundedPounds(int pounds, bool error)
    {
        var submitted = ValidProfile();
        submitted.MeasurementSystem = ProfileOptions.Imperial;
        var result = ProfileFormProcessor.Process(new(submitted, false, 5, 10, pounds, null),
            null, TestTime.Today, []);
        Assert.Equal(pounds / ProfileOptions.PoundsPerKilogram, result.CanonicalProfile.WeightKg);
        Assert.Equal(error, result.Errors.Any(e => e.Field == "WeightLb"));
        Assert.Equal(80, submitted.WeightKg);
    }

    private static UserProfile ValidProfile() => new()
    {
        DateOfBirth = TestTime.Today.AddYears(-30).ToDateTime(TimeOnly.MinValue),
        HeightCm = 180, WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };
}
