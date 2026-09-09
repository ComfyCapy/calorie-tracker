using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CalorieTracker.Data;
using CalorieTracker.Services;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Models
{
    public class UserProfile
    {
        [BindNever]
        public int Id { get; set; }

        [BindNever]
        public string UserId { get; set; } = string.Empty;
        [BindNever]
        public ApplicationUser? User { get; set; }

        [Required]
        public string MeasurementSystem { get; set; } = ProfileOptions.Metric;

        [Required]
        public string ThemePreference { get; set; } = ProfileOptions.SystemTheme;

        [Required(ErrorMessage = "Please enter your date of birth.")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Range(
            50,
            300,
            ErrorMessage = "Please enter a height between 50 cm and 300 cm.")]
        public decimal HeightCm { get; set; }

        [Range(
            20,
            500,
            ErrorMessage = "Please enter a weight between 20 kg and 500 kg.")]
        public decimal WeightKg { get; set; }

        [Required(ErrorMessage = "Please select a sex for the calorie calculation.")]
        public string CalculationSex { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select your activity level.")]
        public string ActivityLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select your goal.")]
        public string Goal { get; set; } = string.Empty;

        [Range(
            20,
            500,
            ErrorMessage = "Please enter a goal weight between 20 kg and 500 kg.")]
        public decimal? GoalWeightKg { get; set; }

        [Range(
            0.25,
            1.0,
            ErrorMessage = "Please select a weekly weight change between 0.25 kg and 1 kg.")]
        public decimal? WeeklyGoalKg { get; set; }

        [Range(
            500,
            10000,
            ErrorMessage = "Please enter a calorie target between 500 and 10,000 kcal.")]
        public decimal? CustomCalorieTarget { get; set; }

        [NotMapped]
        public decimal BMI
        {
            get
            {
                if (HeightCm <= 0)
                {
                    return 0;
                }

                var heightMetres = HeightCm / 100;

                return WeightKg / (heightMetres * heightMetres);
            }
        }

        /// <summary>
        /// Calculates completed years of age on a specific calendar date.
        /// </summary>
        public int CalculateAge(DateOnly date)
        {
            if (!DateOfBirth.HasValue)
            {
                return 0;
            }

            var dateOfBirth = DateOfBirth.Value;
            var age = date.Year - dateOfBirth.Year;
            // DateTime.AddYears (used by the original current-day calculation) advances
            // a Feb 29 birthday on Mar 1 in non-leap years; retain that behavior here.
            var birthdayThisYear = dateOfBirth.Month == 2 &&
                                   dateOfBirth.Day == 29 &&
                                   !DateTime.IsLeapYear(date.Year)
                ? new DateOnly(date.Year, 3, 1)
                : new DateOnly(date.Year, dateOfBirth.Month, dateOfBirth.Day);

            if (date < birthdayThisYear)
            {
                age--;
            }

            return age;
        }

        /// <summary>
        /// Calculates BMR using this profile's inputs as they apply on a specific date.
        /// </summary>
        public decimal CalculateBmr(DateOnly date)
        {
            var baseBmr =
                (10 * WeightKg) +
                (6.25m * HeightCm) -
                (5 * CalculateAge(date));

            return CalculationSex switch
            {
                ProfileOptions.Male => baseBmr + 5,
                ProfileOptions.Female => baseBmr - 161,
                _ => 0
            };
        }

        /// <summary>
        /// Calculates maintenance calories (TDEE) using this profile's inputs as they apply on a specific date.
        /// </summary>
        public decimal CalculateTdee(DateOnly date)
        {
            return CalculateBmr(date) * GetActivityMultiplier();
        }

        /// <summary>
        /// Tries to calculate maintenance calories for a date when the profile has supported maintenance inputs.
        /// Goal and calorie-target settings are intentionally not considered.
        /// </summary>
        public bool TryCalculateMaintenance(DateOnly date, out decimal maintenanceCalories)
        {
            maintenanceCalories = 0;

            if (!DateOfBirth.HasValue ||
                CalculateAge(date) is < 18 or > 120 ||
                HeightCm is < 50 or > 300 ||
                WeightKg is < 20 or > 500 ||
                (CalculationSex != ProfileOptions.Male &&
                 CalculationSex != ProfileOptions.Female) ||
                !ProfileOptions.ActivityLevels.Contains(ActivityLevel))
            {
                return false;
            }

            var tdee = CalculateTdee(date);

            if (tdee <= 0)
            {
                return false;
            }

            maintenanceCalories = tdee;
            return true;
        }

        private decimal GetActivityMultiplier() => ActivityLevel switch
        {
            ProfileOptions.Sedentary => 1.2m,
            ProfileOptions.LightlyActive => 1.375m,
            ProfileOptions.ModeratelyActive => 1.55m,
            ProfileOptions.VeryActive => 1.725m,
            ProfileOptions.ExtraActive => 1.9m,
            _ => 0
        };
        public decimal CalculateDailyCalorieTarget(DateOnly date)
        {
            var tdee = CalculateTdee(date);

            if (tdee <= 0)
            {
                return 0;
            }

            if (Goal == ProfileOptions.Maintain)
            {
                return tdee;
            }

            if (WeeklyGoalKg == null)
            {
                return tdee;
            }

            var dailyAdjustment = (WeeklyGoalKg.Value * 7700) / 7;

            return Goal switch
            {
                ProfileOptions.Lose => tdee - dailyAdjustment,
                ProfileOptions.Gain => tdee + dailyAdjustment,
                _ => tdee
            };
        }

        public decimal CalculateEffectiveCalorieTarget(DateOnly date) =>
            CustomCalorieTarget ?? CalculateDailyCalorieTarget(date);

        public bool HasUsableCalorieEstimatesOn(DateOnly date)
        {
            var age = CalculateAge(date);
            var bmr = CalculateBmr(date);
            var tdee = CalculateTdee(date);
            var effectiveCalorieTarget = CalculateEffectiveCalorieTarget(date);

            return DateOfBirth.HasValue &&
                age is >= 18 and <= 120 &&
                HeightCm is >= 50 and <= 300 &&
                WeightKg is >= 20 and <= 500 &&
                (CalculationSex == ProfileOptions.Male ||
                 CalculationSex == ProfileOptions.Female) &&
                ProfileOptions.ActivityLevels.Contains(ActivityLevel) &&
                BMI > 0 &&
                bmr > 0 &&
                tdee > 0 &&
                effectiveCalorieTarget > 0 &&
                (!CustomCalorieTarget.HasValue ||
                 CustomCalorieTarget.Value is >= 500 and <= 10000);
        }
    }
}
