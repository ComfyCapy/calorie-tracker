const metricSystem = document.getElementById("metricSystem");
const imperialSystem = document.getElementById("imperialSystem");
const heightCmInput = document.getElementById("UserProfile_HeightCm");
const weightKgInput = document.getElementById("UserProfile_WeightKg");
const goalWeightKgInput = document.getElementById("UserProfile_GoalWeightKg");
const heightFeetInput = document.getElementById("HeightFeet");
const heightInchesInput = document.getElementById("HeightInches");
const weightLbInput = document.getElementById("WeightLb");
const goalWeightLbInput = document.getElementById("GoalWeightLb");
const goalSelect = document.getElementById("goalSelect");
const weeklyGoalSelect = document.getElementById("weeklyGoalSelect");
const weeklyGoalSection = document.getElementById("weeklyGoalSection");
const calculatedTarget = document.getElementById("calculatedTarget");
const customTarget = document.getElementById("customTarget");
const customCalorieTargetSection = document.getElementById(
    "customCalorieTargetSection"
);
const metricHeightSection = document.getElementById("metricHeightSection");
const imperialHeightSection = document.getElementById("imperialHeightSection");
const metricWeightSection = document.getElementById("metricWeightSection");
const imperialWeightSection = document.getElementById("imperialWeightSection");
const metricGoalWeightSection = document.getElementById(
    "metricGoalWeightSection"
);
const imperialGoalWeightSection = document.getElementById(
    "imperialGoalWeightSection"
);
const metricGoalConsistencyError = document.getElementById(
    "metricGoalConsistencyError"
);
const imperialGoalConsistencyError = document.getElementById(
    "imperialGoalConsistencyError"
);

function convertMetricToImperial() {
    const heightCm = parseFloat(heightCmInput.value);
    const weightKg = parseFloat(weightKgInput.value);
    const goalWeightKg = parseFloat(goalWeightKgInput.value);

    if (!isNaN(heightCm)) {
        const totalInches = heightCm / 2.54;
        const feet = Math.floor(totalInches / 12);
        const inches = totalInches - (feet * 12);

        heightFeetInput.value = feet;
        heightInchesInput.value = inches.toFixed(1);
    }

    if (!isNaN(weightKg)) {
        weightLbInput.value = (weightKg * 2.2046226218).toFixed(1);
    }

    if (!isNaN(goalWeightKg)) {
        goalWeightLbInput.value = (goalWeightKg * 2.2046226218).toFixed(1);
    }
}

function convertImperialToMetric() {
    const feet = parseFloat(heightFeetInput.value);
    const inches = parseFloat(heightInchesInput.value);
    const weightLb = parseFloat(weightLbInput.value);
    const goalWeightLb = parseFloat(goalWeightLbInput.value);

    if (!isNaN(feet) && !isNaN(inches)) {
        const totalInches = (feet * 12) + inches;
        heightCmInput.value = (totalInches * 2.54).toFixed(1);
    }

    if (!isNaN(weightLb)) {
        weightKgInput.value = (weightLb / 2.2046226218).toFixed(1);
    }

    if (!isNaN(goalWeightLb)) {
        goalWeightKgInput.value = (goalWeightLb / 2.2046226218).toFixed(1);
    }
}

function updateWeeklyGoalLabels() {
    const useImperial = imperialSystem.checked;

    Array.from(weeklyGoalSelect.options).forEach(option => {
        if (option.dataset.metric && option.dataset.imperial) {
            option.textContent = useImperial
                ? option.dataset.imperial
                : option.dataset.metric;
        }
    });
}

function updateGoalFields() {
    const hasWeightChangeGoal =
        goalSelect.value === "Lose" || goalSelect.value === "Gain";

    weeklyGoalSection.style.display = hasWeightChangeGoal ? "block" : "none";

    if (!hasWeightChangeGoal) {
        metricGoalWeightSection.style.display = "none";
        imperialGoalWeightSection.style.display = "none";
        metricGoalConsistencyError.textContent = "";
        imperialGoalConsistencyError.textContent = "";
        return;
    }

    const useImperial = imperialSystem.checked;
    metricGoalWeightSection.style.display = useImperial ? "none" : "block";
    imperialGoalWeightSection.style.display = useImperial ? "block" : "none";
}

function validateGoalWeight() {
    metricGoalConsistencyError.textContent = "";
    imperialGoalConsistencyError.textContent = "";
    goalWeightKgInput.setAttribute("aria-invalid", "false");
    goalWeightLbInput.setAttribute("aria-invalid", "false");

    if (goalSelect.value !== "Lose" && goalSelect.value !== "Gain") {
        return;
    }

    const useImperial = imperialSystem.checked;
    const currentWeightInput = useImperial ? weightLbInput : weightKgInput;
    const goalWeightInput = useImperial
        ? goalWeightLbInput
        : goalWeightKgInput;
    const errorElement = useImperial
        ? imperialGoalConsistencyError
        : metricGoalConsistencyError;
    const currentWeight = parseFloat(currentWeightInput.value);
    const goalWeight = parseFloat(goalWeightInput.value);

    if (isNaN(currentWeight) || isNaN(goalWeight)) {
        return;
    }

    if (goalSelect.value === "Lose" && goalWeight >= currentWeight) {
        errorElement.textContent =
            "Your goal weight must be lower than your current weight.";
        goalWeightInput.setAttribute("aria-invalid", "true");
        return;
    }

    if (goalSelect.value === "Gain" && goalWeight <= currentWeight) {
        errorElement.textContent =
            "Your goal weight must be higher than your current weight.";
        goalWeightInput.setAttribute("aria-invalid", "true");
    }
}

function updateCalorieTargetMode() {
    customCalorieTargetSection.style.display = customTarget.checked
        ? "block"
        : "none";
}

function updateMeasurementSystem() {
    const useImperial = imperialSystem.checked;

    metricHeightSection.style.display = useImperial ? "none" : "block";
    metricWeightSection.style.display = useImperial ? "none" : "block";
    imperialHeightSection.style.display = useImperial ? "block" : "none";
    imperialWeightSection.style.display = useImperial ? "block" : "none";

    updateWeeklyGoalLabels();
    updateGoalFields();
}

metricSystem.addEventListener("change", () => {
    convertImperialToMetric();
    updateMeasurementSystem();
    validateGoalWeight();
});

imperialSystem.addEventListener("change", () => {
    convertMetricToImperial();
    updateMeasurementSystem();
    validateGoalWeight();
});

goalSelect.addEventListener("change", () => {
    updateGoalFields();
    validateGoalWeight();
});

weightKgInput.addEventListener("input", validateGoalWeight);
goalWeightKgInput.addEventListener("input", validateGoalWeight);
weightLbInput.addEventListener("input", validateGoalWeight);
goalWeightLbInput.addEventListener("input", validateGoalWeight);
calculatedTarget.addEventListener("change", updateCalorieTargetMode);
customTarget.addEventListener("change", updateCalorieTargetMode);

updateMeasurementSystem();
updateCalorieTargetMode();
validateGoalWeight();
