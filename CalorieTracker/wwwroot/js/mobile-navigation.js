(function (globalScope, factory) {
    const mobileNavigation = factory();

    if (typeof module === "object" && module.exports) {
        module.exports = mobileNavigation;
        return;
    }

    mobileNavigation.initialize(globalScope);
})(typeof window !== "undefined" ? window : globalThis, () => {
    const leftSwipeThreshold = 64;
    const rightSwipeThreshold = 64;
    const rightSwipeEdgeExclusion = 32;
    const horizontalIntentRatio = 1.5;
    const verticalIntentThreshold = 12;
    const interactivePageSelector = "input, select, textarea, button, a, [role=\"button\"], [contenteditable=\"true\"]";

    function isDeliberateLeftSwipe(gesture) {
        const horizontalDistance = gesture.endX - gesture.startX;
        const verticalDistance = Math.abs(gesture.endY - gesture.startY);

        return !gesture.hasVerticalIntent
            && horizontalDistance <= -leftSwipeThreshold
            && Math.abs(horizontalDistance) >= verticalDistance * horizontalIntentRatio;
    }

    function isDeliberateRightSwipe(gesture) {
        const horizontalDistance = gesture.endX - gesture.startX;
        const verticalDistance = Math.abs(gesture.endY - gesture.startY);

        return !gesture.hasVerticalIntent
            && gesture.startX > rightSwipeEdgeExclusion
            && horizontalDistance >= rightSwipeThreshold
            && horizontalDistance >= verticalDistance * horizontalIntentRatio;
    }

    function getTouchPoint(event) {
        const touch = event.touches?.[0] ?? event.changedTouches?.[0];

        return touch
            ? { x: touch.clientX, y: touch.clientY }
            : null;
    }

    function isHorizontallyScrollable(target, scope, pageContent) {
        let element = target;

        while (element && element !== pageContent) {
            const computedStyle = scope.getComputedStyle?.(element);

            if (computedStyle
                && (computedStyle.overflowX === "auto" || computedStyle.overflowX === "scroll")) {
                return true;
            }

            element = element.parentElement;
        }

        return false;
    }

    function isExcludedPageTarget(target, scope, pageContent) {
        return Boolean(target?.closest?.(interactivePageSelector))
            || isHorizontallyScrollable(target, scope, pageContent);
    }

    function initialize(scope) {
        const sidebar = scope.document?.getElementById("appSidebar");
        const pageContent = scope.document?.querySelector?.(".app-shell-main");
        const offcanvas = scope.bootstrap?.Offcanvas;

        if (!sidebar || !pageContent || !offcanvas) {
            return;
        }

        const mobileViewport = scope.matchMedia("(max-width: 991.98px)");
        let isOpen = false;
        let gesture = null;

        const resetGesture = () => {
            gesture = null;
        };

        sidebar.addEventListener("shown.bs.offcanvas", () => {
            isOpen = true;
        });

        sidebar.addEventListener("hidden.bs.offcanvas", () => {
            isOpen = false;
            resetGesture();
        });

        const beginGesture = (event, source) => {
            if (!mobileViewport.matches
                || (source === "sidebar" && !isOpen)
                || (source === "page" && isOpen)) {
                return;
            }

            const point = getTouchPoint(event);
            if (!point || (source === "page"
                && (point.x <= rightSwipeEdgeExclusion
                    || isExcludedPageTarget(event.target, scope, pageContent)))) {
                return;
            }

            gesture = {
                source,
                startX: point.x,
                startY: point.y,
                endX: point.x,
                endY: point.y,
                hasVerticalIntent: false
            };
        };

        const updateGesture = event => {
            if (!gesture) {
                return;
            }

            const point = getTouchPoint(event);
            if (!point) {
                return;
            }

            gesture.endX = point.x;
            gesture.endY = point.y;

            const horizontalDistance = Math.abs(gesture.endX - gesture.startX);
            const verticalDistance = Math.abs(gesture.endY - gesture.startY);

            if (verticalDistance >= verticalIntentThreshold
                && verticalDistance > horizontalDistance) {
                gesture.hasVerticalIntent = true;
            }
        };

        const endGesture = event => {
            if (!gesture) {
                return;
            }

            const point = getTouchPoint(event);
            if (point) {
                gesture.endX = point.x;
                gesture.endY = point.y;
            }

            const shouldClose = gesture.source === "sidebar"
                && isDeliberateLeftSwipe(gesture);
            const shouldOpen = gesture.source === "page"
                && isDeliberateRightSwipe(gesture);
            resetGesture();

            if (shouldClose && isOpen && mobileViewport.matches) {
                offcanvas.getOrCreateInstance(sidebar).hide();
            }
            else if (shouldOpen && !isOpen && mobileViewport.matches) {
                offcanvas.getOrCreateInstance(sidebar).show();
            }
        };

        sidebar.addEventListener("touchstart", event => beginGesture(event, "sidebar"), { capture: true, passive: true });
        sidebar.addEventListener("touchmove", updateGesture, { capture: true, passive: true });
        sidebar.addEventListener("touchend", endGesture, { capture: true, passive: true });

        sidebar.addEventListener("touchcancel", resetGesture, { capture: true, passive: true });
        pageContent.addEventListener("touchstart", event => beginGesture(event, "page"), { capture: true, passive: true });
        pageContent.addEventListener("touchmove", updateGesture, { capture: true, passive: true });
        pageContent.addEventListener("touchend", endGesture, { capture: true, passive: true });
        pageContent.addEventListener("touchcancel", resetGesture, { capture: true, passive: true });
    }

    return {
        initialize,
        isDeliberateLeftSwipe,
        isDeliberateRightSwipe
    };
});
