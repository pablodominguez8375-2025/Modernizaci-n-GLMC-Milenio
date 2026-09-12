export type DocumentScope = 'order' | 'organization'
export type DocumentClassification = 'internal' | 'confidential' | 'sensitive' | 'restricted'
export type DocumentAccessPolicy = 'library_authenticated' | 'organization_authenticated' | 'management_only'
export type DocumentStatus = 'draft' | 'active' | 'published' | 'retired'
export type DocumentProcessingStatus = 'pending_upload' | 'uploaded' | 'scanning' | 'available' | 'rejected'

export interface DocumentCollection {
  id: string; code: string; name: string; description: string | null; scope: DocumentScope; organizationId: string | null; status: string
}
export interface DocumentCollectionsResponse { total: number; items: DocumentCollection[] }
export interface DocumentListItem {
  id: string; collectionId: string; organizationId: string | null; title: string; documentType: string; classification: DocumentClassification; accessPolicy: DocumentAccessPolicy; status: DocumentStatus; publishedVersionId: string | null; publishedAtUtc: string | null
}
export interface DocumentListResponse { total: number; items: DocumentListItem[] }
export interface DocumentVersion {
  id: string; documentId: string; versionNumber: number; originalFileName: string; contentType: string; sizeBytes: number; processingStatus: DocumentProcessingStatus; hasIntegrityHash: boolean; scanEvidenceRecorded: boolean; createdAtUtc: string
}
export interface InstitutionalDocument extends DocumentListItem { collectionCode: string; versions: DocumentVersion[] }

export interface LibraryDocument {
  id: string
  title: string
  documentType: string
  collectionName: string
  versionNumber: number
  contentType: string
  sizeBytes: number
  publishedAtUtc: string
  minimumDegreeRequired?: number | null
  authorName?: string | null
  authorLodgeName?: string | null
  documentDate?: string | null
  topic?: string | null
  edition?: string | null
  shortDescription?: string | null
  abstractText?: string | null
  officialDocumentType?: string | null
}
export interface LibraryDocumentsResponse { total: number; items: LibraryDocument[] }
export interface LibraryCatalogItem extends LibraryDocument { collectionId: string }
export interface LibraryCatalogResponse { total: number; page: number; pageSize: number; items: LibraryCatalogItem[] }
export interface LibraryFacetItem { value: string; label: string; count: number }
export interface LibraryFacetsResponse {
  collections: LibraryFacetItem[]
  documentTypes: LibraryFacetItem[]
  degrees: LibraryFacetItem[]
  topics: LibraryFacetItem[]
  officialDocumentTypes: LibraryFacetItem[]
}
export interface LibrarySearchParams {
  q?: string
  collectionId?: string
  documentType?: string
  degree?: number
  topic?: string
  officialDocumentType?: string
  fromYear?: number
  toYear?: number
  page?: number
  pageSize?: number
}
export interface LibraryCatalogMetadataRequest {
  minimumDegreeRequired: number | null
  authorName?: string | null
  authorLodgeName?: string | null
  documentDate?: string | null
  topic?: string | null
  edition?: string | null
  shortDescription?: string | null
  abstractText?: string | null
  officialDocumentType?: string | null
}
export interface LibraryCatalogMetadata extends LibraryCatalogMetadataRequest {
  documentId: string
  catalogKind: string
}
export interface CreateDocumentCollectionRequest { code: string; name: string; description?: string | null; scope: DocumentScope; organizationId?: string | null }
export interface CreateInstitutionalDocumentRequest { title: string; documentType: string; classification: DocumentClassification; accessPolicy: DocumentAccessPolicy }
export interface CreateDocumentVersionRequest { originalFileName: string; contentType: string; sizeBytes: number }
export type DocumentAccessTokenProvider = () => Promise<string | null>

interface DocumentApiClientOptions { baseUrl?: string; getAccessToken?: DocumentAccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

class DocumentApiHttpError extends Error {
  constructor(readonly status: number, message: string) { super(message); this.name = 'DocumentApiHttpError' }
}

const demoCollection: DocumentCollection = {
  id: 'dddddddd-1111-1111-1111-111111111111',
  code: 'BIB-VIRTUAL',
  name: 'Biblioteca Virtual',
  description: 'Catálogo institucional demostrativo',
  scope: 'order',
  organizationId: null,
  status: 'active',
}
const demoLibrary: LibraryDocument[] = [
  {
    id: 'eeeeeeee-1111-1111-1111-111111111111',
    title: 'El simbolismo de la piedra bruta',
    documentType: 'work_paper',
    collectionName: demoCollection.name,
    versionNumber: 1,
    contentType: 'application/pdf',
    sizeBytes: 385000,
    publishedAtUtc: '2026-09-08T15:00:00Z',
    minimumDegreeRequired: 1,
    authorName: 'Hno. Andrés Pérez',
    authorLodgeName: 'R∴L∴S∴ Aurora N° 12',
    documentDate: '2026-08-21',
    shortDescription: 'Reflexión breve sobre el trabajo interior del Aprendiz y el valor simbólico de la piedra bruta.',
  },
  {
    id: 'eeeeeeee-2222-2222-2222-222222222222',
    title: 'Historia de la masonería simbólica en Chile',
    documentType: 'book',
    collectionName: demoCollection.name,
    versionNumber: 2,
    contentType: 'application/pdf',
    sizeBytes: 2485000,
    publishedAtUtc: '2026-09-07T15:00:00Z',
    minimumDegreeRequired: 1,
    authorName: 'Autor demostrativo',
    topic: 'Historia masónica',
    edition: '2ª edición',
    abstractText: 'Síntesis histórica demostrativa sobre el desarrollo de la masonería simbólica, sus instituciones y principales procesos en Chile.',
  },
  {
    id: 'eeeeeeee-3333-3333-3333-333333333333',
    title: 'Constitución de la Gran Logia Mixta',
    documentType: 'official_document',
    collectionName: demoCollection.name,
    versionNumber: 4,
    contentType: 'application/pdf',
    sizeBytes: 920000,
    publishedAtUtc: '2026-09-06T15:00:00Z',
    minimumDegreeRequired: null,
    documentDate: '2026-01-01',
    officialDocumentType: 'Constitución',
  },
  {
    id: 'eeeeeeee-4444-4444-4444-444444444444',
    title: 'Ritual de Segundo Grado',
    documentType: 'official_document',
    collectionName: demoCollection.name,
    versionNumber: 3,
    contentType: 'application/pdf',
    sizeBytes: 1120000,
    publishedAtUtc: '2026-09-05T15:00:00Z',
    minimumDegreeRequired: 2,
    documentDate: '2026-03-15',
    officialDocumentType: 'Ritual',
  },
]

export class DocumentApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: DocumentAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockCollections: DocumentCollection[] = [{ ...demoCollection }]
  private readonly mockDocuments = new Map<string, InstitutionalDocument>()
  private readonly mockLibrary: LibraryDocument[] = demoLibrary.map(item => ({ ...item }))

  constructor(options: DocumentApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getLibrary(): Promise<LibraryDocumentsResponse> {
    if (this.useMocks) return { total: this.mockLibrary.length, items: this.mockLibrary.map(item => ({ ...item })) }
    return this.request<LibraryDocumentsResponse>('/api/biblioteca')
  }

  async searchLibrary(params: LibrarySearchParams = {}): Promise<LibraryCatalogResponse> {
    if (this.useMocks) {
      const query = normalizeSearch(params.q)
      let items = this.mockLibrary.map(item => this.toCatalogItem(item))
      if (query) {
        items = items.filter(item => normalizeSearch([
          item.title,
          item.documentType,
          item.collectionName,
          item.authorName,
          item.authorLodgeName,
          item.topic,
          item.edition,
          item.shortDescription,
          item.abstractText,
          item.officialDocumentType,
        ].filter(Boolean).join(' ')).includes(query))
      }
      if (params.collectionId) items = items.filter(item => item.collectionId === params.collectionId)
      if (params.documentType) items = items.filter(item => catalogTypeMatches(item.documentType, params.documentType!))
      if (params.degree) items = items.filter(item => item.minimumDegreeRequired === params.degree)
      if (params.topic) items = items.filter(item => normalizeSearch(item.topic ?? '') === normalizeSearch(params.topic))
      if (params.officialDocumentType) items = items.filter(item => normalizeSearch(item.officialDocumentType ?? '') === normalizeSearch(params.officialDocumentType))
      if (params.fromYear) items = items.filter(item => new Date(item.publishedAtUtc).getUTCFullYear() >= params.fromYear!)
      if (params.toYear) items = items.filter(item => new Date(item.publishedAtUtc).getUTCFullYear() <= params.toYear!)
      items.sort((a, b) => b.publishedAtUtc.localeCompare(a.publishedAtUtc) || a.title.localeCompare(b.title, 'es'))
      const page = Math.max(1, params.page ?? 1)
      const pageSize = Math.min(50, Math.max(1, params.pageSize ?? 24))
      return { total: items.length, page, pageSize, items: items.slice((page - 1) * pageSize, page * pageSize).map(item => ({ ...item })) }
    }

    const query = new URLSearchParams()
    if (params.q?.trim()) query.set('q', params.q.trim())
    if (params.collectionId) query.set('collectionId', params.collectionId)
    if (params.documentType?.trim()) query.set('documentType', params.documentType.trim())
    if (params.degree) query.set('degree', String(params.degree))
    if (params.topic?.trim()) query.set('topic', params.topic.trim())
    if (params.officialDocumentType?.trim()) query.set('officialDocumentType', params.officialDocumentType.trim())
    if (params.fromYear) query.set('fromYear', String(params.fromYear))
    if (params.toYear) query.set('toYear', String(params.toYear))
    if (params.page) query.set('page', String(params.page))
    if (params.pageSize) query.set('pageSize', String(params.pageSize))
    return this.request<LibraryCatalogResponse>(`/api/biblioteca/buscar${query.size ? `?${query}` : ''}`)
  }

  async getLibraryFacets(): Promise<LibraryFacetsResponse> {
    if (this.useMocks) {
      const catalog = this.mockLibrary.map(item => this.toCatalogItem(item))
      const collectionCounts = new Map<string, LibraryFacetItem>()
      const typeCounts = new Map<string, LibraryFacetItem>()
      const degreeCounts = new Map<string, LibraryFacetItem>()
      const topicCounts = new Map<string, LibraryFacetItem>()
      const officialTypeCounts = new Map<string, LibraryFacetItem>()
      for (const item of catalog) {
        const collection = collectionCounts.get(item.collectionId)
        collectionCounts.set(item.collectionId, { value: item.collectionId, label: item.collectionName, count: (collection?.count ?? 0) + 1 })

        const canonicalType = canonicalCatalogType(item.documentType)
        const type = typeCounts.get(canonicalType)
        typeCounts.set(canonicalType, { value: canonicalType, label: canonicalType, count: (type?.count ?? 0) + 1 })

        const degreeValue = item.minimumDegreeRequired?.toString() ?? 'general'
        const degree = degreeCounts.get(degreeValue)
        degreeCounts.set(degreeValue, { value: degreeValue, label: item.minimumDegreeRequired ? `${item.minimumDegreeRequired}° grado` : 'General', count: (degree?.count ?? 0) + 1 })

        if (item.topic) {
          const topic = topicCounts.get(item.topic)
          topicCounts.set(item.topic, { value: item.topic, label: item.topic, count: (topic?.count ?? 0) + 1 })
        }
        if (item.officialDocumentType) {
          const officialType = officialTypeCounts.get(item.officialDocumentType)
          officialTypeCounts.set(item.officialDocumentType, { value: item.officialDocumentType, label: item.officialDocumentType, count: (officialType?.count ?? 0) + 1 })
        }
      }
      return {
        collections: [...collectionCounts.values()],
        documentTypes: [...typeCounts.values()],
        degrees: [...degreeCounts.values()],
        topics: [...topicCounts.values()],
        officialDocumentTypes: [...officialTypeCounts.values()],
      }
    }
    return this.request<LibraryFacetsResponse>('/api/biblioteca/facetas')
  }

  async setLibraryCatalogMetadata(documentId: string, payload: LibraryCatalogMetadataRequest): Promise<LibraryCatalogMetadata> {
    if (this.useMocks) return { documentId, catalogKind: 'Mock', ...payload }
    return this.putJson<LibraryCatalogMetadata>(`/api/documentos/${encodeURIComponent(documentId)}/metadatos-biblioteca`, payload)
  }

  async downloadLibraryDocument(documentId: string): Promise<Blob> {
    if (this.useMocks) return new Blob(['Contenido demostrativo de Biblioteca Virtual'], { type: 'text/plain' })
    const response = await this.authorizedFetch(`/api/biblioteca/${encodeURIComponent(documentId)}/contenido`, {}, 'application/octet-stream')
    return response.blob()
  }

  async getCollections(organizationId?: string): Promise<DocumentCollectionsResponse> {
    if (this.useMocks) {
      const items = this.mockCollections.filter(item => !organizationId || item.organizationId === organizationId).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    const query = new URLSearchParams(); if (organizationId) query.set('organizationId', organizationId)
    return this.request<DocumentCollectionsResponse>(`/api/documentos/colecciones${query.size ? `?${query}` : ''}`)
  }

  async createCollection(payload: CreateDocumentCollectionRequest): Promise<DocumentCollection> {
    if (this.useMocks) {
      const collection: DocumentCollection = { id: crypto.randomUUID(), code: payload.code.trim().toUpperCase(), name: payload.name.trim(), description: payload.description?.trim() || null, scope: payload.scope, organizationId: payload.scope === 'organization' ? payload.organizationId ?? null : null, status: 'active' }
      this.mockCollections.push(collection); return { ...collection }
    }
    return this.postJson<DocumentCollection>('/api/documentos/colecciones', payload)
  }

  async getDocuments(collectionId: string): Promise<DocumentListResponse> {
    if (this.useMocks) {
      const items = [...this.mockDocuments.values()]
        .filter(item => item.collectionId === collectionId)
        .map(item => toListItem(item))
      return { total: items.length, items }
    }
    return this.request<DocumentListResponse>(`/api/documentos/colecciones/${encodeURIComponent(collectionId)}/documentos`)
  }

  async createDocument(collectionId: string, payload: CreateInstitutionalDocumentRequest): Promise<InstitutionalDocument> {
    if (this.useMocks) {
      const collection = this.mockCollections.find(item => item.id === collectionId)
      if (!collection) throw new Error('La colección indicada no existe.')
      const document: InstitutionalDocument = { id: crypto.randomUUID(), collectionId, collectionCode: collection.code, organizationId: collection.organizationId, title: payload.title.trim(), documentType: payload.documentType.trim(), classification: payload.classification, accessPolicy: payload.accessPolicy, status: 'draft', publishedVersionId: null, publishedAtUtc: null, versions: [] }
      this.mockDocuments.set(document.id, document); return cloneDocument(document)
    }
    return this.postJson<InstitutionalDocument>(`/api/documentos/colecciones/${encodeURIComponent(collectionId)}/documentos`, payload)
  }

  async getDocument(documentId: string): Promise<InstitutionalDocument> {
    if (this.useMocks) { const document = this.mockDocuments.get(documentId); if (!document) throw new Error('El documento indicado no existe.'); return cloneDocument(document) }
    return this.request<InstitutionalDocument>(`/api/documentos/${encodeURIComponent(documentId)}`)
  }

  async createVersion(documentId: string, payload: CreateDocumentVersionRequest): Promise<DocumentVersion> {
    if (this.useMocks) {
      const document = this.mockDocuments.get(documentId); if (!document) throw new Error('El documento indicado no existe.')
      const version: DocumentVersion = { id: crypto.randomUUID(), documentId, versionNumber: Math.max(0, ...document.versions.map(item => item.versionNumber)) + 1, originalFileName: payload.originalFileName.trim(), contentType: payload.contentType.trim(), sizeBytes: payload.sizeBytes, processingStatus: 'pending_upload', hasIntegrityHash: false, scanEvidenceRecorded: false, createdAtUtc: new Date().toISOString() }
      document.versions.unshift(version); return { ...version }
    }
    return this.postJson<DocumentVersion>(`/api/documentos/${encodeURIComponent(documentId)}/versiones`, payload)
  }

  async publishDocument(documentId: string, versionId: string): Promise<LibraryDocument> {
    if (this.useMocks) {
      const document = this.mockDocuments.get(documentId); if (!document) throw new Error('El documento indicado no existe.')
      const version = document.versions.find(item => item.id === versionId); if (!version || version.processingStatus !== 'available') throw new Error('Sólo puede publicarse una versión disponible.')
      const collection = this.mockCollections.find(item => item.id === document.collectionId); if (!collection) throw new Error('La colección indicada no existe.')
      const publishedAtUtc = new Date().toISOString()
      document.status = 'published'; document.publishedVersionId = versionId; document.publishedAtUtc = publishedAtUtc
      const row: LibraryDocument = { id: document.id, title: document.title, documentType: document.documentType, collectionName: collection.name, versionNumber: version.versionNumber, contentType: version.contentType, sizeBytes: version.sizeBytes, publishedAtUtc }
      const existing = this.mockLibrary.findIndex(item => item.id === documentId); if (existing >= 0) this.mockLibrary.splice(existing, 1)
      this.mockLibrary.unshift(row); return { ...row }
    }
    return this.postJson<LibraryDocument>(`/api/documentos/${encodeURIComponent(documentId)}/publicar`, { versionId })
  }

  async unpublishDocument(documentId: string): Promise<{ id: string; status: string }> {
    if (this.useMocks) {
      const document = this.mockDocuments.get(documentId); if (!document) throw new Error('El documento indicado no existe.')
      document.status = 'active'; document.publishedVersionId = null; document.publishedAtUtc = null
      const index = this.mockLibrary.findIndex(item => item.id === documentId); if (index >= 0) this.mockLibrary.splice(index, 1)
      return { id: documentId, status: document.status }
    }
    return this.request<{ id: string; status: string }>(`/api/documentos/${encodeURIComponent(documentId)}/retirar-publicacion`, { method: 'POST' })
  }

  private toCatalogItem(item: LibraryDocument): LibraryCatalogItem {
    const document = this.mockDocuments.get(item.id)
    const collectionId = document?.collectionId ?? this.mockCollections.find(collection => collection.name === item.collectionName)?.id ?? demoCollection.id
    return { ...item, collectionId }
  }

  private postJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private putJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await this.authorizedFetch(path, init, 'application/json')
    return response.json() as Promise<T>
  }

  private async authorizedFetch(path: string, init: RequestInit = {}, accept = 'application/json'): Promise<Response> {
    const headers = new Headers(init.headers); headers.set('Accept', accept)
    const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para consultar documentos institucionales.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para acceder a este recurso documental.'
      throw new DocumentApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response
  }
}

export function createDefaultDocumentApiClient(getAccessToken?: DocumentAccessTokenProvider, onUnauthorized?: () => Promise<void>): DocumentApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Documentos debe usar el mismo origen mediante el proxy institucional.')
  return new DocumentApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function normalizeSearch(value?: string) { return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function canonicalCatalogType(value: string) {
  const normalized = normalizeSearch(value).replace(/\s+/g, '_')
  if (['work_paper', 'working_paper', 'plancha', 'plancha_de_trabajo'].includes(normalized)) return 'work_paper'
  if (['book', 'books', 'libro', 'libros'].includes(normalized)) return 'book'
  if (['official_document', 'documento_oficial', 'regulation', 'reglamento', 'constitution', 'constitucion', 'ritual'].includes(normalized)) return 'official_document'
  if (['video', 'videos'].includes(normalized)) return 'video'
  return normalized
}
function catalogTypeMatches(value: string, requested: string) { return canonicalCatalogType(value) === canonicalCatalogType(requested) }
function cloneDocument(value: InstitutionalDocument): InstitutionalDocument { return { ...value, versions: value.versions.map(item => ({ ...item })) } }
function toListItem(value: InstitutionalDocument): DocumentListItem { return { id: value.id, collectionId: value.collectionId, organizationId: value.organizationId, title: value.title, documentType: value.documentType, classification: value.classification, accessPolicy: value.accessPolicy, status: value.status, publishedVersionId: value.publishedVersionId, publishedAtUtc: value.publishedAtUtc } }
