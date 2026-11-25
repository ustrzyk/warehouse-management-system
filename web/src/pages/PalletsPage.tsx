import { useEffect, useState, type FormEvent } from 'react'
import './PalletsPage.css'
import './PalletWorkflow.css'

type Pallet = {
  id: number
  code: string
  status: string
  storageLocationId: number | null
  storageLocationCode: string | null
  createdAtUtc: string
}

type PalletItem = {
  id: number
  logisticUnitId: number
  productId: number
  productCode: string
  productName: string
  productBatchId: number | null
  batchNumber: string | null
  quantity: number
}

type Product = {
  id: number
  code: string
  name: string
  isActive: boolean
}

type ProductBatch = {
  id: number
  productId: number
  batchNumber: string
  isActive: boolean
}

type StorageLocation = {
  id: number
  warehouseCode: string
  warehouseZoneCode: string
  code: string
  description: string | null
  isActive: boolean
}

type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

type ApiProblem = { detail?: string }

type Props = {
  token: string
  canCreatePallets: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10
const LOOKUP_SIZE = 100

function statusLabel(status: string): string {
  switch (status) {
    case 'Created': return 'Utworzona'
    case 'Stored': return 'Odłożona'
    case 'Picking': return 'W kompletacji'
    case 'Shipped': return 'Wysłana'
    default: return status
  }
}

function statusClass(status: string): string {
  switch (status) {
    case 'Created': return 'pallet-status-created'
    case 'Stored': return 'pallet-status-stored'
    case 'Picking': return 'pallet-status-picking'
    case 'Shipped': return 'pallet-status-shipped'
    default: return 'pallet-status-default'
  }
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleString('pl-PL')
}

async function apiError(
  response: Response,
  fallback: string
): Promise<string> {
  if (response.status === 403) {
    return 'Nie masz uprawnień do wykonania tej operacji.'
  }
  if (response.status === 404) {
    return 'Nie znaleziono wskazanego elementu.'
  }
  const problem = (await response.json().catch(() => null)) as
    | ApiProblem
    | null
  return problem?.detail || `${fallback} (HTTP ${response.status}).`
}

export default function PalletsPage({
  token,
  canCreatePallets,
  onUnauthorized,
}: Props) {
  const [data, setData] = useState<PagedResult<Pallet> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)

  const [showCreate, setShowCreate] = useState(false)
  const [newCode, setNewCode] = useState('')
  const [creating, setCreating] = useState(false)

  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [selectedPallet, setSelectedPallet] = useState<Pallet | null>(null)
  const [items, setItems] = useState<PalletItem[]>([])
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [detailsReload, setDetailsReload] = useState(0)

  const [productInput, setProductInput] = useState('')
  const [productSearch, setProductSearch] = useState('')
  const [products, setProducts] = useState<Product[]>([])
  const [productsTotal, setProductsTotal] = useState(0)
  const [productsLoading, setProductsLoading] = useState(false)
  const [productsError, setProductsError] = useState<string | null>(null)
  const [productId, setProductId] = useState(0)

  const [batches, setBatches] = useState<ProductBatch[]>([])
  const [batchesLoading, setBatchesLoading] = useState(false)
  const [batchesError, setBatchesError] = useState<string | null>(null)
  const [batchId, setBatchId] = useState(0)
  const [quantity, setQuantity] = useState('1')

  const [locationInput, setLocationInput] = useState('')
  const [locationSearch, setLocationSearch] = useState('')
  const [locations, setLocations] = useState<StorageLocation[]>([])
  const [locationsTotal, setLocationsTotal] = useState(0)
  const [locationsLoading, setLocationsLoading] = useState(false)
  const [locationsError, setLocationsError] = useState<string | null>(null)
  const [locationId, setLocationId] = useState(0)
  const [working, setWorking] = useState(false)

  // Lista palet.
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      const params = new URLSearchParams({
        Page: String(page),
        PageSize: String(PAGE_SIZE),
      })
      if (search) params.set('Search', search)

      try {
        const response = await fetch(`/api/logistic-units?${params}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        })
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(await apiError(response, 'Nie udało się pobrać palet'))
        }
        const result = (await response.json()) as PagedResult<Pallet>
        if (controller.signal.aborted) return
        setData(result)
        const lastPage = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE))
        if (page > lastPage) setPage(lastPage)
      } catch (err) {
        if (!controller.signal.aborted) {
          setError(err instanceof Error ? err.message : 'Błąd listy palet.')
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [token, search, page, reload, onUnauthorized])

  // Szczegóły i zawartość palety.
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setSelectedPallet(null)
      setItems([])
      setDetailsError(null)
      if (selectedId === null) {
        setDetailsLoading(false)
        return
      }
      setDetailsLoading(true)
      const options = {
        headers: { Authorization: `Bearer ${token}` },
        signal: controller.signal,
      }
      try {
        const [unitResponse, itemsResponse] = await Promise.all([
          fetch(`/api/logistic-units/${selectedId}`, options),
          fetch(`/api/logistic-units/${selectedId}/items`, options),
        ])
        if (controller.signal.aborted) return
        if (unitResponse.status === 401 || itemsResponse.status === 401) {
          onUnauthorized()
          return
        }
        if (!unitResponse.ok) {
          throw new Error(await apiError(unitResponse, 'Błąd pobierania palety'))
        }
        if (!itemsResponse.ok) {
          throw new Error(await apiError(itemsResponse, 'Błąd pobierania pozycji'))
        }
        const pallet = (await unitResponse.json()) as Pallet
        const positions = (await itemsResponse.json()) as PalletItem[]
        if (controller.signal.aborted) return
        setSelectedPallet(pallet)
        setItems(positions)
      } catch (err) {
        if (!controller.signal.aborted) {
          setDetailsError(err instanceof Error ? err.message : 'Błąd szczegółów.')
        }
      } finally {
        if (!controller.signal.aborted) setDetailsLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [token, selectedId, detailsReload, onUnauthorized])

  // Aktywne produkty; maksymalnie 100 wyników, z wyszukiwaniem w API.
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setProducts([])
      setProductsTotal(0)
      setProductsError(null)
      if (selectedPallet?.status !== 'Created') {
        setProductsLoading(false)
        return
      }
      setProductsLoading(true)
      const params = new URLSearchParams({
        Page: '1', PageSize: String(LOOKUP_SIZE),
        IsActive: 'true', SortBy: 'code',
      })
      if (productSearch) params.set('Search', productSearch)
      try {
        const response = await fetch(`/api/products?${params}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        })
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) throw new Error(await apiError(response, 'Błąd produktów'))
        const result = (await response.json()) as PagedResult<Product>
        if (controller.signal.aborted) return
        setProducts(result.items)
        setProductsTotal(result.totalCount)
      } catch (err) {
        if (!controller.signal.aborted) {
          setProductsError(err instanceof Error ? err.message : 'Błąd produktów.')
        }
      } finally {
        if (!controller.signal.aborted) setProductsLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [token, selectedPallet?.status, productSearch, onUnauthorized])

  // Partie aktywne dla wskazanego produktu.
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setBatches([])
      setBatchesError(null)
      if (selectedPallet?.status !== 'Created' || productId === 0) {
        setBatchesLoading(false)
        return
      }
      setBatchesLoading(true)
      const params = new URLSearchParams({
        ProductId: String(productId), IsActive: 'true',
        SortBy: 'batchNumber', Page: '1',
        PageSize: String(LOOKUP_SIZE),
      })
      try {
        const response = await fetch(`/api/product-batches?${params}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        })
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) throw new Error(await apiError(response, 'Błąd partii'))
        const result = (await response.json()) as PagedResult<ProductBatch>
        if (controller.signal.aborted) return
        setBatches(result.items)
      } catch (err) {
        if (!controller.signal.aborted) {
          setBatchesError(err instanceof Error ? err.message : 'Błąd partii.')
        }
      } finally {
        if (!controller.signal.aborted) setBatchesLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [token, selectedPallet?.status, productId, onUnauthorized])

  // Aktywne lokalizacje, potrzebne do odłożenia niepustej palety.
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLocations([])
      setLocationsTotal(0)
      setLocationsError(null)
      if (selectedPallet?.status !== 'Created' || items.length === 0) {
        setLocationsLoading(false)
        return
      }
      setLocationsLoading(true)
      const params = new URLSearchParams({
        IsActive: 'true', Page: '1', PageSize: String(LOOKUP_SIZE),
      })
      if (locationSearch) params.set('Search', locationSearch)
      try {
        const response = await fetch(`/api/storage-locations?${params}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        })
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(await apiError(response, 'Błąd lokalizacji'))
        }
        const result = (await response.json()) as PagedResult<StorageLocation>
        if (controller.signal.aborted) return
        setLocations(result.items)
        setLocationsTotal(result.totalCount)
      } catch (err) {
        if (!controller.signal.aborted) {
          setLocationsError(err instanceof Error ? err.message : 'Błąd lokalizacji.')
        }
      } finally {
        if (!controller.signal.aborted) setLocationsLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [token, selectedPallet?.status, items.length, locationSearch, onUnauthorized])

  function refresh() {
    setReload(value => value + 1)
  }

  function refreshDetails() {
    setDetailsReload(value => value + 1)
    refresh()
  }

  function openDetails(id: number) {
    setSelectedId(id)
    setDetailsReload(value => value + 1)
    setProductInput('')
    setProductSearch('')
    setProductId(0)
    setBatchId(0)
    setQuantity('1')
    setLocationInput('')
    setLocationSearch('')
    setLocationId(0)
    setError(null)
    setSuccess(null)
  }

  async function createPallet(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const code = newCode.trim().toUpperCase()
    if (!code || code.length > 50) {
      setError('Numer LE musi mieć od 1 do 50 znaków.')
      return
    }
    setCreating(true)
    setError(null)
    setSuccess(null)
    try {
      const response = await fetch('/api/logistic-units', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ code }),
      })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await apiError(response, 'Nie udało się utworzyć palety'))
      }
      const created = (await response.json()) as Pallet
      setShowCreate(false)
      setNewCode('')
      setSearch('')
      setSearchInput('')
      setPage(1)
      openDetails(created.id)
      // openDetails czyści komunikat; ustaw go po otwarciu.
      setSuccess(`Paleta ${created.code} została utworzona.`)
      refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd tworzenia palety.')
    } finally {
      setCreating(false)
    }
  }

  async function addItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedPallet || selectedPallet.status !== 'Created') return
    if (productId <= 0) {
      setError('Wybierz produkt.')
      return
    }
    const normalized = quantity.trim().replace(',', '.')
    if (!/^\d{1,15}(?:\.\d{1,3})?$/.test(normalized) || Number(normalized) <= 0) {
      setError('Podaj dodatnią ilość, maksymalnie 3 miejsca po przecinku.')
      return
    }
    setWorking(true)
    setError(null)
    setSuccess(null)
    try {
      const response = await fetch(
        `/api/logistic-units/${selectedPallet.id}/items`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            productId,
            productBatchId: batchId || null,
            quantity: Number(normalized),
          }),
        }
      )
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await apiError(response, 'Nie udało się dodać produktu'))
      }
      setSuccess('Pozycja została dodana do palety.')
      setProductId(0)
      setBatchId(0)
      setQuantity('1')
      refreshDetails()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd dodawania pozycji.')
    } finally {
      setWorking(false)
    }
  }

  async function storePallet(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedPallet || selectedPallet.status !== 'Created' || items.length === 0) {
      setError('Najpierw dodaj produkt do palety.')
      return
    }
    if (locationId <= 0) {
      setError('Wybierz lokalizację składowania.')
      return
    }
    const location = locations.find(item => item.id === locationId)
    if (!location) {
      setError('Wybrana lokalizacja nie znajduje się na aktualnej liście.')
      return
    }
    if (!window.confirm(
      `Odłożyć ${selectedPallet.code} na ${location.warehouseCode} / ${location.warehouseZoneCode} / ${location.code}?`
    )) return

    setWorking(true)
    setError(null)
    setSuccess(null)
    try {
      const response = await fetch(
        `/api/logistic-units/${selectedPallet.id}/store`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({ storageLocationId: locationId }),
        }
      )
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await apiError(response, 'Nie udało się odłożyć palety'))
      }
      setSuccess(`Paleta ${selectedPallet.code} została odłożona na ${location.code}.`)
      setLocationId(0)
      refreshDetails()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd odkładania palety.')
    } finally {
      setWorking(false)
    }
  }

  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  return (
    <div className="pallets-page">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Palety LE</h2>
            <p>Jednostki logistyczne z ASP.NET Core i SQL Server.</p>
          </div>
          <div className="pallet-heading-actions">
            {canCreatePallets && (
              <button type="button" className="button-primary"
                onClick={() => { setShowCreate(true); setError(null); setSuccess(null) }}
                disabled={showCreate || creating}>
                + Utwórz paletę LE
              </button>
            )}
            <button type="button" className="button-secondary"
              onClick={refresh} disabled={loading}>Odśwież</button>
          </div>
        </div>

        {success && <div className="pallet-success" role="status">{success}</div>}
        {error && <div className="error-message" role="alert">{error}</div>}

        {showCreate && canCreatePallets && (
          <form className="pallet-create-form" onSubmit={event => void createPallet(event)}>
            <h3>Nowa paleta LE</h3>
            <div className="pallet-field">
              <label htmlFor="new-le">Numer LE *</label>
              <input id="new-le" value={newCode} maxLength={50} required
                onChange={event => setNewCode(event.target.value)}
                placeholder="np. LE-TEST-002" disabled={creating}/>
            </div>
            <p className="pallet-hint">Nowa paleta ma status Created, bez produktów i lokalizacji.</p>
            <div className="pallet-form-actions">
              <button type="submit" className="button-primary" disabled={creating}>
                {creating ? 'Tworzenie...' : 'Utwórz paletę'}
              </button>
              <button type="button" className="button-secondary" disabled={creating}
                onClick={() => setShowCreate(false)}>Anuluj</button>
            </div>
          </form>
        )}

        <form className="pallet-filters" onSubmit={event => {
          event.preventDefault(); setSearch(searchInput.trim()); setPage(1); setSuccess(null)
        }}>
          <div className="pallet-field pallet-search">
            <label htmlFor="pallet-search">Szukaj palety</label>
            <input id="pallet-search" placeholder="Numer LE" maxLength={100}
              value={searchInput} onChange={event => setSearchInput(event.target.value)}/>
          </div>
          <button className="button-primary" type="submit">Szukaj</button>
          <button className="button-secondary" type="button" onClick={() => {
            setSearchInput(''); setSearch(''); setPage(1); setSuccess(null)
          }}>Wyczyść</button>
        </form>

        <div className="results-info" aria-live="polite">
          {loading ? 'Pobieranie palet...' : `Liczba znalezionych palet: ${totalCount}`}
        </div>
        {!loading && !error && data?.items.length === 0 && (
          <div className="empty-state"><strong>Brak palet</strong>
            <p>Nie znaleziono palet dla podanych kryteriów.</p></div>
        )}
        {!error && data && data.items.length > 0 && (
          <div className="table-scroll" aria-busy={loading}
            style={{ opacity: loading ? 0.5 : 1 }}>
            <table className="data-table">
              <thead><tr><th>ID</th><th>Numer LE</th><th>Status</th>
                <th>Lokalizacja</th><th>Utworzona</th><th>Akcje</th></tr></thead>
              <tbody>{data.items.map(pallet => (
                <tr key={pallet.id}>
                  <td>{pallet.id}</td><td><strong>{pallet.code}</strong></td>
                  <td><span className={`pallet-status ${statusClass(pallet.status)}`}>
                    {statusLabel(pallet.status)}</span></td>
                  <td>{pallet.storageLocationCode || '—'}</td>
                  <td>{formatDate(pallet.createdAtUtc)}</td>
                  <td><button type="button" className="button-secondary pallet-small-button"
                    onClick={() => openDetails(pallet.id)}>Szczegóły</button></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
        {!loading && !error && data && totalCount > 0 && (
          <div className="pagination">
            <span>Strona {page} z {totalPages}</span>
            <div className="pagination-actions">
              <button type="button" className="button-secondary"
                onClick={() => setPage(current => Math.max(1, current - 1))}
                disabled={page <= 1 || loading}>Poprzednia</button>
              <button type="button" className="button-secondary"
                onClick={() => setPage(current => Math.min(totalPages, current + 1))}
                disabled={page >= totalPages || loading}>Następna</button>
            </div>
          </div>
        )}
      </section>

      {selectedId !== null && (
        <section className="panel pallet-details-panel">
          <div className="panel-heading">
            <div><h2>Szczegóły palety LE</h2>
              <p>Zawartość i odkładanie na lokalizację.</p></div>
            <button type="button" className="button-secondary"
              disabled={working} onClick={() => setSelectedId(null)}>Zamknij</button>
          </div>
          {detailsLoading && <p className="pallet-details-loading">Pobieranie szczegółów...</p>}
          {detailsError && <div className="error-message" role="alert">{detailsError}</div>}
          {!detailsLoading && selectedPallet && (
            <>
              <div className="pallet-details-grid">
                <div className="pallet-detail-card"><span>Numer LE</span>
                  <strong>{selectedPallet.code}</strong></div>
                <div className="pallet-detail-card"><span>Status</span>
                  <strong>{statusLabel(selectedPallet.status)}</strong></div>
                <div className="pallet-detail-card"><span>Lokalizacja</span>
                  <strong>{selectedPallet.storageLocationCode || 'Nieprzypisana'}</strong></div>
                <div className="pallet-detail-card"><span>Data utworzenia</span>
                  <strong>{formatDate(selectedPallet.createdAtUtc)}</strong></div>
              </div>

              <div className="pallet-items-heading"><h3>Zawartość palety</h3>
                <p>Liczba pozycji: {items.length}</p></div>
              {items.length === 0 ? (
                <div className="pallet-empty-items">Paleta nie zawiera produktów.</div>
              ) : (
                <div className="table-scroll">
                  <table className="data-table">
                    <thead><tr><th>Kod produktu</th><th>Nazwa</th>
                      <th>Partia</th><th>Ilość</th></tr></thead>
                    <tbody>{items.map(item => (
                      <tr key={item.id}>
                        <td><strong>{item.productCode}</strong></td>
                        <td>{item.productName}</td>
                        <td>{item.batchNumber || '—'}</td>
                        <td>{item.quantity.toLocaleString('pl-PL', {
                          maximumFractionDigits: 3,
                        })}</td>
                      </tr>
                    ))}</tbody>
                  </table>
                </div>
              )}

              {selectedPallet.status === 'Created' && !canCreatePallets && (
                <p className="pallet-next-step">Dodawanie produktów i odkładanie palet
                  wymaga roli Administrator lub WarehouseWorker.</p>
              )}

              {selectedPallet.status === 'Created' && canCreatePallets && (
                <div className="pallet-workflow">
                  <div className="pallet-workflow-section">
                    <h3>1. Dodaj produkt do palety</h3>
                    <p className="pallet-hint">Wybierz aktywny produkt, opcjonalną partię
                      oraz ilość. Pozycję można dodać tylko do palety Created.</p>
                    <form className="pallet-filters" onSubmit={event => {
                      event.preventDefault()
                      setProductSearch(productInput.trim())
                      setProductId(0)
                      setBatchId(0)
                    }}>
                      <div className="pallet-field pallet-search">
                        <label htmlFor="product-lookup">Szukaj produktu</label>
                        <input id="product-lookup" value={productInput}
                          onChange={event => setProductInput(event.target.value)}
                          placeholder="Kod, nazwa lub EAN" maxLength={100}/>
                      </div>
                      <button type="submit" className="button-secondary">Szukaj produktu</button>
                    </form>
                    {productsError && <div className="error-message" role="alert">{productsError}</div>}
                    <form onSubmit={event => void addItem(event)}>
                      <div className="pallet-workflow-grid">
                        <div className="pallet-field">
                          <label htmlFor="item-product">Produkt *</label>
                          <select id="item-product" value={productId} disabled={working || productsLoading}
                            onChange={event => { setProductId(Number(event.target.value)); setBatchId(0) }}>
                            <option value={0}>{productsLoading ? 'Ładowanie...' : 'Wybierz produkt'}</option>
                            {products.map(product => <option key={product.id} value={product.id}>
                              {product.code} — {product.name}</option>)}
                          </select>
                        </div>
                        <div className="pallet-field">
                          <label htmlFor="item-batch">Partia (opcjonalnie)</label>
                          <select id="item-batch" value={batchId}
                            disabled={working || productId === 0 || batchesLoading}
                            onChange={event => setBatchId(Number(event.target.value))}>
                            <option value={0}>{batchesLoading ? 'Ładowanie...' : 'Bez partii'}</option>
                            {batches.map(batch => <option key={batch.id} value={batch.id}>
                              {batch.batchNumber}</option>)}
                          </select>
                        </div>
                        <div className="pallet-field">
                          <label htmlFor="item-quantity">Ilość *</label>
                          <input id="item-quantity" value={quantity} disabled={working}
                            inputMode="decimal" onChange={event => setQuantity(event.target.value)}
                            placeholder="np. 10 lub 2,5" required/>
                        </div>
                      </div>
                      {batchesError && <div className="error-message" role="alert">{batchesError}</div>}
                      {productsTotal > LOOKUP_SIZE && (
                        <p className="pallet-hint">Wyświetlamy 100 z {productsTotal} produktów.
                          Użyj wyszukiwania, aby znaleźć pozostałe.</p>
                      )}
                      <div className="pallet-form-actions">
                        <button type="submit" className="button-primary"
                          disabled={working || productsLoading || batchesLoading || productId === 0}>
                          {working ? 'Zapisywanie...' : '+ Dodaj pozycję'}</button>
                      </div>
                    </form>
                  </div>

                  {items.length > 0 && (
                    <div className="pallet-workflow-section">
                      <h3>2. Odłóż paletę na lokalizację</h3>
                      <p className="pallet-hint">Po odłożeniu paleta otrzyma status Stored.
                        Nie będzie już można dopisywać do niej pozycji.</p>
                      <form className="pallet-filters" onSubmit={event => {
                        event.preventDefault()
                        setLocationSearch(locationInput.trim())
                        setLocationId(0)
                      }}>
                        <div className="pallet-field pallet-search">
                          <label htmlFor="location-lookup">Szukaj lokalizacji</label>
                          <input id="location-lookup" value={locationInput}
                            onChange={event => setLocationInput(event.target.value)}
                            placeholder="np. A-01-01" maxLength={100}/>
                        </div>
                        <button type="submit" className="button-secondary">Szukaj lokalizacji</button>
                      </form>
                      {locationsError && <div className="error-message" role="alert">{locationsError}</div>}
                      <form onSubmit={event => void storePallet(event)}>
                        <div className="pallet-field">
                          <label htmlFor="store-location">Lokalizacja docelowa *</label>
                          <select id="store-location" value={locationId}
                            disabled={working || locationsLoading}
                            onChange={event => setLocationId(Number(event.target.value))}>
                            <option value={0}>{locationsLoading ? 'Ładowanie...' : 'Wybierz lokalizację'}</option>
                            {locations.map(location => <option key={location.id} value={location.id}>
                              {location.warehouseCode} / {location.warehouseZoneCode} / {location.code}
                              {location.description ? ` — ${location.description}` : ''}
                            </option>)}
                          </select>
                        </div>
                        {locationsTotal > LOOKUP_SIZE && (
                          <p className="pallet-hint">Wyświetlamy 100 z {locationsTotal} lokalizacji.
                            Wyszukaj kod, aby znaleźć pozostałe.</p>
                        )}
                        <div className="pallet-form-actions">
                          <button type="submit" className="button-primary"
                            disabled={working || locationsLoading || locationId === 0}>
                            {working ? 'Odkładanie...' : 'Odłóż paletę na lokalizację'}</button>
                        </div>
                      </form>
                    </div>
                  )}
                </div>
              )}

              {selectedPallet.status !== 'Created' && (
                <p className="pallet-next-step">Paleta nie jest już w statusie Created.
                  Jej zawartość można przeglądać, ale nie można ręcznie dodawać pozycji.
                  Przesunięcia palet między lokalizacjami dodamy w kolejnym etapie.</p>
              )}
            </>
          )}
        </section>
      )}
    </div>
  )
}
