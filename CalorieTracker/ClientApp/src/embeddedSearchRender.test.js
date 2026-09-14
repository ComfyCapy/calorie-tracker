import assert from 'node:assert/strict'
import test from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { createServer } from 'vite'

test('embedded database search renders its interactive controls', async () => {
    const vite = await createServer({
        appType: 'custom',
        server: { middlewareMode: true },
    })

    try {
        const { default: App } = await vite.ssrLoadModule('/src/App.jsx')
        const html = renderToStaticMarkup(React.createElement(App, {
            embedded: true,
            returnToDiary: true,
            diaryDate: '2026-09-07',
            diaryMeal: 'Lunch',
            initialProvider: 'cofid',
        }))

        assert.match(html, /role="search"/)
        assert.match(html, /id="food-search-input"/)
        assert.match(html, /aria-label="UK food database — CoFID"/)
        assert.match(html, /aria-label="US food database — USDA FoodData Central"/)
        assert.match(html, />Search<\/button>/)
        assert.doesNotMatch(html, /<h1>Search all foods<\/h1>/)
    } finally {
        await vite.close()
    }
})
