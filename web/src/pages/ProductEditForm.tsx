
import { useEffect, useState, type FormEvent } from 'react'
import './ProductCreateForm.css'
import { readApiError } from './goodsReceiptsApi'

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

type LookupItem = {
  id: number
  name: string
  isActive: boolean
}

type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

type Props = {
  productId: number
  token: string
  onSaved: () => void
  onCancel: () => void
  onUnauthorized: () => void
}

async function loadLookup(
  endpoint: string,
  token: string,
  signal: AbortSignal
): Promise<LookupItem[]> {
  const items: LookupItem[] = []
  let page = 1
  let total: number

  do {
    const response = await fetch(
      `${endpoint}?Page=${page}&PageSize=100`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
        signal,
      }
    )

    if (response.status === 401) {
      throw new Error('SESSION_EXPIRED')
    }

    if (!response.ok) {
      throw new Error(
        `Błąd pobierania słowników (HTTP ${response.status}).`
      )
    }

    const result =
      (await response.json()) as PagedResult<LookupItem>

    items.push(...result.items)
    total = result.totalCount
    page += 1

    if (result.items.length === 0) break
  } while (items.length < total)

  return items
}

async function loadProduct(
  id: number,
  token: string,
  signal: AbortSignal
): Promise<ProductDetail> {
  const response = await fetch(`/api/products/${id}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    signal,
  })

  if (response.status === 401) {
    throw new Error('SESSION_EXPIRED')
  }

  if (response.status === 404) {
    throw new Error('Nie znaleziono produktu.')
  }

  if (!response.ok) {
    throw new Error(
      `Nie udało się pobrać produktu (HTTP ${response.status}).`
    )
  }

  return (await response.json()) as ProductDetail
}

export default function ProductEditForm({
  productId,
  token,
  onSaved,
  onCancel,
  onUnauthorized,
}: Props) {
  const [product, setProduct] = useState<ProductDetail | null>(null)
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [units, setUnits] = useState<LookupItem[]>([])

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadData() {
      try {
        const [productData, categoryData, unitData] =
          await Promise.all([
            loadProduct(productId, token, controller.signal),
            loadLookup(
              '/api/product-categories',
              token,
              controller.signal
            ),
            loadLookup(
              '/api/units-of-measure',
              token,
              controller.signal
            ),
          ])

        if (!controller.signal.aborted) {
          setProduct(productData)
          setCategories(categoryData)
          setUnits(unitData)
        }
      } catch (err) {
        if (controller.signal.aborted) return

        if (
          err instanceof Error &&
          err.message === 'SESSION_EXPIRED'
        ) {
          onUnauthorized()
          return
        }

        setError(
          err instanceof Error
            ? err.message
            : 'Nie udało się załadować formularza.'
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadData()

    return () => controller.abort()
  }, [productId, token, onUnauthorized])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!product) return

    if (
      !product.code.trim() ||
      !product.name.trim() ||
      product.productCategoryId < 1 ||
      product.unitOfMeasureId < 1
    ) {
      setError('Uzupełnij wymagane pola.')
      return
    }

    const selectedCategory = categories.find(
      item => item.id === product.productCategoryId,
    )

    const selectedUnit = units.find(
      item => item.id === product.unitOfMeasureId,
    )

    if (!selectedCategory?.isActive || !selectedUnit?.isActive) {
      setError(
        'Wybrana kategoria lub jednostka miary jest nieaktywna. ' +
        'Wybierz aktywne pozycje przed zapisaniem produktu.',
      )
      return
    }

    setSaving(true)
    setError(null)

    try {
      const response = await fetch(`/api/products/${product.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          code: product.code.trim(),
          name: product.name.trim(),
          description: product.description?.trim() || null,
          ean: product.ean?.trim() || null,
          productCategoryId: product.productCategoryId,
          unitOfMeasureId: product.unitOfMeasureId,
          isActive: product.isActive,
        }),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }

      if (response.status === 403) {
        throw new Error(
          'Nie masz uprawnień do edycji produktów.'
        )
      }

      if (response.status === 404) {
        throw new Error('Produkt nie został znaleziony.')
      }

      if (!response.ok) {
        throw new Error(
          await readApiError(
            response,
            'Nie udało się zapisać zmian produktu',
          ),
        )
      }

      onSaved()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Wystąpił błąd zapisu.'
      )
    } finally {
      setSaving(false)
    }
  }

  function updateProduct(changes: Partial<ProductDetail>) {
    setProduct(current =>
      current ? { ...current, ...changes } : current
    )
  }

  if (loading) {
    return (
      <section className="product-editor">
        Ładowanie danych produktu...
      </section>
    )
  }

  if (!product) {
    return (
      <section className="product-editor">
        {error && (
          <div className="error-message" role="alert">
            {error}
          </div>
        )}

        <button
          className="button-secondary"
          type="button"
          onClick={onCancel}
        >
          Zamknij
        </button>
      </section>
    )
  }

  const disabled = saving

  return (
    <section className="product-editor" aria-label="Edycja produktu">
      <h3>Edytuj produkt: {product.code}</h3>

      <p className="product-editor-description">
        Zmień dane produktu i zapisz. Pola oznaczone * są wymagane.
      </p>

      {error && (
        <div className="error-message" role="alert">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div className="product-editor-grid">
          <div className="field">
            <label htmlFor="edit-code">Kod produktu *</label>
            <input
              id="edit-code"
              value={product.code}
              onChange={event =>
                updateProduct({ code: event.target.value })
              }
              maxLength={50}
              required
              disabled={disabled}
            />
          </div>

          <div className="field">
            <label htmlFor="edit-name">Nazwa produktu *</label>
            <input
              id="edit-name"
              value={product.name}
              onChange={event =>
                updateProduct({ name: event.target.value })
              }
              maxLength={200}
              required
              disabled={disabled}
            />
          </div>

          <div className="field">
            <label htmlFor="edit-ean">EAN</label>
            <input
              id="edit-ean"
              value={product.ean ?? ''}
              onChange={event =>
                updateProduct({ ean: event.target.value })
              }
              maxLength={32}
              disabled={disabled}
            />
          </div>

          <div className="field">
            <label htmlFor="edit-category">Kategoria *</label>
            <select
              id="edit-category"
              value={product.productCategoryId}
              onChange={event =>
                updateProduct({
                  productCategoryId: Number(event.target.value),
                })
              }
              required
              disabled={disabled}
            >
              <option value={0}>Wybierz kategorię</option>
              {categories.map(category => (
                <option
                  key={category.id}
                  value={category.id}
                  disabled={!category.isActive}
                >
                  {category.name}
                  {!category.isActive ? ' (nieaktywna)' : ''}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="edit-unit">Jednostka miary *</label>
            <select
              id="edit-unit"
              value={product.unitOfMeasureId}
              onChange={event =>
                updateProduct({
                  unitOfMeasureId: Number(event.target.value),
                })
              }
              required
              disabled={disabled}
            >
              <option value={0}>Wybierz jednostkę</option>
              {units.map(unit => (
                <option
                  key={unit.id}
                  value={unit.id}
                  disabled={!unit.isActive}
                >
                  {unit.name}
                  {!unit.isActive ? ' (nieaktywna)' : ''}
                </option>
              ))}
            </select>
          </div>

          <div className="field product-description-field">
            <label htmlFor="edit-description">Opis</label>
            <textarea
              id="edit-description"
              rows={3}
              maxLength={500}
              value={product.description ?? ''}
              onChange={event =>
                updateProduct({
                  description: event.target.value,
                })
              }
              disabled={disabled}
            />
          </div>
        </div>

        <div className="product-editor-actions">
          <button
            type="button"
            className="button-secondary"
            onClick={onCancel}
            disabled={saving}
          >
            Anuluj
          </button>

          <button
            type="submit"
            className="button-primary"
            disabled={
              saving ||
              categories.length === 0 ||
              units.length === 0
            }
          >
            {saving ? 'Zapisywanie...' : 'Zapisz zmiany'}
          </button>
        </div>
      </form>
    </section>
  )
}
