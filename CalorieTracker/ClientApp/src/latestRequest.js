export async function runLatestRequest(
    requestSequence,
    request,
    { onStart, onSuccess, onError, onFinish },
) {
    const requestId = ++requestSequence.current
    onStart()

    try {
        const result = await request()

        if (requestId !== requestSequence.current) {
            return
        }

        onSuccess(result)
    } catch (error) {
        if (requestId !== requestSequence.current) {
            return
        }

        onError(error)
    } finally {
        if (requestId === requestSequence.current) {
            onFinish()
        }
    }
}

export function invalidateLatestRequest(requestSequence) {
    requestSequence.current += 1
}
