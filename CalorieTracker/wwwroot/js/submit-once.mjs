export function initializeSubmitOnce(
    documentRef = document,
    schedule = callback => setTimeout(callback, 0),
) {
    const forms = documentRef.querySelectorAll('form[data-submit-once]')

    forms.forEach(form => {
        let submissionPending = false

        form.addEventListener('submit', event => {
            if (submissionPending) {
                event.preventDefault()
                return
            }

            submissionPending = true

            schedule(() => {
                if (event.defaultPrevented) {
                    submissionPending = false
                    return
                }

                form.setAttribute('aria-busy', 'true')

                const submitter = event.submitter
                if (!submitter) {
                    return
                }

                const submittingText = submitter.dataset.submittingText
                if (submittingText) {
                    submitter.textContent = submittingText
                }

                submitter.disabled = true
            })
        })
    })
}

if (typeof document !== 'undefined') {
    initializeSubmitOnce(document)
}
