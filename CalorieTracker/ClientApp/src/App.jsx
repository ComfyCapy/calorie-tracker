import { useCallback, useEffect, useState } from 'react'
import './App.css'

function App({
    returnToDiary,
    diaryDate,
    diaryMeal,
    initialSearchTerm = '',
    embedded = false,
    antiForgeryToken = '',
}) {
    const initialQuery = initialSearchTerm.trim()
    const [searchTerm, setSearchTerm] = useState(initialQuery)
    const [foods, setFoods] = useState([])
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

    const loadSearchPage = useCallback(
        async (query, pageNumber, nextPageSize) => {
            setIsLoading(true)
            setError('')
            setHasSearched(true)
            setStatusMessage('Searching the USDA food database...')

            try {
                const params = new URLSearchParams({
                    query,
                    page: pageNumber.toString(),
                    pageSize: nextPageSize.toString(),
                })

                const response = await fetch(
                    `/api/foods/search?${params.toString()}`
                )

                if (!response.ok) {
                    throw new Error('Unable to search for foods.')
                }

                const result = await response.json()
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
            } catch {
                setFoods([])
                setTotalPages(0)
                setTotalResults(0)
                setStatusMessage(
                    'We could not search the USDA database. Please try again.'
                )
                setError(
                    'We could not search the USDA database. Please try again.'
                )
            } finally {
                setIsLoading(false)
            }
        },
        []
    )

    useEffect(() => {
        if (!initialQuery) {
            return
        }

        const searchTimer = window.setTimeout(() => {
            void loadSearchPage(initialQuery, 1, 20)
        }, 0)

        return () => window.clearTimeout(searchTimer)
    }, [initialQuery, loadSearchPage])

    async function handleSearch(event) {
        event.preventDefault()

        const trimmedSearchTerm = searchTerm.trim()

        if (!trimmedSearchTerm) {
            setFoods([])
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
        await loadSearchPage(trimmedSearchTerm, 1, pageSize)
    }

    async function handlePageSizeChange(event) {
        const nextPageSize = Number(event.target.value)
        setPageSize(nextPageSize)

        if (activeSearchTerm) {
            await loadSearchPage(activeSearchTerm, 1, nextPageSize)
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

        await loadSearchPage(activeSearchTerm, nextPage, pageSize)
    }

    async function handleToggleFavourite(food) {
        setFavouriteFoodId(food.externalId)
        setError('')

        try {
            const response = await fetch(
                `/api/foods/favourites/${encodeURIComponent(food.externalId)}`,
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
                    currentFood.externalId === food.externalId
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
        setSelectedFoodId(food.externalId)
        setError('')

        try {
            const response = await fetch(
                `/api/foods/select/${encodeURIComponent(food.externalId)}`,
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

    return (
        <div
            className={
                embedded
                    ? 'food-search-page food-search-page--embedded'
                    : 'food-search-page'
            }
        >
            {!embedded && (
                <>
                    <section className="food-search-header">
                        <p className="food-search-eyebrow">
                            Nutrition Database
                        </p>

                        <h1>Food Search</h1>
                    </section>

                    <form
                        className="food-search-form"
                        onSubmit={handleSearch}
                    >
                        <label
                            className="visually-hidden"
                            htmlFor="food-search-input"
                        >
                            Search foods
                        </label>
                        <input
                            id="food-search-input"
                            className="food-search-input"
                            type="search"
                            value={searchTerm}
                            onChange={(event) =>
                                setSearchTerm(event.target.value)}
                            placeholder="Try chicken, spaghetti, banana..."
                            aria-label="Search foods"
                        />

                        <button
                            className="food-search-button"
                            type="submit"
                            disabled={isLoading}
                        >
                            {isLoading ? 'Searching...' : 'Search'}
                        </button>
                    </form>
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

            {isLoading && foods.length === 0 && (
                <div
                    className="food-search-message"
                    aria-hidden="true"
                >
                    Searching the USDA food database...
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
                        No matching USDA foods found.
                    </div>
                )}

            {foods.length > 0 && (
                <section
                    className="food-search-results"
                    aria-busy={isLoading}
                >
                    <div className="food-search-results-toolbar">
                        <div className="food-search-results-heading">
                            <h2>
                                {embedded ? 'Food Database' : 'Results'}
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
                                key={food.externalId}
                            >
                                <div className="food-search-result-main">
                                    <div>
                                        <h3>{food.name}</h3>

                                        <p>
                                            Per {food.servingSize}{food.servingUnit}
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
                                                favouriteFoodId === food.externalId
                                            }
                                            aria-label={
                                                food.isFavourite
                                                    ? `Remove ${food.name} from Favourites`
                                                    : `Add ${food.name} to Favourites`
                                            }
                                            aria-pressed={food.isFavourite}
                                            title={
                                                food.isFavourite
                                                    ? 'Remove from Favourites'
                                                    : 'Add to Favourites'
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
                                                selectedFoodId === food.externalId
                                            }
                                            aria-label={`Add ${food.name} to Diary`}
                                        >
                                            {selectedFoodId === food.externalId
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
