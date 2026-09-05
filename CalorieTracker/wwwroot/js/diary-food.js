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

        const exactQuantitySection =
            document.getElementById("exactQuantitySection");

        const portionSection =
            document.getElementById("portionSection");

        const portionSelect =
            document.getElementById("portionSelect");


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

            return "Recent";
        }


        function renderFoodResults() {
            const searchTerm =
                foodSearch.value
                    .trim()
                    .toLowerCase();

            foodResults.innerHTML = "";

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

                foodResults.style.display =
                    "block";

                return;
            }

            matches.forEach(food => {
                const button =
                    document.createElement("button");

                button.type = "button";

                button.className =
                    "list-group-item list-group-item-action d-flex justify-content-between align-items-center";

                const name =
                    document.createElement("span");

                name.textContent =
                    food.name;

                const category =
                    document.createElement("small");

                category.className =
                    "text-muted";

                category.textContent =
                    getFoodCategory(food);

                button.appendChild(name);
                button.appendChild(category);

                button.addEventListener(
                    "click",
                    () => selectFood(food)
                );

                foodResults.appendChild(
                    button
                );
            });

            foodResults.style.display =
                "block";
        }


        function selectFood(food) {
            foodId.value =
                food.id;

            foodSearch.value =
                food.name;

            foodResults.style.display =
                "none";

            updateFood();
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
                selectedFood.name;

            selectedFoodDisplay.style.display =
                "block";
        }

        function updateQuantityUnit() {
            const selectedFood =
                getSelectedFood();

            quantityUnit.textContent =
                selectedFood?.unit || "";
        }


        function updatePortionOptions() {
            const selectedFood =
                getSelectedFood();

            portionSelect.innerHTML =
                '<option value="">Select a portion...</option>';

            if (!selectedFood) {
                return;
            }

            selectedFood.portions.forEach(
                portion => {

                    const option =
                        document.createElement("option");

                    option.value =
                        portion.id;

                    option.textContent =
                        `${portion.name} (${portion.amount} ${selectedFood.unit})`;

                    if (
                        portion.id.toString() ===
                        selectedPortionId
                    ) {
                        option.selected = true;
                    }

                    portionSelect.appendChild(
                        option
                    );
                }
            );
        }


        function updatePortionAvailability() {
            const selectedFood =
                getSelectedFood();

            if (!selectedFood) {
                measurementModeSection.style.display =
                    "none";

                return;
            }

            const hasPortions =
                selectedFood.portions.length > 0;

            if (hasPortions) {
                measurementModeSection.style.display =
                    "block";
            }
            else {
                measurementModeSection.style.display =
                    "none";

                exactMode.checked = true;
                portionMode.checked = false;
            }
        }


        function updateMeasurementMode() {
            const selectedFood =
                getSelectedFood();

            if (!selectedFood) {
                exactQuantitySection.style.display =
                    "none";

                portionSection.style.display =
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
            }
            else {
                exactQuantitySection.style.display =
                    "block";

                portionSection.style.display =
                    "none";
            }
        }


        function updateFood() {
            document.getElementById("measurementFields").hidden = !getSelectedFood();
            updateSelectedFoodDisplay();
            updateQuantityUnit();
            updatePortionOptions();
            updatePortionAvailability();
            updateMeasurementMode();
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

        exactMode.addEventListener(
            "change",
            updateMeasurementMode
        );

        portionMode.addEventListener(
            "change",
            updateMeasurementMode
        );


        document.addEventListener(
            "click",
            event => {
                if (
                    !foodSearch.contains(event.target) &&
                    !foodResults.contains(event.target)
                ) {
                    foodResults.style.display =
                        "none";
                }
            }
        );


        const initialFood =
            getSelectedFood();

        if (initialFood) {
            selectFood(initialFood);
        }

})();
