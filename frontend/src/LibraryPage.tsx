import { useEffect, useMemo, useState } from 'react'
import InstitutionalIcon from './InstitutionalIcon'
import { type DocumentApiClient, type LibraryCatalogItem, type LibraryFacetItem, type LibraryFacetsResponse } from './api/documentApi'
import './documents.css'
import './library-ppt-enhancement.css'

const pageSize = 12
const emptyFacets: LibraryFacetsResponse = { collections: [], documentTypes: [], degrees: [], topics: [], officialDocumentTypes: [] }
const catalogCategories = [
  { value: 'work_paper', label: 'Planchas de Trabajo' },
  { value: 'book', label: 'Libros' },
  { value: 'official_document', label: 'Documentos Oficiales' },
  { value: 'video', label: 'Videos', future: true },
] as const

export default function LibraryPage({ documentApi }: { documentApi: DocumentApiClient }) {
  const [items, setItems] = useState<LibraryCatalogItem[]>([])
  const [facets, setFacets] = useState<LibraryFacetsResponse>(emptyFacets)
  const [query, setQuery] = useState('')
  const [documentType, setDocumentType] = useState('')
  const [degree, setDegree] = useState('')
  const [topic, setTopic] = useState('')
  const [officialDocumentType, setOfficialDocumentType] = useState('')
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    documentApi.getLibraryFacets()
      .then(response => { if (active) setFacets(response) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [documentApi])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      setLoading(true)
      setError(null)
      documentApi.searchLibrary({
        q: query || undefined,
        documentType: documentType || undefined,
        degree: degree ? Number(degree) : undefined,
        topic: topic || undefined,
        officialDocumentType: officialDocumentType || undefined,
        page,
        pageSize,
      })
        .then(response => {
          if (!active) return
          setItems(response.items)
          setTotal(response.total)
        })
        .catch(reason => { if (active) setError(toMessage(reason)) })
        .finally(() => { if (active) setLoading(false) })
    }, query ? 250 : 0)

    return () => { active = false; window.clearTimeout(timer) }
  }, [degree, documentApi, documentType, officialDocumentType, page, query, topic])

  const demoCollectionId = facets.collections[0]?.value ?? items[0]?.collectionId ?? 'demo-library'
  const demoCatalog = useMemo(() => documentApi.useMocks ? createDemoShowcaseCatalog(demoCollectionId) : [], [demoCollectionId, documentApi.useMocks])
  const visibleItems = documentApi.useMocks
    ? filterDemoCatalog(demoCatalog, { query, documentType, degree, topic, officialDocumentType })
    : items
  const visibleTotal = documentApi.useMocks ? visibleItems.length : total
  const categoryTotal = documentApi.useMocks ? demoCatalog.length : (facets.documentTypes.reduce((sum, item) => sum + item.count, 0) || total)
  const categoryCounts = documentApi.useMocks ? buildCategoryCounts(demoCatalog) : buildCategoryCountsFromFacets(facets.documentTypes)
  const topicFacets = documentApi.useMocks ? buildTopicFacets(demoCatalog) : facets.topics
  const officialTypeFacets = documentApi.useMocks ? buildOfficialTypeFacets(demoCatalog) : facets.officialDocumentTypes
  const pageCount = Math.max(1, Math.ceil(visibleTotal / pageSize))
  const showingFutureVideos = documentType === 'video' && categoryCounts.video === 0

  async function download(item: LibraryCatalogItem) {
    setDownloadingId(item.id)
    setError(null)
    try {
      const blob = await documentApi.downloadLibraryDocument(item.id)
      const objectUrl = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = objectUrl
      anchor.download = downloadName(item)
      anchor.rel = 'noopener'
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(objectUrl)
    } catch (reason) {
      setError(toMessage(reason))
    } finally {
      setDownloadingId(null)
    }
  }

  function selectCategory(value: string) {
    setDocumentType(value)
    setOfficialDocumentType('')
    setPage(1)
  }

  return <div className="library-page">
    <section className="page-heading library-heading">
      <div>
        <p className="eyebrow">Conocimiento institucional</p>
        <h1>Biblioteca Virtual</h1>
        <p>Planchas de Trabajo, libros y documentos oficiales catalogados para encontrar el material autorizado con mayor precisión.</p>
      </div>
      <span className="count-badge">{loading && !documentApi.useMocks ? 'cargando…' : publicationCountLabel(visibleTotal)}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la consulta de Biblioteca.</strong><span>{error}</span></div>}

    <section className="panel library-panel">
      <nav className="library-category-rail" aria-label="Categorías de Biblioteca Virtual">
        <button type="button" className={documentType === '' ? 'active' : ''} aria-pressed={documentType === ''} onClick={() => selectCategory('')}>Todos <span>{categoryTotal}</span></button>
        {catalogCategories.map(category => <button key={category.value} type="button" className={`${documentType === category.value ? 'active' : ''} ${category.future ? 'future' : ''}`} aria-pressed={documentType === category.value} onClick={() => selectCategory(category.value)}>
          {category.label}{category.future ? ' · futuro' : ''} <span>{categoryCounts[category.value]}</span>
        </button>)}
      </nav>

      <div className="library-access-band">
        <span className="library-access-band-icon" aria-hidden="true"><InstitutionalIcon name="shield" size={20} /></span>
        <div><strong>Catálogo protegido por grado</strong><span>Primero se aplica tu grado institucional: 1° ve 1°; 2° ve 1° y 2°; 3° ve 1°, 2° y 3°. El contenido General autorizado también puede estar disponible.</span></div>
      </div>

      <div className="library-toolbar">
        <label className="search-field">
          <span>Buscar en Biblioteca</span>
          <input type="search" value={query} onChange={event => { setQuery(event.target.value); setPage(1) }} placeholder="Título, autor, Taller, tema, descripción o tipo documental" />
        </label>
        <div className="library-filters library-catalog-filters">
          <label className="document-field"><span>Grado</span><select value={degree} onChange={event => { setDegree(event.target.value); setPage(1) }}><option value="">Todos los habilitados</option><option value="1">1° grado</option><option value="2">2° grado</option><option value="3">3° grado</option></select></label>
          <label className="document-field"><span>Tema del libro</span><select value={topic} onChange={event => { setTopic(event.target.value); setPage(1) }}><option value="">Todos</option>{topicFacets.map(item => <option key={item.value} value={item.value}>{item.label} ({item.count})</option>)}</select></label>
          {documentType === 'official_document' && <label className="document-field"><span>Tipo oficial</span><select value={officialDocumentType} onChange={event => { setOfficialDocumentType(event.target.value); setPage(1) }}><option value="">Todos</option>{officialTypeFacets.map(item => <option key={item.value} value={item.value}>{item.label} ({item.count})</option>)}</select></label>}
        </div>
      </div>

      {showingFutureVideos ? <div className="library-future-state">
        <span className="library-future-icon" aria-hidden="true"><InstitutionalIcon name="library" size={26} /></span>
        <div><strong>Videos · preparado para una versión futura</strong><p>La categoría y el modelo de catálogo quedan previstos, pero este candidato no habilita todavía reproducción ni publicaciones audiovisuales.</p></div>
      </div> : loading && !documentApi.useMocks ? <div className="loading-rows"><span /><span /><span /></div> : visibleItems.length === 0 ? <div className="empty-state"><strong>No hay publicaciones visibles.</strong><p>Prueba otros filtros. La Biblioteca sólo muestra material publicado y autorizado para tu cuenta y grado.</p></div> : <div className="document-library-grid">{visibleItems.map(item => <LibraryCard key={item.id} item={item} downloading={downloadingId === item.id} onDownload={() => download(item)} />)}</div>}

      {!loading && !documentApi.useMocks && visibleTotal > pageSize && <nav className="library-pagination" aria-label="Paginación de Biblioteca">
        <button className="document-secondary" type="button" disabled={page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>Anterior</button>
        <span>Página {page} de {pageCount}</span>
        <button className="document-secondary" type="button" disabled={page >= pageCount} onClick={() => setPage(value => Math.min(pageCount, value + 1))}>Siguiente</button>
      </nav>}
    </section>

    <section className="document-info-strip"><strong>Acceso controlado</strong><span>La búsqueda consulta sólo publicaciones autorizadas. Las planchas usan descripción corta; los libros usan abstract; las descargas pasan por el backend institucional.</span></section>
  </div>
}

function LibraryCard({ item, downloading, onDownload }: { item: LibraryCatalogItem; downloading: boolean; onDownload: () => void }) {
  const kind = canonicalCatalogType(item.documentType)
  const label = typeLabel(kind)
  return <article className={`library-card library-card-${typeTone(kind)}`}>
    <div className="library-card-cover" aria-hidden="true"><div className="library-icon"><InstitutionalIcon name="library" size={24} /></div><small>{label}</small></div>
    <div className="library-card-title"><span className="document-chip">{label}</span><h3>{item.title}</h3></div>

    {kind === 'work_paper' && <>
      <dl className="library-catalog-meta">
        <Meta label="Hermano autor" value={item.authorName} />
        <Meta label="Taller" value={item.authorLodgeName} />
        <Meta label="Fecha" value={formatCatalogDate(item.documentDate)} />
        <Meta label="Grado" value={degreeLabel(item.minimumDegreeRequired)} />
      </dl>
      <CatalogText label="Descripción corta" value={item.shortDescription} />
    </>}

    {kind === 'book' && <>
      <dl className="library-catalog-meta">
        <Meta label="Autor" value={item.authorName} />
        <Meta label="Tema" value={item.topic} />
        <Meta label="Edición" value={item.edition} />
        <Meta label="Grado" value={degreeLabel(item.minimumDegreeRequired)} />
      </dl>
      <CatalogText label="Abstract" value={item.abstractText} />
    </>}

    {kind === 'official_document' && <dl className="library-catalog-meta">
      <Meta label="Tipo documental" value={item.officialDocumentType ?? 'Documento oficial'} />
      <Meta label="Grado" value={degreeLabel(item.minimumDegreeRequired)} />
      <Meta label="Fecha" value={formatCatalogDate(item.documentDate)} />
      <Meta label="Versión" value={`v${item.versionNumber}`} />
    </dl>}

    {!['work_paper', 'book', 'official_document'].includes(kind) && <dl className="library-catalog-meta"><Meta label="Colección" value={item.collectionName} /><Meta label="Grado" value={degreeLabel(item.minimumDegreeRequired)} /></dl>}

    <div className="library-file-meta"><span>{contentTypeLabel(item.contentType)}</span><span>{formatBytes(item.sizeBytes)}</span><span>Publicado {formatChile(item.publishedAtUtc)}</span></div>
    <div className="library-card-action"><button type="button" className="document-primary compact" disabled={downloading} onClick={onDownload}>{downloading ? 'Preparando…' : 'Descargar'}</button></div>
  </article>
}

function Meta({ label, value }: { label: string; value?: string | null }) {
  return <div><dt>{label}</dt><dd>{value || '—'}</dd></div>
}

function CatalogText({ label, value }: { label: string; value?: string | null }) {
  return <div className="library-catalog-text"><strong>{label}</strong><p>{value || 'Sin información catalogada.'}</p></div>
}

const documentTypeLabels: Record<string, string> = {
  work_paper: 'Plancha de Trabajo',
  book: 'Libro',
  official_document: 'Documento Oficial',
  video: 'Video',
}

function typeLabel(value: string) { return documentTypeLabels[canonicalCatalogType(value)] ?? value.replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase()) }
function typeTone(value: string) {
  const kind = canonicalCatalogType(value)
  if (kind === 'work_paper') return 'work'
  if (kind === 'book') return 'books'
  if (kind === 'official_document') return 'official'
  if (kind === 'video') return 'video'
  return 'general'
}

function canonicalCatalogType(value: string) {
  const normalized = normalizeSearch(value).replace(/\s+/g, '_')
  if (['work_paper', 'working_paper', 'plancha', 'plancha_de_trabajo'].includes(normalized)) return 'work_paper'
  if (['book', 'books', 'libro', 'libros'].includes(normalized)) return 'book'
  if (['official_document', 'documento_oficial', 'regulation', 'reglamento', 'constitution', 'constitucion', 'ritual'].includes(normalized)) return 'official_document'
  if (['video', 'videos'].includes(normalized)) return 'video'
  return normalized
}

function createDemoShowcaseCatalog(collectionId: string): LibraryCatalogItem[] {
  const collectionName = 'Biblioteca Virtual'
  return [
    { id: 'demo-work-1', collectionId, title: 'La piedra bruta y el trabajo interior', documentType: 'work_paper', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 366000, publishedAtUtc: '2026-09-10T15:00:00Z', minimumDegreeRequired: 1, authorName: 'Hno. Andrés Pérez', authorLodgeName: 'R∴L∴S∴ Aurora N° 12', documentDate: '2026-08-21', shortDescription: 'Reflexión breve sobre el trabajo interior del Aprendiz y el simbolismo de la piedra bruta.' },
    { id: 'demo-work-2', collectionId, title: 'Las herramientas del Compañero', documentType: 'work_paper', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 418000, publishedAtUtc: '2026-09-09T15:00:00Z', minimumDegreeRequired: 2, authorName: 'Hno. Martín Silva', authorLodgeName: 'R∴L∴S∴ Libertad N° 23', documentDate: '2026-07-18', shortDescription: 'Síntesis sobre las herramientas simbólicas y su aplicación al progreso del Compañero.' },
    { id: 'demo-work-3', collectionId, title: 'La responsabilidad del Maestro', documentType: 'work_paper', collectionName, versionNumber: 2, contentType: 'application/pdf', sizeBytes: 501000, publishedAtUtc: '2026-09-08T15:00:00Z', minimumDegreeRequired: 3, authorName: 'Hno. Felipe Rojas', authorLodgeName: 'R∴L∴S∴ Igualdad N° 31', documentDate: '2026-06-12', shortDescription: 'Consideraciones sobre liderazgo, transmisión y responsabilidad masónica en el Tercer Grado.' },
    { id: 'demo-book-1', collectionId, title: 'Historia de la masonería simbólica en Chile', documentType: 'book', collectionName, versionNumber: 2, contentType: 'application/pdf', sizeBytes: 2485000, publishedAtUtc: '2026-09-07T15:00:00Z', minimumDegreeRequired: 1, authorName: 'Autor demostrativo', topic: 'Historia masónica', edition: '2ª edición', abstractText: 'Síntesis histórica demostrativa sobre el desarrollo de la masonería simbólica, sus instituciones y principales procesos en Chile.' },
    { id: 'demo-book-2', collectionId, title: 'Símbolos y alegorías del oficio', documentType: 'book', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 1812000, publishedAtUtc: '2026-09-06T15:00:00Z', minimumDegreeRequired: 2, authorName: 'Autora demostrativa', topic: 'Simbolismo', edition: '1ª edición', abstractText: 'Estudio introductorio demostrativo sobre símbolos, alegorías y herramientas presentes en la formación masónica.' },
    { id: 'demo-book-3', collectionId, title: 'Ética, virtud y construcción personal', documentType: 'book', collectionName, versionNumber: 3, contentType: 'application/pdf', sizeBytes: 1940000, publishedAtUtc: '2026-09-05T15:00:00Z', minimumDegreeRequired: null, authorName: 'Autor demostrativo', topic: 'Filosofía y ética', edition: '3ª edición', abstractText: 'Texto demostrativo orientado al estudio de la virtud, la ética y el perfeccionamiento personal desde una perspectiva humanista.' },
    { id: 'demo-official-1', collectionId, title: 'Constitución de la Gran Logia Mixta', documentType: 'official_document', collectionName, versionNumber: 4, contentType: 'application/pdf', sizeBytes: 920000, publishedAtUtc: '2026-09-04T15:00:00Z', minimumDegreeRequired: null, documentDate: '2026-01-01', officialDocumentType: 'Constitución' },
    { id: 'demo-official-2', collectionId, title: 'Reglamento General', documentType: 'official_document', collectionName, versionNumber: 5, contentType: 'application/pdf', sizeBytes: 780000, publishedAtUtc: '2026-09-03T15:00:00Z', minimumDegreeRequired: 1, documentDate: '2026-02-10', officialDocumentType: 'Reglamento' },
    { id: 'demo-official-3', collectionId, title: 'Ritual de Segundo Grado', documentType: 'official_document', collectionName, versionNumber: 3, contentType: 'application/pdf', sizeBytes: 1120000, publishedAtUtc: '2026-09-02T15:00:00Z', minimumDegreeRequired: 2, documentDate: '2026-03-15', officialDocumentType: 'Ritual' },
  ]
}

function filterDemoCatalog(items: LibraryCatalogItem[], filters: { query: string; documentType: string; degree: string; topic: string; officialDocumentType: string }) {
  const normalizedQuery = normalizeSearch(filters.query)
  return items.filter(item => {
    if (filters.documentType && canonicalCatalogType(item.documentType) !== filters.documentType) return false
    if (filters.degree && item.minimumDegreeRequired !== Number(filters.degree)) return false
    if (filters.topic && normalizeSearch(item.topic ?? '') !== normalizeSearch(filters.topic)) return false
    if (filters.officialDocumentType && normalizeSearch(item.officialDocumentType ?? '') !== normalizeSearch(filters.officialDocumentType)) return false
    if (!normalizedQuery) return true
    const searchable = [item.title, item.authorName, item.authorLodgeName, item.topic, item.edition, item.shortDescription, item.abstractText, item.officialDocumentType, item.collectionName].filter(Boolean).join(' ')
    return normalizeSearch(searchable).includes(normalizedQuery)
  })
}

function buildCategoryCounts(items: LibraryCatalogItem[]) {
  const result = { work_paper: 0, book: 0, official_document: 0, video: 0 }
  for (const item of items) {
    const kind = canonicalCatalogType(item.documentType)
    if (kind in result) result[kind as keyof typeof result] += 1
  }
  return result
}

function buildCategoryCountsFromFacets(items: LibraryFacetItem[]) {
  const result = { work_paper: 0, book: 0, official_document: 0, video: 0 }
  for (const item of items) {
    const kind = canonicalCatalogType(item.value)
    if (kind in result) result[kind as keyof typeof result] += item.count
  }
  return result
}

function buildTopicFacets(items: LibraryCatalogItem[]): LibraryFacetItem[] { return buildStringFacet(items.map(item => item.topic)) }
function buildOfficialTypeFacets(items: LibraryCatalogItem[]): LibraryFacetItem[] { return buildStringFacet(items.map(item => item.officialDocumentType)) }
function buildStringFacet(values: Array<string | null | undefined>): LibraryFacetItem[] {
  const counts = new Map<string, number>()
  for (const value of values) if (value) counts.set(value, (counts.get(value) ?? 0) + 1)
  return [...counts.entries()].map(([value, count]) => ({ value, label: value, count })).sort((a, b) => a.label.localeCompare(b.label, 'es'))
}

function normalizeSearch(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function degreeLabel(value?: number | null) { return value ? `${value}° grado` : 'General' }
function formatCatalogDate(value?: string | null) { if (!value) return '—'; return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(`${value}T12:00:00-03:00`)) }
function publicationCountLabel(total: number) { return `${total} ${total === 1 ? 'publicación' : 'publicaciones'}` }
function contentTypeLabel(value: string) { return value === 'application/pdf' ? 'PDF' : value.includes('wordprocessingml') ? 'Word' : value.includes('spreadsheetml') ? 'Excel' : value.includes('presentationml') ? 'PowerPoint' : value.split('/').at(-1)?.toUpperCase() ?? value }
function formatBytes(value: number) { if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`; return `${(value / 1024 / 1024).toFixed(1)} MB` }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value)) }
function downloadName(item: LibraryCatalogItem) { const base = item.title.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-zA-Z0-9._ -]+/g, '').trim().replace(/\s+/g, '-').slice(0, 90) || 'documento'; return `${base}.${extensionFor(item.contentType)}` }
function extensionFor(contentType: string) { if (contentType === 'application/pdf') return 'pdf'; if (contentType === 'image/png') return 'png'; if (contentType === 'image/jpeg') return 'jpg'; if (contentType.includes('wordprocessingml')) return 'docx'; if (contentType.includes('spreadsheetml')) return 'xlsx'; if (contentType.includes('presentationml')) return 'pptx'; if (contentType === 'text/csv') return 'csv'; if (contentType === 'text/markdown') return 'md'; return 'txt' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
