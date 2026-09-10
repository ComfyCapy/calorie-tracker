export function getProviderSwitchSearch(query) {
    const normalizedQuery = query.trim()

    return normalizedQuery.length > 0
        ? { query: normalizedQuery, pageNumber: 1 }
        : null
}
