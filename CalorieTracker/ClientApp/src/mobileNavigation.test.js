import assert from "node:assert/strict";
import { createRequire } from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {
    initialize,
    isDeliberateLeftSwipe,
    isDeliberateRightSwipe
} = require("../../wwwroot/js/mobile-navigation.js");

function gesture(endX, endY, hasVerticalIntent = false) {
    return {
        startX: 200,
        startY: 200,
        endX,
        endY,
        hasVerticalIntent
    };
}

function rightSwipeGesture(startX, endX, endY = 200, hasVerticalIntent = false) {
    return {
        startX,
        startY: 200,
        endX,
        endY,
        hasVerticalIntent
    };
}

test("a deliberate horizontally dominant left swipe closes the drawer", () => {
    assert.equal(isDeliberateLeftSwipe(gesture(120, 220)), true);
});

test("movement shorter than the swipe threshold does nothing", () => {
    assert.equal(isDeliberateLeftSwipe(gesture(137, 200)), false);
});

test("rightward movement does nothing", () => {
    assert.equal(isDeliberateLeftSwipe(gesture(280, 200)), false);
});

test("vertical and diagonal gestures do not close the drawer", () => {
    assert.equal(isDeliberateLeftSwipe(gesture(120, 280)), false);
    assert.equal(isDeliberateLeftSwipe(gesture(120, 205, true)), false);
});

test("a deliberate rightward page swipe opens the drawer", () => {
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(200, 280)), true);
});

test("rightward opening requires the threshold, direction and horizontal intent", () => {
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(200, 263)), false);
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(200, 120)), false);
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(200, 280, 280)), false);
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(200, 280, 205, true)), false);
    assert.equal(isDeliberateRightSwipe(rightSwipeGesture(20, 100)), false);
});

function eventTarget() {
    const listeners = new Map();

    return {
        addEventListener(type, listener) {
            listeners.set(type, listener);
        },
        dispatch(type, event = {}) {
            listeners.get(type)?.(event);
        }
    };
}

function setupNavigation() {
    const sidebar = eventTarget();
    const pageContent = eventTarget();
    const scope = eventTarget();
    let hideCount = 0;
    let showCount = 0;

    scope.document = {
        getElementById: () => sidebar,
        querySelector: () => pageContent
    };
    scope.matchMedia = () => ({ matches: true });
    scope.getComputedStyle = () => ({ overflowX: "visible" });
    scope.bootstrap = {
        Offcanvas: {
            getOrCreateInstance: () => ({
                hide: () => {
                    hideCount += 1;
                },
                show: () => {
                    showCount += 1;
                }
            })
        }
    };

    initialize(scope);

    return {
        sidebar,
        pageContent,
        scope,
        hideCount: () => hideCount,
        showCount: () => showCount
    };
}

function touchPoint(clientX, clientY) {
    return { clientX, clientY };
}

function swipeLeft(navigation) {
    navigation.sidebar.dispatch("touchstart", {
        touches: [touchPoint(200, 200)]
    });
    navigation.sidebar.dispatch("touchmove", {
        touches: [touchPoint(120, 210)]
    });
    navigation.sidebar.dispatch("touchend", {
        touches: [],
        changedTouches: [touchPoint(120, 210)]
    });
}

function swipeRight(navigation, startX = 200, endX = 280, endY = 210, target) {
    navigation.pageContent.dispatch("touchstart", {
        target,
        touches: [touchPoint(startX, 200)]
    });
    navigation.pageContent.dispatch("touchmove", {
        target,
        touches: [touchPoint(endX, endY)]
    });
    navigation.pageContent.dispatch("touchend", {
        target,
        touches: [],
        changedTouches: [touchPoint(endX, endY)]
    });
}

test("the open mobile drawer closes through Bootstrap after a left swipe", () => {
    const navigation = setupNavigation();

    navigation.sidebar.dispatch("shown.bs.offcanvas");
    swipeLeft(navigation);

    assert.equal(navigation.hideCount(), 1);
});

test("a left swipe is ignored while the drawer is not open", () => {
    const navigation = setupNavigation();

    swipeLeft(navigation);

    assert.equal(navigation.hideCount(), 0);
});

test("an open mobile drawer ignores the page opening gesture", () => {
    const navigation = setupNavigation();

    navigation.sidebar.dispatch("shown.bs.offcanvas");
    swipeRight(navigation);

    assert.equal(navigation.showCount(), 0);
});

test("a qualifying rightward page swipe opens the closed drawer through Bootstrap", () => {
    const navigation = setupNavigation();

    swipeRight(navigation);

    assert.equal(navigation.showCount(), 1);
});

test("short, leftward and vertical page swipes do not open the drawer", () => {
    const shortSwipe = setupNavigation();
    swipeRight(shortSwipe, 200, 263);
    assert.equal(shortSwipe.showCount(), 0);

    const leftSwipe = setupNavigation();
    swipeRight(leftSwipe, 200, 120);
    assert.equal(leftSwipe.showCount(), 0);

    const verticalSwipe = setupNavigation();
    swipeRight(verticalSwipe, 200, 280, 280);
    assert.equal(verticalSwipe.showCount(), 0);
});

test("a cancelled page gesture does not open the drawer", () => {
    const navigation = setupNavigation();

    navigation.pageContent.dispatch("touchstart", {
        touches: [touchPoint(200, 200)]
    });
    navigation.pageContent.dispatch("touchcancel");
    navigation.pageContent.dispatch("touchend", {
        touches: [],
        changedTouches: [touchPoint(280, 200)]
    });

    assert.equal(navigation.showCount(), 0);
});

test("page opening ignores extreme-left starts", () => {
    const navigation = setupNavigation();

    swipeRight(navigation, 20, 100);

    assert.equal(navigation.showCount(), 0);
});

test("page opening ignores interactive controls and horizontal scrollers", () => {
    const interactiveTarget = {
        closest: selector => selector.includes("button") ? interactiveTarget : null,
        parentElement: null
    };
    const horizontalContainer = { parentElement: null };
    const horizontalTarget = {
        closest: () => null,
        parentElement: horizontalContainer
    };

    const interactiveNavigation = setupNavigation();
    swipeRight(interactiveNavigation, 200, 280, 210, interactiveTarget);
    assert.equal(interactiveNavigation.showCount(), 0);

    const horizontalNavigation = setupNavigation();
    horizontalNavigation.scope.getComputedStyle = element =>
        element === horizontalContainer
            ? { overflowX: "auto" }
            : { overflowX: "visible" };
    swipeRight(horizontalNavigation, 200, 280, 210, horizontalTarget);
    assert.equal(horizontalNavigation.showCount(), 0);
});

test("vertical touch movement does not trigger swipe-to-close", () => {
    const navigation = setupNavigation();

    navigation.sidebar.dispatch("shown.bs.offcanvas");
    navigation.sidebar.dispatch("touchstart", {
        touches: [touchPoint(200, 200)]
    });
    navigation.sidebar.dispatch("touchmove", {
        touches: [touchPoint(190, 260)]
    });
    navigation.sidebar.dispatch("touchend", {
        touches: [],
        changedTouches: [touchPoint(190, 260)]
    });

    assert.equal(navigation.hideCount(), 0);
});

test("a cancelled touch gesture does not trigger swipe-to-close", () => {
    const navigation = setupNavigation();

    navigation.sidebar.dispatch("shown.bs.offcanvas");
    navigation.sidebar.dispatch("touchstart", {
        touches: [touchPoint(200, 200)]
    });
    navigation.sidebar.dispatch("touchcancel");
    navigation.sidebar.dispatch("touchend", {
        touches: [],
        changedTouches: [touchPoint(120, 200)]
    });

    assert.equal(navigation.hideCount(), 0);
});
