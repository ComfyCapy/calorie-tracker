import assert from "node:assert/strict";
import { createRequire } from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {
    initialize,
    isDeliberateLeftSwipe
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
    const scope = eventTarget();
    let hideCount = 0;

    scope.document = {
        getElementById: () => sidebar
    };
    scope.matchMedia = () => ({ matches: true });
    scope.bootstrap = {
        Offcanvas: {
            getOrCreateInstance: () => ({
                hide: () => {
                    hideCount += 1;
                }
            })
        }
    };

    initialize(scope);

    return {
        sidebar,
        scope,
        hideCount: () => hideCount
    };
}

function swipeLeft(navigation, pointerType = "touch") {
    navigation.sidebar.dispatch("pointerdown", {
        pointerId: 1,
        pointerType,
        isPrimary: true,
        clientX: 200,
        clientY: 200
    });
    navigation.scope.dispatch("pointermove", {
        pointerId: 1,
        clientX: 120,
        clientY: 210
    });
    navigation.scope.dispatch("pointerup", {
        pointerId: 1,
        clientX: 120,
        clientY: 210
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

test("mouse movement does not trigger swipe-to-close", () => {
    const navigation = setupNavigation();

    navigation.sidebar.dispatch("shown.bs.offcanvas");
    swipeLeft(navigation, "mouse");

    assert.equal(navigation.hideCount(), 0);
});
