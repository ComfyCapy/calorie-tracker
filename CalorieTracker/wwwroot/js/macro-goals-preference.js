(() => {
    const preferenceKey = "comfyCapy.showMacroGoals";
    const toggle = document.querySelector("[data-macro-goals-toggle]");
    const goalContent = document.querySelectorAll("[data-macro-goals-content]");
    const compactContent = document.querySelectorAll("[data-macro-goals-compact]");

    if (goalContent.length === 0) {
        return;
    }

    let showMacroGoals = false;

    try {
        showMacroGoals = localStorage.getItem(preferenceKey) === "true";
    }
    catch {
        // Keep the default compact view if storage is unavailable.
    }

    function applyPreference() {
        goalContent.forEach(element => {
            element.hidden = !showMacroGoals;
        });

        compactContent.forEach(element => {
            element.hidden = showMacroGoals;
        });

        if (toggle) {
            toggle.checked = showMacroGoals;
        }
    }

    toggle?.addEventListener("change", () => {
        showMacroGoals = toggle.checked;
        applyPreference();

        try {
            localStorage.setItem(
                preferenceKey,
                showMacroGoals.toString()
            );
        }
        catch {
            // The control still works for this page when storage is unavailable.
        }
    });

    applyPreference();
})();
