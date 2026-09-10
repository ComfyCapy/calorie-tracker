export const autocompleteDelayMilliseconds = 350
export const autocompleteMinimumLength = 2

export function scheduleAutocomplete(
    query,
    callback,
    delay = autocompleteDelayMilliseconds,
    timers = globalThis,
) {
    const normalizedQuery = query.trim()

    if (normalizedQuery.length < autocompleteMinimumLength) {
        return () => {}
    }

    const timer = timers.setTimeout(
        () => callback(normalizedQuery),
        delay,
    )

    return () => timers.clearTimeout(timer)
}
