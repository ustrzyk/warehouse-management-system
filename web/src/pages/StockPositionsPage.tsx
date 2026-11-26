
import { useEffect, useState, type FormEvent } from 'react'
import {
  fetchAllPages,
  readApiError,
  SessionExpiredError,
  type PagedResult,
  type Warehouse,
} from './goodsReceiptsApi'
import './GoodsReceiptsPage.css'
import './StockPositionsPage.css'

type StockPosition = {
  warehouseId: number
  warehouseCode: string
  zoneCode: string
  locationCode: string
  logisticUnitId: number
  logisticUnitCode: string
  productId: number
  productCode: string
  productName: string
  unitSymbol: string
  productBatchId: number | null
  batchNumber: string | null
  expirationDate: string | null
  quantity: number
}

type Filters = {
  warehouseId: string
  search: string
  batchNumber: string
  locationCode: string
}

type Props = {
  token: string
  onUnauthorized: () => void
}

const PAGE_SIZE = 20

const emptyFilters: Filters = {
  warehouseId: '',
  search: '',
  batchNumber: '',
  locationCode: '',
}

function formatQty(value: number) {
  return value.toLocaleString('pl-PL', {
    maximumFractionDigits: 3,
  })
}

function formatDateOnly(value: string | null) {
  if (!value) return '—'

  const parts = value.slice(0, 10).split('-')

  if (parts.length !== 3) return value

  return `${parts[2]}.${parts[1]}.${parts[0]}`
}

function getExpiryStatus(value: string | null) {
  if (!value) return null

  const parts = value.slice(0, 10).split('-').map(Number)

  if (parts.length !== 3 || parts.some(Number.isNaN)) {
    return null
  }

  const [year, month, day] = parts
  const expiry = Date.UTC(year, month - 1, day)

  const today = new Date()
  const currentDay = Date.UTC(
    today.getFullYear(),
    today.getMonth(),
    today.getDate(),
  )

  const days = Math.round(
    (expiry - currentDay) / 86_400_000,
  )

  if (days < 0) {
    return {
      label: 'Po terminie',
      className: 'stock-expiry-expired',
    }
  }

  if (days <= 90) {
    return {
      label: `Za ${days} dni`,
      className: 'stock-expiry-soon',
    }
  }

  return null
}

export default function StockPositionsPage({
  token,
  onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [warehouseError, setWarehouseError] =
    useState<string | null>(null)

  const [form, setForm] = useState<Filters>(emptyFilters)
  const [filters, setFilters] = useState<Filters>(emptyFilters)

  const [data, setData] =
    useState<PagedResult<StockPosition> | null>(null)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)

  // Lista magazynów do wyboru.
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

        setWarehouses(result.filter(item => item.isActive))
        setWarehouseError(null)
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setWarehouseError(
          err instanceof Error
            ? err.message
            : 'Nie udało się pobrać magazynów.',
        )
      }
    }

    void loadWarehouses()

    return () => controller.abort()
  }, [token, onUnauthorized])

  // Pobranie fizycznych stanów magazynowych.
  useEffect(() => {
    const controller = new AbortController()

    async function loadStock() {
      setLoading(true)
      setError(null)

      const params = new URLSearchParams({
        Page: String(page),
        PageSize: String(PAGE_SIZE),
      })

      if (filters.warehouseId) {
        params.set('WarehouseId', filters.warehouseId)
      }

      if (filters.search) {
        params.set('Search', filters.search)
      }

      if (filters.batchNumber) {
        params.set('BatchNumber', filters.batchNumber)
      }

      if (filters.locationCode) {
        params.set('LocationCode', filters.locationCode)
      }

      try {
        const response = await fetch(
          `/api/stock/positions?${params.toString()}`,
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
            await readApiError(
              response,
              'Nie udało się pobrać stanów magazynowych',
            ),
          )
        }

        const result =
          await response.json() as PagedResult<StockPosition>

        if (controller.signal.aborted) return

        const lastPage = Math.max(
          1,
          Math.ceil(result.totalCount / PAGE_SIZE),
        )

        if (page > lastPage) {
          setPage(lastPage)
          return
        }

        setData(result)
      } catch (err) {
        if (controller.signal.aborted) return

        setData(null)
        setError(
          err instanceof Error
            ? err.message
            : 'Błąd pobierania danych magazynowych.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadStock()

    return () => controller.abort()
  }, [
    token,
    page,
    filters,
    reload,
    onUnauthorized,
  ])

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    setPage(1)
    setFilters({
      warehouseId: form.warehouseId,
      search: form.search.trim(),
      batchNumber: form.batchNumber.trim(),
      locationCode: form.locationCode.trim(),
    })
  }

  function clearFilters() {
    setForm(emptyFilters)
    setFilters(emptyFilters)
    setPage(1)
    setReload(value => value + 1)
  }

  const totalCount = data?.totalCount ?? 0

  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE),
  )

  const positions = data?.items ?? []

  return (
    <div className="receipts-page">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Stany magazynowe</h2>
            <p>
              Fizyczne ilości produktów na odłożonych paletach LE.
            </p>
          </div>

          <button
            type="button"
            className="button-secondary"
            disabled={loading}
            onClick={() => setReload(value => value + 1)}
          >
            Odśwież
          </button>
        </div>

        <div className="stock-info">
          Wyświetlane są dodatnie stany produktów na paletach
          w statusie Odłożona (Stored), znajdujących się
          w aktywnych magazynach i lokalizacjach.
          Nie odliczamy tu jeszcze ilości przypisanych
          do zwolnionych zamówień.
        </div>

        {warehouseError && (
          <div className="error-message" role="alert">
            {warehouseError}
          </div>
        )}

        <form
          className="receipt-editor"
          onSubmit={applyFilters}
        >
          <h3>Wyszukiwanie i filtrowanie</h3>

          <div className="receipt-fields-grid">
            <div className="receipt-field">
              <label htmlFor="stock-warehouse">
                Magazyn
              </label>

              <select
                id="stock-warehouse"
                value={form.warehouseId}
                onChange={event => setForm(current => ({
                  ...current,
                  warehouseId: event.target.value,
                }))}
              >
                <option value="">Wszystkie magazyny</option>

                {warehouses.map(warehouse => (
                  <option
                    key={warehouse.id}
                    value={warehouse.id}
                  >
                    {warehouse.code} — {warehouse.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="receipt-field">
              <label htmlFor="stock-search">
                Artykuł, nazwa lub numer LE
              </label>

              <input
                id="stock-search"
                maxLength={100}
                value={form.search}
                placeholder="np. AGR-NPK-25 lub LE-AG-0001"
                onChange={event => setForm(current => ({
                  ...current,
                  search: event.target.value,
                }))}
              />
            </div>

            <div className="receipt-field">
              <label htmlFor="stock-batch">
                Numer partii
              </label>

              <input
                id="stock-batch"
                maxLength={100}
                value={form.batchNumber}
                placeholder="np. B-2026-AGR-NPK-25"
                onChange={event => setForm(current => ({
                  ...current,
                  batchNumber: event.target.value,
                }))}
              />
            </div>

            <div className="receipt-field">
              <label htmlFor="stock-location">
                Lokalizacja
              </label>

              <input
                id="stock-location"
                maxLength={50}
                value={form.locationCode}
                placeholder="np. AD-01"
                onChange={event => setForm(current => ({
                  ...current,
                  locationCode: event.target.value,
                }))}
              />
            </div>
          </div>

          <div className="receipt-actions">
            <button type="submit" className="button-primary">
              Pokaż stany
            </button>

            <button
              type="button"
              className="button-secondary"
              onClick={clearFilters}
            >
              Wyczyść filtry
            </button>
          </div>
        </form>

        {error && (
          <div className="error-message" role="alert">
            {error}
          </div>
        )}

        <div className="stock-results-heading">
          <div>
            <h3>Pozycje magazynowe</h3>
            <p className="receipt-muted">
              {loading
                ? 'Pobieranie danych...'
                : `Znaleziono ${totalCount} pozycji na paletach.`}
            </p>
          </div>
        </div>

        {!loading && !error && positions.length === 0 && (
          <div className="receipt-empty-items">
            Brak stanów spełniających wybrane kryteria.
          </div>
        )}

        {!loading && !error && positions.length > 0 && (
          <div className="table-scroll">
            <table className="data-table stock-table">
              <thead>
                <tr>
                  <th>Magazyn</th>
                  <th>Artykuł</th>
                  <th>Partia i ważność</th>
                  <th>Paleta LE</th>
                  <th>Lokalizacja</th>
                  <th>Ilość fizyczna</th>
                </tr>
              </thead>

              <tbody>
                {positions.map(item => {
                  const expiry =
                    getExpiryStatus(item.expirationDate)

                  return (
                    <tr
                      key={
                        `${item.logisticUnitId}-` +
                        `${item.productId}-` +
                        `${item.productBatchId ?? 'none'}`
                      }
                    >
                      <td>
                        <strong>{item.warehouseCode}</strong>
                        <span className="stock-secondary">
                          {item.zoneCode}
                        </span>
                      </td>

                      <td>
                        <strong>{item.productCode}</strong>
                        <span className="stock-secondary">
                          {item.productName}
                        </span>
                      </td>

                      <td>
                        <span>
                          {item.batchNumber || 'Bez partii'}
                        </span>

                        <span className="stock-secondary">
                          Ważność: {
                            formatDateOnly(item.expirationDate)
                          }
                        </span>

                        {expiry && (
                          <span
                            className={
                              `stock-expiry ${expiry.className}`
                            }
                          >
                            {expiry.label}
                          </span>
                        )}
                      </td>

                      <td>
                        <strong>{item.logisticUnitCode}</strong>
                      </td>

                      <td>
                        <strong>{item.locationCode}</strong>
                      </td>

                      <td className="stock-quantity">
                        <strong>{formatQty(item.quantity)}</strong>
                        <span className="stock-secondary">
                          {item.unitSymbol}
                        </span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {!loading && !error && totalCount > 0 && (
          <div className="pagination">
            <span>
              Strona {page} z {totalPages}
            </span>

            <div className="pagination-actions">
              <button
                type="button"
                className="button-secondary"
                disabled={page <= 1}
                onClick={() => setPage(current =>
                  Math.max(1, current - 1))}
              >
                Poprzednia
              </button>

              <button
                type="button"
                className="button-secondary"
                disabled={page >= totalPages}
                onClick={() => setPage(current =>
                  Math.min(totalPages, current + 1))}
              >
                Następna
              </button>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}
