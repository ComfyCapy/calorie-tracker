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

const initialSearchTerm =
    rootElement?.dataset.initialSearchTerm || ''

const embedded =
    rootElement?.dataset.embedded === 'true'

if (rootElement) {
    createRoot(rootElement).render(
        <StrictMode>
            <App
                returnToDiary={returnToDiary}
                diaryDate={diaryDate}
                diaryMeal={diaryMeal}
                initialSearchTerm={initialSearchTerm}
                embedded={embedded}
            />
        </StrictMode>,
    )
}
