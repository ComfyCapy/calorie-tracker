import assert from 'node:assert/strict'
import test from 'node:test'

import { initializeSubmitOnce } from '../../wwwroot/js/submit-once.mjs'

function setup() {
    const scheduled = []
    const listeners = {}
    const form = {
        attributes: {},
        addEventListener(name, listener) {
            listeners[name] = listener
        },
        setAttribute(name, value) {
            this.attributes[name] = value
        },
    }
    const documentRef = {
        querySelectorAll: () => [form],
    }

    initializeSubmitOnce(documentRef, callback => scheduled.push(callback))

    return { form, listeners, scheduled }
}

test('valid submission becomes busy and disables its submitter', () => {
    const { form, listeners, scheduled } = setup()
    const submitter = {
        dataset: { submittingText: 'Sending…' },
        disabled: false,
        textContent: 'Send',
    }
    const event = {
        defaultPrevented: false,
        preventDefault() {
            this.defaultPrevented = true
        },
        submitter,
    }

    listeners.submit(event)
    scheduled.shift()()

    assert.equal(form.attributes['aria-busy'], 'true')
    assert.equal(submitter.disabled, true)
    assert.equal(submitter.textContent, 'Sending…')
})

test('client-validation rejection leaves the form available', () => {
    const { form, listeners, scheduled } = setup()
    const submitter = {
        dataset: { submittingText: 'Sending…' },
        disabled: false,
        textContent: 'Send',
    }
    const rejectedEvent = {
        defaultPrevented: false,
        preventDefault() {
            this.defaultPrevented = true
        },
        submitter,
    }

    listeners.submit(rejectedEvent)
    rejectedEvent.preventDefault()
    scheduled.shift()()

    assert.equal(form.attributes['aria-busy'], undefined)
    assert.equal(submitter.disabled, false)
    assert.equal(submitter.textContent, 'Send')

    const retryEvent = { ...rejectedEvent, defaultPrevented: false }
    listeners.submit(retryEvent)
    scheduled.shift()()

    assert.equal(submitter.disabled, true)
})

test('a second submission is prevented while the first is pending', () => {
    const { listeners } = setup()
    const firstEvent = {
        defaultPrevented: false,
        preventDefault() {
            this.defaultPrevented = true
        },
    }
    const secondEvent = {
        defaultPrevented: false,
        preventDefault() {
            this.defaultPrevented = true
        },
    }

    listeners.submit(firstEvent)
    listeners.submit(secondEvent)

    assert.equal(firstEvent.defaultPrevented, false)
    assert.equal(secondEvent.defaultPrevented, true)
})
