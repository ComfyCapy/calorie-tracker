(function (globalScope, factory) {
    const mobileNavigation = factory();

    if (typeof module === "object" && module.exports) {
        module.exports = mobileNavigation;
        return;
    }

    mobileNavigation.initialize(globalScope);
})(typeof window !== "undefined" ? window : globalThis, () => {
    const leftSwipeThreshold = 64;
    const horizontalIntentRatio = 1.5;
    const verticalIntentThreshold = 12;

    function isDeliberateLeftSwipe(gesture) {
        const horizontalDistance = gesture.endX - gesture.startX;
        const verticalDistance = Math.abs(gesture.endY - gesture.startY);

        return !gesture.hasVerticalIntent
            && horizontalDistance <= -leftSwipeThreshold
            && Math.abs(horizontalDistance) >= verticalDistance * horizontalIntentRatio;
    }

    function getTouchPoint(event) {
        const touch = event.touches?.[0] ?? event.changedTouches?.[0];

        return touch
            ? { x: touch.clientX, y: touch.clientY }
            : null;
    }

    function initialize(scope) {
        const sidebar = scope.document?.getElementById("appSidebar");
        const offcanvas = scope.bootstrap?.Offcanvas;

        if (!sidebar || !offcanvas) {
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

        sidebar.addEventListener("touchstart", event => {
            if (!isOpen || !mobileViewport.matches) {
                return;
            }

            const point = getTouchPoint(event);
            if (!point) {
                return;
            }

            gesture = {
                startX: point.x,
                startY: point.y,
                endX: point.x,
                endY: point.y,
                hasVerticalIntent: false
            };
        }, { capture: true, passive: true });

        sidebar.addEventListener("touchmove", event => {
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
        }, { capture: true, passive: true });

        sidebar.addEventListener("touchend", event => {
            if (!gesture) {
                return;
            }

            const point = getTouchPoint(event);
            if (point) {
                gesture.endX = point.x;
                gesture.endY = point.y;
            }

            const shouldClose = isDeliberateLeftSwipe(gesture);
            resetGesture();

            if (shouldClose && isOpen && mobileViewport.matches) {
                offcanvas.getOrCreateInstance(sidebar).hide();
            }
        }, { capture: true, passive: true });

        sidebar.addEventListener("touchcancel", resetGesture, { capture: true, passive: true });
    }

    return {
        initialize,
        isDeliberateLeftSwipe
    };
});
