import assert from 'node:assert/strict'
import test from 'node:test'

import {
    applyAchievementVisibility,
    initializeProgressAchievements,
    readHideUnlockedPreference,
} from '../../wwwroot/js/progress-achievements.mjs'

function card(state) {
    return {
        dataset: { achievementState: state },
        hidden: false,
    }
}

function storage(initial = {}) {
    const values = new Map(Object.entries(initial))

    return {
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, value),
        value: key => values.get(key),
    }
}

test('hiding unlocked cards leaves locked cards visible and reflows them', () => {
    const cards = [card('unlocked'), card('locked'), card('unlocked')]
    const emptyState = { hidden: true }

    applyAchievementVisibility(cards, emptyState, true)

    assert.deepEqual(cards.map(item => item.hidden), [true, false, true])
    assert.equal(emptyState.hidden, true)

    applyAchievementVisibility(cards, emptyState, false)

    assert.deepEqual(cards.map(item => item.hidden), [false, false, false])
})

test('all-unlocked state shows a calm empty message when filtered', () => {
    const cards = [card('unlocked'), card('unlocked')]
    const emptyState = { hidden: true }

    applyAchievementVisibility(cards, emptyState, true)

    assert.equal(emptyState.hidden, false)
})

test('preference persists and initializes accessible control state', () => {
    const savedStorage = storage({ 'comfyCapy.hideUnlockedAchievements': 'true' })
    const cards = [card('unlocked'), card('locked')]
    const emptyState = { hidden: true }
    const listeners = {}
    const toggle = {
        hidden: true,
        attributes: {},
        setAttribute(name, value) {
            this.attributes[name] = value
        },
        addEventListener(name, listener) {
            listeners[name] = listener
        },
    }
    const section = {
        querySelector(selector) {
            return selector === '[data-hide-unlocked]' ? toggle : emptyState
        },
        querySelectorAll: () => cards,
    }
    const documentRef = {
        querySelector: () => section,
    }

    assert.equal(readHideUnlockedPreference(savedStorage), true)
    initializeProgressAchievements(documentRef, savedStorage)

    assert.equal(toggle.hidden, false)
    assert.equal(toggle.attributes['aria-pressed'], 'true')
    assert.deepEqual(cards.map(item => item.hidden), [true, false])

    listeners.click()

    assert.equal(toggle.attributes['aria-pressed'], 'false')
    assert.deepEqual(cards.map(item => item.hidden), [false, false])
    assert.equal(savedStorage.value('comfyCapy.hideUnlockedAchievements'), 'false')
})
