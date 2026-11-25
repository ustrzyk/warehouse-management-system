import { useEffect, useState, type FormEvent } from 'react'
import OrdersDetails from './OrdersDetails'
import {
  fetchAllPages,
  formatDate,
  readApiError,
  SessionExpiredError,
  type PagedResult,
  type Warehouse,
} from './goodsReceiptsApi'
import './GoodsReceiptsPage.css'

export type Order = {
  id: number
  number: string
  warehouseId: number
  warehouseCode: string
  customerName: string | null
  status: string
  createdAtUtc: string
  revision: number
  releasedAtUtc: string | null
  completedAtUtc: string | null
  shippedAtUtc: string | null
}

type Props = {
  token: string
  canManageOrders: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10

function orderStatus(status: string) {
  switch (status) {
    case 'Draft': return 'Szkic'
    case 'Released': return 'Do kompletacji'
    case 'Completed': return 'Skompletowane'
    case 'Shipped': return 'Wysłane'
    default: return status
  }
}

export default function OrdersPage({
  token,
  canManageOrders,
  onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [data, setData] = useState<PagedResult<Order> | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [filterStatus, setFilterStatus] = useState('all')
  const [filterWarehouseId, setFilterWarehouseId] = useState(0)

  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)

  const [showCreate, setShowCreate] = useState(false)
  const [number, setNumber] = useState('')
  const [customerName, setCustomerName] = useState('')
  const [warehouseId, setWarehouseId] = useState(0)

  // Pobieranie magazynów do filtrów i formularza.
  useEffect(() => {
    const controller = new AbortController()

    async function loadWarehouses() {
      try {
        const result = await fetchAllPages<Warehouse>(
          '/api/warehouses',
          token,
          controller.signal,
        )

        if (controller.signal.aborted) return

        setWarehouses(result)
        setWarehouseId(
          result.find(item => item.isActive)?.id ?? 0,
        )
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setError(err instanceof Error
          ? err.message
          : 'Nie udało się pobrać magazynów.')
      }
    }

    void loadWarehouses()
    return () => controller.abort()
  }, [token, onUnauthorized])

  // Pobieranie zamówień z backendu.
  useEffect(() => {
    const controller = new AbortController()

    async function loadOrders() {
      setLoading(true)
      setError(null)

      const params = new URLSearchParams({
        Page: String(page),
        PageSize: String(PAGE_SIZE),
      })

      if (search) {
        params.set('Search', search)
      }

      if (filterWarehouseId > 0) {
        params.set('WarehouseId', String(filterWarehouseId))
      }

      if (filterStatus !== 'all') {
        params.set('Status', filterStatus)
      }

      try {
        const response = await fetch(
          `/api/warehouse-orders?${params.toString()}`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
            signal: controller.signal,
          },
        )

        if (controller.signal.aborted) return

        if (response.status === 401) {
          onUnauthorized()
          return
        }

        if (!response.ok) {
          throw new Error(
            await readApiError(response, 'Błąd pobierania zamówień'),
          )
        }

        const result = (await response.json()) as PagedResult<Order>

        if (controller.signal.aborted) return

        setData(result)

        const lastPage = Math.max(
          1,
          Math.ceil(result.totalCount / PAGE_SIZE),
        )

        if (page > lastPage) setPage(lastPage)
      } catch (err) {
        if (controller.signal.aborted) return

        setError(err instanceof Error
          ? err.message
          : 'Nie udało się pobrać zamówień.')
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadOrders()
    return () => controller.abort()
  }, [
    token,
    page,
    search,
    filterStatus,
    filterWarehouseId,
    reload,
    onUnauthorized,
  ])

  function refresh() {
    setReload(value => value + 1)
  }

  function resetFilters() {
    setSearchInput('')
    setSearch('')
    setFilterStatus('all')
    setFilterWarehouseId(0)
    setPage(1)
  }

  async function createOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const normalizedNumber = number.trim().toUpperCase()

    if (!normalizedNumber || normalizedNumber.length > 50 ||
        warehouseId <= 0 || customerName.trim().length > 150) {
      setError('Sprawdź numer zamówienia, klienta i magazyn.')
      return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch('/api/warehouse-orders', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          number: normalizedNumber,
          warehouseId,
          customerName: customerName.trim() || null,
        }),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(
          await readApiError(response, 'Nie udało się utworzyć zamówienia'),
        )
      }

      const created = (await response.json()) as Order

      setSuccess(
        `Utworzono zamówienie ${created.number} ze statusem Szkic.`,
      )

      setSelectedId(created.id)
      setShowCreate(false)
      setNumber('')
      setCustomerName('')
      resetFilters()
      refresh()
    } catch (err) {
      setError(err instanceof Error
        ? err.message
        : 'Błąd tworzenia zamówienia.')
    } finally {
      setSaving(false)
    }
  }

  const activeWarehouses = warehouses.filter(item => item.isActive)
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE),
  )

  return (
    <div className="receipts-page">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Zamówienia magazynowe</h2>
            <p>Zarządzanie dokumentami i procesem kompletacji.</p>
          </div>

          <div className="receipts-heading-actions">
            {canManageOrders && (
              <button
                type="button"
                className="button-primary"
                disabled={showCreate || saving ||
                  activeWarehouses.length === 0}
                onClick={() => {
                  setShowCreate(true)
                  setError(null)
                  setSuccess(null)
                }}
              >
                + Nowe zamówienie
              </button>
            )}

            <button
              type="button"
              className="button-secondary"
              disabled={loading}
              onClick={refresh}
            >
              Odśwież
            </button>
          </div>
        </div>

        {error && (
          <div className="error-message" role="alert">
            {error}
          </div>
        )}

        {success && (
          <div className="receipt-success" role="status">
            {success}
          </div>
        )}

        {showCreate && canManageOrders && (
          <form
            className="receipt-editor"
            onSubmit={event => void createOrder(event)}
          >
            <h3>Nowe zamówienie</h3>

            <div className="receipt-fields-grid">
              <div className="receipt-field">
                <label htmlFor="order-number">
                  Numer zamówienia *
                </label>
                <input
                  id="order-number"
                  required
                  maxLength={50}
                  value={number}
                  disabled={saving}
                  placeholder="np. ORD-TEST-001"
                  onChange={event => setNumber(event.target.value)}
                />
              </div>

              <div className="receipt-field">
                <label htmlFor="order-warehouse">
                  Magazyn *
                </label>
                <select
                  id="order-warehouse"
                  value={warehouseId}
                  disabled={saving}
                  onChange={event =>
                    setWarehouseId(Number(event.target.value))}
                >
                  <option value={0}>Wybierz magazyn</option>
                  {activeWarehouses.map(item => (
                    <option key={item.id} value={item.id}>
                      {item.code} — {item.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="receipt-field receipt-field-full">
                <label htmlFor="order-customer">
                  Klient (opcjonalnie)
                </label>
                <input
                  id="order-customer"
                  maxLength={150}
                  value={customerName}
                  disabled={saving}
                  placeholder="np. Firma testowa"
                  onChange={event =>
                    setCustomerName(event.target.value)}
                />
              </div>
            </div>

            <p className="receipt-hint">
              Nowy dokument otrzyma status Szkic.
              Produkty dodamy w następnym kroku.
            </p>

            <div className="receipt-actions">
              <button
                type="submit"
                className="button-primary"
                disabled={saving || warehouseId === 0}
              >
                {saving ? 'Zapisywanie...' : 'Utwórz zamówienie'}
              </button>

              <button
                type="button"
                className="button-secondary"
                disabled={saving}
                onClick={() => setShowCreate(false)}
              >
                Anuluj
              </button>
            </div>
          </form>
        )}

        <form
          className="receipt-filters"
          onSubmit={event => {
            event.preventDefault()
            setSearch(searchInput.trim())
            setPage(1)
          }}
        >
          <div className="receipt-field receipt-filter-search">
            <label htmlFor="order-search">Szukaj</label>
            <input
              id="order-search"
              maxLength={100}
              value={searchInput}
              placeholder="Numer zamówienia lub klient"
              onChange={event =>
                setSearchInput(event.target.value)}
            />
          </div>

          <div className="receipt-field receipt-filter-warehouse">
            <label htmlFor="order-warehouse-filter">
              Magazyn
            </label>
            <select
              id="order-warehouse-filter"
              value={filterWarehouseId}
              onChange={event => {
                setFilterWarehouseId(Number(event.target.value))
                setPage(1)
              }}
            >
              <option value={0}>Wszystkie</option>
              {warehouses.map(item => (
                <option key={item.id} value={item.id}>
                  {item.code}
                </option>
              ))}
            </select>
          </div>

          <div className="receipt-field receipt-filter-status">
            <label htmlFor="order-status-filter">Status</label>
            <select
              id="order-status-filter"
              value={filterStatus}
              onChange={event => {
                setFilterStatus(event.target.value)
                setPage(1)
              }}
            >
              <option value="all">Wszystkie</option>
              <option value="0">Szkic</option>
              <option value="1">Do kompletacji</option>
              <option value="2">Skompletowane</option>
              <option value="3">Wysłane</option>
            </select>
          </div>

          <button className="button-primary" type="submit">
            Szukaj
          </button>

          <button
            className="button-secondary"
            type="button"
            onClick={resetFilters}
          >
            Wyczyść
          </button>
        </form>

        <div className="results-info" aria-live="polite">
          {loading
            ? 'Pobieranie zamówień...'
            : `Znaleziono zamówień: ${totalCount}`}
        </div>

        {!loading && !error && data?.items.length === 0 && (
          <div className="empty-state">
            <strong>Brak zamówień</strong>
            <p>Utwórz pierwszy dokument lub zmień filtry.</p>
          </div>
        )}

        {data && data.items.length > 0 && (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Numer</th>
                  <th>Magazyn</th>
                  <th>Klient</th>
                  <th>Status</th>
                  <th>Data utworzenia</th>
                  <th>Akcje</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map(order => (
                  <tr key={order.id}>
                    <td><strong>{order.number}</strong></td>
                    <td>{order.warehouseCode}</td>
                    <td>{order.customerName || '—'}</td>
                    <td>
                      <span className="receipt-status receipt-status-draft">
                        {orderStatus(order.status)}
                      </span>
                    </td>
                    <td>{formatDate(order.createdAtUtc)}</td>
                    <td>
                      <button
                        type="button"
                        className="button-secondary receipt-small-button"
                        onClick={() => setSelectedId(order.id)}
                      >
                        Szczegóły
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!loading && data && totalCount > 0 && (
          <div className="pagination">
            <span>Strona {page} z {totalPages}</span>

            <div className="pagination-actions">
              <button
                className="button-secondary"
                type="button"
                disabled={page <= 1}
                onClick={() => setPage(value =>
                  Math.max(1, value - 1))}
              >
                Poprzednia
              </button>

              <button
                className="button-secondary"
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage(value =>
                  Math.min(totalPages, value + 1))}
              >
                Następna
              </button>
            </div>
          </div>
        )}
      </section>
      {selectedId !== null && (
        <OrdersDetails
          key={selectedId}
          orderId={selectedId}
          token={token}
          canManageOrders={canManageOrders}
          onUnauthorized={onUnauthorized}
          onClose={() => setSelectedId(null)}
          onChanged={refresh}
        />
      )}
    </div>
  )
}
