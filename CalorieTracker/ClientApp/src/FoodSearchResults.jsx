export default function FoodSearchResults({
    foods, provider, isLoading, totalResults, pageSize, currentPage, totalPages, favouriteFoodId, selectedFoodId, handlePageSizeChange, handleToggleFavourite, handleSelectFood, handlePageChange,
}) {
    return (
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
    )
}
