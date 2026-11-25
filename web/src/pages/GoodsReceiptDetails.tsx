import { useEffect, useState, type FormEvent } from 'react'
import {
  fetchAllPages, formatDate, readApiError, receiptStatus,
  SessionExpiredError,
  type GoodsReceipt, type GoodsReceiptItem, type Product, type ProductBatch,
} from './goodsReceiptsApi'

type Props = {
  receiptId: number
  token: string
  canOperateLogistics: boolean
  onUnauthorized: () => void
  onClose: () => void
  onChanged: () => void
}

function formatReceiptPalletCode(receiptId: number): string {
  return `LE-GR-${String(receiptId).padStart(8, '0')}-AUTO`
}

export default function GoodsReceiptDetails({
  receiptId, token, canOperateLogistics, onUnauthorized, onClose, onChanged,
}: Props) {
  const [receipt, setReceipt] = useState<GoodsReceipt | null>(null)
  const [items, setItems] = useState<GoodsReceiptItem[]>([])
  const [detailsLoading, setDetailsLoading] = useState(true)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)
  const [success, setSuccess] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [working, setWorking] = useState(false)

  const [products, setProducts] = useState<Product[]>([])
  const [productsLoading, setProductsLoading] = useState(false)
  const [productsError, setProductsError] = useState<string | null>(null)
  const [productId, setProductId] = useState(0)
  const [batches, setBatches] = useState<ProductBatch[]>([])
  const [batchesLoading, setBatchesLoading] = useState(false)
  const [batchesError, setBatchesError] = useState<string | null>(null)
  const [batchId, setBatchId] = useState(0)
  const [quantity, setQuantity] = useState('1')

  useEffect(() => {
    const controller = new AbortController()
    async function loadDetails() {
      setDetailsLoading(true)
      setDetailsError(null)
      try {
        const options = {
          headers: { Authorization: `Bearer ${token}` },
          signal: controller.signal,
        }
        const [detailResponse, itemsResponse] = await Promise.all([
          fetch(`/api/goods-receipts/${receiptId}`, options),
          fetch(`/api/goods-receipts/${receiptId}/items`, options),
        ])
        if (controller.signal.aborted) return
        if (detailResponse.status === 401 || itemsResponse.status === 401) {
          onUnauthorized()
          return
        }
        if (!detailResponse.ok) {
          throw new Error(await readApiError(detailResponse, 'Błąd szczegółów przyjęcia'))
        }
        if (!itemsResponse.ok) {
          throw new Error(await readApiError(itemsResponse, 'Błąd pozycji przyjęcia'))
        }
        const detail = (await detailResponse.json()) as GoodsReceipt
        const positions = (await itemsResponse.json()) as GoodsReceiptItem[]
        if (controller.signal.aborted) return
        setReceipt(detail)
        setItems(positions)
      } catch (err) {
        if (!controller.signal.aborted) {
          setDetailsError(err instanceof Error ? err.message : 'Nie udało się pobrać przyjęcia.')
        }
      } finally {
        if (!controller.signal.aborted) setDetailsLoading(false)
      }
    }
    void loadDetails()
    return () => controller.abort()
  }, [receiptId, token, refresh, onUnauthorized])

  const isDraft = receipt?.status === 'Draft'
  const canAdd = isDraft && canOperateLogistics

  useEffect(() => {
    const controller = new AbortController()
    async function loadProducts() {
      if (!canAdd) {
        setProducts([])
        setProductId(0)
        return
      }
      setProductsLoading(true)
      setProductsError(null)
      try {
        const result = await fetchAllPages<Product>(
          '/api/products?IsActive=true&SortBy=name', token, controller.signal,
        )
        if (controller.signal.aborted) return
        setProducts(result)
        setProductId(current =>
          result.some(product => product.id === current)
            ? current : (result[0]?.id ?? 0),
        )
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }
        setProductsError(err instanceof Error ? err.message : 'Błąd listy produktów.')
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
      setBatchesError(null)
      if (!canAdd || productId === 0) {
        setBatchesLoading(false)
        return
      }
      setBatchesLoading(true)
      try {
        const result = await fetchAllPages<ProductBatch>(
          `/api/product-batches?ProductId=${productId}&IsActive=true&SortBy=batchNumber`,
          token, controller.signal,
        )
        if (controller.signal.aborted) return
        setBatches(result)
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }
        setBatchesError(err instanceof Error ? err.message : 'Błąd listy partii.')
      } finally {
        if (!controller.signal.aborted) setBatchesLoading(false)
      }
    }
    void loadBatches()
    return () => controller.abort()
  }, [productId, canAdd, token, onUnauthorized])

  async function handleAddItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setActionError(null)
    setSuccess(null)

    const raw = quantity.trim().replace(',', '.')
    const amount = Number(raw)
    if (!/^\d+(\.\d{1,3})?$/.test(raw) ||
      !Number.isFinite(amount) || amount < 0.001 || amount >= 1_000_000_000_000_000) {
      setActionError('Ilość musi być dodatnia, maksymalnie 3 miejsca po przecinku.')
      return
    }
    if (!canAdd || productId < 1 || productsLoading || batchesLoading ||
      productsError || batchesError) {
      setActionError('Wybierz aktywny produkt i poczekaj na załadowanie danych.')
      return
    }
    if (items.some(item => item.productId === productId &&
      (item.productBatchId ?? 0) === batchId)) {
      setActionError('Ta kombinacja produktu i partii jest już w dokumencie.')
      return
    }

    setWorking(true)
    try {
      const response = await fetch(`/api/goods-receipts/${receiptId}/items`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          productId,
          productBatchId: batchId > 0 ? batchId : null,
          quantity: amount,
        }),
      })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await readApiError(response, 'Nie udało się dodać pozycji'))
      }
      setSuccess('Pozycja została dodana do przyjęcia.')
      setQuantity('1')
      setRefresh(current => current + 1)
      onChanged()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Błąd dodawania pozycji.')
    } finally {
      setWorking(false)
    }
  }

  async function handleConfirm() {
    if (!isDraft || !canOperateLogistics || items.length === 0) return
    const confirmed = window.confirm(
      `Zatwierdzić przyjęcie ${receipt?.number ?? ''}?\n\nZostanie utworzona paleta LE. Po zatwierdzeniu nie można dodawać pozycji.`,
    )
    if (!confirmed) return

    setWorking(true)
    setActionError(null)
    setSuccess(null)
    try {
      const response = await fetch(`/api/goods-receipts/${receiptId}/confirm`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
      })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(await readApiError(response, 'Nie udało się zatwierdzić przyjęcia'))
      }
      const confirmedReceipt = (await response.json()) as GoodsReceipt
      setReceipt(confirmedReceipt)
      setSuccess(`Przyjęcie zatwierdzone. Utworzono paletę ${formatReceiptPalletCode(confirmedReceipt.id)}.`)
      setRefresh(current => current + 1)
      onChanged()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Błąd zatwierdzenia przyjęcia.')
    } finally {
      setWorking(false)
    }
  }

  return (
    <section className="panel receipt-details">
      <div className="panel-heading">
        <div>
          <h2>Szczegóły przyjęcia</h2>
          <p>Pozycje dokumentu i zatwierdzanie przyjęcia.</p>
        </div>
        <button type="button" className="button-secondary" onClick={onClose}>Zamknij</button>
      </div>

      {detailsError && <div className="error-message" role="alert">{detailsError}</div>}
      {actionError && <div className="error-message" role="alert">{actionError}</div>}
      {success && <div className="receipt-success" role="status">{success}</div>}
      {detailsLoading && <p className="receipt-muted">Pobieranie szczegółów...</p>}

      {receipt && (
        <>
          <div className="receipt-summary-grid">
            <div className="receipt-summary-card"><span>Numer dokumentu</span><strong>{receipt.number}</strong></div>
            <div className="receipt-summary-card"><span>Status</span><strong>{receiptStatus(receipt.status)}</strong></div>
            <div className="receipt-summary-card"><span>Magazyn</span><strong>{receipt.warehouseCode}</strong></div>
            <div className="receipt-summary-card"><span>Dostawca</span><strong>{receipt.supplierName || '—'}</strong></div>
            <div className="receipt-summary-card"><span>Utworzono</span><strong>{formatDate(receipt.createdAtUtc)}</strong></div>
            <div className="receipt-summary-card"><span>Data przyjęcia</span><strong>{formatDate(receipt.receivedAtUtc)}</strong></div>
          </div>
          <div className="receipt-items-header">
            <h3>Pozycje dokumentu</h3>
            <p>Liczba pozycji: {items.length}</p>
          </div>
          {items.length === 0 ? (
            <div className="receipt-empty-items">Dokument nie zawiera jeszcze żadnych produktów.</div>
          ) : (
            <div className="table-scroll">
              <table className="data-table">
                <thead><tr><th>Kod produktu</th><th>Nazwa</th><th>Partia</th><th>Ilość</th></tr></thead>
                <tbody>{items.map(item => (
                  <tr key={item.id}>
                    <td><strong>{item.productCode}</strong></td>
                    <td>{item.productName}</td>
                    <td>{item.batchNumber || '—'}</td>
                    <td>{item.quantity.toLocaleString('pl-PL', { maximumFractionDigits: 3 })}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}

          {canAdd && (
            <form className="receipt-editor receipt-item-form"
              onSubmit={event => void handleAddItem(event)}>
              <h3>Dodaj produkt do przyjęcia</h3>
              {productsError && <div className="error-message" role="alert">{productsError}</div>}
              {batchesError && <div className="error-message" role="alert">{batchesError}</div>}
              <div className="receipt-fields-grid">
                <div className="receipt-field receipt-field-full">
                  <label htmlFor="receipt-product">Produkt *</label>
                  <select id="receipt-product" value={productId} required
                    disabled={working || productsLoading}
                    onChange={event => setProductId(Number(event.target.value))}>
                    <option value={0}>{productsLoading ? 'Pobieranie produktów...' : 'Wybierz produkt'}</option>
                    {products.map(product => (
                      <option key={product.id} value={product.id}>
                        {product.code} — {product.name} ({product.unitOfMeasureSymbol})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="receipt-field">
                  <label htmlFor="receipt-batch">Partia (opcjonalnie)</label>
                  <select id="receipt-batch" value={batchId}
                    disabled={working || batchesLoading || productId === 0}
                    onChange={event => setBatchId(Number(event.target.value))}>
                    <option value={0}>{batchesLoading ? 'Pobieranie partii...' : 'Bez partii'}</option>
                    {batches.map(batch => (
                      <option key={batch.id} value={batch.id}>{batch.batchNumber}</option>
                    ))}
                  </select>
                </div>
                <div className="receipt-field">
                  <label htmlFor="receipt-quantity">Ilość *</label>
                  <input id="receipt-quantity" type="text" inputMode="decimal"
                    value={quantity} onChange={event => setQuantity(event.target.value)}
                    placeholder="np. 10 lub 1,250" required disabled={working} />
                </div>
              </div>
              <p className="receipt-hint">Możesz podać maksymalnie trzy miejsca po przecinku. Ta sama kombinacja produktu i partii może wystąpić tylko raz.</p>
              <div className="receipt-actions">
                <button className="button-primary" type="submit" disabled={working ||
                  productsLoading || batchesLoading || !!productsError || !!batchesError || productId === 0}>
                  {working ? 'Zapisywanie...' : '+ Dodaj pozycję'}
                </button>
              </div>
            </form>
          )}

          {receipt.status === 'Draft' && (
            <div className="receipt-confirm-section">
              <div>
                <h3>Zatwierdzenie przyjęcia</h3>
                <p>Zatwierdzenie tworzy paletę LE ze wszystkimi pozycjami. Nie można później dopisywać produktów do tego dokumentu.</p>
              </div>
              {canOperateLogistics && (
                <button type="button" className="button-primary"
                  onClick={() => void handleConfirm()}
                  disabled={working || detailsLoading || items.length === 0}>
                  {working ? 'Zatwierdzanie...' : 'Zatwierdź przyjęcie'}
                </button>
              )}
            </div>
          )}
          {receipt.status === 'Received' && (
            <div className="receipt-completed">
              <strong>Przyjęcie zatwierdzone.</strong>
              <p>Powiązana paleta: <strong>{formatReceiptPalletCode(receipt.id)}</strong> (ID: {receipt.logisticUnitId ?? '—'}). Znajdziesz ją w module „Palety LE”. Powstała ze statusem „Utworzona” i można ją odłożyć na lokalizację.</p>
            </div>
          )}
        </>
      )}
    </section>
  )
}
