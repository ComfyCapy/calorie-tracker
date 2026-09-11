const customisationPage = document.querySelector(".customisation-page");

function getAntiForgeryToken() {
    return document.querySelector(
        'input[name="__RequestVerificationToken"]'
    ).value;
}

async function provisionCapy() {
    const status = document.getElementById("capy-provision-status");
    if (status) status.hidden = false;

    const body = new URLSearchParams();
    body.append("__RequestVerificationToken", getAntiForgeryToken());

    try {
        const response = await fetch(
            `${window.location.pathname}?handler=Provision`,
            {
                method: "POST",
                headers: {
                    "Content-Type":
                        "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body
            }
        );

        if (!response.ok) throw new Error("Provisioning failed.");
        window.location.reload();
    } catch {
        if (status) status.hidden = true;
        const error = document.getElementById("capy-error");
        error.textContent = "We couldn't set up your Capy. ";
        error.hidden = false;

        const retry = document.createElement("button");
        retry.type = "button";
        retry.className = "btn btn-sm btn-outline-danger";
        retry.textContent = "Try again";
        retry.addEventListener("click", () => {
            error.hidden = true;
            void provisionCapy();
        });
        error.appendChild(retry);
    }
}

if (customisationPage?.dataset.needsProvisioning === "true") {
    void provisionCapy();
}

customisationPage.addEventListener("click", async event => {
    const button = event.target.closest(".capy-item-tile[data-category]");
    if (!button || button.disabled) return;
    document.getElementById("capy-error").hidden = true;

    const category = button.dataset.category;
    const itemId = button.dataset.itemId;
    const itemName = button.querySelector(
        ".capy-item-details strong"
    )?.textContent.trim();
    button.disabled = true;

    try {
        const body = new URLSearchParams();
        body.append("__RequestVerificationToken", getAntiForgeryToken());
        body.append("category", category);
        body.append("itemId", itemId);

        const response = await fetch(
            `${window.location.pathname}?handler=Equip`,
            {
                method: "POST",
                headers: {
                    "Content-Type":
                        "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body
            }
        );

        if (!response.ok) {
            throw new Error(
                `Could not save Capy appearance. Status: ${response.status}`
            );
        }

        const result = await response.json();

        document.querySelectorAll(
            `.capy-item-tile[data-category="${category}"]`
        ).forEach(tile => {
            tile.classList.remove("is-equipped");
            tile.setAttribute("aria-pressed", "false");
            const status = tile.querySelector(".capy-item-status");
            if (status) status.textContent = "Unlocked";
        });

        button.classList.add("is-equipped");
        button.setAttribute("aria-pressed", "true");
        const selectedStatus = button.querySelector(".capy-item-status");
        if (selectedStatus) selectedStatus.textContent = "Equipped";

        const loadoutValue = document.querySelector(
            `[data-capy-loadout="${category}"]`
        );
        if (loadoutValue) {
            loadoutValue.textContent = itemId ? itemName : "None";
        }

        // Update every visible Capy renderer: the preview and navbar avatar.
        document.querySelectorAll(
            `[data-capy-layer="${result.category}"]`
        ).forEach(layer => {
            layer.innerHTML = "";

            if (result.imagePath) {
                const image = document.createElement("img");
                image.src = result.imagePath;
                image.alt = "";
                layer.appendChild(image);
            }
        });
    } catch (error) {
        console.error(error);
        const feedback = document.getElementById("capy-error");
        feedback.textContent =
            "We couldn't save your Capy's appearance. Please try again.";
        feedback.hidden = false;
    } finally {
        button.disabled = false;
    }
});

function updateSelectedTheme() {
    const selectedTheme =
        document.documentElement.dataset.themePreference || "system";

    document.querySelectorAll(".capy-theme-option").forEach(button => {
        const isSelected = button.dataset.theme === selectedTheme;
        button.classList.toggle("is-selected", isSelected);
        button.setAttribute("aria-pressed", isSelected ? "true" : "false");
    });
}

updateSelectedTheme();

document.querySelectorAll(".capy-theme-option").forEach(button => {
    button.addEventListener("click", updateSelectedTheme);
});

document.querySelectorAll(".capy-category-tab").forEach(button => {
    button.addEventListener("click", () => {
        const selectedCategory = button.dataset.categoryFilter;

        document.querySelectorAll("[data-capy-category-group]").forEach(
            group => {
                group.hidden = selectedCategory !== "all" &&
                    group.dataset.capyCategoryGroup !== selectedCategory;
            }
        );

        document.querySelectorAll(".capy-category-tab").forEach(tab => {
            const isSelected = tab === button;
            tab.classList.toggle("is-selected", isSelected);
            tab.setAttribute("aria-pressed", isSelected ? "true" : "false");
        });
    });
});

document.querySelectorAll(".capy-inventory-filter").forEach(button => {
    button.addEventListener("click", () => {
        const filter = button.dataset.filter;

        document.querySelectorAll(".capy-item-locked").forEach(item => {
            item.style.display = filter === "owned" ? "none" : "";
        });

        document.querySelectorAll(".capy-inventory-filter").forEach(
            filterButton => {
                const isSelected = filterButton.dataset.filter === filter;
                filterButton.classList.toggle("btn-primary", isSelected);
                filterButton.classList.toggle(
                    "btn-outline-secondary",
                    !isSelected
                );
                filterButton.setAttribute(
                    "aria-pressed",
                    isSelected ? "true" : "false"
                );
            }
        );
    });
});

document.querySelectorAll(".capy-secret-crown").forEach(crown => {
    crown.addEventListener("click", async () => {
        document.getElementById("capy-error").hidden = true;
        const itemId = crown.dataset.unlockItemId;

        document.querySelectorAll(".capy-secret-crown").forEach(
            secretCrown => {
                secretCrown.disabled = true;
            }
        );

        const body = new URLSearchParams();
        body.append("__RequestVerificationToken", getAntiForgeryToken());
        body.append("itemId", itemId);

        try {
            const response = await fetch(
                `${window.location.pathname}?handler=Unlock`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/x-www-form-urlencoded; charset=UTF-8"
                    },
                    body
                }
            );

            if (!response.ok) {
                throw new Error(
                    `Could not unlock Capy item. Status: ${response.status}`
                );
            }

            const result = await response.json();

            const message = document.getElementById("capy-unlock-message");
            message.textContent =
                `${result.name} unlocked. A cozy new choice is ready for your Capy.`;
            message.hidden = false;

            setTimeout(() => {
                window.location.reload();
            }, 900);
        } catch (error) {
            console.error(error);
            const feedback = document.getElementById("capy-error");
            feedback.textContent =
                "We couldn't unlock that item. Please try again.";
            feedback.hidden = false;
            document.querySelectorAll(".capy-secret-crown").forEach(
                secretCrown => {
                    secretCrown.disabled = false;
                }
            );
        }
    });
});
