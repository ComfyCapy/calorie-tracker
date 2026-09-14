import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.jsx'

const rootElement = document.getElementById('react-food-search')
const returnToDiary =
    rootElement?.dataset.returnToDiary === 'true'

const diaryDate =
    rootElement?.dataset.diaryDate || ''

const diaryMeal =
    rootElement?.dataset.diaryMeal || ''

const diaryDateInputId =
    rootElement?.dataset.diaryDateInputId || ''

const diaryMealInputId =
    rootElement?.dataset.diaryMealInputId || ''

const initialSearchTerm =
    rootElement?.dataset.initialSearchTerm || ''

const initialProvider =
    rootElement?.dataset.initialProvider || 'cofid'

const embedded =
    rootElement?.dataset.embedded === 'true'

const antiForgeryToken = document.querySelector(
    'input[name="__RequestVerificationToken"]'
)?.value || ''

if (rootElement) {
    createRoot(rootElement).render(
        <StrictMode>
            <App
                returnToDiary={returnToDiary}
                diaryDate={diaryDate}
                diaryMeal={diaryMeal}
                diaryDateInputId={diaryDateInputId}
                diaryMealInputId={diaryMealInputId}
                initialSearchTerm={initialSearchTerm}
                initialProvider={initialProvider}
                embedded={embedded}
                antiForgeryToken={antiForgeryToken}
            />
        </StrictMode>,
    )
}
