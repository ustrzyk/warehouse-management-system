
import { useEffect, useState, type FormEvent } from 'react'
import './WarehouseZonesPage.css'

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
  canEditZones: boolean
  onUnauthorized: () => void
}

type StatusFilter = 'all' | 'active' | 'inactive'
type FormMode = 'create' | 'edit' | null

const PAGE_SIZE = 10
const LOOKUP_PAGE_SIZE = 100

async function getApiError(
  response: Response,
  fallback: string
): Promise<string> {
  if (response.status === 403) {
    return 'Nie masz uprawnień do wykonania tej operacji.'
  }

  if (response.status === 404) {
    return 'Nie znaleziono wskazanego elementu.'
  }

  const problem = (await response.json().catch(() => null)) as
    | ApiProblem
    | null

  return (
    problem?.detail ||
    `${fallback} (HTTP ${response.status}).`
  )
}

export default function WarehouseZonesPage({
  token,
  canEditZones,
  onUnauthorized,
}: Props) {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([])
  const [warehousesLoading, setWarehousesLoading] = useState(true)
  const [warehousesError, setWarehousesError] =
    useState<string | null>(null)

  const [selectedWarehouseId, setSelectedWarehouseId] =
    useState(0)

  const [data, setData] =
    useState<PagedResult<WarehouseZone> | null>(null)

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<StatusFilter>('all')
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [selectedZone, setSelectedZone] =
    useState<WarehouseZone | null>(null)

  const [formCode, setFormCode] = useState('')
  const [formName, setFormName] = useState('')

  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState<number | null>(null)

  // Pobranie wszystkich magazynów do listy wyboru.
  // Backend pozwala pobrać maksymalnie 100 na stronę.
  useEffect(() => {
    const controller = new AbortController()

    async function loadWarehouses() {
      setWarehousesLoading(true)
      setWarehousesError(null)

      try {
        const result: Warehouse[] = []
        let currentPage = 1
        let totalCount = 0

        do {
          const parameters = new URLSearchParams({
            Page: String(currentPage),
            PageSize: String(LOOKUP_PAGE_SIZE),
          })

          const response = await fetch(
            `/api/warehouses?${parameters.toString()}`,
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
              `Nie udało się pobrać magazynów (HTTP ${response.status}).`
            )
          }

          const pageData =
            (await response.json()) as PagedResult<Warehouse>

          if (controller.signal.aborted) return

          result.push(...pageData.items)
          totalCount = pageData.totalCount

          if (pageData.items.length === 0) {
            break
          }

          currentPage += 1
        } while (result.length < totalCount)

        if (controller.signal.aborted) return

        setWarehouses(result)

        setSelectedWarehouseId(current => {
          if (result.some(item => item.id === current)) {
            return current
          }

          return (
            result.find(item => item.isActive)?.id ||
            result[0]?.id ||
            0
          )
        })
      } catch (err) {
        if (!controller.signal.aborted) {
          setWarehousesError(
            err instanceof Error
              ? err.message
              : 'Nie udało się pobrać listy magazynów.'
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setWarehousesLoading(false)
        }
      }
    }

    void loadWarehouses()

    return () => controller.abort()
  }, [token, onUnauthorized])

  // Pobranie stref wybranego magazynu.
  useEffect(() => {
    const controller = new AbortController()

    async function loadZones() {
      setData(null)
      setError(null)

      if (selectedWarehouseId === 0) {
        setLoading(false)
        return
      }

      setLoading(true)

      const parameters = new URLSearchParams({
        WarehouseId: String(selectedWarehouseId),
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
          `/api/warehouse-zones?${parameters.toString()}`,
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
            `Nie udało się pobrać stref (HTTP ${response.status}).`
          )
        }

        const result =
          (await response.json()) as PagedResult<WarehouseZone>

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
          setError(
            err instanceof Error
              ? err.message
              : 'Wystąpił błąd pobierania stref.'
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadZones()

    return () => controller.abort()
  }, [
    token,
    selectedWarehouseId,
    page,
    search,
    status,
    reloadKey,
    onUnauthorized,
  ])

  const selectedWarehouse =
    warehouses.find(
      warehouse => warehouse.id === selectedWarehouseId
    ) || null

  const totalCount = data?.totalCount ?? 0

  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE)
  )

  const formOpen = formMode !== null

  function refreshZones() {
    setReloadKey(current => current + 1)
  }

  function clearFilters() {
    setSearchInput('')
    setSearch('')
    setStatus('all')
    setPage(1)
  }

  function changeWarehouse(value: string) {
    setSelectedWarehouseId(Number(value))
    setPage(1)
    setSearchInput('')
    setSearch('')
    setStatus('all')
    setError(null)
    setSuccess(null)
    closeForm()
  }

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
    setSuccess(null)
  }

  function startCreating() {
    if (!selectedWarehouse?.isActive) {
      setError(
        'Aby dodać strefę, wybierz aktywny magazyn.'
      )
      return
    }

    setFormMode('create')
    setSelectedZone(null)
    setFormCode('')
    setFormName('')
    setError(null)
    setSuccess(null)
  }

  function startEditing(zone: WarehouseZone) {
    setFormMode('edit')
    setSelectedZone(zone)
    setFormCode(zone.code)
    setFormName(zone.name)
    setError(null)
    setSuccess(null)
  }

  function closeForm() {
    setFormMode(null)
    setSelectedZone(null)
    setFormCode('')
    setFormName('')
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const code = formCode.trim().toUpperCase()
    const name = formName.trim()
    const editing = formMode === 'edit'

    if (!code || !name) {
      setError('Kod i nazwa strefy są wymagane.')
      return
    }

    if (code.length > 30 || name.length > 150) {
      setError('Kod lub nazwa strefy są zbyt długie.')
      return
    }

    if (!editing && !selectedWarehouse?.isActive) {
      setError('Wybierz aktywny magazyn.')
      return
    }

    if (editing && !selectedZone) {
      setError('Nie wybrano strefy do edycji.')
      return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)

    try {
      const endpoint =
        editing && selectedZone
          ? `/api/warehouse-zones/${selectedZone.id}`
          : '/api/warehouse-zones'

      const requestBody =
        editing && selectedZone
          ? {
              code,
              name,
              isActive: selectedZone.isActive,
            }
          : {
              warehouseId: selectedWarehouseId,
              code,
              name,
            }

      const response = await fetch(endpoint, {
        method: editing ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(requestBody),
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
              ? 'Nie udało się edytować strefy'
              : 'Nie udało się utworzyć strefy'
          )
        )
      }

      closeForm()

      if (editing) {
        setSuccess(`Zmiany strefy ${code} zostały zapisane.`)
      } else {
        setSuccess(`Strefa ${code} została utworzona.`)
        clearFilters()
      }

      refreshZones()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zapisywania strefy.'
      )
    } finally {
      setSaving(false)
    }
  }

  async function handleChangeStatus(
    zone: WarehouseZone,
    activate: boolean
  ) {
    if (!activate) {
      const confirmed = window.confirm(
        `Czy na pewno chcesz dezaktywować strefę ${zone.code} — ${zone.name}?\n\nStrefa pozostanie w bazie danych.`
      )

      if (!confirmed) return
    }

    setBusyId(zone.id)
    setError(null)
    setSuccess(null)

    try {
      let response: Response

      if (activate) {
        // PUT wymaga kodu, nazwy oraz stanu aktywności.
        const detailResponse = await fetch(
          `/api/warehouse-zones/${zone.id}`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          }
        )

        if (detailResponse.status === 401) {
          onUnauthorized()
          return
        }

        if (!detailResponse.ok) {
          throw new Error(
            await getApiError(
              detailResponse,
              'Nie udało się pobrać strefy'
            )
          )
        }

        const details =
          (await detailResponse.json()) as WarehouseZone

        if (details.isActive) {
          setSuccess(
            `Strefa ${details.code} jest już aktywna.`
          )
          refreshZones()
          return
        }

        response = await fetch(
          `/api/warehouse-zones/${zone.id}`,
          {
            method: 'PUT',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${token}`,
            },
            body: JSON.stringify({
              code: details.code,
              name: details.name,
              isActive: true,
            }),
          }
        )
      } else {
        response = await fetch(
          `/api/warehouse-zones/${zone.id}`,
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
              ? 'Nie udało się aktywować strefy'
              : 'Nie udało się dezaktywować strefy'
          )
        )
      }

      setSuccess(
        activate
          ? `Strefa ${zone.code} została aktywowana.`
          : `Strefa ${zone.code} została dezaktywowana.`
      )

      refreshZones()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zmiany statusu strefy.'
      )
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>Strefy magazynowe</h2>
          <p>
            Drugi poziom struktury magazynu.
            Dane pobierane z ASP.NET Core i SQL Server.
          </p>
        </div>

        <div className="zones-heading-actions">
          {canEditZones && (
            <button
              type="button"
              className="button-primary"
              onClick={startCreating}
              disabled={
                formOpen ||
                busyId !== null ||
                warehousesLoading ||
                !selectedWarehouse?.isActive
              }
            >
              + Dodaj strefę
            </button>
          )}

          <button
            type="button"
            className="button-secondary"
            onClick={refreshZones}
            disabled={loading || selectedWarehouseId === 0}
          >
            Odśwież
          </button>
        </div>
      </div>

      <div className="zones-warehouse-picker">
        <label htmlFor="zones-warehouse">
          Wybierz magazyn
        </label>

        <select
          id="zones-warehouse"
          value={selectedWarehouseId}
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
              {warehouse.isActive ? '' : ' (nieaktywny)'}
            </option>
          ))}
        </select>

        {selectedWarehouse && (
          <div className="zones-selected-info">
            <span>
              Wybrany magazyn:
              <strong> {selectedWarehouse.code}</strong>
            </span>

            <span
              className={
                selectedWarehouse.isActive
                  ? 'status-pill active'
                  : 'status-pill inactive'
              }
            >
              {selectedWarehouse.isActive
                ? 'Aktywny'
                : 'Nieaktywny'}
            </span>
          </div>
        )}
      </div>

      {warehousesError && (
        <div className="error-message" role="alert">
          {warehousesError}
        </div>
      )}

      {success && (
        <div className="zones-success" role="status">
          {success}
        </div>
      )}

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      {selectedWarehouse && !selectedWarehouse.isActive && (
        <div className="zones-notice">
          Wybrany magazyn jest nieaktywny.
          Nie można dodawać w nim nowych stref ani
          aktywować istniejących.
        </div>
      )}

      {canEditZones && formOpen && (
        <form
          className="zones-form"
          onSubmit={event => void handleSave(event)}
        >
          <h3>
            {formMode === 'create'
              ? 'Dodaj strefę'
              : `Edytuj strefę ${selectedZone?.code ?? ''}`}
          </h3>

          <div className="zones-form-grid">
            <div className="zones-field">
              <label htmlFor="zone-code">
                Kod strefy *
              </label>

              <input
                id="zone-code"
                value={formCode}
                onChange={event =>
                  setFormCode(event.target.value)
                }
                placeholder="np. A"
                maxLength={30}
                required
                disabled={saving}
              />
            </div>

            <div className="zones-field">
              <label htmlFor="zone-name">
                Nazwa strefy *
              </label>

              <input
                id="zone-name"
                value={formName}
                onChange={event =>
                  setFormName(event.target.value)
                }
                placeholder="np. Regały wysokiego składowania"
                maxLength={150}
                required
                disabled={saving}
              />
            </div>
          </div>

          <p className="zones-hint">
            Kod strefy musi być unikalny w obrębie
            wybranego magazynu.
          </p>

          <div className="zones-form-actions">
            <button
              type="submit"
              className="button-primary"
              disabled={saving}
            >
              {saving
                ? 'Zapisywanie...'
                : formMode === 'create'
                  ? 'Dodaj strefę'
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
        className="zones-filters"
        onSubmit={handleSearch}
      >
        <div className="zones-field zones-search">
          <label htmlFor="zones-search">
            Szukaj strefy
          </label>

          <input
            id="zones-search"
            value={searchInput}
            onChange={event =>
              setSearchInput(event.target.value)
            }
            placeholder="Kod lub nazwa strefy"
            maxLength={100}
            disabled={!selectedWarehouse}
          />
        </div>

        <div className="zones-field zones-status-filter">
          <label htmlFor="zones-status">
            Status
          </label>

          <select
            id="zones-status"
            value={status}
            onChange={event => {
              setStatus(
                event.target.value as StatusFilter
              )
              setPage(1)
            }}
            disabled={!selectedWarehouse}
          >
            <option value="all">Wszystkie</option>
            <option value="active">Aktywne</option>
            <option value="inactive">Nieaktywne</option>
          </select>
        </div>

        <button
          type="submit"
          className="button-primary"
          disabled={!selectedWarehouse}
        >
          Szukaj
        </button>

        <button
          type="button"
          className="button-secondary"
          onClick={clearFilters}
          disabled={!selectedWarehouse}
        >
          Wyczyść
        </button>
      </form>

      <div className="results-info" aria-live="polite">
        {warehousesLoading
          ? 'Pobieranie magazynów...'
          : loading
            ? 'Pobieranie stref...'
            : selectedWarehouse
              ? `Liczba znalezionych stref: ${totalCount}`
              : 'Wybierz magazyn, aby zobaczyć jego strefy.'}
      </div>

      {!loading &&
        selectedWarehouse &&
        data?.items.length === 0 &&
        !error && (
          <div className="empty-state">
            <strong>Brak stref</strong>
            <p>
              Nie znaleziono stref dla wybranego magazynu
              i podanych filtrów.
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
                <th>Kod strefy</th>
                <th>Nazwa strefy</th>
                <th>Status</th>
                {canEditZones && <th>Akcje</th>}
              </tr>
            </thead>

            <tbody>
              {data.items.map(zone => (
                <tr key={zone.id}>
                  <td>{zone.id}</td>
                  <td>{zone.warehouseCode}</td>

                  <td>
                    <strong>{zone.code}</strong>
                  </td>

                  <td>{zone.name}</td>

                  <td>
                    <span
                      className={
                        zone.isActive
                          ? 'status-pill active'
                          : 'status-pill inactive'
                      }
                    >
                      {zone.isActive
                        ? 'Aktywna'
                        : 'Nieaktywna'}
                    </span>
                  </td>

                  {canEditZones && (
                    <td>
                      <div className="zones-row-actions">
                        <button
                          type="button"
                          className="button-secondary zones-small-button"
                          onClick={() =>
                            startEditing(zone)
                          }
                          disabled={
                            formOpen ||
                            busyId !== null
                          }
                        >
                          Edytuj
                        </button>

                        {zone.isActive ? (
                          <button
                            type="button"
                            className="zones-danger-button"
                            onClick={() =>
                              void handleChangeStatus(
                                zone,
                                false
                              )
                            }
                            disabled={
                              formOpen ||
                              busyId !== null
                            }
                          >
                            {busyId === zone.id
                              ? 'Dezaktywowanie...'
                              : 'Dezaktywuj'}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="zones-activate-button"
                            onClick={() =>
                              void handleChangeStatus(
                                zone,
                                true
                              )
                            }
                            disabled={
                              formOpen ||
                              busyId !== null ||
                              !selectedWarehouse?.isActive
                            }
                          >
                            {busyId === zone.id
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
