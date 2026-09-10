import assert from 'node:assert/strict'
import test from 'node:test'

import {
    autocompleteDelayMilliseconds,
    scheduleAutocomplete,
} from './autocomplete.js'

test('autocomplete waits for a useful query and trims it', () => {
    let scheduledDelay = null
    let receivedQuery = null
    const timers = {
        setTimeout(callback, delay) {
            scheduledDelay = delay
            callback()
            return 1
        },
        clearTimeout() {},
    }

    scheduleAutocomplete('  apple  ', (query) => {
        receivedQuery = query
    }, autocompleteDelayMilliseconds, timers)

    assert.equal(scheduledDelay, 350)
    assert.equal(receivedQuery, 'apple')
})

test('autocomplete does not schedule one-character searches', () => {
    let scheduled = false
    const timers = {
        setTimeout() {
            scheduled = true
            return 1
        },
        clearTimeout() {},
    }

    scheduleAutocomplete('a', () => {}, 350, timers)
    assert.equal(scheduled, false)
})

test('autocomplete cleanup cancels the pending request', () => {
    let clearedTimer = null
    const timers = {
        setTimeout() {
            return 42
        },
        clearTimeout(timer) {
            clearedTimer = timer
        },
    }

    const cancel = scheduleAutocomplete('apple', () => {}, 350, timers)
    cancel()

    assert.equal(clearedTimer, 42)
})
