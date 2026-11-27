
import { useState } from 'react'
import StockPositionsPage from './StockPositionsPage'
import StockAvailabilityPage from './StockAvailabilityPage'
import './StockAvailabilityPage.css'

type Props = {
  token: string
  onUnauthorized: () => void
}

type StockTab = 'positions' | 'availability'

export default function StockPage({
  token,
  onUnauthorized,
}: Props) {
  const [tab, setTab] = useState<StockTab>('positions')

  return (
    <div className="receipts-page">
      <section className="panel">
        <h2>Stany i dostępność magazynowa</h2>

        <p className="receipt-muted">
          Sprawdzaj aktualne ilości na paletach
          oraz wolny zapas produktów.
        </p>

        <div
          className="stock-tabs"
          aria-label="Widok stanów magazynowych"
        >
          <button
            type="button"
            className={
              tab === 'positions'
                ? 'stock-tab stock-tab-active'
                : 'stock-tab'
            }
            aria-pressed={tab === 'positions'}
            onClick={() => setTab('positions')}
          >
            Stany fizyczne
          </button>

          <button
            type="button"
            className={
              tab === 'availability'
                ? 'stock-tab stock-tab-active'
                : 'stock-tab'
            }
            aria-pressed={tab === 'availability'}
            onClick={() => setTab('availability')}
          >
            Dostępność towaru
          </button>
        </div>
      </section>

      {tab === 'positions' ? (
        <StockPositionsPage
          token={token}
          onUnauthorized={onUnauthorized}
        />
      ) : (
        <StockAvailabilityPage
          token={token}
          onUnauthorized={onUnauthorized}
        />
      )}
    </div>
  )
}
