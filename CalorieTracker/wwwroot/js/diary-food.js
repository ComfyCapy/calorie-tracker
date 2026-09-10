(() => {
        const foodId =
            document.getElementById("foodId");

        const foodSearch =
            document.getElementById("foodSearch");

        const foodResults =
            document.getElementById("foodResults");

        const selectedFoodDisplay =
            document.getElementById("selectedFoodDisplay");

        const selectedFoodName =
            document.getElementById("selectedFoodName");

        const quantityUnit =
            document.getElementById("quantityUnit");

        const measurementModeSection =
            document.getElementById("measurementModeSection");

        const exactMode =
            document.getElementById("exactMode");

        const portionMode =
            document.getElementById("portionMode");

        const approximateMode =
            document.getElementById("approximateMode");

        const exactQuantitySection =
            document.getElementById("exactQuantitySection");

        const portionSection =
            document.getElementById("portionSection");

        const approximationSection =
            document.getElementById("approximationSection");

        const approximationBaseSection =
            document.getElementById("approximationBaseSection");

        const approximationPortionSelect =
            document.getElementById("approximationPortionSelect");

        const approximationSummary =
            document.getElementById("approximationSummary");

        const portionSelect =
            document.getElementById("portionSelect");

        const portionQuantity =
            document.getElementById("portionQuantity");

        const portionSummary =
            document.getElementById("portionSummary");

        const portionQuantityLabel =
            document.getElementById("portionQuantityLabel");

        const portionModeLabel =
            document.getElementById("portionModeLabel");

        const foodSearchStatus =
            document.getElementById("foodSearchStatus");

        let activeSuggestionIndex = -1;

        const initialApproximationPortionId =
            typeof selectedApproximationPortionId === "undefined"
                ? ""
                : selectedApproximationPortionId;


        function getSelectedFood() {
            const id =
                parseInt(foodId.value);

            if (!id) {
                return null;
            }

            return foods.find(food =>
                food.id === id
            ) || null;
        }


        function getFoodCategory(food) {
            if (food.isFavourite) {
                return "Favourite";
            }

            if (food.isCustom) {
                return "Custom";
            }

            return "";
        }

        function setSuggestionsVisibility(isVisible) {
            foodResults.style.display =
                isVisible ? "block" : "none";

            foodSearch.setAttribute(
                "aria-expanded",
                isVisible ? "true" : "false"
            );

            if (!isVisible) {
                activeSuggestionIndex = -1;
                foodSearch.removeAttribute("aria-activedescendant");
            }
        }

        function updateActiveSuggestion(index) {
            const options =
                foodResults.querySelectorAll('[role="option"]');

            if (options.length === 0) {
                activeSuggestionIndex = -1;
                foodSearch.removeAttribute("aria-activedescendant");
                return;
            }

            activeSuggestionIndex =
                (index + options.length) % options.length;

            options.forEach((option, optionIndex) => {
                const isActive =
                    optionIndex === activeSuggestionIndex;

                option.setAttribute(
                    "aria-selected",
                    isActive ? "true" : "false"
                );

                option.classList.toggle(
                    "active",
                    isActive
                );
            });

            const activeOption =
                options[activeSuggestionIndex];

            foodSearch.setAttribute(
                "aria-activedescendant",
                activeOption.id
            );

            activeOption.scrollIntoView({
                block: "nearest"
            });
        }


        function renderFoodResults() {
            const searchTerm =
                foodSearch.value
                    .trim()
                    .toLowerCase();

            foodResults.innerHTML = "";
            activeSuggestionIndex = -1;
            foodSearch.removeAttribute("aria-activedescendant");

            const matches = foods
                .filter(food =>
                    food.name
                        .toLowerCase()
                        .includes(searchTerm))
                .slice(0, 10);

            if (matches.length === 0) {
                const emptyResult =
                    document.createElement("div");

                emptyResult.className =
                    "list-group-item text-muted";

                emptyResult.textContent =
                    "No foods found.";

                foodResults.appendChild(
                    emptyResult
                );

                foodSearchStatus.textContent =
                    "No foods found.";

                setSuggestionsVisibility(true);

                return;
            }

            foodSearchStatus.textContent =
                `${matches.length} food suggestion${matches.length === 1 ? "" : "s"} available.`;

            matches.forEach(food => {
                const button =
                    document.createElement("button");

                button.type = "button";

                button.id =
                    `food-option-${food.id}`;

                button.setAttribute(
                    "role",
                    "option"
                );

                button.setAttribute(
                    "aria-selected",
                    "false"
                );

                button.className =
                    "list-group-item list-group-item-action d-flex justify-content-between align-items-center";

                const name =
                    document.createElement("span");

                name.textContent =
                    food.name;

                const categoryName =
                    getFoodCategory(food);

                button.appendChild(name);
                if (categoryName) {
                    const category =
                        document.createElement("small");

                    category.className =
                        "text-muted";

                    category.textContent =
                        categoryName;

                    button.appendChild(category);
                }

                button.addEventListener(
                    "click",
                    () => selectFood(food)
                );

                foodResults.appendChild(
                    button
                );
            });

            setSuggestionsVisibility(true);
        }


        function selectFood(food, preserveInitialValue = false) {
            foodId.value =
                food.id;

            foodSearch.value =
                preserveInitialValue && foodSearch.dataset.initialName
                    ? foodSearch.dataset.initialName
                    : food.name;

            setSuggestionsVisibility(false);

            foodSearchStatus.textContent =
                `Selected ${food.name}.`;

            updateFood(preserveInitialValue);
        }

        function updateSelectedFoodDisplay() {
            const selectedFood =
                getSelectedFood();

            if (!selectedFood) {
                selectedFoodDisplay.style.display =
                    "none";

                selectedFoodName.textContent =
                    "";

                return;
            }

            selectedFoodName.textContent =
                foodSearch.value || selectedFood.name;

            selectedFoodDisplay.style.display =
                "block";
        }

        function updateQuantityUnit(preserveInitialValue = false) {
            const selectedFood =
                getSelectedFood();

            quantityUnit.textContent =
                preserveInitialValue && quantityUnit.dataset.initialUnit
                    ? quantityUnit.dataset.initialUnit
                    : selectedFood?.unit || "";
        }


        function updatePortionOptions(preserveInitialValue = false) {
            const selectedFood =
                getSelectedFood();

            portionSelect.innerHTML =
                '<option value="">Select a portion...</option>';

            approximationPortionSelect.innerHTML =
                '<option value="">Select a serving...</option>';

            if (!selectedFood) {
                return;
            }

            selectedFood.portions.forEach(
                portion => {

                    const option =
                        document.createElement("option");

                    option.value =
                        portion.id;

                    const portionName =
                        preserveInitialValue &&
                        portion.id.toString() === selectedPortionId &&
                        portionSelect.dataset.initialPortionName
                            ? portionSelect.dataset.initialPortionName
                            : portion.name;

                    option.textContent = selectedFood.isUsda
                        ? portionName
                        : `${portionName} (${portion.amount} ${selectedFood.unit})`;

                    if (
                        portion.id.toString() ===
                        selectedPortionId
                    ) {
                        option.selected = true;
                    }

                    portionSelect.appendChild(
                        option
                    );

                    const approximationOption =
                        document.createElement("option");

                    approximationOption.value = portion.id;
                    approximationOption.textContent = option.textContent;
                    approximationOption.selected =
                        portion.id.toString() ===
                        initialApproximationPortionId;

                    approximationPortionSelect.appendChild(
                        approximationOption
                    );
                }
            );

            if (!approximationPortionSelect.value &&
                selectedFood.portions.length > 0) {
                approximationPortionSelect.value =
                    selectedFood.portions[0].id.toString();
            }
        }


        function updatePortionAvailability() {
            const selectedFood =
                getSelectedFood();

            portionModeLabel.textContent = selectedFood?.isUsda
                ? "Portion or measure"
                : "Named portion";

            if (!selectedFood) {
                measurementModeSection.style.display =
                    "none";

                return;
            }

            const hasPortions =
                selectedFood.portions.length > 0;

            const hasApproximationBase =
                hasPortions || selectedFood.isDirectPortion;

            portionMode.disabled = !hasPortions;
            approximateMode.disabled = !hasApproximationBase;

            if (hasPortions || hasApproximationBase) {
                measurementModeSection.style.display =
                    "block";
            }
            else {
                measurementModeSection.style.display =
                    "none";

                exactMode.checked = true;
                portionMode.checked = false;
                approximateMode.checked = false;
            }
        }

        function updateApproximationSummary() {
            const selectedFood = getSelectedFood();
            const size = ["Small", "Medium", "Large"]
                .find(label =>
                    document.getElementById(`approximation${label}`).checked
                );
            const multiplier = new Map([
                ["Small", 0.75],
                ["Medium", 1],
                ["Large", 1.5]
            ]).get(size);
            const basePortion = selectedFood?.portions.find(portion =>
                portion.id.toString() === approximationPortionSelect.value
            );

            approximationBaseSection.style.display =
                selectedFood?.portions.length > 0 ? "block" : "none";

            if (!approximateMode.checked ||
                !selectedFood ||
                !multiplier ||
                (!basePortion && !selectedFood.isDirectPortion)) {
                approximationSummary.textContent = "";
                approximationSummary.hidden = true;
                return;
            }

            const baseAmount = basePortion
                ? basePortion.canonicalAmount
                : selectedFood.canonicalServingSize;
            const totalAmount = baseAmount * multiplier;
            const nutritionFactor =
                totalAmount / selectedFood.canonicalServingSize;
            const format = new Intl.NumberFormat(
                undefined,
                { maximumFractionDigits: 1 }
            );
            const basis = basePortion
                ? `${basePortion.name} (${format.format(basePortion.amount)} ${selectedFood.unit})`
                : `${format.format(selectedFood.canonicalServingSize)} ${selectedFood.unit}`;

            approximationSummary.textContent =
                `${size} estimate based on ${basis}: about ` +
                `${format.format(selectedFood.calories * nutritionFactor)} kcal, ` +
                `${format.format(selectedFood.protein * nutritionFactor)}g protein, ` +
                `${format.format(selectedFood.carbohydrates * nutritionFactor)}g carbs and ` +
                `${format.format(selectedFood.fat * nutritionFactor)}g fat. ` +
                "This will be saved as an estimate.";
            approximationSummary.hidden = false;
        }


        function updatePortionSummary() {
            const selectedFood =
                getSelectedFood();

            const selectedPortion =
                selectedFood?.portions.find(portion =>
                    portion.id.toString() === portionSelect.value
                );

            const quantity =
                Number.parseFloat(portionQuantity.value);

            const portionName = selectedPortion &&
                selectedPortion.id.toString() === selectedPortionId &&
                portionSelect.dataset.initialPortionName
                    ? portionSelect.dataset.initialPortionName
                    : selectedPortion?.name;

            // Presentation only: these are counts of an explicitly selected,
            // stored portion. Never derive density, eligibility, or other units
            // from a label (cached portions can also be user-authored/edited).
            const unit = selectedFood?.isUsda && selectedFood.unit === "g"
                ? new Map([
                    ["1 fl oz", "fl oz"], ["1 cup", "cup"],
                    ["1 tbsp", "tbsp"], ["1 tsp", "tsp"]
                ]).get(portionName?.trim().replace(/\s+/g, " ").toLowerCase())
                : null;

            portionQuantityLabel.textContent = unit
                ? `Amount (${unit})`
                : "Number of servings";

            if (
                !portionMode.checked ||
                !selectedPortion ||
                !Number.isFinite(quantity) ||
                quantity <= 0
            ) {
                portionSummary.textContent = "";
                return;
            }

            const numberFormat = new Intl.NumberFormat(
                undefined,
                { maximumFractionDigits: 4 }
            );

            const totalAmount =
                quantity * selectedPortion.amount;

            const total =
                `${numberFormat.format(totalAmount)} ${selectedFood.unit}`;

            portionSummary.textContent = selectedFood.isUsda
                ? (unit
                    ? `${numberFormat.format(quantity)} ${unit} `
                    : `${numberFormat.format(quantity)} × ${portionName} `) +
                  `(${total} used for nutrition)`
                : `${numberFormat.format(quantity)} × ${portionName} ` +
                  `(${total} total)`;
        }


        function updateMeasurementMode() {
            const selectedFood =
                getSelectedFood();

            if (!selectedFood) {
                exactQuantitySection.style.display =
                    "none";

                portionSection.style.display =
                    "none";

                approximationSection.style.display =
                    "none";

                return;
            }

            const hasPortions =
                selectedFood.portions.length > 0;

            if (
                portionMode.checked &&
                hasPortions
            ) {
                exactQuantitySection.style.display =
                    "none";

                portionSection.style.display =
                    "block";

                approximationSection.style.display =
                    "none";
            }
            else if (
                approximateMode.checked &&
                (hasPortions || selectedFood.isDirectPortion)
            ) {
                exactQuantitySection.style.display =
                    "none";

                portionSection.style.display =
                    "none";

                approximationSection.style.display =
                    "block";
            }
            else {
                exactQuantitySection.style.display =
                    "block";

                portionSection.style.display =
                    "none";

                approximationSection.style.display =
                    "none";
            }
        }


        function updateFood(preserveInitialValue = false) {
            document.getElementById("measurementFields").hidden = !getSelectedFood();
            updateSelectedFoodDisplay();
            updateQuantityUnit(preserveInitialValue);
            updatePortionOptions(preserveInitialValue);
            updatePortionAvailability();
            updateMeasurementMode();
            updatePortionSummary();
            updateApproximationSummary();
        }


        foodSearch.addEventListener(
            "input",
            () => {
                const selectedFood =
                    getSelectedFood();

                if (
                    selectedFood &&
                    foodSearch.value !== selectedFood.name
                ) {
                    foodId.value = "";

                    updateFood();
                }

                renderFoodResults();
            }
        );

        foodSearch.addEventListener(
            "focus",
            renderFoodResults
        );

        foodSearch.addEventListener(
            "keydown",
            event => {
                const options =
                    foodResults.querySelectorAll('[role="option"]');

                if (event.key === "ArrowDown") {
                    event.preventDefault();
                    if (options.length === 0) {
                        renderFoodResults();
                    }
                    updateActiveSuggestion(activeSuggestionIndex + 1);
                }
                else if (event.key === "ArrowUp") {
                    event.preventDefault();
                    updateActiveSuggestion(activeSuggestionIndex - 1);
                }
                else if (event.key === "Enter" &&
                         activeSuggestionIndex >= 0 &&
                         options[activeSuggestionIndex]) {
                    event.preventDefault();
                    options[activeSuggestionIndex].click();
                }
                else if (event.key === "Escape") {
                    event.preventDefault();
                    setSuggestionsVisibility(false);
                }
            }
        );

        exactMode.addEventListener(
            "change",
            () => {
                updateMeasurementMode();
                updatePortionSummary();
                updateApproximationSummary();
            }
        );

        portionMode.addEventListener(
            "change",
            () => {
                updateMeasurementMode();
                updatePortionSummary();
                updateApproximationSummary();
            }
        );

        approximateMode.addEventListener(
            "change",
            () => {
                updateMeasurementMode();
                updateApproximationSummary();
            }
        );

        portionSelect.addEventListener(
            "change",
            updatePortionSummary
        );

        portionQuantity.addEventListener(
            "input",
            updatePortionSummary
        );

        approximationPortionSelect.addEventListener(
            "change",
            updateApproximationSummary
        );

        ["Small", "Medium", "Large"].forEach(label =>
            document.getElementById(`approximation${label}`).addEventListener(
                "change",
                updateApproximationSummary
            )
        );


        document.addEventListener(
            "click",
            event => {
                if (
                    !foodSearch.contains(event.target) &&
                    !foodResults.contains(event.target)
                ) {
                    setSuggestionsVisibility(false);
                }
            }
        );


        const initialFood =
            getSelectedFood();

        if (initialFood) {
            selectFood(initialFood, true);
        }

})();
