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
        public int Age
        {
            get
            {
                if (!DateOfBirth.HasValue)
                {
                    return 0;
                }

                var today = DateTime.Today;
                var dateOfBirth = DateOfBirth.Value;

                var age = today.Year - dateOfBirth.Year;

                if (dateOfBirth.Date > today.AddYears(-age))
                {
                    age--;
                }

                return age;
            }
        }

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

        [NotMapped]
        public decimal BMR
        {
            get
            {
                var baseBmr =
                    (10 * WeightKg) +
                    (6.25m * HeightCm) -
                    (5 * Age);

                return CalculationSex switch
                {
                    ProfileOptions.Male => baseBmr + 5,
                    ProfileOptions.Female => baseBmr - 161,
                    _ => 0
                };
            }
        }

        [NotMapped]
        public decimal TDEE
        {
            get
            {
                var activityMultiplier = ActivityLevel switch
                {
                    ProfileOptions.Sedentary => 1.2m,
                    ProfileOptions.LightlyActive => 1.375m,
                    ProfileOptions.ModeratelyActive => 1.55m,
                    ProfileOptions.VeryActive => 1.725m,
                    ProfileOptions.ExtraActive => 1.9m,
                    _ => 0
                };

                return BMR * activityMultiplier;
            }
        }
        [NotMapped]
        public decimal DailyCalorieTarget
        {
            get
            {
                if (TDEE <= 0)
                {
                    return 0;
                }

                if (Goal == ProfileOptions.Maintain)
                {
                    return TDEE;
                }

                if (WeeklyGoalKg == null)
                {
                    return TDEE;
                }

                var dailyAdjustment = (WeeklyGoalKg.Value * 7700) / 7;

                return Goal switch
                {
                    ProfileOptions.Lose => TDEE - dailyAdjustment,
                    ProfileOptions.Gain => TDEE + dailyAdjustment,
                    _ => TDEE
                };
            }
        }
        [NotMapped]
        public decimal EffectiveCalorieTarget
        {
            get
            {
                return CustomCalorieTarget ?? DailyCalorieTarget;
            }
        }
    }
}
