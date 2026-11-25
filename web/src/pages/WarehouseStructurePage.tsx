
import { useState } from 'react'
import WarehousesPage from './WarehousesPage'
import WarehouseZonesPage from './WarehouseZonesPage'
import './WarehouseZonesPage.css'

type Props = {
  token: string
  canEditWarehouses: boolean
  onUnauthorized: () => void
}

export default function WarehouseStructurePage({
  token,
  canEditWarehouses,
  onUnauthorized,
}: Props) {
  const [tab, setTab] = useState<'warehouses' | 'zones'>(
    'warehouses'
  )

  return (
    <>
      <nav
        className="structure-tabs"
        aria-label="Struktura magazynowa"
      >
        <button
          type="button"
          className={
            tab === 'warehouses'
              ? 'structure-tab selected'
              : 'structure-tab'
          }
          aria-current={
            tab === 'warehouses' ? 'page' : undefined
          }
          onClick={() => setTab('warehouses')}
        >
          Magazyny
        </button>

        <button
          type="button"
          className={
            tab === 'zones'
              ? 'structure-tab selected'
              : 'structure-tab'
          }
          aria-current={
            tab === 'zones' ? 'page' : undefined
          }
          onClick={() => setTab('zones')}
        >
          Strefy magazynowe
        </button>
      </nav>

      {tab === 'warehouses' ? (
        <WarehousesPage
          token={token}
          canEditWarehouses={canEditWarehouses}
          onUnauthorized={onUnauthorized}
        />
      ) : (
        <WarehouseZonesPage
          token={token}
          canEditZones={canEditWarehouses}
          onUnauthorized={onUnauthorized}
        />
      )}
    </>
  )
}
