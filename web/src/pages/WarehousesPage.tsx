
import { useState } from 'react'
import WarehousesListPage from './WarehousesListPage'
import WarehouseZonesPage from './WarehouseZonesPage.tsx'
import StorageLocationsPage from './StorageLocationsPage.tsx'
import './WarehouseZonesPage.css'

type Props = {
  token: string
  canEditWarehouses: boolean
  onUnauthorized: () => void
}

type ActiveTab = 'warehouses' | 'zones' | 'locations'

export default function WarehousesPage({
  token,
  canEditWarehouses,
  onUnauthorized,
}: Props) {
  const [activeTab, setActiveTab] =
    useState<ActiveTab>('warehouses')

  return (
    <div className="structure-page">
      <div
        className="structure-tabs"
        aria-label="Struktura magazynowa"
      >
        <button
          type="button"
          className={
            activeTab === 'warehouses'
              ? 'structure-tab structure-tab-active'
              : 'structure-tab'
          }
          aria-pressed={activeTab === 'warehouses'}
          onClick={() => setActiveTab('warehouses')}
        >
          Magazyny
        </button>

        <button
          type="button"
          className={
            activeTab === 'zones'
              ? 'structure-tab structure-tab-active'
              : 'structure-tab'
          }
          aria-pressed={activeTab === 'zones'}
          onClick={() => setActiveTab('zones')}
        >
          Strefy magazynowe
        </button>

        <button
          type="button"
          className={
            activeTab === 'locations'
              ? 'structure-tab structure-tab-active'
              : 'structure-tab'
          }
          aria-pressed={activeTab === 'locations'}
          onClick={() => setActiveTab('locations')}
        >
          Lokalizacje składowania
        </button>
      </div>

      {activeTab === 'warehouses' ? (
        <WarehousesListPage
          token={token}
          canEditWarehouses={canEditWarehouses}
          onUnauthorized={onUnauthorized}
        />
      ) : activeTab === 'zones' ? (
        <WarehouseZonesPage
          token={token}
          canEditZones={canEditWarehouses}
          onUnauthorized={onUnauthorized}
        />
      ) : (
        <StorageLocationsPage
          token={token}
          canEditLocations={canEditWarehouses}
          onUnauthorized={onUnauthorized}
        />
      )}
    </div>
  )
}
