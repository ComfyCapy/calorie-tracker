import assert from 'node:assert/strict'
import test from 'node:test'

import { getProviderSwitchSearch } from './providerSwitch.js'

test('provider switch auto-searches a trimmed non-empty query', () => {
    assert.deepEqual(
        getProviderSwitchSearch('  burger  '),
        { query: 'burger', pageNumber: 1 },
    )
})

test('provider switch does not search an empty query', () => {
    assert.equal(getProviderSwitchSearch('   '), null)
})
