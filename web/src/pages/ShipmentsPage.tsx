
import { useEffect, useState, type FormEvent } from 'react'
import {
  fetchAllPages,
  formatDate,
  readApiError,
  SessionExpiredError,
} from './goodsReceiptsApi'
import type { Order } from './OrdersPage'
import './GoodsReceiptsPage.css'

type Shipment = {
  id: number
  number: string
  warehouseOrderId: number
  warehouseOrderNumber: string
  carrierName: string
  trackingNumber: string | null
  status: string
  createdAtUtc: string
  shippedAtUtc: string | null
  revision: number
}

type Props = {
  token: string
  canManageShipments: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10

export default function ShipmentsPage({
  token,
  canManageShipments,
  onUnauthorized,
}: Props) {
  const [orders, setOrders] = useState<Order[]>([])
  const [loading, setLoading] = useState(true)
  const [reload, setReload] = useState(0)

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)

  const [selectedOrder, setSelectedOrder] = useState<Order | null>(null)
  const [shipment, setShipment] = useState<Shipment | null>(null)
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [shipmentLookupFailed, setShipmentLookupFailed] =
    useState(false)
  const [working, setWorking] = useState(false)

  const [carrier, setCarrier] = useState('')
  const [tracking, setTracking] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Backend nie ma jeszcze osobnej listy wysyłek.
  // Korzystamy z zamówień Completed i Shipped.
  useEffect(() => {
    const controller = new AbortController()

    async function loadOrders() {
      setLoading(true)
      setOrders([])
      setError(null)

      try {
        const statuses = filter === 'all'
          ? [2, 3]
          : [Number(filter)]

        const results = await Promise.all(
          statuses.map(status => {
            const params = new URLSearchParams({
              Status: String(status),
            })

            if (search) params.set('Search', search)

            return fetchAllPages<Order>(
              `/api/warehouse-orders?${params}`,
              token,
              controller.signal,
            )
          }),
        )

        if (controller.signal.aborted) return

        const sorted = results.flat().sort(
          (a, b) =>
            b.createdAtUtc.localeCompare(a.createdAtUtc) ||
            b.id - a.id,
        )

        setOrders(sorted)

        const lastPage = Math.max(
          1,
          Math.ceil(sorted.length / PAGE_SIZE),
        )

        setPage(current => Math.min(current, lastPage))
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setError(err instanceof Error
          ? err.message
          : 'Nie udało się pobrać zamówień.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void loadOrders()
    return () => controller.abort()
  }, [token, filter, search, reload, onUnauthorized])

  const selectedOrderId = selectedOrder?.id

  // Pobranie dokumentu wysyłki dla wybranego zamówienia.
  useEffect(() => {
    const controller = new AbortController()

    async function loadShipment() {
      setShipment(null)
      setShipmentLookupFailed(false)

      if (selectedOrderId === undefined) {
        setDetailsLoading(false)
        return
      }

      setDetailsLoading(true)

      try {
        const response = await fetch(
          `/api/shipments/by-order/${selectedOrderId}`,
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

        // Brak wysyłki jest normalny dla nowego
        // zamówienia Completed.
        if (response.status === 404) {
          setShipment(null)
          return
        }

        if (!response.ok) {
          throw new Error(await readApiError(
            response, 'Nie udało się pobrać wysyłki',
          ))
        }

        setShipment(await response.json() as Shipment)
      } catch (err) {
        if (controller.signal.aborted) return

        setShipmentLookupFailed(true)

        setError(err instanceof Error
          ? err.message
          : 'Błąd pobierania wysyłki.')
      } finally {
        if (!controller.signal.aborted) {
          setDetailsLoading(false)
        }
      }
    }

    void loadShipment()
    return () => controller.abort()
  }, [selectedOrderId, token, reload, onUnauthorized])

  function openOrder(order: Order) {
    // Ten sam identyfikator nie uruchomi ponownie efektu pobierania,
    // więc nie zerujemy stanu wysyłki.
    const sameOrder = order.id === selectedOrderId

    setSelectedOrder(order)
    if (sameOrder) return

    setShipment(null)
    setDetailsLoading(true)
    setShipmentLookupFailed(false)
    setCarrier('')
    setTracking('')
    setError(null)
    setSuccess(null)
  }

  async function createShipment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!selectedOrder ||
        selectedOrder.status !== 'Completed' ||
        !canManageShipments || working ||
        detailsLoading || shipmentLookupFailed) {
      return
    }

    const carrierName = carrier.trim()
    const trackingNumber = tracking.trim()

    if (!carrierName || carrierName.length > 100 ||
        trackingNumber.length > 100) {
      setError('Sprawdź przewoźnika i numer śledzenia.')
      return
    }

    setWorking(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch('/api/shipments', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          warehouseOrderId: selectedOrder.id,
          carrierName,
          trackingNumber: trackingNumber || null,
        }),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(await readApiError(
          response, 'Nie udało się przygotować wysyłki',
        ))
      }

      const created = await response.json() as Shipment

      setShipment(created)
      setSuccess(
        `Utworzono wysyłkę ${created.number}. ` +
        'Możesz teraz potwierdzić jej wysłanie.',
      )
    } catch (err) {
      setError(err instanceof Error
        ? err.message
        : 'Błąd tworzenia wysyłki.')
    } finally {
      setWorking(false)
    }
  }

  async function dispatchShipment() {
    if (!shipment || !selectedOrder ||
        shipment.status !== 'Prepared' ||
        !canManageShipments || working) {
      return
    }

    const confirmed = window.confirm(
      `Potwierdzić wysłanie ${shipment.number}?\n\n` +
      `Zamówienie: ${selectedOrder.number}\n` +
      `Przewoźnik: ${shipment.carrierName}\n\n` +
      'Status zamówienia zmieni się na Shipped.',
    )

    if (!confirmed) return

    setWorking(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch(
        `/api/shipments/${shipment.id}/dispatch`,
        {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${token}`,
          },
        },
      )

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(await readApiError(
          response, 'Nie udało się potwierdzić wysyłki',
        ))
      }

      const updated = await response.json() as Shipment

      setShipment(updated)
      setSelectedOrder(current => current
        ? {
            ...current,
            status: 'Shipped',
            shippedAtUtc: updated.shippedAtUtc,
          }
        : null,
      )

      setSuccess(
        `Wysyłka ${updated.number} została potwierdzona.`,
      )

      setFilter('all')
      setPage(1)
      setReload(value => value + 1)
    } catch (err) {
      setError(err instanceof Error
        ? err.message
        : 'Błąd potwierdzania wysyłki.')
    } finally {
      setWorking(false)
    }
  }

  const totalPages = Math.max(
    1,
    Math.ceil(orders.length / PAGE_SIZE),
  )

  const visibleOrders = orders.slice(
    (page - 1) * PAGE_SIZE,
    page * PAGE_SIZE,
  )

  return (
    <div className="receipts-page">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Wysyłki</h2>
            <p>
              Przygotowanie przesyłek i potwierdzanie
              wydania zamówień przewoźnikom.
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

        <form
          className="receipt-filters"
          onSubmit={event => {
            event.preventDefault()
            setSearch(searchInput.trim())
            setPage(1)
          }}
        >
          <div className="receipt-field receipt-filter-search">
            <label htmlFor="shipment-search">
              Numer zamówienia lub klient
            </label>
            <input
              id="shipment-search"
              maxLength={100}
              value={searchInput}
              placeholder="np. ORD-CH-AG-001"
              onChange={event =>
                setSearchInput(event.target.value)}
            />
          </div>

          <div className="receipt-field receipt-filter-status">
            <label htmlFor="shipment-filter">
              Status zamówienia
            </label>
            <select
              id="shipment-filter"
              value={filter}
              onChange={event => {
                setFilter(event.target.value)
                setPage(1)
              }}
            >
              <option value="all">Wszystkie</option>
              <option value="2">Skompletowane</option>
              <option value="3">Wysłane</option>
            </select>
          </div>

          <button type="submit" className="button-primary">
            Szukaj
          </button>

          <button
            type="button"
            className="button-secondary"
            onClick={() => {
              setSearchInput('')
              setSearch('')
              setFilter('all')
              setPage(1)
            }}
          >
            Wyczyść
          </button>
        </form>

        <div className="results-info">
          {loading
            ? 'Pobieranie danych...'
            : `Znaleziono zamówień: ${orders.length}`}
        </div>

        {!loading && orders.length === 0 && !error && (
          <div className="empty-state">
            <strong>Brak zamówień do wyświetlenia</strong>
            <p>
              Wysyłkę można przygotować po zakończeniu
              kompletacji zamówienia.
            </p>
          </div>
        )}

        {visibleOrders.length > 0 && (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Numer zamówienia</th>
                  <th>Magazyn</th>
                  <th>Klient</th>
                  <th>Status</th>
                  <th>Data utworzenia</th>
                  <th>Akcje</th>
                </tr>
              </thead>
              <tbody>
                {visibleOrders.map(order => (
                  <tr key={order.id}>
                    <td><strong>{order.number}</strong></td>
                    <td>{order.warehouseCode}</td>
                    <td>{order.customerName || '—'}</td>
                    <td>
                      <span className={
                        order.status === 'Shipped'
                          ? 'receipt-status receipt-status-received'
                          : 'receipt-status receipt-status-draft'
                      }>
                        {order.status === 'Shipped'
                          ? 'Wysłane'
                          : 'Skompletowane'}
                      </span>
                    </td>
                    <td>{formatDate(order.createdAtUtc)}</td>
                    <td>
                      <button
                        type="button"
                        className="button-secondary receipt-small-button"
                        onClick={() => openOrder(order)}
                      >
                        Wysyłka
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!loading && totalPages > 1 && (
          <div className="pagination">
            <span>Strona {page} z {totalPages}</span>

            <div className="pagination-actions">
              <button
                type="button"
                className="button-secondary"
                disabled={page === 1}
                onClick={() =>
                  setPage(value => Math.max(1, value - 1))}
              >
                Poprzednia
              </button>

              <button
                type="button"
                className="button-secondary"
                disabled={page === totalPages}
                onClick={() =>
                  setPage(value =>
                    Math.min(totalPages, value + 1))}
              >
                Następna
              </button>
            </div>
          </div>
        )}
      </section>

      {selectedOrder && (
        <section className="panel">
          <div className="panel-heading">
            <div>
              <h2>Wysyłka dla {selectedOrder.number}</h2>
              <p>
                {selectedOrder.customerName || 'Brak nazwy klienta'}
                {' · '}{selectedOrder.warehouseCode}
              </p>
            </div>

            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                setSelectedOrder(null)
                setShipment(null)
                setError(null)
                setSuccess(null)
              }}
            >
              Zamknij
            </button>
          </div>

          {detailsLoading && (
            <p className="receipt-muted">
              Pobieranie informacji o wysyłce...
            </p>
          )}

          {!detailsLoading && shipmentLookupFailed && (
            <div className="receipt-editor">
              <p className="receipt-hint">
                Nie udało się sprawdzić, czy dokument wysyłki
                już istnieje. Tworzenie kolejnego dokumentu
                zostało wstrzymane.
              </p>

              <button
                type="button"
                className="button-secondary"
                onClick={() => {
                  setError(null)
                  setReload(value => value + 1)
                }}
              >
                Ponów sprawdzenie wysyłki
              </button>
            </div>
          )}

          {!detailsLoading && !shipment &&
            !shipmentLookupFailed &&
            selectedOrder.status === 'Completed' &&
            canManageShipments && (
            <form
              className="receipt-editor"
              onSubmit={event => void createShipment(event)}
            >
              <h3>Przygotuj wysyłkę</h3>

              <p className="receipt-hint">
                Numer wysyłki zostanie nadany automatycznie.
                Jeden dokument wysyłki przypada na
                jedno zamówienie.
              </p>

              <div className="receipt-fields-grid">
                <div className="receipt-field">
                  <label htmlFor="shipment-carrier">
                    Przewoźnik *
                  </label>
                  <input
                    id="shipment-carrier"
                    required
                    maxLength={100}
                    value={carrier}
                    disabled={working}
                    placeholder="np. przewoźnik krajowy"
                    onChange={event =>
                      setCarrier(event.target.value)}
                  />
                </div>

                <div className="receipt-field">
                  <label htmlFor="shipment-tracking">
                    Numer śledzenia (opcjonalnie)
                  </label>
                  <input
                    id="shipment-tracking"
                    maxLength={100}
                    value={tracking}
                    disabled={working}
                    placeholder="Numer przesyłki"
                    onChange={event =>
                      setTracking(event.target.value)}
                  />
                </div>
              </div>

              <div className="receipt-actions">
                <button
                  type="submit"
                  className="button-primary"
                  disabled={working}
                >
                  {working
                    ? 'Zapisywanie...'
                    : 'Przygotuj wysyłkę'}
                </button>
              </div>
            </form>
          )}

          {!detailsLoading && !shipment &&
            selectedOrder.status === 'Completed' &&
            !canManageShipments && (
            <p className="receipt-hint">
              Utworzenie wysyłki wymaga roli
              Administrator lub Manager.
            </p>
          )}

          {!detailsLoading && !shipment &&
            !shipmentLookupFailed &&
            selectedOrder.status === 'Shipped' && (
            <div className="error-message">
              Zamówienie jest wysłane, ale nie odnaleziono
              powiązanego dokumentu wysyłki.
            </div>
          )}

          {!detailsLoading && shipment && (
            <>
              <div className="receipt-summary-grid">
                <div className="receipt-summary-card">
                  <span>Numer wysyłki</span>
                  <strong>{shipment.number}</strong>
                </div>

                <div className="receipt-summary-card">
                  <span>Status</span>
                  <strong>
                    {shipment.status === 'Prepared'
                      ? 'Przygotowana'
                      : 'Wysłana'}
                  </strong>
                </div>

                <div className="receipt-summary-card">
                  <span>Przewoźnik</span>
                  <strong>{shipment.carrierName}</strong>
                </div>

                <div className="receipt-summary-card">
                  <span>Numer śledzenia</span>
                  <strong>{shipment.trackingNumber || '—'}</strong>
                </div>

                <div className="receipt-summary-card">
                  <span>Utworzono</span>
                  <strong>{formatDate(shipment.createdAtUtc)}</strong>
                </div>

                <div className="receipt-summary-card">
                  <span>Wysłano</span>
                  <strong>{formatDate(shipment.shippedAtUtc)}</strong>
                </div>
              </div>

              {shipment.status === 'Prepared' && (
                <div className="receipt-confirm-section">
                  <div>
                    <h3>Potwierdzenie wydania</h3>
                    <p>
                      Po potwierdzeniu dokument wysyłki
                      i zamówienie otrzymają status Shipped.
                      Operacji nie można ponowić.
                    </p>
                  </div>

                  {canManageShipments && (
                    <button
                      type="button"
                      className="button-primary"
                      disabled={working}
                      onClick={() => void dispatchShipment()}
                    >
                      {working
                        ? 'Potwierdzanie...'
                        : 'Potwierdź wysłanie'}
                    </button>
                  )}
                </div>
              )}

              {shipment.status === 'Shipped' && (
                <div className="receipt-completed">
                  <strong>Wysyłka potwierdzona.</strong>
                  <p>
                    Zamówienie zostało oznaczone jako wysłane.
                    Historia kompletacji pozostaje zachowana.
                  </p>
                </div>
              )}
            </>
          )}
        </section>
      )}
    </div>
  )
}
