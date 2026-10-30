import type { AreaCollaboratorsDemo, AddAreaCollaborator, AreaCollaboratorCandidates, AreaCollaborators, CollaboratorArea } from './areaCollaborators'
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
export interface EditorialMagazine { documentId: string; title: string; documentType: string; collectionName: string; status: string; minimumDegreeRequired: number | null; authorName: string | null; documentDate: string | null; topic: string | null; edition: string | null; shortDescription: string | null; createdAtUtc: string; publishedAtUtc: string | null; publisherProfile?: 'librarian' | 'communications' | null; canCorrect?: boolean }
/** Publicaciones editoriales de Biblioteca: revista institucional y Boletín mensual. */
export type EditorialType = 'magazine' | 'bulletin'
export interface EditorialMagazineList { total: number; page: number; pageSize: number; items: EditorialMagazine[] }
export interface EditorialMagazineVersion { id: string; versionNumber: number; processingStatus: string; originalFileName: string; contentType: string; sizeBytes: number; createdAtUtc: string }
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
export interface MemberWorkPaperVersion { id: string; versionNumber: number; originalFileName: string; processingStatus: DocumentProcessingStatus; createdAtUtc: string; isCurrent: boolean }
export interface MemberWorkPaper { id: string; title: string; shortDescription: string; minimumDegreeRequired: number; status: DocumentStatus; publishedVersionId: string | null; reviewStatus: WorkPaperReviewStatus; returnReason?: string | null; versions: MemberWorkPaperVersion[] }
/** draft = aún sin enviar a revisión · pending_review = espera al Bibliotecario · returned = devuelta con motivo · approved = en Biblioteca. */
export type WorkPaperReviewStatus = 'draft' | 'pending_review' | 'returned' | 'approved'
export interface ManagedPublication { id: string; title: string; categoryCode: string; status: DocumentStatus; degree: number | null; author: string | null; topic: string | null; edition: string | null; description: string | null; createdAtUtc: string; publishedAtUtc: string | null }
export interface ManagedPublicationInput { categoryCode: string; title: string; authorName?: string; topic?: string; edition?: string; description?: string; minimumDegreeRequired: number | null }
export interface WorkPaperReview { id: string; title: string; authorName: string | null; lodge: string | null; description: string | null; submittedAtUtc: string; degree: number | null; replacesPublished: boolean; versionId: string; fileName: string; sizeBytes: number }
export interface MemberWorkPaperLink { documentId: string; authorMemberId: string | null; title: string; shortDescription: string }
export type DocumentAccessTokenProvider = () => Promise<string | null>

export interface LibraryCategoryDto { value: string; label: string; base: boolean; active: boolean; position: number; publications?: number }
interface DocumentApiClientOptions { baseUrl?: string; getAccessToken?: DocumentAccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

export class DocumentApiHttpError extends Error {
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
  private readonly mockWorkPapers: MemberWorkPaper[] = [{
    id: 'demo-own-work-paper-1', title: 'El trabajo interior del Aprendiz', shortDescription: 'Una reflexión demostrativa sobre disciplina y constancia.', minimumDegreeRequired: 1, status: 'published', publishedVersionId: 'demo-own-work-paper-version-1', reviewStatus: 'approved',
    versions: [{ id: 'demo-own-work-paper-version-1', versionNumber: 1, originalFileName: 'trabajo-aprendiz-demo.pdf', processingStatus: 'available', createdAtUtc: '2026-09-10T15:00:00Z', isCurrent: true }],
  }]

  private mockReviews: WorkPaperReview[] = [
    { id: 'demo-sub-1', title: 'La palabra perdida y el trabajo del Aprendiz', authorName: 'Hermano Ejemplo Uno', lodge: 'Taller Fraternidad N° 3', description: 'Reflexión sobre el silencio y el estudio en el primer grado.', submittedAtUtc: '2026-10-18T15:00:00Z', degree: 1, replacesPublished: false, versionId: 'demo-sub-1-v1', fileName: 'palabra-perdida.pdf', sizeBytes: 380000 },
    { id: 'demo-sub-2', title: 'El simbolismo de la columna Boaz', authorName: 'Hermana Ejemplo Dos', lodge: 'Taller Luz del Sur N° 7', description: 'Estudio comparado de las columnas del Templo.', submittedAtUtc: '2026-10-18T16:00:00Z', degree: 2, replacesPublished: false, versionId: 'demo-sub-2-v1', fileName: 'columna-boaz.pdf', sizeBytes: 410000 },
  ]
  private mockEditorial: EditorialMagazine[] = [
    { documentId: 'demo-ed-boletin-oct', title: 'Boletín de la Orden · Octubre 2026', documentType: 'bulletin', collectionName: 'Publicaciones de la Orden', status: 'published', minimumDegreeRequired: null, authorName: 'Comunicaciones', documentDate: '2026-10-01', topic: null, edition: null, shortDescription: 'Actividades de la Orden durante octubre.', createdAtUtc: '2026-10-02T12:00:00Z', publishedAtUtc: '2026-10-03T12:00:00Z', publisherProfile: 'communications', canCorrect: true },
    { documentId: 'demo-ed-horizonte-4', title: 'Horizonte Masónico · N° 4', documentType: 'magazine', collectionName: 'Publicaciones de la Orden', status: 'published', minimumDegreeRequired: null, authorName: 'Comunicaciones', documentDate: '2026-09-15', topic: 'El trabajo en logia', edition: 'N° 4', shortDescription: 'Cuarto número de la revista institucional.', createdAtUtc: '2026-09-10T12:00:00Z', publishedAtUtc: '2026-09-15T12:00:00Z', publisherProfile: 'communications', canCorrect: true },
    { documentId: 'demo-ed-especial', title: 'Horizonte Masónico · Edición especial', documentType: 'magazine', collectionName: 'Publicaciones de la Orden', status: 'published', minimumDegreeRequired: null, authorName: 'Bibliotecario', documentDate: '2026-08-20', topic: null, edition: 'Especial', shortDescription: 'Edición especial publicada por el Bibliotecario.', createdAtUtc: '2026-08-18T12:00:00Z', publishedAtUtc: '2026-08-20T12:00:00Z', publisherProfile: 'librarian', canCorrect: false },
    { documentId: 'demo-ed-boletin-nov', title: 'Boletín de la Orden · Noviembre 2026', documentType: 'bulletin', collectionName: 'Publicaciones de la Orden', status: 'draft', minimumDegreeRequired: null, authorName: 'Comunicaciones', documentDate: '2026-11-01', topic: null, edition: null, shortDescription: 'Borrador en preparación.', createdAtUtc: '2026-10-20T12:00:00Z', publishedAtUtc: null, publisherProfile: 'communications', canCorrect: true }
  ]
  private mockEditorialVersions: Array<EditorialMagazineVersion & { documentId: string }> = this.mockEditorial.map(item => ({ documentId: item.documentId, id: `${item.documentId}-v1`, versionNumber: 1, processingStatus: 'available', originalFileName: `${item.documentId}.pdf`, contentType: 'application/pdf', sizeBytes: 1_200_000, createdAtUtc: item.createdAtUtc }))

  // Una única instancia bajo demanda, incluso si dos solicitudes inician simultáneamente.
  // La simulación no debe entrar en el bundle de la portada pública.
  private collaboratorsDemoPromise: Promise<AreaCollaboratorsDemo> | null = null
  private getCollaboratorsDemo(): Promise<AreaCollaboratorsDemo> {
    return this.collaboratorsDemoPromise ??= import('./areaCollaborators')
      .then(({ AreaCollaboratorsDemo }) => new AreaCollaboratorsDemo())
  }
  private collaboratorsPath(area: CollaboratorArea) { return area === 'library' ? '/api/biblioteca/colaboradores' : area === 'communications' ? '/api/comunicaciones/colaboradores' : '/api/documentos/colaboradores' }
  // El servidor decide: Bibliotecario, Comunicaciones y Gran Archivero administran ámbitos distintos.
  async getAreaCollaborators(area: CollaboratorArea): Promise<AreaCollaborators> {
    if (this.useMocks) return (await this.getCollaboratorsDemo()).list(area)
    return this.request<AreaCollaborators>(this.collaboratorsPath(area))
  }
  async getAreaCollaboratorCandidates(area: CollaboratorArea): Promise<AreaCollaboratorCandidates> {
    if (this.useMocks) return (await this.getCollaboratorsDemo()).candidates()
    return this.request<AreaCollaboratorCandidates>(`${this.collaboratorsPath(area)}/candidatos`)
  }
  async addAreaCollaborator(area: CollaboratorArea, request: AddAreaCollaborator): Promise<AreaCollaborators> {
    if (this.useMocks) return (await this.getCollaboratorsDemo()).add(area, request)
    await this.postJson(this.collaboratorsPath(area), request)
    return this.getAreaCollaborators(area)
  }
  async updateAreaCollaborator(area: CollaboratorArea, assignmentId: string, request: AddAreaCollaborator): Promise<AreaCollaborators> {
    if (this.useMocks) return (await this.getCollaboratorsDemo()).update(area, assignmentId, request)
    await this.authorizedFetch(`${this.collaboratorsPath(area)}/${encodeURIComponent(assignmentId)}`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request)
    })
    return this.getAreaCollaborators(area)
  }
  async revokeAreaCollaborator(area: CollaboratorArea, assignmentId: string, expectedVersion: number): Promise<AreaCollaborators> {
    if (this.useMocks) return (await this.getCollaboratorsDemo()).revoke(area, assignmentId, expectedVersion)
    await this.authorizedFetch(`${this.collaboratorsPath(area)}/${encodeURIComponent(assignmentId)}?expectedVersion=${expectedVersion}`, { method: 'DELETE' })
    return this.getAreaCollaborators(area)
  }

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

  async getMyWorkPapers(): Promise<MemberWorkPaper[]> {
    if (this.useMocks) return this.mockWorkPapers.map(item => ({ ...item, versions: item.versions.map(version => ({ ...version })) }))
    const response = await this.request<{ items: MemberWorkPaper[] }>('/api/biblioteca/mis-planchas')
    return response.items
  }

  // #588 — revisión del Bibliotecario. El servidor decide (solo perfil Bibliotecario) y falla cerrado.
  async listWorkPaperReviews(): Promise<WorkPaperReview[]> {
    if (this.useMocks) return this.mockReviews.map(item => ({ ...item }))
    const response = await this.request<{ items: WorkPaperReview[] }>('/api/biblioteca/revision')
    return response.items
  }

  async approveWorkPaper(documentId: string): Promise<void> {
    if (this.useMocks) { this.mockReviews = this.mockReviews.filter(item => item.id !== documentId); const own = this.mockWorkPapers.find(item => item.id === documentId); if (own) { own.reviewStatus = 'approved'; own.status = 'published'; own.returnReason = null; own.versions = own.versions.map((version, index) => ({ ...version, isCurrent: index === 0 })); own.publishedVersionId = own.versions[0]?.id ?? null } return }
    await this.authorizedFetch(`/api/biblioteca/revision/${encodeURIComponent(documentId)}/aprobar`, { method: 'POST' })
  }

  async returnWorkPaper(documentId: string, reason: string): Promise<void> {
    if (this.useMocks) { this.mockReviews = this.mockReviews.filter(item => item.id !== documentId); const own = this.mockWorkPapers.find(item => item.id === documentId); if (own) { own.reviewStatus = 'returned'; own.returnReason = reason.trim() } return }
    await this.authorizedFetch(`/api/biblioteca/revision/${encodeURIComponent(documentId)}/devolver`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason: reason.trim() }) })
  }

  async downloadWorkPaperForReview(documentId: string): Promise<Blob> {
    if (this.useMocks) return new Blob(['Plancha demostrativa en revisión'], { type: 'text/plain' })
    const response = await this.authorizedFetch(`/api/biblioteca/revision/${encodeURIComponent(documentId)}/contenido`, {}, 'application/octet-stream')
    return response.blob()
  }

  // #588 — carga del Bibliotecario (libros y categorías propias). Solo perfil Bibliotecario; el servidor falla cerrado.
  private mockManaged: ManagedPublication[] = []
  async listManagedPublications(): Promise<ManagedPublication[]> {
    if (this.useMocks) return this.mockManaged.map(item => ({ ...item }))
    const response = await this.request<{ items: ManagedPublication[] }>('/api/biblioteca/gestion/publicaciones')
    return response.items
  }

  /** Crea el borrador, carga el archivo, lo hace analizar y, si se pide, lo publica. Devuelve el id del documento. */
  async uploadManagedPublication(input: ManagedPublicationInput, file: File, publishNow: boolean): Promise<{ documentId: string; published: boolean }> {
    if (this.useMocks) {
      const id = crypto.randomUUID(); const now = new Date().toISOString()
      this.mockManaged.unshift({ id, title: input.title.trim(), categoryCode: input.categoryCode, status: publishNow ? 'published' : 'draft', degree: input.minimumDegreeRequired, author: input.authorName ?? null, topic: input.topic ?? null, edition: input.edition ?? null, description: input.description ?? null, createdAtUtc: now, publishedAtUtc: publishNow ? now : null })
      return { documentId: id, published: publishNow }
    }
    const created = await this.postJson<{ documentId: string }>('/api/biblioteca/gestion/publicaciones', input)
    const version = await this.createVersion(created.documentId, { originalFileName: file.name, contentType: file.type, sizeBytes: file.size })
    await this.uploadVersionContent(version.id, file)
    const scanned = await this.scanVersionContent(version.id)
    if (scanned.processingStatus !== 'available') throw new Error('El archivo no quedó disponible después del análisis de seguridad; el borrador quedó sin publicar.')
    if (publishNow) await this.publishEditorialMagazine(created.documentId, version.id)
    return { documentId: created.documentId, published: publishNow }
  }

  /** Publica la última versión disponible de un borrador propio del Bibliotecario. */
  async publishManagedDraft(documentId: string): Promise<void> {
    if (this.useMocks) { const item = this.mockManaged.find(entry => entry.id === documentId); if (item) { item.status = 'published'; item.publishedAtUtc = new Date().toISOString() } return }
    const versions = await this.listMagazineVersions(documentId)
    const latest = versions.filter(version => version.processingStatus === 'available').sort((a, b) => b.versionNumber - a.versionNumber)[0]
    if (!latest) throw new Error('El borrador no tiene un archivo disponible para publicar.')
    await this.publishEditorialMagazine(documentId, latest.id)
  }

  async getWorkPaperByVersion(versionId: string): Promise<MemberWorkPaperLink> {
    if (this.useMocks) {
      const paper = this.mockWorkPapers.find(item => item.versions.some(version => version.id === versionId))
      if (!paper) return { documentId: '', authorMemberId: null, title: '', shortDescription: '' }
      return { documentId: paper.id, authorMemberId: 'demo-member-1', title: paper.title, shortDescription: paper.shortDescription }
    }
    return this.request<MemberWorkPaperLink>(`/api/biblioteca/mis-planchas/versiones/${encodeURIComponent(versionId)}`)
  }

  async submitWorkPaper(input: { organizationId: string; title: string; shortDescription: string; file: File; authorMemberId?: string }): Promise<{ documentId: string; versionId: string }> {
    const { file } = input
    if (this.useMocks) {
      const id = crypto.randomUUID(); const versionId = crypto.randomUUID(); const now = new Date().toISOString()
      // #588: la plancha queda «para revisión»; no entra a la Biblioteca hasta que el Bibliotecario la apruebe.
      this.mockWorkPapers.unshift({ id, title: input.title.trim(), shortDescription: input.shortDescription.trim(), minimumDegreeRequired: 3, status: 'draft', publishedVersionId: null, reviewStatus: 'pending_review', returnReason: null, versions: [{ id: versionId, versionNumber: 1, originalFileName: file.name, processingStatus: 'available', createdAtUtc: now, isCurrent: false }] })
      this.mockReviews.unshift({ id, title: input.title.trim(), authorName: 'Hermano Demostrativo', lodge: 'Taller Demostrativo Nº 23', description: input.shortDescription.trim(), submittedAtUtc: now, degree: 3, replacesPublished: false, versionId, fileName: file.name, sizeBytes: file.size })
      return { documentId: id, versionId }
    }
    const created = await this.postJson<{ documentId: string; versionId: string }>('/api/biblioteca/mis-planchas', {
      organizationId: input.organizationId, authorMemberId: input.authorMemberId ?? null, title: input.title.trim(), shortDescription: input.shortDescription.trim(),
      originalFileName: file.name, contentType: file.type, sizeBytes: file.size,
    })
    await this.uploadVersionContent(created.versionId, file)
    const scanned = await this.scanVersionContent(created.versionId)
    if (scanned.processingStatus !== 'available') throw new Error('La plancha no quedó enviada: el análisis de seguridad no la declaró disponible.')
    return created
  }

  async replaceWorkPaper(documentId: string, input: { title: string; shortDescription: string; file: File }): Promise<{ versionId: string }> {
    const { file } = input
    if (this.useMocks) {
      const document = this.mockWorkPapers.find(item => item.id === documentId)
      if (!document) throw new Error('La plancha indicada no existe en tus trabajos.')
      const versionId = crypto.randomUUID(); const now = new Date().toISOString()
      if (document.reviewStatus === 'pending_review') throw new Error('La plancha está en revisión del Bibliotecario; espera su respuesta antes de enviar otra versión.')
      document.reviewStatus = 'pending_review'; document.returnReason = null
      document.versions = [{ id: versionId, versionNumber: Math.max(0, ...document.versions.map(version => version.versionNumber)) + 1, originalFileName: file.name, processingStatus: 'available', createdAtUtc: now, isCurrent: false }, ...document.versions]
      this.mockReviews = this.mockReviews.filter(item => item.id !== documentId)
      this.mockReviews.unshift({ id: documentId, title: input.title.trim(), authorName: 'Hermano Demostrativo', lodge: 'Taller Demostrativo Nº 23', description: input.shortDescription.trim(), submittedAtUtc: now, degree: document.minimumDegreeRequired, replacesPublished: document.publishedVersionId !== null, versionId, fileName: file.name, sizeBytes: file.size })
      return { versionId }
    }
    const created = await this.postJson<{ versionId: string }>(`/api/biblioteca/mis-planchas/${encodeURIComponent(documentId)}/versiones`, {
      title: input.title.trim(), shortDescription: input.shortDescription.trim(), originalFileName: file.name, contentType: file.type, sizeBytes: file.size,
    })
    await this.uploadVersionContent(created.versionId, file)
    const scanned = await this.scanVersionContent(created.versionId)
    if (scanned.processingStatus !== 'available') throw new Error('La nueva versión no quedó disponible; se conserva la versión anterior.')
    return created
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

  private mockCategories: LibraryCategoryDto[] = [
    { value: 'work_paper', label: 'Planchas de Trabajo', base: true, active: true, position: 1 },
    { value: 'book', label: 'Libros', base: true, active: true, position: 2 },
    { value: 'magazine', label: 'Revistas', base: true, active: true, position: 3 },
    { value: 'bulletin', label: 'Boletines', base: true, active: true, position: 4 },
  ]

  /** #588 — categorías persistentes de la Biblioteca (el Bibliotecario las administra). */
  async listLibraryCategories(): Promise<{ canManage: boolean; items: LibraryCategoryDto[] }> {
    if (this.useMocks) return { canManage: true, items: this.mockCategories.map(c => ({ ...c })) }
    return this.request('/api/biblioteca/categorias')
  }
  async createLibraryCategory(label: string): Promise<LibraryCategoryDto> {
    if (this.useMocks) {
      const clean = label.trim()
      if (this.mockCategories.some(c => c.label.toLowerCase() === clean.toLowerCase())) throw new Error('Ya existe una categoría con ese nombre.')
      let value = clean.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '').slice(0, 36) || 'categoria'
      while (this.mockCategories.some(c => c.value === value)) value += '_2'
      const created = { value, label: clean, base: false, active: true, position: this.mockCategories.length + 1 }
      this.mockCategories.push(created); return { ...created }
    }
    return this.postJson('/api/biblioteca/categorias', { label })
  }
  async updateLibraryCategory(code: string, patch: { label?: string; active?: boolean }): Promise<LibraryCategoryDto> {
    if (this.useMocks) {
      const c = this.mockCategories.find(x => x.value === code); if (!c) throw new Error('Categoría no encontrada.')
      if (patch.active === false && c.base) throw new Error('Las categorías base no se desactivan.')
      if (patch.label !== undefined) c.label = patch.label.trim()
      if (patch.active !== undefined) c.active = patch.active
      return { ...c }
    }
    return this.putJson(`/api/biblioteca/categorias/${encodeURIComponent(code)}`, patch)
  }
  async reorderLibraryCategories(codes: string[]): Promise<void> {
    if (this.useMocks) { this.mockCategories = codes.map((v, i) => ({ ...this.mockCategories.find(c => c.value === v)!, position: i + 1 })); return }
    await this.putJson('/api/biblioteca/categorias/orden', { codes })
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
      const editorial = this.mockEditorial.find(item => item.documentId === documentId)
      if (editorial) {
        if (editorial.canCorrect === false) throw new DocumentApiHttpError(403, 'Solo el perfil que publicó puede corregir esta publicación.')
        const number = Math.max(0, ...this.mockEditorialVersions.filter(item => item.documentId === documentId).map(item => item.versionNumber)) + 1
        const created = { documentId, id: crypto.randomUUID(), versionNumber: number, processingStatus: 'pending_upload', originalFileName: payload.originalFileName.trim(), contentType: payload.contentType.trim(), sizeBytes: payload.sizeBytes, createdAtUtc: new Date().toISOString() }
        this.mockEditorialVersions.push(created)
        return { id: created.id, documentId, versionNumber: number, originalFileName: created.originalFileName, contentType: created.contentType, sizeBytes: created.sizeBytes, processingStatus: 'pending_upload', hasIntegrityHash: false, scanEvidenceRecorded: false, createdAtUtc: created.createdAtUtc }
      }
      const document = this.mockDocuments.get(documentId); if (!document) throw new Error('El documento indicado no existe.')
      const version: DocumentVersion = { id: crypto.randomUUID(), documentId, versionNumber: Math.max(0, ...document.versions.map(item => item.versionNumber)) + 1, originalFileName: payload.originalFileName.trim(), contentType: payload.contentType.trim(), sizeBytes: payload.sizeBytes, processingStatus: 'pending_upload', hasIntegrityHash: false, scanEvidenceRecorded: false, createdAtUtc: new Date().toISOString() }
      document.versions.unshift(version); return { ...version }
    }
    return this.postJson<DocumentVersion>(`/api/documentos/${encodeURIComponent(documentId)}/versiones`, payload)
  }

  async uploadVersionContent(versionId: string, file: File): Promise<DocumentVersion> {
    if (this.useMocks) {
      const editorial = this.mockEditorialVersions.find(item => item.id === versionId)
      if (editorial) { editorial.processingStatus = 'uploaded'; return { id: versionId, documentId: editorial.documentId, versionNumber: editorial.versionNumber, originalFileName: editorial.originalFileName, contentType: editorial.contentType, sizeBytes: editorial.sizeBytes, processingStatus: 'uploaded', hasIntegrityHash: true, scanEvidenceRecorded: false, createdAtUtc: editorial.createdAtUtc } }
      const document = [...this.mockDocuments.values()].find(item => item.versions.some(version => version.id === versionId))
      const version = document?.versions.find(item => item.id === versionId)
      if (!version) throw new Error('La versión indicada no existe.')
      if (version.sizeBytes !== file.size) throw new Error('El tamaño del archivo no coincide con la versión registrada.')
      version.processingStatus = 'uploaded'; version.hasIntegrityHash = true
      return { ...version }
    }
    const response = await this.authorizedFetch(
      `/api/documentos/versiones/${encodeURIComponent(versionId)}/contenido`,
      { method: 'PUT', headers: { 'Content-Type': file.type }, body: file },
    )
    const state = await response.json() as { processingStatus: DocumentProcessingStatus; sizeBytes: number; hasIntegrityHash: boolean }
    return { id: versionId, documentId: '', versionNumber: 0, originalFileName: file.name, contentType: file.type, sizeBytes: state.sizeBytes, processingStatus: state.processingStatus, hasIntegrityHash: state.hasIntegrityHash, scanEvidenceRecorded: false, createdAtUtc: new Date().toISOString() }
  }

  async scanVersionContent(versionId: string): Promise<DocumentVersion> {
    if (this.useMocks) {
      const editorial = this.mockEditorialVersions.find(item => item.id === versionId)
      if (editorial) { editorial.processingStatus = 'available'; return { id: versionId, documentId: editorial.documentId, versionNumber: editorial.versionNumber, originalFileName: editorial.originalFileName, contentType: editorial.contentType, sizeBytes: editorial.sizeBytes, processingStatus: 'available', hasIntegrityHash: true, scanEvidenceRecorded: true, createdAtUtc: editorial.createdAtUtc } }
      const document = [...this.mockDocuments.values()].find(item => item.versions.some(version => version.id === versionId))
      const version = document?.versions.find(item => item.id === versionId)
      if (!version) throw new Error('La versión indicada no existe.')
      if (version.processingStatus !== 'uploaded' && version.processingStatus !== 'scanning') throw new Error('La versión no está lista para análisis.')
      version.processingStatus = 'available'; version.scanEvidenceRecorded = true
      return { ...version }
    }
    const state = await this.request<{ processingStatus: DocumentProcessingStatus; clean: boolean }>(`/api/documentos/versiones/${encodeURIComponent(versionId)}/analizar`, { method: 'POST' })
    return { id: versionId, documentId: '', versionNumber: 0, originalFileName: '', contentType: '', sizeBytes: 0, processingStatus: state.processingStatus, hasIntegrityHash: true, scanEvidenceRecorded: true, createdAtUtc: new Date().toISOString() }
  }

  async uploadManagedFile(
    collectionId: string,
    payload: CreateInstitutionalDocumentRequest,
    file: File,
  ): Promise<{ document: InstitutionalDocument; version: DocumentVersion }> {
    const document = await this.createDocument(collectionId, payload)
    const version = await this.createVersion(document.id, {
      originalFileName: file.name,
      contentType: file.type,
      sizeBytes: file.size,
    })
    await this.uploadVersionContent(version.id, file)
    const scanned = await this.scanVersionContent(version.id)
    if (scanned.processingStatus !== 'available') throw new Error('El archivo no quedó disponible después del análisis.')
    return { document: await this.getDocument(document.id), version: scanned }
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

  /** Revistas editables por el usuario (403 si no tiene permiso editorial). En demo no hay API real: lista vacía. */
  async listEditableMagazines(page = 1, pageSize = 50): Promise<EditorialMagazineList> {
    if (this.useMocks) return { total: this.mockEditorial.length, page, pageSize, items: this.mockEditorial.map(item => ({ ...item })) }
    return this.request<EditorialMagazineList>(`/api/biblioteca/editorial/revistas?page=${page}&pageSize=${pageSize}`)
  }

  /** Alta de borrador editorial: Revista (por omisión) o Boletín mensual. */
  async createEditorialMagazine(collectionId: string, title: string, documentType: EditorialType = 'magazine'): Promise<{ documentId: string; status: string }> {
    if (this.useMocks) throw new Error('La demostración no crea publicaciones reales.')
    return this.postJson<{ documentId: string; status: string }>('/api/biblioteca/editorial/revistas', { collectionId, title, documentType })
  }

  async listMagazineVersions(documentId: string): Promise<EditorialMagazineVersion[]> {
    if (this.useMocks) return this.mockEditorialVersions.filter(item => item.documentId === documentId).sort((a, b) => b.versionNumber - a.versionNumber).map(({ documentId: _ignored, ...version }) => version)
    const response = await this.request<{ items: EditorialMagazineVersion[] }>(`/api/biblioteca/editorial/revistas/${encodeURIComponent(documentId)}/versiones?pageSize=50`)
    return response.items
  }

  /** `reason` es obligatorio (10 a 2000 caracteres) al reemplazar el archivo de una publicación vigente sin despublicar. */
  async publishEditorialMagazine(documentId: string, versionId: string, reason?: string): Promise<void> {
    if (this.useMocks) {
      const item = this.mockEditorial.find(entry => entry.documentId === documentId)
      if (!item) throw new Error('La demostración no publica revistas reales.')
      if (item.canCorrect === false) throw new DocumentApiHttpError(403, 'Solo el perfil que publicó puede corregir esta publicación.')
      if (item.status === 'published' && (reason?.trim().length ?? 0) < 10) throw new DocumentApiHttpError(400, 'Indique el motivo del reemplazo (10 a 2000 caracteres).')
      item.status = 'published'; item.publishedAtUtc = new Date().toISOString(); return
    }
    await this.authorizedFetch(`/api/biblioteca/editorial/revistas/${encodeURIComponent(documentId)}/publicar`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(reason ? { versionId, reason } : { versionId }) })
  }

  async unpublishEditorialMagazine(documentId: string): Promise<void> {
    if (this.useMocks) {
      const item = this.mockEditorial.find(entry => entry.documentId === documentId)
      if (!item) throw new Error('La demostración no retira revistas reales.')
      if (item.canCorrect === false) throw new DocumentApiHttpError(403, 'Solo el perfil que publicó puede retirar esta publicación.')
      item.status = 'draft'; item.publishedAtUtc = null; return
    }
    await this.authorizedFetch(`/api/biblioteca/editorial/revistas/${encodeURIComponent(documentId)}/retirar-publicacion`, { method: 'POST' })
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
  if (['magazine', 'magazines', 'revista', 'revistas'].includes(normalized)) return 'magazine'
  if (['bulletin', 'bulletins', 'boletin', 'boletines'].includes(normalized)) return 'bulletin'
  if (['video', 'videos'].includes(normalized)) return 'video'
  return normalized
}
function catalogTypeMatches(value: string, requested: string) { return canonicalCatalogType(value) === canonicalCatalogType(requested) }
function cloneDocument(value: InstitutionalDocument): InstitutionalDocument { return { ...value, versions: value.versions.map(item => ({ ...item })) } }
function toListItem(value: InstitutionalDocument): DocumentListItem { return { id: value.id, collectionId: value.collectionId, organizationId: value.organizationId, title: value.title, documentType: value.documentType, classification: value.classification, accessPolicy: value.accessPolicy, status: value.status, publishedVersionId: value.publishedVersionId, publishedAtUtc: value.publishedAtUtc } }
