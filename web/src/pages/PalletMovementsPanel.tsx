import { useEffect, useState, type FormEvent } from 'react'
import './PalletMovementsPanel.css'

type PalletSummary = {
  id: number
  code: string
  status: string
  storageLocationId: number | null
  storageLocationCode: string | null
}

type StorageLocation = {
  id: number
  warehouseId: number
  warehouseCode: string
  warehouseZoneId: number
  warehouseZoneCode: string
  code: string
  description: string | null
  isActive: boolean
}

type PalletMovement = {
  id: number
  logisticUnitId: number
  logisticUnitCode: string
  fromStorageLocationId: number
  fromLocationCode: string
  toStorageLocationId: number
  toLocationCode: string
  movedAtUtc: string
  note: string | null
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
  pallet: PalletSummary
  canMove: boolean
  onUnauthorized: () => void
  onMoved: () => void
}

const PAGE_SIZE = 10
const LOOKUP_SIZE = 100

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
    return 'Nie masz uprawnień do tej operacji.'
  }
  if (response.status === 404) {
    return 'Nie znaleziono wskazanego elementu.'
  }
  const problem = (await response.json().catch(() => null)) as
    | ApiProblem
    | null
  return problem?.detail || `${fallback} (HTTP ${response.status}).`
}

export default function PalletMovementsPanel({
  token,
  pallet,
  canMove,
  onUnauthorized,
  onMoved,
}: Props) {
  const [warehouseId, setWarehouseId] = useState<number | null>(null)
  const [warehouseLoading, setWarehouseLoading] = useState(false)
  const [locationInput, setLocationInput] = useState('')
  const [locationSearch, setLocationSearch] = useState('')
  const [locations, setLocations] = useState<StorageLocation[]>([])
  const [locationsTotal, setLocationsTotal] = useState(0)
  const [locationsLoading, setLocationsLoading] = useState(false)
  const [locationError, setLocationError] = useState<string | null>(null)
  const [targetId, setTargetId] = useState(0)
  const [note, setNote] = useState('')
  const [moving, setMoving] = useState(false)
  const [moveError, setMoveError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [history, setHistory] = useState<PagedResult<PalletMovement> | null>(null)
  const [historyPage, setHistoryPage] = useState(1)
  const [historyReload, setHistoryReload] = useState(0)
  const [historyLoading, setHistoryLoading] = useState(true)
  const [historyError, setHistoryError] = useState<string | null>(null)

  const canMoveNow =
    canMove &&
    pallet.status === 'Stored' &&
    pallet.storageLocationId !== null

  // Ustalamy magazyn aktualnej lokalizacji, aby nie proponować
  // przenoszenia palety do innego magazynu.
  useEffect(() => {
    const controller = new AbortController()

    async function loadSourceLocation() {
      setWarehouseId(null)
      setLocationError(null)
      setTargetId(0)

      if (!canMoveNow || pallet.storageLocationId === null) {
        setWarehouseLoading(false)
        return
      }

      setWarehouseLoading(true)
      try {
        const response = await fetch(
          `/api/storage-locations/${pallet.storageLocationId}`,
          {
            headers: { Authorization: `Bearer ${token}` },
            signal: controller.signal,
          }
        )
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(
            await apiError(response, 'Nie udało się pobrać lokalizacji palety')
          )
        }

        const source = (await response.json()) as StorageLocation
        if (!controller.signal.aborted) {
          setWarehouseId(source.warehouseId)
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          setLocationError(
            err instanceof Error ? err.message : 'Błąd lokalizacji źródłowej.'
          )
        }
      } finally {
        if (!controller.signal.aborted) setWarehouseLoading(false)
      }
    }

    void loadSourceLocation()
    return () => controller.abort()
  }, [
    token,
    canMoveNow,
    pallet.storageLocationId,
    onUnauthorized,
  ])

  // Pobieramy tylko aktywne miejsca z tego samego magazynu.
  // Przy większej liczbie lokalizacji dostępne jest wyszukiwanie w API.
  useEffect(() => {
    const controller = new AbortController()

    async function loadLocations() {
      setLocations([])
      setLocationsTotal(0)
      setTargetId(0)
      setLocationError(null)

      if (!canMoveNow || warehouseId === null) {
        setLocationsLoading(false)
        return
      }

      setLocationsLoading(true)
      const params = new URLSearchParams({
        WarehouseId: String(warehouseId),
        IsActive: 'true',
        Page: '1',
        PageSize: String(LOOKUP_SIZE),
      })
      if (locationSearch) params.set('Search', locationSearch)

      try {
        const response = await fetch(
          `/api/storage-locations?${params.toString()}`,
          {
            headers: { Authorization: `Bearer ${token}` },
            signal: controller.signal,
          }
        )
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(
            await apiError(response, 'Nie udało się pobrać lokalizacji')
          )
        }

        const result = (await response.json()) as PagedResult<StorageLocation>
        if (controller.signal.aborted) return
        setLocations(result.items.filter(location =>
          location.id !== pallet.storageLocationId
        ))
        setLocationsTotal(result.totalCount)
      } catch (err) {
        if (!controller.signal.aborted) {
          setLocationError(
            err instanceof Error ? err.message : 'Błąd listy lokalizacji.'
          )
        }
      } finally {
        if (!controller.signal.aborted) setLocationsLoading(false)
      }
    }

    void loadLocations()
    return () => controller.abort()
  }, [
    token,
    canMoveNow,
    warehouseId,
    locationSearch,
    pallet.storageLocationId,
    onUnauthorized,
  ])

  // Historia ruchów dostępna do odczytu niezależnie od statusu palety.
  useEffect(() => {
    const controller = new AbortController()

    async function loadHistory() {
      setHistoryLoading(true)
      setHistoryError(null)
      const params = new URLSearchParams({
        Page: String(historyPage),
        PageSize: String(PAGE_SIZE),
      })

      try {
        const response = await fetch(
          `/api/logistic-units/${pallet.id}/movements?${params.toString()}`,
          {
            headers: { Authorization: `Bearer ${token}` },
            signal: controller.signal,
          }
        )
        if (controller.signal.aborted) return
        if (response.status === 401) {
          onUnauthorized()
          return
        }
        if (!response.ok) {
          throw new Error(
            await apiError(response, 'Nie udało się pobrać historii ruchów')
          )
        }
        const result = (await response.json()) as PagedResult<PalletMovement>
        if (controller.signal.aborted) return
        setHistory(result)
        const lastPage = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE))
        if (historyPage > lastPage) setHistoryPage(lastPage)
      } catch (err) {
        if (!controller.signal.aborted) {
          setHistoryError(
            err instanceof Error ? err.message : 'Błąd historii palety.'
          )
        }
      } finally {
        if (!controller.signal.aborted) setHistoryLoading(false)
      }
    }

    void loadHistory()
    return () => controller.abort()
  }, [token, pallet.id, historyPage, historyReload, onUnauthorized])

  async function movePallet(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canMoveNow || moving) return

    const destination = locations.find(location => location.id === targetId)
    if (!destination) {
      setMoveError('Wybierz nową lokalizację docelową.')
      return
    }
    if (note.length > 250) {
      setMoveError('Notatka może mieć maksymalnie 250 znaków.')
      return
    }

    const destinationLabel =
      `${destination.warehouseCode} / ${destination.warehouseZoneCode} / ${destination.code}`

    if (!window.confirm(
      `Przenieść paletę ${pallet.code} z ${pallet.storageLocationCode ?? '—'} do ${destinationLabel}?`
    )) return

    setMoving(true)
    setMoveError(null)
    setSuccess(null)
    try {
      const response = await fetch(`/api/logistic-units/${pallet.id}/move`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          toStorageLocationId: destination.id,
          note: note.trim() || null,
        }),
      })
      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (!response.ok) {
        throw new Error(
          await apiError(response, 'Nie udało się przesunąć palety')
        )
      }

      setSuccess(`Paleta ${pallet.code} została przeniesiona do ${destination.code}.`)
      setNote('')
      setTargetId(0)
      setHistoryPage(1)
      setHistoryReload(value => value + 1)
      onMoved()
    } catch (err) {
      setMoveError(
        err instanceof Error ? err.message : 'Wystąpił błąd przesunięcia.'
      )
    } finally {
      setMoving(false)
    }
  }

  const totalHistory = history?.totalCount ?? 0
  const historyPages = Math.max(1, Math.ceil(totalHistory / PAGE_SIZE))

  return (
    <section className="pallet-movements">
      <div className="pallet-movements-heading">
        <div>
          <h3>Przesunięcia i historia palety</h3>
          <p>Ruchy pomiędzy lokalizacjami są zapisywane w SQL Server.</p>
        </div>
        <button
          type="button"
          className="button-secondary"
          onClick={() => setHistoryReload(value => value + 1)}
          disabled={historyLoading}
        >
          Odśwież historię
        </button>
      </div>

      {canMoveNow && (
        <form className="pallet-move-form" onSubmit={event => void movePallet(event)}>
          <h4>Przenieś paletę na inną lokalizację</h4>
          <p className="pallet-move-hint">
            Można przesuwać wyłącznie palety o statusie Stored.
            Lokalizacja docelowa musi być aktywna i należeć do tego samego magazynu.
          </p>
          <div className="pallet-move-current">
            Aktualna lokalizacja: <strong>{pallet.storageLocationCode ?? '—'}</strong>
          </div>

          <div className="pallet-move-search">
            <label htmlFor="move-location-search">Szukaj lokalizacji docelowej</label>
            <div className="pallet-move-search-row">
              <input
                id="move-location-search"
                value={locationInput}
                onChange={event => setLocationInput(event.target.value)}
                placeholder="Kod lub opis, np. A-01-02"
                maxLength={100}
                disabled={moving || warehouseLoading}
              />
              <button
                type="button"
                className="button-secondary"
                onClick={() => setLocationSearch(locationInput.trim())}
                disabled={moving || warehouseLoading}
              >
                Szukaj
              </button>
            </div>
          </div>

          {locationError && (
            <div className="error-message" role="alert">{locationError}</div>
          )}

          <div className="pallet-move-fields">
            <div className="pallet-move-field">
              <label htmlFor="move-location">Nowa lokalizacja *</label>
              <select
                id="move-location"
                value={targetId}
                onChange={event => setTargetId(Number(event.target.value))}
                disabled={moving || warehouseLoading || locationsLoading || warehouseId === null}
                required
              >
                <option value={0}>
                  {warehouseLoading || locationsLoading
                    ? 'Pobieranie lokalizacji...'
                    : 'Wybierz lokalizację'}
                </option>
                {locations.map(location => (
                  <option key={location.id} value={location.id}>
                    {location.warehouseCode} / {location.warehouseZoneCode} / {location.code}
                    {location.description ? ` — ${location.description}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="pallet-move-field">
              <label htmlFor="move-note">Notatka (opcjonalnie)</label>
              <input
                id="move-note"
                value={note}
                onChange={event => setNote(event.target.value)}
                placeholder="np. Przesunięcie do innego regału"
                maxLength={250}
                disabled={moving}
              />
            </div>
          </div>

          {locationsTotal > LOOKUP_SIZE && (
            <p className="pallet-move-hint">
              Wyświetlamy maksymalnie 100 lokalizacji z {locationsTotal}.
              Użyj wyszukiwania, aby znaleźć pozostałe.
            </p>
          )}

          {moveError && <div className="error-message" role="alert">{moveError}</div>}
          {success && <div className="pallet-success" role="status">{success}</div>}

          <button
            type="submit"
            className="button-primary"
            disabled={moving || targetId === 0 || locationsLoading || warehouseLoading}
          >
            {moving ? 'Przesuwanie...' : 'Przenieś paletę'}
          </button>
        </form>
      )}

      {!canMoveNow && (
        <p className="pallet-move-hint">
          {pallet.status === 'Stored' && !canMove
            ? 'Przesuwanie palet wymaga roli Administrator lub WarehouseWorker.'
            : 'Przesuwanie jest dostępne dla palet w statusie Stored z przypisaną lokalizacją.'}
        </p>
      )}

      <div className="pallet-movement-history">
        <h4>Historia przesunięć</h4>
        <div className="results-info" aria-live="polite">
          {historyLoading
            ? 'Pobieranie historii...'
            : historyError
              ? 'Nie udało się pobrać historii.'
              : `Zarejestrowane przesunięcia: ${totalHistory}`}
        </div>
        {historyError && <div className="error-message" role="alert">{historyError}</div>}

        {!historyLoading && !historyError && totalHistory === 0 && (
          <div className="pallet-empty-items">
            Brak zarejestrowanych przesunięć. Pierwsze odłożenie palety
            nie jest przesunięciem między lokalizacjami.
          </div>
        )}

        {!historyLoading && !historyError && history && history.items.length > 0 && (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Z lokalizacji</th>
                  <th>Do lokalizacji</th>
                  <th>Notatka</th>
                </tr>
              </thead>
              <tbody>
                {history.items.map(movement => (
                  <tr key={movement.id}>
                    <td>{formatDate(movement.movedAtUtc)}</td>
                    <td>{movement.fromLocationCode}</td>
                    <td><strong>{movement.toLocationCode}</strong></td>
                    <td>{movement.note || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!historyLoading && !historyError && totalHistory > 0 && (
          <div className="pagination">
            <span>Strona {historyPage} z {historyPages}</span>
            <div className="pagination-actions">
              <button
                type="button"
                className="button-secondary"
                onClick={() => setHistoryPage(value => Math.max(1, value - 1))}
                disabled={historyPage <= 1}
              >
                Poprzednia
              </button>
              <button
                type="button"
                className="button-secondary"
                onClick={() => setHistoryPage(value => Math.min(historyPages, value + 1))}
                disabled={historyPage >= historyPages}
              >
                Następna
              </button>
            </div>
          </div>
        )}
      </div>
    </section>
  )
}
