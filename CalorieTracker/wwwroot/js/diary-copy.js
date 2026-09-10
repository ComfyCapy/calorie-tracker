document.querySelectorAll("[data-copy-form]").forEach(form => {
    form.addEventListener("submit", () => {
        const submitButton = form.querySelector('button[type="submit"]');

        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = "Copying...";
        }
    });
});
