
import { useEffect, useState, type FormEvent } from 'react'
import './WarehousesPage.css'

type Warehouse = {
  id: number
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
  canEditWarehouses: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10

async function getApiError(
  response: Response,
  fallback: string
): Promise<string> {
  if (response.status === 403) {
    return 'Nie masz uprawnień do wykonania tej operacji.'
  }

  if (response.status === 404) {
    return 'Nie znaleziono magazynu.'
  }

  const problem = (await response.json().catch(() => null)) as
    | ApiProblem
    | null

  return problem?.detail || `${fallback} (HTTP ${response.status}).`
}

export default function WarehousesPage({
  token,
  canEditWarehouses,
  onUnauthorized,
}: Props) {
  const [data, setData] = useState<PagedResult<Warehouse> | null>(
    null
  )
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)

  const [formMode, setFormMode] = useState<
    'create' | 'edit' | null
  >(null)

  const [selectedWarehouse, setSelectedWarehouse] =
    useState<Warehouse | null>(null)

  const [formCode, setFormCode] = useState('')
  const [formName, setFormName] = useState('')
  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState<number | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadWarehouses() {
      setLoading(true)
      setError(null)

      const parameters = new URLSearchParams({
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

        const result =
          (await response.json()) as PagedResult<Warehouse>

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
              : 'Wystąpił błąd pobierania magazynów.'
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadWarehouses()

    return () => controller.abort()
  }, [token, page, search, status, reloadKey, onUnauthorized])

  function refreshWarehouses() {
    setReloadKey(value => value + 1)
  }

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
    setSuccess(null)
  }

  function clearFilters() {
    setSearchInput('')
    setSearch('')
    setStatus('all')
    setPage(1)
    setSuccess(null)
  }

  function startCreating() {
    setFormMode('create')
    setSelectedWarehouse(null)
    setFormCode('')
    setFormName('')
    setError(null)
    setSuccess(null)
  }

  function startEditing(warehouse: Warehouse) {
    setFormMode('edit')
    setSelectedWarehouse(warehouse)
    setFormCode(warehouse.code)
    setFormName(warehouse.name)
    setError(null)
    setSuccess(null)
  }

  function closeForm() {
    setFormMode(null)
    setSelectedWarehouse(null)
    setFormCode('')
    setFormName('')
    setError(null)
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const code = formCode.trim().toUpperCase()
    const name = formName.trim()

    if (!code || !name) {
      setError('Kod i nazwa magazynu są wymagane.')
      return
    }

    if (code.length > 30 || name.length > 150) {
      setError('Kod lub nazwa magazynu są zbyt długie.')
      return
    }

    const editing = formMode === 'edit'

    if (editing && !selectedWarehouse) {
      return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)

    try {
      const endpoint = editing
        ? `/api/warehouses/${selectedWarehouse!.id}`
        : '/api/warehouses'

      const requestBody = editing
        ? {
            code,
            name,
            isActive: selectedWarehouse!.isActive,
          }
        : {
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
              ? 'Nie udało się zapisać zmian magazynu'
              : 'Nie udało się utworzyć magazynu'
          )
        )
      }

      setFormMode(null)
      setSelectedWarehouse(null)
      setFormCode('')
      setFormName('')

      if (editing) {
        setSuccess(`Zmiany magazynu ${code} zostały zapisane.`)
      } else {
        setSuccess(`Magazyn ${code} został utworzony.`)
        setSearchInput('')
        setSearch('')
        setStatus('all')
        setPage(1)
      }

      refreshWarehouses()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zapisywania magazynu.'
      )
    } finally {
      setSaving(false)
    }
  }

  async function handleChangeStatus(
    warehouse: Warehouse,
    activate: boolean
  ) {
    if (!activate) {
      const confirmed = window.confirm(
        `Czy na pewno chcesz dezaktywować magazyn ${warehouse.code} — ${warehouse.name}?\n\nMagazyn pozostanie w bazie danych.`
      )

      if (!confirmed) return
    }

    setBusyId(warehouse.id)
    setError(null)
    setSuccess(null)

    try {
      let response: Response

      if (activate) {
        // PUT wymaga również kodu i nazwy.
        // Dlatego pobieramy aktualne dane magazynu.
        const detailsResponse = await fetch(
          `/api/warehouses/${warehouse.id}`,
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
              'Nie udało się pobrać magazynu'
            )
          )
        }

        const details =
          (await detailsResponse.json()) as Warehouse

        if (details.isActive) {
          setSuccess(
            `Magazyn ${details.code} jest już aktywny.`
          )
          refreshWarehouses()
          return
        }

        response = await fetch(
          `/api/warehouses/${warehouse.id}`,
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
          `/api/warehouses/${warehouse.id}`,
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
              ? 'Nie udało się aktywować magazynu'
              : 'Nie udało się dezaktywować magazynu'
          )
        )
      }

      setSuccess(
        activate
          ? `Magazyn ${warehouse.code} został aktywowany.`
          : `Magazyn ${warehouse.code} został dezaktywowany.`
      )

      refreshWarehouses()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zmiany statusu magazynu.'
      )
    } finally {
      setBusyId(null)
    }
  }

  const totalCount = data?.totalCount ?? 0

  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE)
  )

  const formOpen = formMode !== null

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>Magazyny i lokalizacje</h2>
          <p>
            Zarządzanie strukturą magazynową w SQL Server.
          </p>
        </div>

        <div className="warehouse-heading-actions">
          {canEditWarehouses && (
            <button
              type="button"
              className="button-primary"
              onClick={startCreating}
              disabled={formOpen || busyId !== null}
            >
              + Dodaj magazyn
            </button>
          )}

          <button
            type="button"
            className="button-secondary"
            onClick={refreshWarehouses}
            disabled={loading}
          >
            Odśwież
          </button>
        </div>
      </div>

      <div className="warehouse-section-title">
        <h3>Lista magazynów</h3>
        <p>
          Pierwszy poziom struktury magazynowej.
          Strefy i miejsca składowania dodamy w kolejnym etapie.
        </p>
      </div>

      {success && (
        <div
          className="warehouse-success"
          role="status"
        >
          {success}
        </div>
      )}

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      {canEditWarehouses && formOpen && (
        <form
          className="warehouse-form"
          onSubmit={event => void handleSave(event)}
        >
          <h3>
            {formMode === 'create'
              ? 'Dodaj magazyn'
              : `Edytuj magazyn ${selectedWarehouse?.code ?? ''}`}
          </h3>

          <div className="warehouse-form-grid">
            <div className="warehouse-field">
              <label htmlFor="warehouse-code">
                Kod magazynu *
              </label>
              <input
                id="warehouse-code"
                value={formCode}
                onChange={event =>
                  setFormCode(event.target.value)
                }
                required
                maxLength={30}
                disabled={saving}
                placeholder="np. WH-001"
              />
            </div>

            <div className="warehouse-field">
              <label htmlFor="warehouse-name">
                Nazwa magazynu *
              </label>
              <input
                id="warehouse-name"
                value={formName}
                onChange={event =>
                  setFormName(event.target.value)
                }
                required
                maxLength={150}
                disabled={saving}
                placeholder="np. Magazyn główny"
              />
            </div>
          </div>

          <p className="warehouse-form-hint">
            Kod musi być unikalny. Nowy magazyn jest
            automatycznie aktywny.
          </p>

          <div className="warehouse-form-actions">
            <button
              type="submit"
              className="button-primary"
              disabled={saving}
            >
              {saving
                ? 'Zapisywanie...'
                : formMode === 'create'
                  ? 'Dodaj magazyn'
                  : 'Zapisz zmiany'}
            </button>

            <button
              type="button"
              className="button-secondary"
              onClick={closeForm}
              disabled={saving}
            >
              Anuluj
            </button>
          </div>
        </form>
      )}

      <form
        className="warehouse-filters"
        onSubmit={handleSearch}
      >
        <div className="warehouse-field warehouse-search">
          <label htmlFor="warehouse-search">
            Szukaj magazynu
          </label>
          <input
            id="warehouse-search"
            value={searchInput}
            onChange={event =>
              setSearchInput(event.target.value)
            }
            placeholder="Kod lub nazwa"
            maxLength={100}
          />
        </div>

        <div className="warehouse-field warehouse-status">
          <label htmlFor="warehouse-status">
            Status
          </label>
          <select
            id="warehouse-status"
            value={status}
            onChange={event => {
              setStatus(event.target.value)
              setPage(1)
            }}
          >
            <option value="all">Wszystkie</option>
            <option value="active">Aktywne</option>
            <option value="inactive">Nieaktywne</option>
          </select>
        </div>

        <button
          type="submit"
          className="button-primary"
        >
          Szukaj
        </button>

        <button
          type="button"
          className="button-secondary"
          onClick={clearFilters}
        >
          Wyczyść
        </button>
      </form>

      <div className="results-info" aria-live="polite">
        {loading
          ? 'Pobieranie magazynów...'
          : error && !data
            ? 'Nie udało się pobrać magazynów.'
            : `Liczba znalezionych magazynów: ${totalCount}`}
      </div>

      {!loading && !error && data?.items.length === 0 && (
        <div className="empty-state">
          <strong>Brak magazynów</strong>
          <p>
            Nie znaleziono magazynów dla wybranych filtrów.
          </p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <div
          className="table-scroll"
          aria-busy={loading}
          style={{ opacity: loading ? 0.5 : 1 }}
        >
          <table className="data-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Kod</th>
                <th>Nazwa magazynu</th>
                <th>Status</th>
                {canEditWarehouses && <th>Akcje</th>}
              </tr>
            </thead>

            <tbody>
              {data.items.map(warehouse => (
                <tr key={warehouse.id}>
                  <td>{warehouse.id}</td>

                  <td>
                    <strong>{warehouse.code}</strong>
                  </td>

                  <td>{warehouse.name}</td>

                  <td>
                    <span
                      className={
                        warehouse.isActive
                          ? 'status-pill active'
                          : 'status-pill inactive'
                      }
                    >
                      {warehouse.isActive
                        ? 'Aktywny'
                        : 'Nieaktywny'}
                    </span>
                  </td>

                  {canEditWarehouses && (
                    <td>
                      <div className="warehouse-row-actions">
                        <button
                          type="button"
                          className="button-secondary warehouse-small-button"
                          onClick={() =>
                            startEditing(warehouse)
                          }
                          disabled={
                            formOpen || busyId !== null
                          }
                        >
                          Edytuj
                        </button>

                        {warehouse.isActive ? (
                          <button
                            type="button"
                            className="warehouse-danger-button"
                            disabled={
                              formOpen || busyId !== null
                            }
                            onClick={() =>
                              void handleChangeStatus(
                                warehouse,
                                false
                              )
                            }
                          >
                            {busyId === warehouse.id
                              ? 'Dezaktywowanie...'
                              : 'Dezaktywuj'}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="warehouse-activate-button"
                            disabled={
                              formOpen || busyId !== null
                            }
                            onClick={() =>
                              void handleChangeStatus(
                                warehouse,
                                true
                              )
                            }
                          >
                            {busyId === warehouse.id
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
              disabled={page <= 1 || loading}
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
              disabled={
                page >= totalPages || loading
              }
            >
              Następna
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
