export type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export type Warehouse = {
  id: number
  code: string
  name: string
  isActive: boolean
}

export type Product = {
  id: number
  code: string
  name: string
  isActive: boolean
  unitOfMeasureSymbol: string
}

export type ProductBatch = {
  id: number
  productId: number
  batchNumber: string
  isActive: boolean
}

export type GoodsReceipt = {
  id: number
  number: string
  warehouseId: number
  warehouseCode: string
  supplierName: string | null
  status: string
  createdAtUtc: string
  receivedAtUtc: string | null
  logisticUnitId: number | null
}

export type GoodsReceiptItem = {
  id: number
  goodsReceiptId: number
  productId: number
  productCode: string
  productName: string
  productBatchId: number | null
  batchNumber: string | null
  quantity: number
}

type ApiProblem = { detail?: string; title?: string }

export class SessionExpiredError extends Error {
  constructor() {
    super('Sesja wygasła.')
  }
}

export async function readApiError(response: Response, fallback: string) {
  if (response.status === 401) return 'Sesja wygasła.'
  if (response.status === 403) return 'Brak uprawnień do tej operacji.'
  if (response.status === 404) return 'Nie znaleziono wskazanego elementu.'
  const problem = (await response.json().catch(() => null)) as ApiProblem | null
  return problem?.detail || `${fallback} (HTTP ${response.status}).`
}

export async function fetchAllPages<T>(
  url: string,
  token: string,
  signal: AbortSignal,
): Promise<T[]> {
  const items: T[] = []
  let page = 1
  let totalCount: number
  do {
    const params = new URLSearchParams({ Page: String(page), PageSize: '100' })
    const separator = url.includes('?') ? '&' : '?'
    const response = await fetch(`${url}${separator}${params}`, {
      headers: { Authorization: `Bearer ${token}` },
      signal,
    })
    if (response.status === 401) throw new SessionExpiredError()
    if (!response.ok) {
      throw new Error(await readApiError(response, 'Nie udało się pobrać danych'))
    }
    const result = (await response.json()) as PagedResult<T>
    items.push(...result.items)
    totalCount = result.totalCount
    if (result.items.length === 0) break
    page += 1
  } while (items.length < totalCount)
  return items
}

export function formatDate(value: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('pl-PL')
}

export function receiptStatus(status: string) {
  return status === 'Draft' ? 'Szkic' : status === 'Received' ? 'Przyjęte' : status
}
