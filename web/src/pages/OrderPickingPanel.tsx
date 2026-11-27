
import { useEffect, useRef, useState, type FormEvent } from 'react'
import {
  fetchAllPages,
  formatDate,
  readApiError,
  SessionExpiredError,
} from './goodsReceiptsApi'

type OrderLine = {
  id: number
  productId: number
  productCode: string
  productName: string
  productBatchId: number | null
  batchNumber: string | null
  quantityRequested: number
}

type Pallet = {
  id: number
  code: string
  status: string
  storageLocationCode: string | null
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

type Pick = {
  id: number
  requestId: string
  warehouseOrderId: number
  warehouseOrderItemId: number
  logisticUnitId: number
  logisticUnitItemId: number
  logisticUnitCode: string
  productCode: string
  batchNumber: string | null
  sourceLocationCode: string
  quantity: number
  pickedAtUtc: string
}

type Props = {
  orderId: number
  status: string
  items: OrderLine[]
  token: string
  canPick: boolean
  onUnauthorized: () => void
  onChanged: () => void
}

function formatQty(value: number) {
  return value.toLocaleString('pl-PL', {
    maximumFractionDigits: 3,
  })
}

export default function OrderPickingPanel({
  orderId,
  status,
  items,
  token,
  canPick,
  onUnauthorized,
  onChanged,
}: Props) {
  const [picks, setPicks] = useState<Pick[]>([])
  const [historyLoading, setHistoryLoading] = useState(true)
  const [historyReady, setHistoryReady] = useState(false)
  const [reload, setReload] = useState(0)

  const [lineId, setLineId] = useState(0)
  const [palletCode, setPalletCode] = useState('')
  const [pallet, setPallet] = useState<Pallet | null>(null)
  const [palletItems, setPalletItems] = useState<PalletItem[]>([])
  const [palletItemId, setPalletItemId] = useState(0)
  const [quantity, setQuantity] = useState('1')

  const [searching, setSearching] = useState(false)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Przy niepewnym wyniku żądania sieciowego
  // ponawiamy je z tym samym RequestId.
  const pendingRequest = useRef<{
    key: string
    requestId: string
  } | null>(null)

  // Historia pobrań jest źródłem danych o postępie.
  useEffect(() => {
    const controller = new AbortController()

    async function loadHistory() {
      setHistoryLoading(true)
      setHistoryReady(false)
      setError(null)

      try {
        const result = await fetchAllPages<Pick>(
          `/api/warehouse-orders/${orderId}/picks`,
          token,
          controller.signal,
        )

        if (controller.signal.aborted) return

        setPicks(result)
        setHistoryReady(true)
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setError(err instanceof Error
          ? err.message
          : 'Nie udało się pobrać historii kompletacji.')
      } finally {
        if (!controller.signal.aborted) {
          setHistoryLoading(false)
        }
      }
    }

    void loadHistory()
    return () => controller.abort()
  }, [orderId, token, reload, onUnauthorized])

  function pickedQuantity(itemId: number) {
    return picks
      .filter(pick => pick.warehouseOrderItemId === itemId)
      .reduce((sum, pick) => sum + pick.quantity, 0)
  }

  function remainingQuantity(line: OrderLine) {
    return Math.max(
      0,
      line.quantityRequested - pickedQuantity(line.id),
    )
  }

  const selectedLine = items.find(item => item.id === lineId)
  const remaining = selectedLine
    ? remainingQuantity(selectedLine)
    : 0

  const matchingPalletItems = selectedLine
    ? palletItems.filter(item =>
        item.productId === selectedLine.productId &&
        (
          selectedLine.productBatchId === null ||
          selectedLine.productBatchId === item.productBatchId
        ) &&
        item.quantity > 0
      )
    : []

  const selectedPalletItem = matchingPalletItems.find(
    item => item.id === palletItemId,
  )

  const sortedHistory = [...picks].sort((a, b) =>
    b.pickedAtUtc.localeCompare(a.pickedAtUtc),
  )

  async function searchPallet(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!canPick || status !== 'Released' || searching || working) {
      return
    }

    const code = palletCode.trim().toUpperCase()

    setError(null)
    setSuccess(null)
    setPallet(null)
    setPalletItems([])
    setPalletItemId(0)

    if (!code || code.length > 50) {
      setError('Wprowadź pełny numer palety LE.')
      return
    }

    setSearching(true)

    try {
      const results = await fetchAllPages<Pallet>(
        `/api/logistic-units?Search=${encodeURIComponent(code)}`,
        token,
        new AbortController().signal,
      )

      const found = results.find(
        item => item.code.toUpperCase() === code,
      )

      if (!found) {
        throw new Error('Nie znaleziono palety o podanym numerze LE.')
      }

      if (found.status !== 'Stored' || !found.storageLocationCode) {
        throw new Error(
          'Ta paleta nie jest odłożona na lokalizacji. ' +
          'Wymagany status: Stored.',
        )
      }

      const response = await fetch(
        `/api/logistic-units/${found.id}/items`,
        {
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
          response,
          'Nie udało się pobrać zawartości palety',
        ))
      }

      const positions = await response.json() as PalletItem[]

      setPallet(found)
      setPalletItems(positions)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        onUnauthorized()
        return
      }

      setError(err instanceof Error
        ? err.message
        : 'Błąd wyszukiwania palety.')
    } finally {
      setSearching(false)
    }
  }

  async function confirmPick(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!canPick || status !== 'Released' ||
        working || searching || !historyReady ||
        !selectedLine || !selectedPalletItem || !pallet) {
      return
    }

    const raw = quantity.trim().replace(',', '.')
    const amount = Number(raw)

    if (!/^\d{1,15}(\.\d{1,3})?$/.test(raw) ||
        !Number.isFinite(amount) || amount <= 0) {
      setError(
        'Podaj dodatnią ilość z maksymalnie trzema miejscami po przecinku.',
      )
      return
    }

    if (amount > remaining) {
      setError(
        `Do pobrania pozostało tylko ${formatQty(remaining)} szt.`,
      )
      return
    }

    if (amount > selectedPalletItem.quantity) {
      setError(
        `Na wybranej pozycji palety dostępne jest ` +
        `${formatQty(selectedPalletItem.quantity)} szt.`,
      )
      return
    }

    const confirmed = window.confirm(
      `Potwierdzić pobranie?\n\n` +
      `Artykuł: ${selectedLine.productCode}\n` +
      `Paleta: ${pallet.code}\n` +
      `Partia: ${selectedPalletItem.batchNumber ?? 'Bez partii'}\n` +
      `Ilość: ${formatQty(amount)}`,
    )

    if (!confirmed) return

    const key = [
      orderId,
      selectedLine.id,
      selectedPalletItem.id,
      amount.toFixed(3),
    ].join(':')

    const requestId = pendingRequest.current?.key === key
      ? pendingRequest.current.requestId
      : crypto.randomUUID()

    pendingRequest.current = { key, requestId }

    setWorking(true)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch(
        `/api/warehouse-orders/${orderId}/picks`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            requestId,
            warehouseOrderItemId: selectedLine.id,
            logisticUnitItemId: selectedPalletItem.id,
            quantity: amount,
          }),
        },
      )

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        const message = await readApiError(
          response,
          'Nie udało się potwierdzić pobrania',
        )

        // Odpowiedź 4xx oznacza odrzucenie operacji.
        // Przy błędzie serwera lub sieci zachowujemy RequestId.
        if (response.status < 500) {
          pendingRequest.current = null
        }

        throw new Error(message)
      }

      const saved = await response.json() as Pick

      pendingRequest.current = null
      setSuccess(
        `Zapisano pobranie ${formatQty(saved.quantity)} szt. ` +
        `z palety ${saved.logisticUnitCode}.`,
      )

      setQuantity('1')
      setPallet(null)
      setPalletItems([])
      setPalletCode('')
      setPalletItemId(0)

      if (amount >= remaining) {
        setLineId(0)
      }

      // Odczytujemy aktualną historię i status zamówienia.
      setReload(value => value + 1)
      onChanged()
    } catch (err) {
      setError(err instanceof Error
        ? err.message
        : 'Błąd podczas potwierdzania kompletacji.')
    } finally {
      setWorking(false)
    }
  }

  return (
    <section className="receipt-editor">
      <div className="panel-heading">
        <div>
          <h3>Kompletacja zamówienia</h3>
          <p>Wybierz pozycję zamówienia oraz paletę źródłową.</p>
        </div>

        <button
          type="button"
          className="button-secondary"
          disabled={historyLoading || working}
          onClick={() => setReload(value => value + 1)}
        >
          Odśwież postęp
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

      <div className="receipt-items-header">
        <h3>Postęp pozycji</h3>
      </div>

      {historyLoading && (
        <p className="receipt-muted">Pobieranie historii...</p>
      )}

      {historyReady && (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Artykuł</th>
                <th>Partia</th>
                <th>Zamówiono</th>
                <th>Pobrano</th>
                <th>Pozostało</th>
              </tr>
            </thead>
            <tbody>
              {items.map(line => (
                <tr key={line.id}>
                  <td><strong>{line.productCode}</strong></td>
                  <td>{line.batchNumber ?? 'Dowolna'}</td>
                  <td>{formatQty(line.quantityRequested)}</td>
                  <td>{formatQty(pickedQuantity(line.id))}</td>
                  <td>
                    <strong>
                      {formatQty(remainingQuantity(line))}
                    </strong>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {status === 'Released' && canPick && historyReady && (
        <>
          <div className="receipt-items-header">
            <h3>Potwierdź pobranie</h3>
          </div>

          <div className="receipt-field">
            <label htmlFor="picking-order-line">
              Pozycja zamówienia *
            </label>
            <select
              id="picking-order-line"
              value={lineId}
              disabled={working || searching}
              onChange={event => {
                setLineId(Number(event.target.value))
                setPalletItemId(0)
                setError(null)
              }}
            >
              <option value={0}>Wybierz artykuł</option>
              {items.map(line => (
                <option
                  key={line.id}
                  value={line.id}
                  disabled={remainingQuantity(line) <= 0}
                >
                  {line.productCode} — {line.productName}
                  {' '}({line.batchNumber ?? 'dowolna partia'})
                  {' '}— pozostało {formatQty(remainingQuantity(line))}
                </option>
              ))}
            </select>
          </div>

          <form
            className="receipt-filters"
            onSubmit={event => void searchPallet(event)}
          >
            <div className="receipt-field receipt-filter-search">
              <label htmlFor="picking-pallet-code">
                Numer palety LE *
              </label>
              <input
                id="picking-pallet-code"
                value={palletCode}
                maxLength={50}
                placeholder="Zeskanuj lub wpisz pełny numer LE"
                disabled={working || searching}
                onChange={event => {
                  setPalletCode(event.target.value)
                  setPallet(null)
                  setPalletItems([])
                  setPalletItemId(0)
                }}
              />
            </div>

            <button
              type="submit"
              className="button-secondary"
              disabled={searching || working || lineId === 0}
            >
              {searching ? 'Wyszukiwanie...' : 'Znajdź paletę'}
            </button>
          </form>

          {pallet && (
            <>
              <div className="receipt-summary-grid">
                <div className="receipt-summary-card">
                  <span>Paleta LE</span>
                  <strong>{pallet.code}</strong>
                </div>
                <div className="receipt-summary-card">
                  <span>Status</span>
                  <strong>Odłożona</strong>
                </div>
                <div className="receipt-summary-card">
                  <span>Lokalizacja</span>
                  <strong>{pallet.storageLocationCode ?? '—'}</strong>
                </div>
              </div>

              {matchingPalletItems.length === 0 ? (
                <div className="receipt-empty-items">
                  Na tej palecie nie znaleziono dostępnego
                  artykułu i partii zgodnych z pozycją zamówienia.
                </div>
              ) : (
                <form onSubmit={event => void confirmPick(event)}>
                  <div className="receipt-fields-grid">
                    <div className="receipt-field">
                      <label htmlFor="picking-pallet-item">
                        Pozycja na palecie *
                      </label>
                      <select
                        id="picking-pallet-item"
                        value={palletItemId}
                        disabled={working}
                        onChange={event =>
                          setPalletItemId(Number(event.target.value))}
                      >
                        <option value={0}>Wybierz pozycję</option>
                        {matchingPalletItems.map(item => (
                          <option key={item.id} value={item.id}>
                            {item.productCode}
                            {' '}— {item.batchNumber ?? 'Bez partii'}
                            {' '}— dostępne {formatQty(item.quantity)}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="receipt-field">
                      <label htmlFor="picking-quantity">
                        Ilość pobrana *
                      </label>
                      <input
                        id="picking-quantity"
                        value={quantity}
                        inputMode="decimal"
                        required
                        disabled={working}
                        placeholder="np. 5 lub 1,250"
                        onChange={event =>
                          setQuantity(event.target.value)}
                      />
                    </div>
                  </div>

                  <p className="receipt-hint">
                    Pozostało do pobrania: {formatQty(remaining)}.
                    System dodatkowo sprawdzi magazyn,
                    partię i aktualny stan palety.
                  </p>

                  <div className="receipt-actions">
                    <button
                      type="submit"
                      className="button-primary"
                      disabled={
                        working ||
                        !selectedPalletItem ||
                        remaining <= 0
                      }
                    >
                      {working
                        ? 'Zapisywanie pobrania...'
                        : 'Potwierdź pobranie'}
                    </button>
                  </div>
                </form>
              )}
            </>
          )}
        </>
      )}

      {status === 'Released' && !canPick && (
        <p className="receipt-hint">
          Potwierdzanie pobrań wymaga roli Administrator
          lub WarehouseWorker.
        </p>
      )}

      {status === 'Completed' && (
        <div className="receipt-completed">
          <strong>Zamówienie skompletowane.</strong>
          <p>Wszystkie wymagane pobrania zostały zapisane.</p>
        </div>
      )}

      <div className="receipt-items-header">
        <h3>Historia kompletacji</h3>
        <p>Liczba zapisanych pobrań: {picks.length}</p>
      </div>

      {historyReady && picks.length === 0 && (
        <div className="receipt-empty-items">
          Brak zapisanych operacji kompletacji.
        </div>
      )}

      {historyReady && picks.length > 0 && (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Data</th>
                <th>Paleta LE</th>
                <th>Lokalizacja</th>
                <th>Artykuł</th>
                <th>Partia</th>
                <th>Ilość</th>
              </tr>
            </thead>
            <tbody>
              {sortedHistory.map(pick => (
                <tr key={pick.id}>
                  <td>{formatDate(pick.pickedAtUtc)}</td>
                  <td><strong>{pick.logisticUnitCode}</strong></td>
                  <td>{pick.sourceLocationCode}</td>
                  <td>{pick.productCode}</td>
                  <td>{pick.batchNumber ?? '—'}</td>
                  <td>{formatQty(pick.quantity)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
