
import { useEffect, useState, type FormEvent } from 'react'
import './StorageLocationsPage.css'

type Warehouse = {
  id: number
  code: string
  name: string
  isActive: boolean
}

type WarehouseZone = {
  id: number
  warehouseId: number
  warehouseCode: string
  code: string
  name: string
  isActive: boolean
}

type StorageLocation = {
  id: number
  warehouseZoneId: number
  warehouseZoneCode: string
  warehouseId: number
  warehouseCode: string
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

type ApiProblem = {
  detail?: string
  title?: string
}

type Props = {
  token: string
  canEditLocations: boolean
  onUnauthorized: () => void
}

type StatusFilter = 'all' | 'active' | 'inactive'
type FormMode = 'create' | 'edit' | null

const PAGE_SIZE = 10
const LOOKUP_PAGE_SIZE = 100

class SessionExpiredError extends Error {}

async function fetchAllPages<T>(
  path: string,
  token: string,
  signal: AbortSignal
): Promise<T[]> {
  const items: T[] = []
  let page = 1
  let totalCount: number

  do {
    const parameters = new URLSearchParams({
      Page: String(page),
      PageSize: String(LOOKUP_PAGE_SIZE),
    })

    const separator = path.includes('?') ? '&' : '?'

    const response = await fetch(
      `${path}${separator}${parameters.toString()}`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
        signal,
      }
    )

    if (response.status === 401) {
      throw new SessionExpiredError()
    }

    if (!response.ok) {
      throw new Error(
        `Nie udało się pobrać danych (HTTP ${response.status}).`
      )
    }

    const result =
      (await response.json()) as PagedResult<T>

    items.push(...result.items)
    totalCount = result.totalCount

    if (result.items.length === 0) {
      break
    }

    page += 1
  } while (items.length < totalCount)

  return items
}

async function getApiError(
  response: Response,
  fallback: string
): Promise<string> {
  if (response.status === 403) {
    return 'Nie masz uprawnień do wykonania tej operacji.'
  }

  if (response.status === 404) {
    return 'Nie znaleziono wskazanej lokalizacji.'
  }

  const problem = (
    await response.json().catch(() => null)
  ) as ApiProblem | null

  return (
    problem?.detail ||
    `${fallback} (HTTP ${response.status}).`
  )
}

export default function StorageLocationsPage({
  token,
  canEditLocations,
  onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [zones, setZones] = useState<WarehouseZone[]>([])

  const [warehouseId, setWarehouseId] = useState(0)
  const [zoneId, setZoneId] = useState(0)

  const [warehousesLoading, setWarehousesLoading] =
    useState(true)
  const [zonesLoading, setZonesLoading] = useState(false)

  const [lookupError, setLookupError] =
    useState<string | null>(null)

  const [data, setData] =
    useState<PagedResult<StorageLocation> | null>(null)

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<StatusFilter>('all')
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [editedLocation, setEditedLocation] =
    useState<StorageLocation | null>(null)

  const [formCode, setFormCode] = useState('')
  const [formDescription, setFormDescription] = useState('')

  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState<number | null>(null)

  // 1. Lista magazynów
  useEffect(() => {
    const controller = new AbortController()

    async function loadWarehouses() {
      setWarehousesLoading(true)
      setLookupError(null)

      try {
        const result = await fetchAllPages<Warehouse>(
          '/api/warehouses',
          token,
          controller.signal
        )

        if (controller.signal.aborted) return

        setWarehouses(result)

        setWarehouseId(current =>
          result.some(item => item.id === current)
            ? current
            : (
                result.find(item => item.isActive)?.id ??
                result[0]?.id ??
                0
              )
        )
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setLookupError(
          err instanceof Error
            ? err.message
            : 'Nie udało się pobrać magazynów.'
        )
      } finally {
        if (!controller.signal.aborted) {
          setWarehousesLoading(false)
        }
      }
    }

    void loadWarehouses()

    return () => controller.abort()
  }, [token, onUnauthorized])

  // 2. Lista stref wybranego magazynu
  useEffect(() => {
    const controller = new AbortController()

    async function loadZones() {
      setZones([])
      setZoneId(0)
      setData(null)

      if (warehouseId === 0) {
        setZonesLoading(false)
        return
      }

      setZonesLoading(true)
      setLookupError(null)

      try {
        const result = await fetchAllPages<WarehouseZone>(
          `/api/warehouse-zones?WarehouseId=${warehouseId}`,
          token,
          controller.signal
        )

        if (controller.signal.aborted) return

        setZones(result)

        setZoneId(
          result.find(zone => zone.isActive)?.id ??
          result[0]?.id ??
          0
        )
      } catch (err) {
        if (controller.signal.aborted) return

        if (err instanceof SessionExpiredError) {
          onUnauthorized()
          return
        }

        setLookupError(
          err instanceof Error
            ? err.message
            : 'Nie udało się pobrać stref.'
        )
      } finally {
        if (!controller.signal.aborted) {
          setZonesLoading(false)
        }
      }
    }

    void loadZones()

    return () => controller.abort()
  }, [warehouseId, token, onUnauthorized])

  // 3. Lista lokalizacji wybranej strefy
  useEffect(() => {
    const controller = new AbortController()

    async function loadLocations() {
      setData(null)
      setListError(null)

      if (warehouseId === 0 || zoneId === 0) {
        setLoading(false)
        return
      }

      setLoading(true)

      const parameters = new URLSearchParams({
        WarehouseId: String(warehouseId),
        WarehouseZoneId: String(zoneId),
        Page: String(page),
        PageSize: String(PAGE_SIZE),
      })

      if (search) {
        parameters.set('Search', search)
      }

      if (status !== 'all') {
        parameters.set(
          'IsActive',
          String(status === 'active')
        )
      }

      try {
        const response = await fetch(
          `/api/storage-locations?${parameters.toString()}`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
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
            `Nie udało się pobrać lokalizacji (HTTP ${response.status}).`
          )
        }

        const result =
          (await response.json()) as PagedResult<StorageLocation>

        if (controller.signal.aborted) return

        setData(result)

        const lastPage = Math.max(
          1,
          Math.ceil(result.totalCount / PAGE_SIZE)
        )

        if (page > lastPage) {
          setPage(lastPage)
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          setListError(
            err instanceof Error
              ? err.message
              : 'Wystąpił błąd pobierania lokalizacji.'
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadLocations()

    return () => controller.abort()
  }, [
    token,
    warehouseId,
    zoneId,
    page,
    search,
    status,
    reloadKey,
    onUnauthorized,
  ])

  const selectedWarehouse =
    warehouses.find(item => item.id === warehouseId) ?? null

  const selectedZone =
    zones.find(item => item.id === zoneId) ?? null

  const canCreateHere =
    selectedWarehouse?.isActive === true &&
    selectedZone?.isActive === true

  const totalCount = data?.totalCount ?? 0

  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE)
  )

  const formOpen = formMode !== null

  function refreshLocations() {
    setReloadKey(current => current + 1)
  }

  function resetFilters() {
    setSearchInput('')
    setSearch('')
    setStatus('all')
    setPage(1)
  }

  function closeForm() {
    setFormMode(null)
    setEditedLocation(null)
    setFormCode('')
    setFormDescription('')
  }

  function changeWarehouse(value: string) {
    setWarehouseId(Number(value))
    setZoneId(0)
    setZones([])
    setData(null)
    resetFilters()
    closeForm()
    setError(null)
    setSuccess(null)
  }

  function changeZone(value: string) {
    setZoneId(Number(value))
    setData(null)
    resetFilters()
    closeForm()
    setError(null)
    setSuccess(null)
  }

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
    setSuccess(null)
  }

  function startCreating() {
    if (!canCreateHere) {
      setError(
        'Można dodawać lokalizacje tylko do aktywnej strefy w aktywnym magazynie.'
      )
      return
    }

    setFormMode('create')
    setEditedLocation(null)
    setFormCode('')
    setFormDescription('')
    setError(null)
    setSuccess(null)
  }

  function startEditing(location: StorageLocation) {
    setFormMode('edit')
    setEditedLocation(location)
    setFormCode(location.code)
    setFormDescription(location.description ?? '')
    setError(null)
    setSuccess(null)
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const code = formCode.trim().toUpperCase()
    const description = formDescription.trim() || null

    const editing = formMode === 'edit'
    const current = editedLocation

    if (!code || code.length > 50) {
      setError('Kod lokalizacji musi mieć od 1 do 50 znaków.')
      return
    }

    if (description && description.length > 250) {
      setError('Opis może mieć maksymalnie 250 znaków.')
      return
    }

    if (editing && !current) {
      setError('Nie wybrano lokalizacji do edycji.')
      return
    }

    if (!editing && !canCreateHere) {
      setError('Wybierz aktywny magazyn i aktywną strefę.')
      return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)

    try {
      const endpoint =
        editing && current
          ? `/api/storage-locations/${current.id}`
          : '/api/storage-locations'

      const body =
        editing && current
          ? {
              code,
              description,
              isActive: current.isActive,
            }
          : {
              warehouseZoneId: zoneId,
              code,
              description,
            }

      const response = await fetch(endpoint, {
        method: editing ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(body),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(
          await getApiError(
            response,
            editing
              ? 'Nie udało się edytować lokalizacji'
              : 'Nie udało się dodać lokalizacji'
          )
        )
      }

      closeForm()

      if (editing) {
        setSuccess(
          `Zmiany lokalizacji ${code} zostały zapisane.`
        )
      } else {
        setSuccess(
          `Lokalizacja ${code} została utworzona.`
        )
        resetFilters()
      }

      refreshLocations()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zapisywania lokalizacji.'
      )
    } finally {
      setSaving(false)
    }
  }

  async function handleChangeStatus(
    location: StorageLocation,
    activate: boolean
  ) {
    if (!activate) {
      const confirmed = window.confirm(
        `Czy dezaktywować lokalizację ${location.code}?\n\nLokalizacja pozostanie w bazie danych.`
      )

      if (!confirmed) return
    }

    if (activate && !canCreateHere) {
      setError(
        'Nie można aktywować lokalizacji w nieaktywnej strefie lub magazynie.'
      )
      return
    }

    setBusyId(location.id)
    setError(null)
    setSuccess(null)

    try {
      let response: Response

      if (activate) {
        const detailsResponse = await fetch(
          `/api/storage-locations/${location.id}`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          }
        )

        if (detailsResponse.status === 401) {
          onUnauthorized()
          return
        }

        if (!detailsResponse.ok) {
          throw new Error(
            await getApiError(
              detailsResponse,
              'Nie udało się pobrać lokalizacji'
            )
          )
        }

        const details =
          (await detailsResponse.json()) as StorageLocation

        if (details.isActive) {
          setSuccess(
            `Lokalizacja ${details.code} jest już aktywna.`
          )
          refreshLocations()
          return
        }

        response = await fetch(
          `/api/storage-locations/${location.id}`,
          {
            method: 'PUT',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${token}`,
            },
            body: JSON.stringify({
              code: details.code,
              description: details.description,
              isActive: true,
            }),
          }
        )
      } else {
        response = await fetch(
          `/api/storage-locations/${location.id}`,
          {
            method: 'DELETE',
            headers: {
              Authorization: `Bearer ${token}`,
            },
          }
        )
      }

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (!response.ok) {
        throw new Error(
          await getApiError(
            response,
            activate
              ? 'Nie udało się aktywować lokalizacji'
              : 'Nie udało się dezaktywować lokalizacji'
          )
        )
      }

      setSuccess(
        activate
          ? `Lokalizacja ${location.code} została aktywowana.`
          : `Lokalizacja ${location.code} została dezaktywowana.`
      )

      refreshLocations()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zmiany statusu lokalizacji.'
      )
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>Lokalizacje składowania</h2>
          <p>
            Trzeci poziom struktury magazynowej.
            Dane pobierane z ASP.NET Core i SQL Server.
          </p>
        </div>

        <div className="location-heading-actions">
          {canEditLocations && (
            <button
              type="button"
              className="button-primary"
              onClick={startCreating}
              disabled={
                !canCreateHere ||
                warehousesLoading ||
                zonesLoading ||
                formOpen ||
                busyId !== null
              }
            >
              + Dodaj lokalizację
            </button>
          )}

          <button
            type="button"
            className="button-secondary"
            onClick={refreshLocations}
            disabled={loading || zoneId === 0}
          >
            Odśwież
          </button>
        </div>
      </div>

      <div className="location-selection">
        <div className="location-field">
          <label htmlFor="location-warehouse">
            Magazyn
          </label>

          <select
            id="location-warehouse"
            value={warehouseId}
            onChange={event =>
              changeWarehouse(event.target.value)
            }
            disabled={
              warehousesLoading ||
              formOpen ||
              busyId !== null
            }
          >
            {warehouses.length === 0 && (
              <option value={0}>
                {warehousesLoading
                  ? 'Pobieranie magazynów...'
                  : 'Brak magazynów'}
              </option>
            )}

            {warehouses.map(warehouse => (
              <option
                key={warehouse.id}
                value={warehouse.id}
              >
                {warehouse.code} — {warehouse.name}
                {warehouse.isActive
                  ? ''
                  : ' (nieaktywny)'}
              </option>
            ))}
          </select>
        </div>

        <div className="location-field">
          <label htmlFor="location-zone">
            Strefa magazynowa
          </label>

          <select
            id="location-zone"
            value={zoneId}
            onChange={event =>
              changeZone(event.target.value)
            }
            disabled={
              zonesLoading ||
              warehouseId === 0 ||
              formOpen ||
              busyId !== null
            }
          >
            {zones.length === 0 && (
              <option value={0}>
                {zonesLoading
                  ? 'Pobieranie stref...'
                  : 'Brak stref'}
              </option>
            )}

            {zones.map(zone => (
              <option key={zone.id} value={zone.id}>
                {zone.code} — {zone.name}
                {zone.isActive ? '' : ' (nieaktywna)'}
              </option>
            ))}
          </select>
        </div>

        {selectedWarehouse && selectedZone && (
          <div className="location-selection-summary">
            <span>
              Wybrano:
              <strong>
                {' '}
                {selectedWarehouse.code} / {selectedZone.code}
              </strong>
            </span>

            <span
              className={
                canCreateHere
                  ? 'status-pill active'
                  : 'status-pill inactive'
              }
            >
              {canCreateHere
                ? 'Aktywna struktura'
                : 'Nieaktywna struktura'}
            </span>
          </div>
        )}
      </div>

      {lookupError && (
        <div className="error-message" role="alert">
          {lookupError}
        </div>
      )}

      {listError && (
        <div className="error-message" role="alert">
          {listError}
        </div>
      )}

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      {success && (
        <div className="location-success" role="status">
          {success}
        </div>
      )}

      {selectedZone && !canCreateHere && (
        <div className="location-notice">
          Magazyn lub strefa są nieaktywne.
          Dodawanie i ponowna aktywacja lokalizacji
          wymagają aktywnej struktury.
        </div>
      )}

      {canEditLocations && formOpen && (
        <form
          className="location-form"
          onSubmit={event => void handleSave(event)}
        >
          <h3>
            {formMode === 'create'
              ? 'Dodaj lokalizację'
              : `Edytuj lokalizację ${editedLocation?.code ?? ''}`}
          </h3>

          <div className="location-form-grid">
            <div className="location-field">
              <label htmlFor="location-code">
                Kod lokalizacji *
              </label>

              <input
                id="location-code"
                value={formCode}
                onChange={event =>
                  setFormCode(event.target.value)
                }
                placeholder="np. A-01-01"
                maxLength={50}
                required
                disabled={saving}
              />
            </div>

            <div className="location-field">
              <label htmlFor="location-description">
                Opis
              </label>

              <input
                id="location-description"
                value={formDescription}
                onChange={event =>
                  setFormDescription(event.target.value)
                }
                placeholder="np. Regał A1, poziom 1"
                maxLength={250}
                disabled={saving}
              />
            </div>
          </div>

          <p className="location-form-hint">
            Kod musi być unikalny w obrębie strefy.
            Nowa lokalizacja zostanie automatycznie aktywowana.
          </p>

          <div className="location-form-actions">
            <button
              type="submit"
              className="button-primary"
              disabled={saving}
            >
              {saving
                ? 'Zapisywanie...'
                : formMode === 'create'
                  ? 'Dodaj lokalizację'
                  : 'Zapisz zmiany'}
            </button>

            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                closeForm()
                setError(null)
              }}
              disabled={saving}
            >
              Anuluj
            </button>
          </div>
        </form>
      )}

      <form
        className="location-filters"
        onSubmit={handleSearch}
      >
        <div className="location-field location-search">
          <label htmlFor="location-search">
            Szukaj lokalizacji
          </label>

          <input
            id="location-search"
            value={searchInput}
            onChange={event =>
              setSearchInput(event.target.value)
            }
            placeholder="Kod lub opis lokalizacji"
            maxLength={100}
            disabled={zoneId === 0}
          />
        </div>

        <div className="location-field location-status">
          <label htmlFor="location-status">
            Status
          </label>

          <select
            id="location-status"
            value={status}
            onChange={event => {
              setStatus(
                event.target.value as StatusFilter
              )
              setPage(1)
            }}
            disabled={zoneId === 0}
          >
            <option value="all">Wszystkie</option>
            <option value="active">Aktywne</option>
            <option value="inactive">Nieaktywne</option>
          </select>
        </div>

        <button
          type="submit"
          className="button-primary"
          disabled={zoneId === 0}
        >
          Szukaj
        </button>

        <button
          type="button"
          className="button-secondary"
          onClick={resetFilters}
          disabled={zoneId === 0}
        >
          Wyczyść
        </button>
      </form>

      <div className="results-info" aria-live="polite">
        {warehousesLoading
          ? 'Pobieranie magazynów...'
          : zonesLoading
            ? 'Pobieranie stref...'
            : loading
              ? 'Pobieranie lokalizacji...'
              : !selectedZone
                ? 'Wybierz magazyn i strefę.'
                : listError
                  ? 'Nie udało się pobrać lokalizacji.'
                  : `Liczba znalezionych lokalizacji: ${totalCount}`}
      </div>

      {!loading &&
        !listError &&
        selectedZone &&
        data?.items.length === 0 && (
          <div className="empty-state">
            <strong>Brak lokalizacji</strong>
            <p>
              Nie znaleziono lokalizacji dla wybranej
              strefy i podanych filtrów.
            </p>
          </div>
        )}

      {!loading && data && data.items.length > 0 && (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Magazyn</th>
                <th>Strefa</th>
                <th>Kod lokalizacji</th>
                <th>Opis</th>
                <th>Status</th>
                {canEditLocations && <th>Akcje</th>}
              </tr>
            </thead>

            <tbody>
              {data.items.map(location => (
                <tr key={location.id}>
                  <td>{location.id}</td>
                  <td>{location.warehouseCode}</td>
                  <td>{location.warehouseZoneCode}</td>

                  <td>
                    <strong>{location.code}</strong>
                  </td>

                  <td>{location.description || '—'}</td>

                  <td>
                    <span
                      className={
                        location.isActive
                          ? 'status-pill active'
                          : 'status-pill inactive'
                      }
                    >
                      {location.isActive
                        ? 'Aktywna'
                        : 'Nieaktywna'}
                    </span>
                  </td>

                  {canEditLocations && (
                    <td>
                      <div className="location-row-actions">
                        <button
                          type="button"
                          className="button-secondary location-small-button"
                          onClick={() =>
                            startEditing(location)
                          }
                          disabled={
                            formOpen || busyId !== null
                          }
                        >
                          Edytuj
                        </button>

                        {location.isActive ? (
                          <button
                            type="button"
                            className="location-danger-button"
                            onClick={() =>
                              void handleChangeStatus(
                                location,
                                false
                              )
                            }
                            disabled={
                              formOpen || busyId !== null
                            }
                          >
                            {busyId === location.id
                              ? 'Dezaktywowanie...'
                              : 'Dezaktywuj'}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="location-activate-button"
                            onClick={() =>
                              void handleChangeStatus(
                                location,
                                true
                              )
                            }
                            disabled={
                              formOpen ||
                              busyId !== null ||
                              !canCreateHere
                            }
                          >
                            {busyId === location.id
                              ? 'Aktywowanie...'
                              : 'Aktywuj'}
                          </button>
                        )}
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!loading && data && totalCount > 0 && (
        <div className="pagination">
          <span>
            Strona {page} z {totalPages}
          </span>

          <div className="pagination-actions">
            <button
              type="button"
              className="button-secondary"
              onClick={() =>
                setPage(current =>
                  Math.max(1, current - 1)
                )
              }
              disabled={page <= 1}
            >
              Poprzednia
            </button>

            <button
              type="button"
              className="button-secondary"
              onClick={() =>
                setPage(current =>
                  Math.min(totalPages, current + 1)
                )
              }
              disabled={page >= totalPages}
            >
              Następna
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
