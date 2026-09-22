using CalorieTracker.Models;

namespace CalorieTracker.Services;

public sealed record DiaryMeasurementInput(
    string Mode,
    decimal Quantity,
    int? PortionId,
    decimal? PortionQuantity,
    int? EstimatePortionId,
    string EstimateSize);

public sealed record DiaryMeasurementError(string Field, string Message);

public sealed record DiaryMeasurementResult(
    decimal Quantity,
    FoodPortion? Portion,
    decimal EstimateMultiplier,
    bool HasEstimateBasis,
    IReadOnlyList<DiaryMeasurementError> Errors,
    IReadOnlyList<string> DerivedFields);

// Callers authorize the food and choose eligible portions. Historical amount policy
// stays in Edit; this resolver neither queries nor changes entities or snapshots.
public static class DiaryMeasurementResolver
{
    public static DiaryMeasurementResult Resolve(
        DiaryMeasurementInput input,
        Food? food,
        IReadOnlyCollection<FoodPortion> availablePortions,
        Func<FoodPortion, decimal>? portionAmount = null,
        Func<FoodPortion?, decimal>? estimateAmount = null)
    {
        var quantity = input.Quantity;
        FoodPortion? selectedPortion = null;
        var multiplier = 0m;
        var hasEstimateBasis = false;
        var errors = new List<DiaryMeasurementError>();
        var derivedFields = new List<string>();

        void Error(string field, string message) => errors.Add(new(field, message));

        void Multiply(decimal amount, decimal count, string field, string message)
        {
            try { quantity = checked(amount * count); }
            catch (OverflowException) { Error(field, message); }
        }

        if (input.Mode == "Approximate")
        {
            if (!ApproximatePortions.TryGetMultiplier(input.EstimateSize, out multiplier))
            {
                Error("ApproximationSize", "Please select a valid estimate.");
            }
            else if (food != null)
            {
                if (availablePortions.Count > 0)
                {
                    selectedPortion = availablePortions.FirstOrDefault(p => p.Id == input.EstimatePortionId);
                    if (selectedPortion == null)
                        Error("ApproximationPortionId", "Please select the serving your estimate is based on.");
                    else
                        hasEstimateBasis = true;
                }
                else if (food.ServingBasis == FoodServingBasis.Portion && food.CanonicalServingSize > 0)
                {
                    hasEstimateBasis = true;
                }
                else
                {
                    Error("MeasurementMode", "This food does not have a trustworthy serving to estimate from. Use an exact amount instead.");
                }

                if (hasEstimateBasis)
                {
                    var amount = estimateAmount != null
                        ? estimateAmount(selectedPortion)
                        : selectedPortion?.Amount ?? food.CanonicalServingSize;
                    // Legacy zero/negative serving amounts leave the posted quantity unchanged.
                    if (amount > 0)
                        Multiply(amount, multiplier, "ApproximationSize", "The estimated quantity is too large.");
                }
            }
            derivedFields.AddRange(["DiaryEntry.Quantity", "SelectedPortionId", "PortionQuantity"]);
        }
        else if (input.Mode == "Portion")
        {
            if (input.PortionId == null)
                Error("SelectedPortionId", "Please select a portion.");
            if (input.PortionQuantity == null || input.PortionQuantity <= 0)
                Error("PortionQuantity", "Portion quantity must be greater than 0.");

            if (input.PortionId != null && input.PortionQuantity > 0 && food != null)
            {
                selectedPortion = availablePortions.FirstOrDefault(p => p.Id == input.PortionId);
                if (selectedPortion == null)
                {
                    Error("SelectedPortionId", "The selected portion is not valid for this food.");
                }
                else
                {
                    var amount = portionAmount != null ? portionAmount(selectedPortion) : selectedPortion.Amount;
                    Multiply(amount, input.PortionQuantity.Value, "PortionQuantity", "The resulting quantity is too large.");
                    derivedFields.Add("DiaryEntry.Quantity");
                }
            }
        }
        else
        {
            derivedFields.AddRange(["SelectedPortionId", "PortionQuantity"]);
            if (input.Quantity <= 0)
                Error("DiaryEntry.Quantity", "Quantity must be greater than 0.");
            else if (food != null && food.ServingBasis != FoodServingBasis.Portion)
            {
                if (MeasurementUnits.TryToCanonical(input.Quantity, food.ServingUnit,
                        out var canonical, out _, out _))
                    quantity = canonical;
                else
                    Error("DiaryEntry.Quantity", "The quantity could not be converted.");
            }
        }

        return new(quantity, selectedPortion, multiplier, hasEstimateBasis, errors, derivedFields);
    }
}
