
import { useEffect, useState, type FormEvent } from 'react'
import './ProductCreateForm.css'
import { readApiError } from './goodsReceiptsApi'

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
  token: string
  onSaved: () => void
  onCancel: () => void
  onUnauthorized: () => void
}

async function getLookupItems(
  endpoint: string,
  token: string,
  signal: AbortSignal
): Promise<LookupItem[]> {
  const items: LookupItem[] = []
  let page = 1
  let total: number

  do {
    const response = await fetch(
      `${endpoint}?Page=${page}&PageSize=100&IsActive=true`,
      {
        headers: { Authorization: `Bearer ${token}` },
        signal,
      }
    )

    if (response.status === 401) {
      throw new Error('SESSION_EXPIRED')
    }
    if (!response.ok) {
      throw new Error(`Nie udało się pobrać danych (HTTP ${response.status}).`)
    }

    const result = (await response.json()) as PagedResult<LookupItem>
    items.push(...result.items)
    total = result.totalCount

    if (result.items.length === 0) {
      break
    }

    page += 1
  } while (items.length < total)

  return items
}

export default function ProductCreateForm({
  token,
  onSaved,
  onCancel,
  onUnauthorized,
}: Props) {
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [ean, setEan] = useState('')
  const [categoryId, setCategoryId] = useState(0)
  const [unitId, setUnitId] = useState(0)
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [units, setUnits] = useState<LookupItem[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadLookups() {
      try {
        const [categoryItems, unitItems] = await Promise.all([
          getLookupItems('/api/product-categories', token, controller.signal),
          getLookupItems('/api/units-of-measure', token, controller.signal),
        ])
        if (!controller.signal.aborted) {
          setCategories(categoryItems)
          setUnits(unitItems)
        }
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof Error && err.message === 'SESSION_EXPIRED') {
          onUnauthorized()
          return
        }
        setError(
          err instanceof Error ? err.message : 'Nie można pobrać słowników.'
        )
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void loadLookups()
    return () => controller.abort()
  }, [token, onUnauthorized])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!code.trim() || !name.trim() || categoryId < 1 || unitId < 1) {
      setError('Uzupełnij kod, nazwę, kategorię i jednostkę miary.')
      return
    }

    setSaving(true)
    try {
      const response = await fetch('/api/products', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          code: code.trim(),
          name: name.trim(),
          description: description.trim() || null,
          ean: ean.trim() || null,
          productCategoryId: categoryId,
          unitOfMeasureId: unitId,
        }),
      })

      if (response.status === 401) {
        onUnauthorized()
        return
      }
      if (response.status === 403) {
        throw new Error('Nie masz uprawnień do dodawania produktów.')
      }
      if (!response.ok) {
        throw new Error(
          await readApiError(
            response,
            'Nie udało się dodać produktu',
          ),
        )
      }

      onSaved()
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Nieznany błąd zapisu.'
      )
    } finally {
      setSaving(false)
    }
  }

  const disabled = loading || saving

  return (
    <section className="product-editor" aria-label="Dodawanie produktu">
      <h3>Dodaj nowy produkt</h3>
      <p className="product-editor-description">
        Pola oznaczone gwiazdką (*) są wymagane.
      </p>

      {error && <div className="error-message" role="alert">{error}</div>}
      {loading && <p>Ładowanie kategorii i jednostek miary...</p>}

      {!loading && (categories.length === 0 || units.length === 0) && (
        <p role="status">
          Brakuje aktywnych kategorii lub jednostek miary w bazie.
          Dodaj je w backendzie, zanim utworzysz produkt.
        </p>
      )}

      <form onSubmit={handleSubmit}>
        <div className="product-editor-grid">
          <div className="field">
            <label htmlFor="create-code">Kod produktu *</label>
            <input id="create-code" value={code}
              onChange={event => setCode(event.target.value)}
              placeholder="np. ART-002" maxLength={50}
              required disabled={disabled} />
          </div>

          <div className="field">
            <label htmlFor="create-name">Nazwa produktu *</label>
            <input id="create-name" value={name}
              onChange={event => setName(event.target.value)}
              placeholder="np. Produkt testowy" maxLength={200}
              required disabled={disabled} />
          </div>

          <div className="field">
            <label htmlFor="create-ean">EAN</label>
            <input id="create-ean" value={ean}
              onChange={event => setEan(event.target.value)}
              placeholder="Kod kreskowy" maxLength={32}
              disabled={disabled} />
          </div>

          <div className="field">
            <label htmlFor="create-category">Kategoria *</label>
            <select id="create-category" value={categoryId}
              onChange={event => setCategoryId(Number(event.target.value))}
              required disabled={disabled}>
              <option value={0}>Wybierz kategorię</option>
              {categories.map(item => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="create-unit">Jednostka miary *</label>
            <select id="create-unit" value={unitId}
              onChange={event => setUnitId(Number(event.target.value))}
              required disabled={disabled}>
              <option value={0}>Wybierz jednostkę</option>
              {units.map(item => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </div>

          <div className="field product-description-field">
            <label htmlFor="create-description">Opis</label>
            <textarea id="create-description" rows={3}
              value={description}
              onChange={event => setDescription(event.target.value)}
              maxLength={500} disabled={disabled} />
          </div>
        </div>

        <div className="product-editor-actions">
          <button type="button" className="button-secondary"
            onClick={onCancel} disabled={saving}>Anuluj</button>
          <button type="submit" className="button-primary"
            disabled={disabled || !categories.length || !units.length}>
            {saving ? 'Zapisywanie...' : 'Zapisz produkt'}
          </button>
        </div>
      </form>
    </section>
  )
}
