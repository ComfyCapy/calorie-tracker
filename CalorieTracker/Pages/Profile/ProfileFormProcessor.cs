using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Profile;

public sealed record ProfileFormInput(
    UserProfile Profile,
    bool UseCustomCalorieTarget,
    int? HeightFeet,
    decimal? HeightInches,
    decimal? WeightLb,
    decimal? GoalWeightLb);

public sealed record ProfileFormError(string Field, string Message);

public sealed record ProfileFormResult(
    UserProfile CanonicalProfile,
    decimal? GoalWeightLb,
    IReadOnlyList<ProfileFormError> Errors,
    IReadOnlyList<string> ClearedFields)
{
    // Copy form values only; identity and tracked-entity ownership are not input.
    public void ApplyTo(UserProfile profile)
    {
        profile.MeasurementSystem = CanonicalProfile.MeasurementSystem;
        profile.ThemePreference = CanonicalProfile.ThemePreference;
        profile.DateOfBirth = CanonicalProfile.DateOfBirth;
        profile.HeightCm = CanonicalProfile.HeightCm;
        profile.WeightKg = CanonicalProfile.WeightKg;
        profile.CalculationSex = CanonicalProfile.CalculationSex;
        profile.ActivityLevel = CanonicalProfile.ActivityLevel;
        profile.Goal = CanonicalProfile.Goal;
        profile.GoalWeightKg = CanonicalProfile.GoalWeightKg;
        profile.WeeklyGoalKg = CanonicalProfile.WeeklyGoalKg;
        profile.CustomCalorieTarget = CanonicalProfile.CustomCalorieTarget;
    }
}

public static class ProfileFormProcessor
{
    private const decimal CentimetresPerInch = 2.54m;
    private const decimal InchesPerFoot = 12m;

    public static ProfileFormResult Process(
        ProfileFormInput input,
        UserProfile? savedProfile,
        DateOnly today,
        IEnumerable<string> invalidInputFields)
    {
        var profile = new UserProfile
        {
            MeasurementSystem = input.Profile.MeasurementSystem,
            ThemePreference = input.Profile.ThemePreference,
            DateOfBirth = input.Profile.DateOfBirth,
            HeightCm = input.Profile.HeightCm,
            WeightKg = input.Profile.WeightKg,
            CalculationSex = input.Profile.CalculationSex,
            ActivityLevel = input.Profile.ActivityLevel,
            Goal = input.Profile.Goal,
            GoalWeightKg = input.Profile.GoalWeightKg,
            WeeklyGoalKg = input.Profile.WeeklyGoalKg,
            CustomCalorieTarget = input.Profile.CustomCalorieTarget,
        };
        var goalWeightLb = input.GoalWeightLb;
        var errors = new List<ProfileFormError>();
        var clearedFields = new List<string>();
        var invalidFields = invalidInputFields.ToHashSet(StringComparer.OrdinalIgnoreCase);

        void AddError(string field, string message) => errors.Add(new(field, message));
        void ClearField(string field)
        {
            invalidFields.Remove(field);
            errors.RemoveAll(error => error.Field == field);
            clearedFields.Add(field);
        }

        // Keep this order: conversion, cross-field checks, stale-field cleanup,
        // then calculated-target validation only when no other errors remain.
        ValidateBasicProfileFields();
        ApplyImperialConversions();
        ValidateGoalFields(savedProfile);
        ApplyCalorieTargetMode();
        ValidateCalculatedTarget();
        return new(profile, goalWeightLb, errors, clearedFields);

        void ValidateBasicProfileFields()
        {
            if (profile.MeasurementSystem != ProfileOptions.Metric &&
                profile.MeasurementSystem != ProfileOptions.Imperial)
            {
                AddError(
                    "UserProfile.MeasurementSystem",
                    "Please select a valid measurement system.");
            }

            if (profile.ThemePreference != ProfileOptions.SystemTheme &&
                profile.ThemePreference != ProfileOptions.LightTheme &&
                profile.ThemePreference != ProfileOptions.DarkTheme)
            {
                AddError(
                    "UserProfile.ThemePreference",
                    "Please select a valid theme.");
            }

            if (profile.CalculationSex != ProfileOptions.Male &&
                profile.CalculationSex != ProfileOptions.Female)
            {
                AddError(
                    "UserProfile.CalculationSex",
                    "Please select a valid calculation sex.");
            }

            if (!ProfileOptions.ActivityLevels.Contains(profile.ActivityLevel))
            {
                AddError(
                    "UserProfile.ActivityLevel",
                    "Please select a valid activity level.");
            }

            if (!ProfileOptions.Goals.Contains(profile.Goal))
            {
                AddError(
                    "UserProfile.Goal",
                    "Please select a valid goal.");
            }

            if (profile.DateOfBirth.HasValue &&
                DateOnly.FromDateTime(profile.DateOfBirth.Value) > today)
            {
                AddError(
                    "UserProfile.DateOfBirth",
                    "Date of birth cannot be in the future.");
            }

            if (profile.DateOfBirth.HasValue &&
                (profile.CalculateAge(today) < 18 ||
                 profile.CalculateAge(today) > 120))
            {
                AddError(
                    "UserProfile.DateOfBirth",
                    "You must be between 18 and 120 years old.");
            }
        }

        void ApplyImperialConversions()
        {
            if (profile.MeasurementSystem != ProfileOptions.Imperial)
            {
                return;
            }

            if (!input.HeightFeet.HasValue)
            {
                AddError(
                    "HeightFeet",
                    "Please enter your height in feet.");
            }

            if (!input.HeightInches.HasValue)
            {
                AddError(
                    "HeightInches",
                    "Please enter your remaining height in inches.");
            }

            if (input.HeightFeet.HasValue && input.HeightInches.HasValue)
            {
                var totalInches =
                    (input.HeightFeet.Value * InchesPerFoot) + input.HeightInches.Value;

                profile.HeightCm = totalInches * CentimetresPerInch;

                ClearField("UserProfile.HeightCm");

                if (profile.HeightCm < 50 ||
                    profile.HeightCm > 300)
                {
                    AddError(
                        "HeightFeet",
                        "Height must convert to between 50 cm and 300 cm.");
                }
            }

            if (!input.WeightLb.HasValue)
            {
                AddError(
                    "WeightLb",
                    "Please enter your current weight.");
            }
            else
            {
                profile.WeightKg =
                    input.WeightLb.Value / ProfileOptions.PoundsPerKilogram;

                ClearField("UserProfile.WeightKg");

                if (profile.WeightKg < 20 ||
                    profile.WeightKg > 500)
                {
                    AddError(
                        "WeightLb",
                        "Weight must convert to between 20 kg and 500 kg.");
                }
            }

            if (goalWeightLb.HasValue)
            {
                profile.GoalWeightKg =
                    goalWeightLb.Value / ProfileOptions.PoundsPerKilogram;

                ClearField("UserProfile.GoalWeightKg");

                if (profile.GoalWeightKg < 20 ||
                    profile.GoalWeightKg > 500)
                {
                    AddError(
                        "GoalWeightLb",
                        "Goal weight must convert to between 20 kg and 500 kg.");
                }
            }
            else
            {
                profile.GoalWeightKg = null;
                ClearField("UserProfile.GoalWeightKg");
            }
        }

        void ValidateGoalFields(UserProfile? existingProfile)
        {
            if ((profile.Goal == ProfileOptions.Lose ||
                 profile.Goal == ProfileOptions.Gain) &&
                !profile.GoalWeightKg.HasValue)
            {
                var fieldName = profile.MeasurementSystem == ProfileOptions.Imperial
                    ? "GoalWeightLb"
                    : "UserProfile.GoalWeightKg";

                AddError(
                    fieldName,
                    "Please enter a goal weight.");
            }

            if ((profile.Goal == ProfileOptions.Lose ||
                 profile.Goal == ProfileOptions.Gain) &&
                !profile.WeeklyGoalKg.HasValue)
            {
                AddError(
                    "UserProfile.WeeklyGoalKg",
                    "Please select a weekly weight change.");
            }

            if (profile.WeeklyGoalKg.HasValue &&
                !ProfileOptions.WeeklyGoals.Contains(profile.WeeklyGoalKg.Value))
            {
                AddError(
                    "UserProfile.WeeklyGoalKg",
                    "Please select a valid weekly weight change.");
            }

            var existingGoalIsUnchanged =
                existingProfile != null &&
                existingProfile.Goal == profile.Goal &&
                existingProfile.GoalWeightKg.HasValue &&
                profile.GoalWeightKg.HasValue &&
                Math.Abs(
                    existingProfile.GoalWeightKg.Value -
                    profile.GoalWeightKg.Value) < 0.01m;

            if (profile.Goal == ProfileOptions.Lose &&
                profile.GoalWeightKg.HasValue &&
                profile.GoalWeightKg.Value >= profile.WeightKg &&
                !existingGoalIsUnchanged)
            {
                var fieldName = profile.MeasurementSystem == ProfileOptions.Imperial
                    ? "GoalWeightLb"
                    : "UserProfile.GoalWeightKg";

                AddError(
                    fieldName,
                    "Your goal weight must be lower than your current weight.");
            }

            if (profile.Goal == ProfileOptions.Gain &&
                profile.GoalWeightKg.HasValue &&
                profile.GoalWeightKg.Value <= profile.WeightKg &&
                !existingGoalIsUnchanged)
            {
                var fieldName = profile.MeasurementSystem == ProfileOptions.Imperial
                    ? "GoalWeightLb"
                    : "UserProfile.GoalWeightKg";

                AddError(
                    fieldName,
                    "Your goal weight must be higher than your current weight.");
            }
        }

        void ApplyCalorieTargetMode()
        {
            if (input.UseCustomCalorieTarget &&
                !profile.CustomCalorieTarget.HasValue)
            {
                AddError(
                    "UserProfile.CustomCalorieTarget",
                    "Please enter a custom calorie target.");
            }
            else if (!input.UseCustomCalorieTarget)
            {
                profile.CustomCalorieTarget = null;
                ClearField("UserProfile.CustomCalorieTarget");
            }

            if (profile.Goal == ProfileOptions.Maintain)
            {
                // Maintain has no weight-change inputs; clear stale values from an earlier goal.
                profile.GoalWeightKg = null;
                profile.WeeklyGoalKg = null;
                goalWeightLb = null;
                ClearField("UserProfile.GoalWeightKg");
                ClearField("UserProfile.WeeklyGoalKg");
                ClearField("GoalWeightLb");
            }
        }

        void ValidateCalculatedTarget()
        {
            // A custom target remains valid even when the unused calculated target is non-positive.
            if ((invalidFields.Count == 0 && errors.Count == 0) &&
                !input.UseCustomCalorieTarget &&
                profile.CalculateDailyCalorieTarget(today) <= 0)
            {
                AddError(
                    string.Empty,
                    "These profile values do not produce a valid calculated calorie target.");
            }
        }

    }
}
