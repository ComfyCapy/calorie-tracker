(() => {
    const storageKey = "comfyCapy.developmentBannerDismissed";
    const banner = document.querySelector("[data-development-banner]");
    const dismissButton = document.querySelector(
        "[data-development-banner-dismiss]"
    );

    if (!banner || !dismissButton) {
        return;
    }

    try {
        if (sessionStorage.getItem(storageKey) === "true") {
            banner.hidden = true;
            return;
        }
    }
    catch {
        // Keep the banner visible if session storage is unavailable.
    }

    dismissButton.addEventListener("click", () => {
        banner.hidden = true;

        try {
            sessionStorage.setItem(storageKey, "true");
        }
        catch {
            // The banner still dismisses for this page when storage is unavailable.
        }
    });
})();
