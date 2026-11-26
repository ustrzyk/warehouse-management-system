
import { useEffect, useState, type FormEvent } from 'react'
import OrderPickingPanel from './OrderPickingPanel'
import {
  fetchAllPages,
  formatDate,
  readApiError,
  SessionExpiredError,
  type Product,
  type ProductBatch,
} from './goodsReceiptsApi'
import type { Order } from './OrdersPage'

type OrderItem = {
  id: number
  warehouseOrderId: number
  productId: number
  productCode: string
  productName: string
  productBatchId: number | null
  batchNumber: string | null
  quantityRequested: number
}

type Props = {
  orderId: number
  token: string
  canManageOrders: boolean
  canPick: boolean
  onUnauthorized: () => void
  onClose: () => void
  onChanged: () => void
}

export default function OrdersDetails({
  orderId, token, canManageOrders, canPick,
  onUnauthorized, onClose, onChanged,
}: Props) {
  const [order, setOrder] = useState<Order | null>(null)
  const [items, setItems] = useState<OrderItem[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [reload, setReload] = useState(0)

  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [products, setProducts] = useState<Product[]>([])
  const [batches, setBatches] = useState<ProductBatch[]>([])
  const [productId, setProductId] = useState(0)
  const [batchId, setBatchId] = useState(0)
  const [quantity, setQuantity] = useState('1')
  const [productsLoading, setProductsLoading] = useState(false)
  const [batchesLoading, setBatchesLoading] = useState(false)

  const canAdd = order?.status === 'Draft' && canManageOrders

  useEffect(() => {
    const controller = new AbortController()

    async function loadDetails() {
      setLoading(true)
      setError(null)

      try {
        const options = {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        }

        const [orderResponse, itemsResponse] = await Promise.all([
          fetch(`/api/warehouse-orders/${orderId}`, options),
          fetch(`/api/warehouse-orders/${orderId}/items`, options),
        ])

        if (controller.signal.aborted) return

        if (orderResponse.status === 401 || itemsResponse.status === 401) {
          onUnauthorized()
          return
        }

        if (!orderResponse.ok) {
          throw new Error(await readApiError(
            orderResponse, 'Nie udało się pobrać zamówienia',
          ))
        }

        if (!itemsResponse.ok) {
          throw new Error(await readApiError(
            itemsResponse, 'Nie udało się pobrać pozycji',
          ))
        }

        const details = await orderResponse.json() as Order
        const positions = await itemsResponse.json() as OrderItem[]

        if (controller.signal.aborted) return
        setOrder(details)
        setItems(positions)
      } catch (err) {
        if (controller.signal.aborted) return
        setError(err instanceof Error
          ? err.message : 'Błąd pobierania szczegółów.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void loadDetails()
    return () => controller.abort()
  }, [orderId, token, reload, onUnauthorized])

  useEffect(() => {
    const controller = new AbortController()

    async function loadProducts() {
      if (!canAdd) {
        setProducts([])
        setProductId(0)
        return
      }

      setProductsLoading(true)
      try {
        const result = await fetchAllPages<Product>(
          '/api/products?IsActive=true&SortBy=name',
          token,
          controller.signal,
        )

        if (controller.signal.aborted) return

        setProducts(result)
        setProductId(current =>
          result.some(item => item.id === current)
            ? current : (result[0]?.id ?? 0),
        )
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }
        setError(err instanceof Error
          ? err.message : 'Błąd pobierania produktów.')
      } finally {
        if (!controller.signal.aborted) setProductsLoading(false)
      }
    }

    void loadProducts()
    return () => controller.abort()
  }, [canAdd, token, onUnauthorized])

  useEffect(() => {
    const controller = new AbortController()

    async function loadBatches() {
      setBatches([])
      setBatchId(0)

      if (!canAdd || productId === 0) return

      setBatchesLoading(true)

      try {
        const result = await fetchAllPages<ProductBatch>(
          `/api/product-batches?ProductId=${productId}&IsActive=true`,
          token,
          controller.signal,
        )

        if (!controller.signal.aborted) setBatches(result)
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }
        setError(err instanceof Error
          ? err.message : 'Błąd pobierania partii.')
      } finally {
        if (!controller.signal.aborted) setBatchesLoading(false)
      }
    }

    void loadBatches()
    return () => controller.abort()
  }, [canAdd, productId, token, onUnauthorized])

  async function addItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canAdd || working || productsLoading || batchesLoading) return

    const raw = quantity.trim().replace(',', '.')
    const amount = Number(raw)

    if (!/^\d+(\.\d{1,3})?$/.test(raw) ||
        !Number.isFinite(amount) ||
        amount < 0.001 || amount >= 1_000_000_000_000_000) {
      setError('Podaj dodatnią ilość, maksymalnie 3 miejsca po przecinku.')
      return
    }

    if (productId <= 0) {
      setError('Wybierz produkt.')
      return
    }

    if (items.some(item =>
      item.productId === productId &&
      (item.productBatchId ?? 0) === batchId
    )) {
      setError('Ten produkt i partia są już w zamówieniu.')
      return
    }

    setWorking(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch(
        `/api/warehouse-orders/${orderId}/items`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            productId,
            productBatchId: batchId > 0 ? batchId : null,
            quantityRequested: amount,
          }),
        },
      )

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(await readApiError(
          response, 'Nie udało się dodać produktu',
        ))
      }

      setSuccess('Produkt został dodany do zamówienia.')
      setQuantity('1')
      setReload(value => value + 1)
      onChanged()
    } catch (err) {
      setError(err instanceof Error
        ? err.message : 'Błąd dodawania produktu.')
    } finally {
      setWorking(false)
    }
  }

  async function releaseOrder() {
    if (!order || order.status !== 'Draft' ||
        !canManageOrders || items.length === 0 || working) {
      return
    }

    const confirmed = window.confirm(
      `Zwolnić zamówienie ${order.number} do kompletacji?\n\n` +
      'System sprawdzi dostępny zapas i uwzględni inne zwolnione zamówienia.',
    )

    if (!confirmed) return

    setWorking(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch(
        `/api/warehouse-orders/${orderId}/release`,
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
        throw new Error(
          await readApiError(
            response,
            'Nie udało się zwolnić zamówienia',
          ),
        )
      }

      const updated = (await response.json()) as Order

      setOrder(updated)
      setSuccess(
        `Zamówienie ${updated.number} zostało zwolnione do kompletacji.`,
      )

      setReload(value => value + 1)
      onChanged()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd podczas zwalniania zamówienia.',
      )
    } finally {
      setWorking(false)
    }
  }

  return (
    <section className="panel receipt-details">
      <div className="panel-heading">
        <div>
          <h2>Szczegóły zamówienia</h2>
          <p>Dokument magazynowy i jego pozycje.</p>
        </div>
        <button type="button" className="button-secondary"
          onClick={onClose}>
          Zamknij
        </button>
      </div>

      {loading && <p className="receipt-muted">Pobieranie danych...</p>}
      {error && <div className="error-message" role="alert">{error}</div>}
      {success && <div className="receipt-success" role="status">{success}</div>}

      {order && (
        <>
          <div className="receipt-summary-grid">
            <div className="receipt-summary-card">
              <span>Numer</span><strong>{order.number}</strong>
            </div>
            <div className="receipt-summary-card">
              <span>Status</span><strong>{order.status}</strong>
            </div>
            <div className="receipt-summary-card">
              <span>Magazyn</span><strong>{order.warehouseCode}</strong>
            </div>
            <div className="receipt-summary-card">
              <span>Klient</span><strong>{order.customerName || '—'}</strong>
            </div>
            <div className="receipt-summary-card">
              <span>Utworzono</span><strong>{formatDate(order.createdAtUtc)}</strong>
            </div>
            <div className="receipt-summary-card">
              <span>Pozycji</span><strong>{items.length}</strong>
            </div>
          </div>

          <div className="receipt-items-header">
            <h3>Produkty w zamówieniu</h3>
          </div>

          {items.length === 0 ? (
            <div className="receipt-empty-items">
              Zamówienie nie zawiera jeszcze produktów.
            </div>
          ) : (
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Artykuł</th>
                    <th>Nazwa</th>
                    <th>Partia</th>
                    <th>Ilość zamówiona</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map(item => (
                    <tr key={item.id}>
                      <td><strong>{item.productCode}</strong></td>
                      <td>{item.productName}</td>
                      <td>{item.batchNumber || '—'}</td>
                      <td>
                        {item.quantityRequested.toLocaleString('pl-PL', {
                          maximumFractionDigits: 3,
                        })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {canAdd && (
            <form className="receipt-editor" onSubmit={event => void addItem(event)}>
              <h3>Dodaj produkt do zamówienia</h3>

              <div className="receipt-fields-grid">
                <div className="receipt-field receipt-field-full">
                  <label htmlFor="order-product">Produkt *</label>
                  <select id="order-product" value={productId}
                    disabled={working || productsLoading}
                    onChange={event => {
                      setProductId(Number(event.target.value))
                      setBatchId(0)
                    }}>
                    <option value={0}>Wybierz produkt</option>
                    {products.map(item => (
                      <option key={item.id} value={item.id}>
                        {item.code} — {item.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="receipt-field">
                  <label htmlFor="order-batch">Partia (opcjonalnie)</label>
                  <select id="order-batch" value={batchId}
                    disabled={working || batchesLoading || productId === 0}
                    onChange={event =>
                      setBatchId(Number(event.target.value))}>
                    <option value={0}>Bez konkretnej partii</option>
                    {batches.map(item => (
                      <option key={item.id} value={item.id}>
                        {item.batchNumber}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="receipt-field">
                  <label htmlFor="order-quantity">Ilość *</label>
                  <input id="order-quantity" value={quantity}
                    disabled={working}
                    inputMode="decimal"
                    onChange={event => setQuantity(event.target.value)}
                    placeholder="np. 10 lub 1,250" required />
                </div>
              </div>

              <div className="receipt-actions">
                <button type="submit" className="button-primary"
                  disabled={working || productsLoading ||
                    batchesLoading || productId === 0}>
                  {working ? 'Zapisywanie...' : '+ Dodaj produkt'}
                </button>
              </div>
            </form>
          )}

          {order.status === 'Draft' && (
            <div className="receipt-confirm-section">
              <div>
                <h3>Zwolnienie do kompletacji</h3>
                <p>
                  System sprawdzi dostępność produktów i partii,
                  uwzględniając zapas potrzebny innym zamówieniom.
                  Po zwolnieniu nie będzie można dodawać kolejnych
                  pozycji do tego zamówienia.
                </p>
              </div>

              {canManageOrders && (
                <button
                  type="button"
                  className="button-primary"
                  onClick={() => void releaseOrder()}
                  disabled={working || loading || items.length === 0}
                >
                  {working
                    ? 'Sprawdzanie zapasu...'
                    : 'Zwolnij do kompletacji'}
                </button>
              )}
            </div>
          )}

          {order.status === 'Released' && (
            <div className="receipt-completed">
              <strong>Zamówienie przekazane do kompletacji.</strong>
              <p>
                Można rozpocząć pobieranie produktów z palet magazynowych.
                Zawartość zamówienia jest teraz tylko do odczytu.
              </p>
            </div>
          )}

          {(
            order.status === 'Released' ||
            order.status === 'Completed' ||
            order.status === 'Shipped'
          ) && (
            <OrderPickingPanel
              orderId={order.id}
              status={order.status}
              items={items}
              token={token}
              canPick={canPick}
              onUnauthorized={onUnauthorized}
              onChanged={() => {
                setReload(value => value + 1)
                onChanged()
              }}
            />
          )}
        </>
      )}
    </section>
  )
}
