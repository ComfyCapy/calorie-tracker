import assert from 'node:assert/strict'
import test from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { createServer } from 'vite'

test('search results preserve accessible controls, busy states and callback arguments', async () => {
    const vite = await createServer({ appType: 'custom', server: { middlewareMode: true } })
    try {
        const { default: Results } = await vite.ssrLoadModule('/src/FoodSearchResults.jsx')
        const food = { externalId: 'abc', name: 'Rice', servingSize: 100, servingUnit: 'g', calories: 200,
            protein: 4, carbohydrates: 40, fat: 1, isFavourite: true }
        const calls = []
        const props = { foods: [food], provider: 'cofid', isLoading: false, totalResults: 40, pageSize: 20,
            currentPage: 1, totalPages: 2, favouriteFoodId: null, selectedFoodId: null,
            handlePageSizeChange: event => calls.push(event), handleToggleFavourite: item => calls.push(item),
            handleSelectFood: item => calls.push(item), handlePageChange: page => calls.push(page) }
        const html = renderToStaticMarkup(React.createElement(Results, props))
        assert.match(html, /aria-label="Remove Rice from Favourites"/)
        assert.match(html, /aria-pressed="true"/)
        assert.match(html, /aria-label="Add Rice to Diary"/)
        assert.match(html, /Page 1 of 2/)
        const elements = []
        function visit(node) {
            if (Array.isArray(node)) return node.forEach(visit)
            if (!node || typeof node !== 'object') return
            elements.push(node)
            visit(node.props?.children)
        }
        visit(Results(props))
        const button = label => elements.find(node => node.props?.['aria-label'] === label)
        assert.equal(button('Previous page').props.disabled, true)
        assert.equal(button('Next page').props.disabled, false)
        button('Next page').props.onClick()
        button('Remove Rice from Favourites').props.onClick()
        button('Add Rice to Diary').props.onClick()
        assert.deepEqual(calls, [2, food, food])
        const busy = renderToStaticMarkup(React.createElement(Results, { ...props, isLoading: true,
            favouriteFoodId: 'cofid:abc', selectedFoodId: 'cofid:abc' }))
        assert.match(busy, /aria-busy="true"/)
        assert.match(busy, /Adding\.\.\./)
        assert.equal((busy.match(/disabled=""/g) || []).length, 5)
    } finally { await vite.close() }
})

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
