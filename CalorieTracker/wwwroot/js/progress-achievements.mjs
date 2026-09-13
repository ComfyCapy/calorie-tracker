const storageKey = "comfyCapy.hideUnlockedAchievements";

export function readHideUnlockedPreference(storage) {
    try {
        return storage?.getItem(storageKey) === "true";
    }
    catch {
        return false;
    }
}

export function writeHideUnlockedPreference(storage, hideUnlocked) {
    try {
        storage?.setItem(storageKey, hideUnlocked ? "true" : "false");
    }
    catch {
        // Local storage can be unavailable in privacy-restricted browsers.
    }
}

export function applyAchievementVisibility(cards, emptyState, hideUnlocked) {
    let unlockedCount = 0;
    let visibleCount = 0;

    cards.forEach(card => {
        const isUnlocked = card.dataset.achievementState === "unlocked";

        if (isUnlocked) {
            unlockedCount += 1;
        }

        card.hidden = hideUnlocked && isUnlocked;

        if (!card.hidden) {
            visibleCount += 1;
        }
    });

    if (emptyState) {
        emptyState.hidden = !hideUnlocked
            || unlockedCount === 0
            || visibleCount !== 0;
    }
}

export function initializeProgressAchievements(documentRef, storage) {
    const section = documentRef?.querySelector("[data-achievements-section]");

    if (!section) {
        return;
    }

    const toggle = section.querySelector("[data-hide-unlocked]");
    const cards = [...section.querySelectorAll("[data-achievement-state]")];
    const emptyState = section.querySelector("[data-achievements-empty]");

    if (!toggle) {
        return;
    }

    let hideUnlocked = readHideUnlockedPreference(storage);

    const render = () => {
        toggle.hidden = false;
        toggle.setAttribute("aria-pressed", String(hideUnlocked));
        applyAchievementVisibility(cards, emptyState, hideUnlocked);
    };

    toggle.addEventListener("click", () => {
        hideUnlocked = !hideUnlocked;
        writeHideUnlockedPreference(storage, hideUnlocked);
        render();
    });

    render();
}

if (typeof document !== "undefined") {
    let storage;

    try {
        storage = window.localStorage;
    }
    catch {
        storage = undefined;
    }

    initializeProgressAchievements(document, storage);
}
