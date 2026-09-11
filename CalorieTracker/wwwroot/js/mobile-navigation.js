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

        sidebar.addEventListener("pointerdown", event => {
            if (!isOpen
                || !mobileViewport.matches
                || event.pointerType !== "touch"
                || !event.isPrimary) {
                return;
            }

            gesture = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                endX: event.clientX,
                endY: event.clientY,
                hasVerticalIntent: false
            };
        });

        scope.addEventListener("pointermove", event => {
            if (!gesture || event.pointerId !== gesture.pointerId) {
                return;
            }

            gesture.endX = event.clientX;
            gesture.endY = event.clientY;

            const horizontalDistance = Math.abs(gesture.endX - gesture.startX);
            const verticalDistance = Math.abs(gesture.endY - gesture.startY);

            if (verticalDistance >= verticalIntentThreshold
                && verticalDistance > horizontalDistance) {
                gesture.hasVerticalIntent = true;
            }
        }, { passive: true });

        scope.addEventListener("pointerup", event => {
            if (!gesture || event.pointerId !== gesture.pointerId) {
                return;
            }

            gesture.endX = event.clientX;
            gesture.endY = event.clientY;
            const shouldClose = isDeliberateLeftSwipe(gesture);
            resetGesture();

            if (shouldClose && isOpen && mobileViewport.matches) {
                offcanvas.getOrCreateInstance(sidebar).hide();
            }
        }, { passive: true });

        scope.addEventListener("pointercancel", resetGesture, { passive: true });
    }

    return {
        initialize,
        isDeliberateLeftSwipe
    };
});
