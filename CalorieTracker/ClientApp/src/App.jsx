import { useCallback, useEffect, useRef, useState } from 'react'
import './App.css'
import {
    invalidateLatestRequest,
    runLatestRequest,
} from './latestRequest.js'
import { scheduleAutocomplete } from './autocomplete.js'
import { getProviderSwitchSearch } from './providerSwitch.js'

const providers = {
    cofid: {
        label: 'UK',
        accessibleLabel: 'UK food database — CoFID',
        attribution: 'UK foods — nutrition data from the UK food composition database (CoFID).',
    },
    usda: {
        label: 'US',
        accessibleLabel: 'US food database — USDA FoodData Central',
        attribution: 'US foods — nutrition data from the U.S. Department of Agriculture food database (USDA FoodData Central).',
    },
}

function App({
    returnToDiary,
    diaryDate,
    diaryMeal,
    initialSearchTerm = '',
    initialProvider = 'cofid',
    embedded = false,
    antiForgeryToken = '',
}) {
    const initialQuery = initialSearchTerm.trim()
    const startingProvider = providers[initialProvider]
        ? initialProvider
        : 'cofid'
    const [searchTerm, setSearchTerm] = useState(initialQuery)
    const [provider, setProvider] = useState(startingProvider)
    const [foods, setFoods] = useState([])
    const [localSuggestions, setLocalSuggestions] = useState([])
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState('')
    const [hasSearched, setHasSearched] = useState(false)
    const [selectedFoodId, setSelectedFoodId] = useState(null)
    const [favouriteFoodId, setFavouriteFoodId] = useState(null)
    const [pageSize, setPageSize] = useState(20)
    const [currentPage, setCurrentPage] = useState(1)
    const [totalPages, setTotalPages] = useState(0)
    const [totalResults, setTotalResults] = useState(0)
    const [activeSearchTerm, setActiveSearchTerm] = useState(initialQuery)
    const [statusMessage, setStatusMessage] = useState('')
    const searchRequestSequence = useRef(0)

    const loadSearchPage = useCallback(
        async (query, pageNumber, nextPageSize, nextProvider) => {
            await runLatestRequest(
                searchRequestSequence,
                async () => {
                    const params = new URLSearchParams({
                        query,
                        page: pageNumber.toString(),
                        pageSize: nextPageSize.toString(),
                        provider: nextProvider,
                    })

                    const response = await fetch(
                        `/api/foods/search?${params.toString()}`
                    )

                    if (!response.ok) {
                        throw new Error('Unable to search for foods.')
                    }

                    return response.json()
                },
                {
                    onStart() {
                        setIsLoading(true)
                        setError('')
                        setHasSearched(true)
                        setStatusMessage(
                            'Searching the wider food catalogue...')
                    },
                    onSuccess(result) {
                        setLocalSuggestions([])
                        const resultPage = result.pageNumber || pageNumber
                        const resultTotalPages = result.totalPages || 0
                        const resultTotal = result.totalResults || 0

                        setFoods(Array.isArray(result.foods) ? result.foods : [])
                        setCurrentPage(resultPage)
                        setTotalPages(resultTotalPages)
                        setTotalResults(resultTotal)
                        setStatusMessage(resultTotalPages > 0
                            ? `${resultTotal.toLocaleString()} foods found. Page ${resultPage} of ${resultTotalPages}.`
                            : `${resultTotal.toLocaleString()} foods found.`)
                    },
                    onError() {
                        setFoods([])
                        setTotalPages(0)
                        setTotalResults(0)
                        setStatusMessage(
                            'We could not search the wider food catalogue. Please try again.'
                        )
                        setError(
                            'We could not search the wider food catalogue. Please try again.'
                        )
                    },
                    onFinish() {
                        setIsLoading(false)
                    },
                },
            )
        },
        []
    )

    const loadAutocomplete = useCallback(
        async (query, nextProvider) => {
            await runLatestRequest(
                searchRequestSequence,
                async () => {
                    const localResponse = await fetch(
                        `/api/foods/suggestions?query=${encodeURIComponent(query)}`
                    )

                    if (!localResponse.ok) {
                        throw new Error('Unable to load local suggestions.')
                    }

                    const local = await localResponse.json()

                    if (Array.isArray(local) && local.length > 0) {
                        return { local, external: null }
                    }

                    const params = new URLSearchParams({
                        query,
                        page: '1',
                        pageSize: '20',
                        provider: nextProvider,
                    })
                    const externalResponse = await fetch(
                        `/api/foods/search?${params.toString()}`
                    )

                    if (!externalResponse.ok) {
                        throw new Error('Unable to search for foods.')
                    }

                    return {
                        local: [],
                        external: await externalResponse.json(),
                    }
                },
                {
                    onStart() {
                        setIsLoading(true)
                        setError('')
                        setStatusMessage('Looking for useful food suggestions...')
                    },
                    onSuccess(result) {
                        setLocalSuggestions(result.local)

                        if (result.local.length > 0) {
                            setFoods([])
                            setHasSearched(false)
                            setTotalPages(0)
                            setTotalResults(0)
                            setStatusMessage(
                                `${result.local.length} suggestion${result.local.length === 1 ? '' : 's'} from your foods.`
                            )
                            return
                        }

                        const external = result.external || {}
                        const resultFoods = Array.isArray(external.foods)
                            ? external.foods
                            : []
                        setFoods(resultFoods)
                        setHasSearched(true)
                        setActiveSearchTerm(query)
                        setCurrentPage(external.pageNumber || 1)
                        setTotalPages(external.totalPages || 0)
                        setTotalResults(external.totalResults || 0)
                        setStatusMessage(
                            resultFoods.length > 0
                                ? `${external.totalResults || resultFoods.length} foods found in the selected database.`
                                : 'No matching foods found.'
                        )
                    },
                    onError() {
                        setLocalSuggestions([])
                        setFoods([])
                        setError('We could not load food suggestions. Please try again.')
                        setStatusMessage('We could not load food suggestions.')
                    },
                    onFinish() {
                        setIsLoading(false)
                    },
                },
            )
        },
        [],
    )

    useEffect(() => {
        const trimmed = searchTerm.trim()

        if (trimmed.length < 2) {
            return
        }

        if (activeSearchTerm === trimmed && hasSearched) {
            return
        }

        return scheduleAutocomplete(
            trimmed,
            (query) => void loadAutocomplete(query, provider),
        )
    }, [activeSearchTerm, hasSearched, loadAutocomplete, provider, searchTerm])

    function handleSearchTermChange(event) {
        const value = event.target.value
        setSearchTerm(value)
        invalidateLatestRequest(searchRequestSequence)
        setLocalSuggestions([])
        setFoods([])
        setHasSearched(false)
        setIsLoading(false)
        setError('')

        if (value.trim().length < 2) {
            setStatusMessage(value.trim() ? 'Keep typing for suggestions.' : 'Search cleared.')
        } else {
            setStatusMessage('Waiting for suggestions...')
        }
    }

    async function handleSearch(event) {
        event.preventDefault()

        const trimmedSearchTerm = searchTerm.trim()

        if (!trimmedSearchTerm) {
            invalidateLatestRequest(searchRequestSequence)
            setFoods([])
            setLocalSuggestions([])
            setError('')
            setHasSearched(false)
            setActiveSearchTerm('')
            setCurrentPage(1)
            setTotalPages(0)
            setTotalResults(0)
            setStatusMessage('Search cleared.')
            return
        }

        setActiveSearchTerm(trimmedSearchTerm)
        setLocalSuggestions([])
        await loadSearchPage(trimmedSearchTerm, 1, pageSize, provider)
    }

    function handleProviderChange(nextProvider) {
        if (nextProvider === provider) {
            return
        }

        setProvider(nextProvider)
        setFoods([])
        setLocalSuggestions([])
        setError('')
        setHasSearched(false)
        setCurrentPage(1)
        setTotalPages(0)
        setTotalResults(0)
        invalidateLatestRequest(searchRequestSequence)
        setIsLoading(false)
        setStatusMessage('Food database changed. Updating suggestions...')

        const providerSwitchSearch = getProviderSwitchSearch(searchTerm)

        if (providerSwitchSearch) {
            setActiveSearchTerm(providerSwitchSearch.query)
            setHasSearched(true)
            void loadSearchPage(
                providerSwitchSearch.query,
                providerSwitchSearch.pageNumber,
                pageSize,
                nextProvider,
            )
        }
    }

    async function handlePageSizeChange(event) {
        const nextPageSize = Number(event.target.value)
        setPageSize(nextPageSize)

        if (activeSearchTerm) {
            await loadSearchPage(
                activeSearchTerm,
                1,
                nextPageSize,
                provider,
            )
        }
    }

    async function handlePageChange(nextPage) {
        if (
            isLoading ||
            nextPage < 1 ||
            nextPage > totalPages
        ) {
            return
        }

        await loadSearchPage(
            activeSearchTerm,
            nextPage,
            pageSize,
            provider,
        )
    }

    async function handleToggleFavourite(food) {
        const foodProvider = food.provider || provider
        const foodKey = `${foodProvider}:${food.externalId}`
        setFavouriteFoodId(foodKey)
        setError('')

        try {
            const response = await fetch(
                `/api/foods/favourites/${encodeURIComponent(food.externalId)}?provider=${encodeURIComponent(foodProvider)}`,
                {
                    method: food.isFavourite ? 'DELETE' : 'POST',
                    headers: {
                        'X-CSRF-TOKEN': antiForgeryToken,
                    },
                }
            )

            if (!response.ok) {
                throw new Error('Unable to update favourite.')
            }

            if (embedded) {
                // Embedded search is server-composed; reload so surrounding Razor lists reflect the mutation.
                window.location.reload()
                return
            }

            setFoods((currentFoods) =>
                currentFoods.map((currentFood) =>
                    currentFood.externalId === food.externalId &&
                    (currentFood.provider || provider) === foodProvider
                        ? {
                            ...currentFood,
                            isFavourite: !food.isFavourite,
                        }
                        : currentFood
                )
            )
        } catch {
            setError(
                'We could not update that favourite. Please try again.'
            )
        } finally {
            setFavouriteFoodId(null)
        }
    }

    async function handleSelectFood(food) {
        const foodProvider = food.provider || provider
        const foodKey = `${foodProvider}:${food.externalId}`
        setSelectedFoodId(foodKey)
        setError('')

        try {
            const response = await fetch(
                `/api/foods/select/${encodeURIComponent(food.externalId)}?provider=${encodeURIComponent(foodProvider)}`,
                {
                    method: 'POST',
                    headers: {
                        'X-CSRF-TOKEN': antiForgeryToken,
                    },
                }
            )

            if (!response.ok) {
                throw new Error('Unable to select food.')
            }

            const result = await response.json()
            const params = new URLSearchParams({
                foodId: result.foodId.toString(),
            })

            if (returnToDiary && diaryDate) {
                params.set('date', diaryDate)
            }

            if (returnToDiary && diaryMeal) {
                params.set('meal', diaryMeal)
            }

            params.set('returnToFoodSearch', 'true')
            params.set('foodSearchProvider', foodProvider)

            if (activeSearchTerm) {
                params.set('foodSearchTerm', activeSearchTerm)
            }

            if (embedded) {
                params.set('returnToFoodsIndex', 'true')
            }

            window.location.assign(`/Diary/Create?${params.toString()}`)
        } catch {
            setError(
                'We could not add that food to your diary. Please try again.'
            )
            setSelectedFoodId(null)
        }
    }

    function handleSelectLocalFood(food) {
        const params = new URLSearchParams({
            foodId: food.foodId.toString(),
            returnToFoodSearch: 'true',
        })

        if (returnToDiary && diaryDate) {
            params.set('date', diaryDate)
        }
        if (returnToDiary && diaryMeal) {
            params.set('meal', diaryMeal)
        }
        if (searchTerm.trim()) {
            params.set('foodSearchTerm', searchTerm.trim())
        }
        if (embedded) {
            params.set('returnToFoodsIndex', 'true')
        }

        window.location.assign(`/Diary/Create?${params.toString()}`)
    }

    return (
        <div
            className={
                embedded
                    ? 'food-search-page food-search-page--embedded'
                    : 'food-search-page'
            }
        >
            <span id="food-favourite-help" className="visually-hidden">
                Favourite this food to find it faster in My Foods.
            </span>

            {!embedded && (
                <>
                    <section className="food-search-header">
                        <h1>Search all foods</h1>
                    </section>

                    <form
                        className="food-search-form"
                        onSubmit={handleSearch}
                    >
                        <label
                            className="visually-hidden"
                            htmlFor="food-search-input"
                        >
                            Search the wider food catalogue
                        </label>
                        <input
                            id="food-search-input"
                            className="food-search-input"
                            type="search"
                            value={searchTerm}
                            onChange={handleSearchTermChange}
                            placeholder="Try chicken, spaghetti, banana..."
                            aria-label="Search the wider food catalogue"
                            role="combobox"
                            aria-autocomplete="list"
                            aria-controls="food-autocomplete-results"
                            aria-expanded={localSuggestions.length > 0 || foods.length > 0}
                        />

                        <fieldset className="food-search-provider-selector">
                            <legend className="visually-hidden">
                                Food database
                            </legend>

                            {Object.entries(providers).map(
                                ([providerId, details]) => (
                                    <button
                                        key={providerId}
                                        type="button"
                                        className={
                                            provider === providerId
                                                ? 'is-selected'
                                                : ''
                                        }
                                        aria-label={details.accessibleLabel}
                                        aria-pressed={provider === providerId}
                                        onClick={() =>
                                            handleProviderChange(providerId)}
                                    >
                                        {details.label}
                                    </button>
                                ),
                            )}
                        </fieldset>

                        <button
                            className="food-search-button"
                            type="submit"
                            disabled={isLoading}
                        >
                            {isLoading ? 'Searching...' : 'Search'}
                        </button>
                    </form>

                    <p className="food-search-attribution">
                        {providers[provider].attribution}
                    </p>
                </>
            )}

            <div
                className="visually-hidden"
                role="status"
                aria-live="polite"
                aria-atomic="true"
            >
                {statusMessage}
            </div>

            {localSuggestions.length > 0 && (
                <section
                    id="food-autocomplete-results"
                    className="food-local-suggestions"
                    aria-label="Suggestions from your foods"
                >
                    <h2>Your foods</h2>
                    <div className="list-group">
                        {localSuggestions.map((food) => (
                            <button
                                key={food.foodId}
                                type="button"
                                className="list-group-item list-group-item-action d-flex justify-content-between align-items-center gap-3"
                                onClick={() => handleSelectLocalFood(food)}
                            >
                                <span className="text-start">
                                    <strong>{food.name}</strong>
                                    <small className="d-block text-body-secondary">
                                        {food.serving}
                                    </small>
                                </span>
                                {food.category && (
                                    <span className="food-local-suggestion-kind">
                                        {food.category}
                                    </span>
                                )}
                            </button>
                        ))}
                    </div>
                    <p className="food-search-attribution mb-0 mt-2">
                        Press Search to search the selected {providers[provider].label} database instead.
                    </p>
                </section>
            )}

            {isLoading && foods.length === 0 && (
                <div
                    className="food-search-message"
                    aria-hidden="true"
                >
                    Searching the wider food catalogue...
                </div>
            )}

            {error && (
                <div
                    className="food-search-message food-search-error"
                    role="alert"
                >
                    {error}
                </div>
            )}

            {!isLoading &&
                !error &&
                hasSearched &&
                foods.length === 0 && (
                    <div
                        className="food-search-message"
                        aria-hidden="true"
                    >
                        No matching foods found in the wider catalogue.
                    </div>
                )}

            {foods.length > 0 && (
                <section
                    id="food-autocomplete-results"
                    className="food-search-results"
                    aria-busy={isLoading}
                >
                    <div className="food-search-results-toolbar">
                        <div className="food-search-results-heading">
                            <h2>
                                Search results
                            </h2>

                            <span>
                                {totalResults.toLocaleString()} foods found
                            </span>
                        </div>

                        <label className="food-search-page-size">
                            <span>Results per page</span>

                            <select
                                value={pageSize}
                                onChange={handlePageSizeChange}
                                disabled={isLoading}
                            >
                                <option value="20">20</option>
                                <option value="50">50</option>
                                <option value="100">100</option>
                            </select>
                        </label>
                    </div>

                    <div className="food-search-list">
                        {foods.map((food) => (
                            <article
                                className="food-search-result"
                                key={`${food.provider || provider}:${food.externalId}`}
                            >
                                <div className="food-search-result-main">
                                    <div>
                                        <h3>{food.name}</h3>

                                        <p>
                                            Nutrition per {food.servingSize}{food.servingUnit}
                                        </p>
                                    </div>

                                    <strong className="food-search-calories">
                                        {food.calories} kcal
                                    </strong>
                                </div>

                                <div className="food-search-result-bottom">
                                    <div className="food-search-macros">
                                        <span>
                                            <strong aria-hidden="true">P</strong>
                                            <span className="visually-hidden">Protein </span>
                                            {food.protein}g
                                        </span>

                                        <span>
                                            <strong aria-hidden="true">C</strong>
                                            <span className="visually-hidden">Carbohydrates </span>
                                            {food.carbohydrates}g
                                        </span>

                                        <span>
                                            <strong aria-hidden="true">F</strong>
                                            <span className="visually-hidden">Fat </span>
                                            {food.fat}g
                                        </span>
                                    </div>

                                    <div className="food-search-actions">
                                        <button
                                            type="button"
                                            className={
                                                food.isFavourite
                                                    ? 'food-search-favourite-button is-favourite'
                                                    : 'food-search-favourite-button'
                                            }
                                            onClick={() =>
                                                handleToggleFavourite(food)}
                                            disabled={
                                                favouriteFoodId ===
                                                `${food.provider || provider}:${food.externalId}`
                                            }
                                            aria-label={
                                                food.isFavourite
                                                    ? `Remove ${food.name} from Favourites`
                                                    : `Add ${food.name} to Favourites`
                                            }
                                            aria-pressed={food.isFavourite}
                                            aria-describedby="food-favourite-help"
                                            title={
                                                food.isFavourite
                                                    ? 'Remove from Favourites'
                                                    : 'Favourite this food to find it faster in My Foods.'
                                            }
                                        >
                                            {food.isFavourite ? '★' : '☆'}
                                        </button>

                                        <button
                                            type="button"
                                            className="food-search-add-button"
                                            onClick={() =>
                                                handleSelectFood(food)}
                                            disabled={
                                                selectedFoodId ===
                                                `${food.provider || provider}:${food.externalId}`
                                            }
                                            aria-label={`Add ${food.name} to Diary`}
                                        >
                                            {selectedFoodId ===
                                            `${food.provider || provider}:${food.externalId}`
                                                ? 'Adding...'
                                                : 'Add to Diary'}
                                        </button>
                                    </div>
                                </div>
                            </article>
                        ))}
                    </div>

                    <nav
                        className="food-search-pagination"
                        aria-label="Food search result pages"
                    >
                        <button
                            type="button"
                            onClick={() =>
                                handlePageChange(currentPage - 1)}
                            disabled={isLoading || currentPage <= 1}
                            aria-label="Previous page"
                            title="Previous page"
                        >
                            ←
                        </button>

                        <span aria-current="page">
                            Page {currentPage} of {totalPages}
                        </span>

                        <button
                            type="button"
                            onClick={() =>
                                handlePageChange(currentPage + 1)}
                            disabled={
                                isLoading || currentPage >= totalPages
                            }
                            aria-label="Next page"
                            title="Next page"
                        >
                            →
                        </button>
                    </nav>
                </section>
            )}
        </div>
    )
}

export default App
