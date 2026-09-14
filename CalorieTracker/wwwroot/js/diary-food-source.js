(() => {
    const dateInput = document.getElementById("DiaryEntry_Date");
    const mealInput = document.getElementById("DiaryEntry_MealType");

    function currentContext() {
        return {
            date: dateInput?.value || "",
            meal: mealInput?.value || ""
        };
    }

    document.querySelectorAll("[data-diary-food-source-link]")
        .forEach(link => {
            link.addEventListener("click", () => {
                const context = currentContext();
                const url = new URL(link.href);

                if (context.date) {
                    url.searchParams.set("date", context.date);
                }

                if (context.meal) {
                    url.searchParams.set("meal", context.meal);
                }

                link.href = url.toString();
            });
        });

    const communitySearch = document.getElementById(
        "diary-community-search");

    communitySearch?.addEventListener("submit", () => {
        const context = currentContext();
        const date = document.querySelector("[data-diary-context-date]");
        const meal = document.querySelector("[data-diary-context-meal]");

        if (date) {
            date.value = context.date;
        }

        if (meal) {
            meal.value = context.meal;
        }
    });
})();
