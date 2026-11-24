
import { useEffect, useState, type FormEvent } from 'react'
import ProductCreateForm from './ProductCreateForm'
import ProductEditForm from './ProductEditForm'
import './ProductsPage.css'

type Product = {
  id: number
  code: string
  name: string
  ean: string | null
  productCategoryName: string
  unitOfMeasureSymbol: string
  isActive: boolean
}

type ProductDetail = {
  id: number
  code: string
  name: string
  description: string | null
  ean: string | null
  productCategoryId: number
  unitOfMeasureId: number
  isActive: boolean
}

type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

type Props = {
  token: string
  canEditProducts: boolean
  onUnauthorized: () => void
}

const PAGE_SIZE = 10

export default function ProductsPage({
  token,
  canEditProducts,
  onUnauthorized,
}: Props) {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)

  const [showCreateForm, setShowCreateForm] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const [success, setSuccess] = useState<string | null>(null)
  const [data, setData] = useState<PagedResult<Product> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadProducts() {
      setLoading(true)
      setError(null)

      const parameters = new URLSearchParams({
        Page: String(page),
        PageSize: String(PAGE_SIZE),
        SortBy: 'name',
      })

      if (search) {
        parameters.set('Search', search)
      }

      if (status !== 'all') {
        parameters.set('IsActive', String(status === 'active'))
      }

      try {
        const response = await fetch(
          `/api/products?${parameters.toString()}`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
            signal: controller.signal,
          }
        )

        if (response.status === 401) {
          onUnauthorized()
          return
        }

        if (!response.ok) {
          throw new Error(
            `Nie udało się pobrać produktów (HTTP ${response.status}).`
          )
        }

        const result =
          (await response.json()) as PagedResult<Product>

        if (!controller.signal.aborted) {
          setData(result)
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          setError(
            err instanceof Error
              ? err.message
              : 'Wystąpił błąd pobierania produktów.'
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadProducts()

    return () => controller.abort()
  }, [token, page, search, status, reloadKey, onUnauthorized])

  function refreshProducts() {
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
  }

  function startCreating() {
    setEditingId(null)
    setShowCreateForm(true)
    setSuccess(null)
    setError(null)
  }

  function startEditing(productId: number) {
    setShowCreateForm(false)
    setEditingId(productId)
    setSuccess(null)
    setError(null)
  }

  function handleProductCreated() {
    setShowCreateForm(false)
    setSuccess('Produkt został pomyślnie dodany.')
    clearFilters()
    refreshProducts()
  }

  function handleProductUpdated() {
    setEditingId(null)
    setSuccess('Zmiany produktu zostały zapisane.')
    refreshProducts()
  }

  // Dezaktywacja produktu
  async function handleDeactivate(product: Product) {
    const confirmed = window.confirm(
      `Czy na pewno chcesz dezaktywować produkt ${product.code} — ${product.name}?\n\nProdukt pozostanie w bazie danych, ale będzie nieaktywny.`
    )

    if (!confirmed) return

    setBusyId(product.id)
    setError(null)
    setSuccess(null)

    try {
      const response = await fetch(`/api/products/${product.id}`, {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${token}`,
        },
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (response.status === 403) {
        throw new Error(
          'Nie masz uprawnień do dezaktywacji produktu.'
        )
      }

      if (response.status === 404) {
        throw new Error('Nie znaleziono produktu.')
      }

      if (!response.ok) {
        throw new Error(
          `Nie udało się dezaktywować produktu (HTTP ${response.status}).`
        )
      }

      setSuccess(`Produkt ${product.code} został dezaktywowany.`)
      setEditingId(null)
      setShowCreateForm(false)
      refreshProducts()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd dezaktywacji.'
      )
    } finally {
      setBusyId(null)
    }
  }

  // Ponowna aktywacja produktu
  async function handleActivate(product: Product) {
    setBusyId(product.id)
    setError(null)
    setSuccess(null)

    try {
      // Najpierw pobieramy aktualne dane produktu.
      const detailResponse = await fetch(
        `/api/products/${product.id}`,
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

      if (detailResponse.status === 404) {
        throw new Error('Nie znaleziono produktu.')
      }

      if (!detailResponse.ok) {
        throw new Error(
          `Nie udało się pobrać produktu (HTTP ${detailResponse.status}).`
        )
      }

      const details =
        (await detailResponse.json()) as ProductDetail

      if (details.isActive) {
        setSuccess(`Produkt ${details.code} jest już aktywny.`)
        refreshProducts()
        return
      }

      // PUT wymaga pełnych danych produktu.
      // Zmieniamy tylko isActive na true.
      const response = await fetch(
        `/api/products/${product.id}`,
        {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            code: details.code,
            name: details.name,
            description: details.description,
            ean: details.ean,
            productCategoryId: details.productCategoryId,
            unitOfMeasureId: details.unitOfMeasureId,
            isActive: true,
          }),
        }
      )

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (response.status === 403) {
        throw new Error(
          'Nie masz uprawnień do aktywacji produktu.'
        )
      }

      if (response.status === 404) {
        throw new Error('Nie znaleziono produktu.')
      }

      if (!response.ok) {
        throw new Error(
          `Nie udało się aktywować produktu (HTTP ${response.status}). Sprawdź, czy jego kategoria i jednostka miary są aktywne.`
        )
      }

      setSuccess(
        `Produkt ${details.code} został ponownie aktywowany.`
      )

      refreshProducts()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd aktywacji produktu.'
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

  const editorOpen = showCreateForm || editingId !== null

  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>Katalog produktów</h2>
          <p>
            Dane pobierane bezpośrednio z ASP.NET Core i SQL Server.
          </p>
        </div>

        <div className="panel-heading-actions">
          {canEditProducts && (
            <button
              type="button"
              className="button-primary"
              onClick={startCreating}
              disabled={editorOpen || busyId !== null}
            >
              + Dodaj produkt
            </button>
          )}

          <button
            type="button"
            className="button-secondary"
            onClick={refreshProducts}
            disabled={loading}
          >
            Odśwież
          </button>
        </div>
      </div>

      {success && (
        <div className="product-success" role="status">
          {success}
        </div>
      )}

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      {canEditProducts && showCreateForm && (
        <ProductCreateForm
          token={token}
          onSaved={handleProductCreated}
          onCancel={() => setShowCreateForm(false)}
          onUnauthorized={onUnauthorized}
        />
      )}

      {canEditProducts && editingId !== null && (
        <ProductEditForm
          key={editingId}
          productId={editingId}
          token={token}
          onSaved={handleProductUpdated}
          onCancel={() => setEditingId(null)}
          onUnauthorized={onUnauthorized}
        />
      )}

      <form className="product-filters" onSubmit={handleSearch}>
        <div className="filter-search">
          <label htmlFor="product-search">Szukaj produktu</label>
          <input
            id="product-search"
            value={searchInput}
            onChange={event => setSearchInput(event.target.value)}
            placeholder="Kod, nazwa lub EAN"
            maxLength={100}
          />
        </div>

        <div className="filter-status">
          <label htmlFor="product-status">Status</label>
          <select
            id="product-status"
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
          ? 'Pobieranie produktów...'
          : error
            ? 'Nie udało się załadować listy.'
            : `Liczba znalezionych produktów: ${totalCount}`}
      </div>

      {!loading && !error && data?.items.length === 0 && (
        <div className="empty-state">
          <strong>Brak produktów</strong>
          <p>
            Nie znaleziono produktów dla podanych kryteriów.
          </p>
        </div>
      )}

      {!error && data && data.items.length > 0 && (
        <div
          className="table-scroll"
          aria-busy={loading}
          style={{ opacity: loading ? 0.5 : 1 }}
        >
          <table className="data-table">
            <thead>
              <tr>
                <th>Kod</th>
                <th>Nazwa produktu</th>
                <th>Kategoria</th>
                <th>Jednostka</th>
                <th>EAN</th>
                <th>Status</th>
                {canEditProducts && <th>Akcje</th>}
              </tr>
            </thead>

            <tbody>
              {data.items.map(product => (
                <tr key={product.id}>
                  <td>
                    <strong>{product.code}</strong>
                  </td>

                  <td>{product.name}</td>

                  <td>{product.productCategoryName}</td>

                  <td>{product.unitOfMeasureSymbol}</td>

                  <td>{product.ean || '—'}</td>

                  <td>
                    <span
                      className={
                        product.isActive
                          ? 'status-pill active'
                          : 'status-pill inactive'
                      }
                    >
                      {product.isActive
                        ? 'Aktywny'
                        : 'Nieaktywny'}
                    </span>
                  </td>

                  {canEditProducts && (
                    <td>
                      <div className="product-row-actions">
                        <button
                          type="button"
                          className="button-secondary product-small-button"
                          onClick={() => startEditing(product.id)}
                          disabled={editorOpen || busyId !== null}
                        >
                          Edytuj
                        </button>

                        {product.isActive ? (
                          <button
                            type="button"
                            className="product-danger-button"
                            onClick={() =>
                              void handleDeactivate(product)
                            }
                            disabled={editorOpen || busyId !== null}
                          >
                            {busyId === product.id
                              ? 'Dezaktywowanie...'
                              : 'Dezaktywuj'}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="product-activate-button"
                            onClick={() =>
                              void handleActivate(product)
                            }
                            disabled={editorOpen || busyId !== null}
                          >
                            {busyId === product.id
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

      {!loading && !error && data && totalCount > 0 && (
        <div className="pagination">
          <span>
            Strona {page} z {totalPages}
          </span>

          <div className="pagination-actions">
            <button
              type="button"
              className="button-secondary"
              onClick={() => setPage(current => current - 1)}
              disabled={page <= 1 || loading}
            >
              Poprzednia
            </button>

            <button
              type="button"
              className="button-secondary"
              onClick={() => setPage(current => current + 1)}
              disabled={page >= totalPages || loading}
            >
              Następna
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
