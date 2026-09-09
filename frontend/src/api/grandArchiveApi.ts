export type GrandArchiveStatus = 'active' | 'withdrawn'
export type GrandArchiveRecordType = 'decree' | 'communication' | 'minutes' | 'resolution' | 'regulation' | 'correspondence' | 'historical_record' | 'other'

export interface GrandArchiveRecord {
  id: string
  archiveCode: string
  documentId: string
  documentVersionId: string
  recordType: GrandArchiveRecordType | string
  documentDate: string | null
  originatingBody: string | null
  historicalPeriod: string | null
  description: string | null
  status: GrandArchiveStatus | string
  archivedAtUtc: string
  createdByDisplayName: string | null
  withdrawnAtUtc: string | null
  withdrawnByDisplayName: string | null
  withdrawalReason: string | null
  title: string
  documentType: string
  classification: string
  documentStatus: string
  versionNumber: number | null
  originalFileName: string | null
  contentType: string | null
  sizeBytes: number | null
  processingStatus: string
}

export interface GrandArchiveCandidate {
  documentId: string
  documentVersionId: string
  title: string
  documentType: string
  classification: string
  documentStatus: string
  versionNumber: number
  originalFileName: string
  sizeBytes: number
  createdAtUtc: string
}

export interface GrandArchiveListResponse { total: number; returned: number; items: GrandArchiveRecord[] }
export interface GrandArchiveCandidatesResponse { total: number; items: GrandArchiveCandidate[] }
export interface GrandArchiveFilters { status?: string; recordType?: string; fromDate?: string; toDate?: string; search?: string; limit?: number }
export interface RegisterGrandArchiveInput {
  documentId: string
  documentVersionId: string
  archiveCode: string
  recordType: GrandArchiveRecordType
  documentDate?: string | null
  originatingBody?: string | null
  historicalPeriod?: string | null
  description?: string | null
}

export type GrandArchiveTokenProvider = () => Promise<string | null>
interface GrandArchiveApiOptions { baseUrl?: string; getAccessToken?: GrandArchiveTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

export class GrandArchiveApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: GrandArchiveTokenProvider
  private readonly onUnauthorized?: () => Promise<void>
  readonly useMocks: boolean

  constructor(options: GrandArchiveApiOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.onUnauthorized = options.onUnauthorized
    this.useMocks = options.useMocks ?? false
  }

  async list(filters: GrandArchiveFilters = {}): Promise<GrandArchiveListResponse> {
    if (this.useMocks) return mockList(filters)
    const query = new URLSearchParams()
    if (filters.status) query.set('status', filters.status)
    if (filters.recordType) query.set('recordType', filters.recordType)
    if (filters.fromDate) query.set('fromDate', filters.fromDate)
    if (filters.toDate) query.set('toDate', filters.toDate)
    if (filters.search?.trim()) query.set('search', filters.search.trim())
    if (filters.limit) query.set('limit', String(filters.limit))
    return this.request<GrandArchiveListResponse>(`/api/grand-archive/${query.size ? `?${query}` : ''}`)
  }

  async candidates(search?: string): Promise<GrandArchiveCandidatesResponse> {
    if (this.useMocks) {
      const term = normalize(search)
      const items = mockCandidates.filter(item => !term || normalize(`${item.title} ${item.documentType} ${item.originalFileName}`).includes(term)).map(clone)
      return { total: items.length, items }
    }
    const query = new URLSearchParams(); if (search?.trim()) query.set('search', search.trim()); query.set('limit', '100')
    return this.request<GrandArchiveCandidatesResponse>(`/api/grand-archive/candidates?${query}`)
  }

  async register(input: RegisterGrandArchiveInput): Promise<GrandArchiveRecord> {
    if (this.useMocks) return mockRegister(input)
    return this.request<GrandArchiveRecord>('/api/grand-archive/', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) })
  }

  async withdraw(recordId: string, reason: string): Promise<GrandArchiveRecord> {
    if (this.useMocks) return mockWithdraw(recordId, reason)
    return this.request<GrandArchiveRecord>(`/api/grand-archive/${encodeURIComponent(recordId)}/withdraw`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason }) })
  }

  async download(recordId: string): Promise<Blob> {
    if (this.useMocks) return new Blob(['Contenido demostrativo de Gran Archivero'], { type: 'text/plain' })
    const response = await this.authorizedFetch(`/api/grand-archive/${encodeURIComponent(recordId)}/content`, {}, 'application/octet-stream')
    return response.blob()
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await this.authorizedFetch(path, init)
    return response.json() as Promise<T>
  }

  private async authorizedFetch(path: string, init: RequestInit = {}, accept = 'application/json'): Promise<Response> {
    const headers = new Headers(init.headers); headers.set('Accept', accept)
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para operar Gran Archivero.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', cache: 'no-store', redirect: 'error', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = body.message ?? '' } catch { /* sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para operar Gran Archivero.'
      throw new Error(message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response
  }
}

export function createDefaultGrandArchiveApiClient(getAccessToken?: GrandArchiveTokenProvider, onUnauthorized?: () => Promise<void>): GrandArchiveApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Gran Archivero debe usar el mismo origen mediante el proxy institucional.')
  return new GrandArchiveApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

let mockRecords: GrandArchiveRecord[] = [{
  id: 'ga-demo-1', archiveCode: 'GA-2026-001', documentId: 'doc-demo-1', documentVersionId: 'ver-demo-1', recordType: 'historical_record', documentDate: '1986-07-12', originatingBody: 'Gran Logia Mixta de Chile', historicalPeriod: '1980–1989', description: 'Documento institucional histórico demostrativo.', status: 'active', archivedAtUtc: '2026-09-09T18:00:00Z', createdByDisplayName: 'Gran Archivero QA', withdrawnAtUtc: null, withdrawnByDisplayName: null, withdrawalReason: null, title: 'Acta histórica demostrativa', documentType: 'historical_record', classification: 'confidential', documentStatus: 'active', versionNumber: 1, originalFileName: 'acta-historica-demo.pdf', contentType: 'application/pdf', sizeBytes: 245000, processingStatus: 'available'
}]

const mockCandidates: GrandArchiveCandidate[] = [
  { documentId: 'doc-demo-2', documentVersionId: 'ver-demo-2', title: 'Decreto demostrativo 2026', documentType: 'decree', classification: 'confidential', documentStatus: 'active', versionNumber: 1, originalFileName: 'decreto-demo.pdf', sizeBytes: 155000, createdAtUtc: '2026-09-08T15:00:00Z' },
  { documentId: 'doc-demo-3', documentVersionId: 'ver-demo-3', title: 'Comunicación institucional demostrativa', documentType: 'communication', classification: 'internal', documentStatus: 'active', versionNumber: 1, originalFileName: 'comunicacion-demo.pdf', sizeBytes: 92000, createdAtUtc: '2026-09-08T16:00:00Z' },
]

function mockList(filters: GrandArchiveFilters): GrandArchiveListResponse {
  let items = mockRecords.map(clone)
  if (filters.status) items = items.filter(x => x.status === filters.status)
  if (filters.recordType) items = items.filter(x => x.recordType === filters.recordType)
  if (filters.search?.trim()) { const term = normalize(filters.search); items = items.filter(x => normalize(`${x.archiveCode} ${x.title} ${x.originatingBody ?? ''} ${x.historicalPeriod ?? ''}`).includes(term)) }
  items.sort((a, b) => (b.documentDate ?? '').localeCompare(a.documentDate ?? '') || b.archivedAtUtc.localeCompare(a.archivedAtUtc))
  const total = items.length; items = items.slice(0, Math.min(filters.limit ?? 200, 500)); return { total, returned: items.length, items }
}
function mockRegister(input: RegisterGrandArchiveInput): GrandArchiveRecord {
  const candidate = mockCandidates.find(x => x.documentId === input.documentId && x.documentVersionId === input.documentVersionId)
  if (!candidate) throw new Error('La versión documental no está disponible para incorporación archivística.')
  if (mockRecords.some(x => x.archiveCode.toUpperCase() === input.archiveCode.trim().toUpperCase())) throw new Error('Ya existe un registro con ese código archivístico.')
  const row: GrandArchiveRecord = { id: crypto.randomUUID(), archiveCode: input.archiveCode.trim().toUpperCase(), documentId: candidate.documentId, documentVersionId: candidate.documentVersionId, recordType: input.recordType, documentDate: input.documentDate ?? null, originatingBody: input.originatingBody?.trim() || null, historicalPeriod: input.historicalPeriod?.trim() || null, description: input.description?.trim() || null, status: 'active', archivedAtUtc: new Date().toISOString(), createdByDisplayName: 'Gran Archivero QA', withdrawnAtUtc: null, withdrawnByDisplayName: null, withdrawalReason: null, title: candidate.title, documentType: candidate.documentType, classification: candidate.classification, documentStatus: candidate.documentStatus, versionNumber: candidate.versionNumber, originalFileName: candidate.originalFileName, contentType: 'application/pdf', sizeBytes: candidate.sizeBytes, processingStatus: 'available' }
  mockRecords = [row, ...mockRecords]; return clone(row)
}
function mockWithdraw(recordId: string, reason: string): GrandArchiveRecord { const row = mockRecords.find(x => x.id === recordId); if (!row) throw new Error('El registro archivístico no existe.'); if (row.status !== 'active') throw new Error('El registro ya fue retirado del catálogo activo.'); row.status = 'withdrawn'; row.withdrawnAtUtc = new Date().toISOString(); row.withdrawnByDisplayName = 'Gran Archivero QA'; row.withdrawalReason = reason.trim(); return clone(row) }
function normalize(value?: string) { return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T }
