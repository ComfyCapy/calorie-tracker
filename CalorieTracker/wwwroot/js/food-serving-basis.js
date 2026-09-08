(() => {
    document.querySelectorAll("[data-serving-basis-form]").forEach(form => {
        const measuredFields = form.querySelector("#measuredServingFields");
        const portionFields = form.querySelector("#portionServingFields");
        const basisInputs = form.querySelectorAll(
            'input[name="Food.ServingBasis"]');

        if (!measuredFields || !portionFields || basisInputs.length === 0) {
            return;
        }

        const updateFields = () => {
            const selected = Array.from(basisInputs)
                .find(input => input.checked)?.value;
            const isPortion = selected === "Portion";

            measuredFields.hidden = isPortion;
            portionFields.hidden = !isPortion;
        };

        basisInputs.forEach(input =>
            input.addEventListener("change", updateFields));

        updateFields();
    });
})();
