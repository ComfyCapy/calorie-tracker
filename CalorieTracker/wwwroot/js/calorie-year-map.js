(() => {
    const maps = document.querySelectorAll(".calorie-year-map");

    maps.forEach(map => {
        const layer = map.querySelector(".calorie-year-tooltip-layer");
        const scroll = map.querySelector(".calorie-year-scroll");
        const days = map.querySelectorAll("a.calorie-year-day");

        if (!layer || !scroll || days.length === 0) {
            return;
        }

        let activeDay = null;
        let activeTooltip = null;

        const hideTooltip = () => {
            activeDay = null;
            activeTooltip?.remove();
            activeTooltip = null;
        };

        const positionTooltip = () => {
            if (!activeDay || !activeTooltip) {
                return;
            }

            const dayRect = activeDay.getBoundingClientRect();
            const tooltipRect = activeTooltip.getBoundingClientRect();
            const edgePadding = 8;
            const gap = 8;
            const maxLeft = window.innerWidth - tooltipRect.width - edgePadding;
            const left = Math.max(
                edgePadding,
                Math.min(
                    dayRect.left + (dayRect.width / 2) - (tooltipRect.width / 2),
                    Math.max(edgePadding, maxLeft)));
            let top = dayRect.top - tooltipRect.height - gap;

            if (top < edgePadding) {
                top = dayRect.bottom + gap;
            }

            top = Math.min(
                top,
                Math.max(edgePadding, window.innerHeight - tooltipRect.height - edgePadding));

            activeTooltip.style.left = `${left}px`;
            activeTooltip.style.top = `${top}px`;
        };

        const showTooltip = day => {
            const source = day.querySelector(".calorie-year-tooltip");

            if (!source) {
                return;
            }

            if (activeDay !== day) {
                activeTooltip?.remove();
                activeDay = day;
                activeTooltip = source.cloneNode(true);
                activeTooltip.classList.add("is-visible");
                layer.appendChild(activeTooltip);
            }

            positionTooltip();
        };

        days.forEach(day => {
            day.addEventListener("pointerenter", () => showTooltip(day));
            day.addEventListener("pointerleave", hideTooltip);
            day.addEventListener("focus", () => showTooltip(day));
            day.addEventListener("blur", hideTooltip);
        });

        scroll.addEventListener("scroll", positionTooltip, { passive: true });
        window.addEventListener("resize", positionTooltip);
        window.addEventListener("scroll", positionTooltip, { passive: true });
    });
})();
