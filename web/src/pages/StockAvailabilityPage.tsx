
import { useEffect, useState, type FormEvent } from 'react'
import {
  fetchAllPages,
  readApiError,
  SessionExpiredError,
  type PagedResult,
  type Warehouse,
} from './goodsReceiptsApi'
import './GoodsReceiptsPage.css'

type StockAvailability = {
  warehouseId: number
  warehouseCode: string
  productId: number
  productCode: string
  productName: string
  unitSymbol: string
  physicalQuantity: number
  outstandingQuantity: number
  freeQuantity: number
}

type Filters = {
  warehouseId: string
  search: string
}

type Props = {
  token: string
  onUnauthorized: () => void
}

const PAGE_SIZE = 20

function formatQuantity(quantity: number) {
  return quantity.toLocaleString('pl-PL', {
    maximumFractionDigits: 3,
  })
}

export default function StockAvailabilityPage({
  token,
  onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [warehouseError, setWarehouseError] =
    useState<string | null>(null)

  const [form, setForm] = useState<Filters>({
    warehouseId: '',
    search: '',
  })

  const [filters, setFilters] = useState<Filters>({
    warehouseId: '',
    search: '',
  })

  const [data, setData] =
    useState<PagedResult<StockAvailability> | null>(null)

  const [page, setPage] = useState(1)
  const [reload, setReload] = useState(0)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Pobranie aktywnych magazynów.
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

        setWarehouses(result.filter(x => x.isActive))
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

  // Pobieranie dostępności z nowego endpointu.
  useEffect(() => {
    const controller = new AbortController()

    async function loadAvailability() {
      setLoading(true)
      setError(null)
      setData(null)

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

      try {
        const response = await fetch(
          `/api/stock/availability?${params.toString()}`,
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
              'Nie udało się pobrać dostępności',
            ),
          )
        }

        const result =
          await response.json() as PagedResult<StockAvailability>

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

        setError(
          err instanceof Error
            ? err.message
            : 'Wystąpił błąd pobierania danych.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadAvailability()

    return () => controller.abort()
  }, [token, page, filters, reload, onUnauthorized])

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    setPage(1)
    setFilters({
      warehouseId: form.warehouseId,
      search: form.search.trim(),
    })
  }

  function clearFilters() {
    const empty = {
      warehouseId: '',
      search: '',
    }

    setForm(empty)
    setFilters(empty)
    setPage(1)
    setReload(value => value + 1)
  }

  const items = data?.items ?? []
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
            <h2>Dostępność towaru</h2>
            <p>
              Zestawienie ilości według magazynu i artykułu.
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
          Wolny zapas to stan fizyczny pomniejszony
          o niezrealizowane ilości zamówień w statusie
          Released. Zestawienie nie przypisuje
          zapotrzebowania do konkretnych palet LE.
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
          <h3>Filtry dostępności</h3>

          <div className="receipt-fields-grid">
            <div className="receipt-field">
              <label htmlFor="availability-warehouse">
                Magazyn
              </label>

              <select
                id="availability-warehouse"
                value={form.warehouseId}
                onChange={event => {
                  setForm(current => ({
                    ...current,
                    warehouseId: event.target.value,
                  }))
                }}
              >
                <option value="">
                  Wszystkie magazyny
                </option>

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
              <label htmlFor="availability-search">
                Artykuł lub nazwa
              </label>

              <input
                id="availability-search"
                maxLength={100}
                value={form.search}
                placeholder="np. AGR-NPK-25"
                onChange={event => {
                  setForm(current => ({
                    ...current,
                    search: event.target.value,
                  }))
                }}
              />
            </div>
          </div>

          <div className="receipt-actions">
            <button
              type="submit"
              className="button-primary"
            >
              Pokaż dostępność
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
            <h3>Zestawienie artykułów</h3>

            <p className="receipt-muted">
              {loading
                ? 'Pobieranie danych...'
                : `Znaleziono ${totalCount} pozycji.`}
            </p>
          </div>
        </div>

        {!loading && !error && items.length === 0 && (
          <div className="receipt-empty-items">
            Brak artykułów spełniających wybrane kryteria.
          </div>
        )}

        {!loading && !error && items.length > 0 && (
          <div className="table-scroll">
            <table className="data-table stock-availability-table">
              <thead>
                <tr>
                  <th>Magazyn</th>
                  <th>Artykuł</th>
                  <th>Fizycznie</th>
                  <th>Zapotrzebowanie</th>
                  <th>Wolny zapas</th>
                </tr>
              </thead>

              <tbody>
                {items.map(item => (
                  <tr
                    key={`${item.warehouseId}-${item.productId}`}
                  >
                    <td>
                      <strong>{item.warehouseCode}</strong>
                    </td>

                    <td>
                      <strong>{item.productCode}</strong>
                      <span className="stock-secondary">
                        {item.productName}
                      </span>
                    </td>

                    <td className="stock-number">
                      <strong>
                        {formatQuantity(item.physicalQuantity)}
                      </strong>
                      <span className="stock-secondary">
                        {item.unitSymbol}
                      </span>
                    </td>

                    <td className="stock-number">
                      <strong>
                        {formatQuantity(item.outstandingQuantity)}
                      </strong>
                      <span className="stock-secondary">
                        {item.unitSymbol}
                      </span>
                    </td>

                    <td className="stock-number">
                      <strong
                        className={
                          item.freeQuantity < 0
                            ? 'stock-free-negative'
                            : item.freeQuantity === 0
                              ? 'stock-free-zero'
                              : 'stock-free-positive'
                        }
                      >
                        {formatQuantity(item.freeQuantity)}
                      </strong>

                      <span className="stock-secondary">
                        {item.unitSymbol}
                      </span>

                      {item.freeQuantity < 0 && (
                        <span className="stock-shortage">
                          Niedobór
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
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
