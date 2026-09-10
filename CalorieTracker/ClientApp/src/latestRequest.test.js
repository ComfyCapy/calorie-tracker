import assert from 'node:assert/strict'
import test from 'node:test'

import { runLatestRequest } from './latestRequest.js'

function deferred() {
    let resolve
    let reject
    const promise = new Promise((resolvePromise, rejectPromise) => {
        resolve = resolvePromise
        reject = rejectPromise
    })

    return { promise, reject, resolve }
}

function callbacks(state, provider) {
    return {
        onStart() {
            state.loading = true
            state.error = ''
        },
        onSuccess(foods) {
            state.provider = provider
            state.foods = foods
        },
        onError() {
            state.error = `${provider} failed`
            state.foods = []
        },
        onFinish() {
            state.loading = false
        },
    }
}

test('older success cannot overwrite newer provider results', async () => {
    const sequence = { current: 0 }
    const state = { error: '', foods: [], loading: false, provider: '' }
    const usda = deferred()
    const cofid = deferred()
    const oldRequest = runLatestRequest(
        sequence,
        () => usda.promise,
        callbacks(state, 'USDA'),
    )
    const newRequest = runLatestRequest(
        sequence,
        () => cofid.promise,
        callbacks(state, 'CoFID'),
    )

    cofid.resolve(['CoFID food'])
    await newRequest
    usda.resolve(['USDA food'])
    await oldRequest

    assert.deepEqual(state.foods, ['CoFID food'])
    assert.equal(state.provider, 'CoFID')
    assert.equal(state.error, '')
})

test('older failure cannot replace a newer successful state', async () => {
    const sequence = { current: 0 }
    const state = { error: '', foods: [], loading: false, provider: '' }
    const old = deferred()
    const current = deferred()
    const oldRequest = runLatestRequest(
        sequence,
        () => old.promise,
        callbacks(state, 'USDA'),
    )
    const currentRequest = runLatestRequest(
        sequence,
        () => current.promise,
        callbacks(state, 'CoFID'),
    )

    current.resolve(['Current result'])
    await currentRequest
    old.reject(new Error('Late failure'))
    await oldRequest

    assert.deepEqual(state.foods, ['Current result'])
    assert.equal(state.error, '')
})

test('older finally cannot clear loading for a newer request', async () => {
    const sequence = { current: 0 }
    const state = { error: '', foods: [], loading: false, provider: '' }
    const old = deferred()
    const current = deferred()
    const oldRequest = runLatestRequest(
        sequence,
        () => old.promise,
        callbacks(state, 'USDA'),
    )
    const currentRequest = runLatestRequest(
        sequence,
        () => current.promise,
        callbacks(state, 'CoFID'),
    )

    old.resolve(['Stale result'])
    await oldRequest
    assert.equal(state.loading, true)

    current.resolve(['Current result'])
    await currentRequest
    assert.equal(state.loading, false)
})

test('rapid provider switching deterministically keeps the latest request', async () => {
    const sequence = { current: 0 }
    const state = { error: '', foods: [], loading: false, provider: '' }
    const first = deferred()
    const second = deferred()
    const third = deferred()
    const requests = [
        runLatestRequest(
            sequence,
            () => first.promise,
            callbacks(state, 'USDA first'),
        ),
        runLatestRequest(
            sequence,
            () => second.promise,
            callbacks(state, 'CoFID second'),
        ),
        runLatestRequest(
            sequence,
            () => third.promise,
            callbacks(state, 'USDA final'),
        ),
    ]

    second.resolve(['Second'])
    first.resolve(['First'])
    third.resolve(['Final'])
    await Promise.all(requests)

    assert.deepEqual(state.foods, ['Final'])
    assert.equal(state.provider, 'USDA final')
    assert.equal(state.error, '')
    assert.equal(state.loading, false)
})
