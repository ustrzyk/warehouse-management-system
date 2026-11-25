import { useEffect, useState, type FormEvent } from 'react'
import GoodsReceiptDetails from './GoodsReceiptDetails'
import {
  fetchAllPages, formatDate, readApiError, receiptStatus,
  SessionExpiredError,
  type GoodsReceipt, type PagedResult, type Warehouse,
} from './goodsReceiptsApi'
import './GoodsReceiptsPage.css'

type Props = {
  token: string
  canOperateLogistics: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10

export default function GoodsReceiptsPage({
  token, canOperateLogistics, onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [lookupLoading, setLookupLoading] = useState(true)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [data, setData] = useState<PagedResult<GoodsReceipt> | null>(null)
  const [loading, setLoading] = useState(true)
  const [listError, setListError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [filterWarehouseId, setFilterWarehouseId] = useState(0)
  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)
  const [showCreate, setShowCreate] = useState(false)
  const [number, setNumber] = useState('')
  const [warehouseId, setWarehouseId] = useState(0)
  const [supplier, setSupplier] = useState('')
  const [saving, setSaving] = useState(false)
  const [selectedId, setSelectedId] = useState<number | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function loadWarehouses() {
      setLookupLoading(true)
      setLookupError(null)
      try {
        const result = await fetchAllPages<Warehouse>(
          '/api/warehouses', token, controller.signal,
        )
        if (controller.signal.aborted) return
        setWarehouses(result)
        setWarehouseId(current =>
          result.some(item => item.id === current && item.isActive)
            ? current : (result.find(item => item.isActive)?.id ?? 0),
        )
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }
        setLookupError(err instanceof Error ? err.message : 'Błąd magazynów.')
      } finally {
        if (!controller.signal.aborted) setLookupLoading(false)
      }
    }
    void loadWarehouses()
    return () => controller.abort()
  }, [token, onUnauthorized])

  useEffect(() => {
    const controller = new AbortController()
    async function loadReceipts() {
      setLoading(true)
      setListError(null)
      const params = new URLSearchParams({
        Page: String(page), PageSize: String(PAGE_SIZE),
      })
      if (search) params.set('Search', search)
      if (filterWarehouseId > 0) params.set('WarehouseId', String(filterWarehouseId))
      // ASP.NET Core enum: Draft=0, Received=1.
      if (status !== 'all') params.set('Status', status)
      try {
        const response = await fetch(`/api/goods-receipts?${params}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        })
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(await readApiError(response, 'Nie udało się pobrać przyjęć'))
        }
        const result = (await response.json()) as PagedResult<GoodsReceipt>
        if (controller.signal.aborted) return
        setData(result)
        const lastPage = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE))
        if (page > lastPage) setPage(lastPage)
      } catch (err) {
        if (!controller.signal.aborted) {
          setListError(err instanceof Error ? err.message : 'Błąd listy przyjęć.')
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void loadReceipts()
    return () => controller.abort()
  }, [token, page, search, filterWarehouseId, status, reload, onUnauthorized])

  function refresh() { setReload(current => current + 1) }

  function resetFilters() {
    setSearch('')
    setSearchInput('')
    setStatus('all')
    setFilterWarehouseId(0)
    setPage(1)
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedNumber = number.trim().toUpperCase()
    if (!trimmedNumber || trimmedNumber.length > 50 || warehouseId < 1) {
      setActionError('Podaj numer dokumentu i aktywny magazyn.')
      return
    }
    setSaving(true)
    setActionError(null)
    setSuccess(null)
    try {
      const response = await fetch('/api/goods-receipts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          number: trimmedNumber,
          warehouseId,
          supplierName: supplier.trim() || null,
        }),
      })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await readApiError(response, 'Nie udało się utworzyć przyjęcia'))
      }
      const created = (await response.json()) as GoodsReceipt
      setShowCreate(false)
      setNumber('')
      setSupplier('')
      resetFilters()
      setSelectedId(created.id)
      setSuccess(`Utworzono przyjęcie ${created.number}. Dodaj teraz pozycje.`)
      refresh()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Błąd tworzenia przyjęcia.')
    } finally {
      setSaving(false)
    }
  }

  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
  const activeWarehouses = warehouses.filter(item => item.isActive)

  return (
    <div className="receipts-page">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Przyjęcia towaru</h2>
            <p>Dokumenty przyjęć magazynowych — ASP.NET Core i SQL Server.</p>
          </div>
          <div className="receipts-heading-actions">
            {canOperateLogistics && (
              <button className="button-primary" type="button"
                onClick={() => { setShowCreate(true); setActionError(null); setSuccess(null) }}
                disabled={showCreate || saving || lookupLoading || activeWarehouses.length === 0}>
                + Nowe przyjęcie
              </button>
            )}
            <button className="button-secondary" type="button"
              onClick={refresh} disabled={loading}>Odśwież</button>
          </div>
        </div>

        {lookupError && <div className="error-message" role="alert">{lookupError}</div>}
        {listError && <div className="error-message" role="alert">{listError}</div>}
        {actionError && <div className="error-message" role="alert">{actionError}</div>}
        {success && <div className="receipt-success" role="status">{success}</div>}

        {canOperateLogistics && showCreate && (
          <form className="receipt-editor" onSubmit={event => void handleCreate(event)}>
            <h3>Nowe przyjęcie towaru</h3>
            <div className="receipt-fields-grid">
              <div className="receipt-field">
                <label htmlFor="receipt-number">Numer dokumentu *</label>
                <input id="receipt-number" value={number} required maxLength={50}
                  onChange={e => setNumber(e.target.value)} disabled={saving}
                  placeholder="np. GR-TEST-001" />
              </div>
              <div className="receipt-field">
                <label htmlFor="receipt-warehouse">Magazyn *</label>
                <select id="receipt-warehouse" value={warehouseId} required disabled={saving}
                  onChange={e => setWarehouseId(Number(e.target.value))}>
                  <option value={0}>Wybierz magazyn</option>
                  {activeWarehouses.map(item => (
                    <option key={item.id} value={item.id}>{item.code} — {item.name}</option>
                  ))}
                </select>
              </div>
              <div className="receipt-field receipt-field-full">
                <label htmlFor="receipt-supplier">Dostawca (opcjonalnie)</label>
                <input id="receipt-supplier" value={supplier} maxLength={150}
                  onChange={e => setSupplier(e.target.value)} disabled={saving}
                  placeholder="np. Dostawca testowy" />
              </div>
            </div>
            <p className="receipt-hint">Dokument zostanie utworzony ze statusem Szkic.</p>
            <div className="receipt-actions">
              <button className="button-primary" type="submit"
                disabled={saving || warehouseId === 0}>
                {saving ? 'Zapisywanie...' : 'Utwórz dokument'}
              </button>
              <button className="button-secondary" type="button"
                disabled={saving} onClick={() => setShowCreate(false)}>Anuluj</button>
            </div>
          </form>
        )}

        <form className="receipt-filters" onSubmit={event => {
          event.preventDefault(); setSearch(searchInput.trim()); setPage(1)
        }}>
          <div className="receipt-field receipt-filter-search">
            <label htmlFor="receipt-search">Szukaj przyjęcia</label>
            <input id="receipt-search" value={searchInput}
              onChange={e => setSearchInput(e.target.value)}
              placeholder="Numer dokumentu lub dostawca" maxLength={100} />
          </div>
          <div className="receipt-field receipt-filter-warehouse">
            <label htmlFor="receipt-filter-warehouse">Magazyn</label>
            <select id="receipt-filter-warehouse" value={filterWarehouseId}
              onChange={e => { setFilterWarehouseId(Number(e.target.value)); setPage(1) }}>
              <option value={0}>Wszystkie</option>
              {warehouses.map(item => (
                <option key={item.id} value={item.id}>{item.code}</option>
              ))}
            </select>
          </div>
          <div className="receipt-field receipt-filter-status">
            <label htmlFor="receipt-status">Status</label>
            <select id="receipt-status" value={status}
              onChange={e => { setStatus(e.target.value); setPage(1) }}>
              <option value="all">Wszystkie</option>
              <option value="0">Szkic</option>
              <option value="1">Przyjęte</option>
            </select>
          </div>
          <button className="button-primary" type="submit">Szukaj</button>
          <button className="button-secondary" type="button" onClick={resetFilters}>Wyczyść</button>
        </form>

        <div className="results-info" aria-live="polite">
          {loading ? 'Pobieranie przyjęć...' : listError
            ? 'Nie udało się pobrać listy.'
            : `Liczba znalezionych przyjęć: ${totalCount}`}
        </div>

        {!loading && !listError && data?.items.length === 0 && (
          <div className="empty-state"><strong>Brak dokumentów</strong>
            <p>Nie znaleziono przyjęć dla wybranych kryteriów.</p>
          </div>
        )}

        {!listError && data && data.items.length > 0 && (
          <div className="table-scroll" style={{ opacity: loading ? 0.5 : 1 }}>
            <table className="data-table">
              <thead><tr>
                <th>Numer</th><th>Magazyn</th><th>Dostawca</th><th>Status</th>
                <th>Utworzono</th><th>Paleta LE ID</th><th>Akcje</th>
              </tr></thead>
              <tbody>
                {data.items.map(item => (
                  <tr key={item.id}>
                    <td><strong>{item.number}</strong></td>
                    <td>{item.warehouseCode}</td>
                    <td>{item.supplierName || '—'}</td>
                    <td><span className={item.status === 'Received'
                      ? 'receipt-status receipt-status-received'
                      : 'receipt-status receipt-status-draft'}>
                      {receiptStatus(item.status)}
                    </span></td>
                    <td>{formatDate(item.createdAtUtc)}</td>
                    <td>{item.logisticUnitId ?? '—'}</td>
                    <td><button className="button-secondary receipt-small-button"
                      type="button" onClick={() => setSelectedId(item.id)}>
                      Szczegóły
                    </button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!loading && !listError && data && totalCount > 0 && (
          <div className="pagination">
            <span>Strona {page} z {totalPages}</span>
            <div className="pagination-actions">
              <button className="button-secondary" type="button" disabled={page <= 1}
                onClick={() => setPage(current => Math.max(1, current - 1))}>
                Poprzednia
              </button>
              <button className="button-secondary" type="button"
                disabled={page >= totalPages}
                onClick={() => setPage(current => Math.min(totalPages, current + 1))}>
                Następna
              </button>
            </div>
          </div>
        )}
      </section>

      {selectedId !== null && (
        <GoodsReceiptDetails key={selectedId}
          receiptId={selectedId} token={token}
          canOperateLogistics={canOperateLogistics}
          onUnauthorized={onUnauthorized}
          onClose={() => setSelectedId(null)}
          onChanged={refresh}
        />
      )}
    </div>
  )
}
