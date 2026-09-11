import { useEffect, useState } from 'react'
import InstitutionalIcon from './InstitutionalIcon'
import { type DocumentApiClient, type LibraryCatalogItem, type LibraryFacetItem, type LibraryFacetsResponse } from './api/documentApi'
import './documents.css'
import './library-ppt-enhancement.css'

const pageSize = 12
const emptyFacets: LibraryFacetsResponse = { collections: [], documentTypes: [] }

export default function LibraryPage({ documentApi }: { documentApi: DocumentApiClient }) {
  const [items, setItems] = useState<LibraryCatalogItem[]>([])
  const [facets, setFacets] = useState<LibraryFacetsResponse>(emptyFacets)
  const [query, setQuery] = useState('')
  const [collectionId, setCollectionId] = useState('')
  const [documentType, setDocumentType] = useState('')
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
        collectionId: collectionId || undefined,
        documentType: documentType || undefined,
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
  }, [collectionId, documentApi, documentType, page, query])

  const demoCollectionId = facets.collections[0]?.value ?? items[0]?.collectionId ?? 'demo-library'
  const demoCatalog = documentApi.useMocks ? createDemoShowcaseCatalog(demoCollectionId) : []
  const visibleItems = documentApi.useMocks ? filterDemoCatalog(demoCatalog, { query, collectionId, documentType }) : items
  const visibleDocumentTypes = documentApi.useMocks ? buildTypeFacets(demoCatalog) : facets.documentTypes
  const visibleTotal = documentApi.useMocks ? visibleItems.length : total
  const categoryTotal = documentApi.useMocks ? demoCatalog.length : (facets.documentTypes.reduce((sum, item) => sum + item.count, 0) || total)
  const pageCount = Math.max(1, Math.ceil(visibleTotal / pageSize))

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

  return <div className="library-page">
    <section className="page-heading library-heading">
      <div><p className="eyebrow">Conocimiento institucional</p><h1>Biblioteca Virtual</h1><p>Documentos publicados expresamente para su consulta según alcance y política de acceso.</p></div>
      <span className="count-badge">{loading && !documentApi.useMocks ? 'cargando…' : publicationCountLabel(visibleTotal)}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la consulta de Biblioteca.</strong><span>{error}</span></div>}

    <section className="panel library-panel">
      <nav className="library-category-rail" aria-label="Categorías de Biblioteca Virtual">
        <button type="button" className={documentType === '' ? 'active' : ''} aria-pressed={documentType === ''} onClick={() => { setDocumentType(''); setPage(1) }}>Todos <span>{categoryTotal}</span></button>
        {visibleDocumentTypes.map(item => <button key={item.value} type="button" className={documentType === item.value ? 'active' : ''} aria-pressed={documentType === item.value} onClick={() => { setDocumentType(item.value); setPage(1) }}>{typeLabel(item.label)} <span>{item.count}</span></button>)}
      </nav>

      <div className="library-access-band">
        <span className="library-access-band-icon" aria-hidden="true"><InstitutionalIcon name="shield" size={20} /></span>
        <div><strong>Catálogo protegido por grado</strong><span>La Biblioteca ya llega filtrada desde el backend: sólo aparecen publicaciones autorizadas para tu grado institucional vigente y sus grados inferiores.</span></div>
      </div>

      <div className="library-toolbar">
        <label className="search-field"><span>Buscar en Biblioteca</span><input type="search" value={query} onChange={event => { setQuery(event.target.value); setPage(1) }} placeholder="Título, colección o tipo documental" /></label>
        <div className="library-filters">
          <label className="document-field"><span>Colección</span><select value={collectionId} onChange={event => { setCollectionId(event.target.value); setPage(1) }}><option value="">Todas</option>{facets.collections.map(item => <option key={item.value} value={item.value}>{item.label} ({documentApi.useMocks ? demoCatalog.length : item.count})</option>)}</select></label>
          <label className="document-field"><span>Tipo documental</span><select value={documentType} onChange={event => { setDocumentType(event.target.value); setPage(1) }}><option value="">Todos</option>{visibleDocumentTypes.map(item => <option key={item.value} value={item.value}>{typeLabel(item.label)} ({item.count})</option>)}</select></label>
        </div>
      </div>

      {loading && !documentApi.useMocks ? <div className="loading-rows"><span /><span /><span /></div> : visibleItems.length === 0 ? <div className="empty-state"><strong>No hay publicaciones visibles.</strong><p>La Biblioteca sólo muestra documentos publicados, disponibles y autorizados para su cuenta.</p></div> : <div className="document-library-grid">{visibleItems.map(item => <LibraryCard key={item.id} item={item} downloading={downloadingId === item.id} onDownload={() => download(item)} />)}</div>}

      {!loading && !documentApi.useMocks && visibleTotal > pageSize && <nav className="library-pagination" aria-label="Paginación de Biblioteca">
        <button className="document-secondary" type="button" disabled={page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>Anterior</button>
        <span>Página {page} de {pageCount}</span>
        <button className="document-secondary" type="button" disabled={page >= pageCount} onClick={() => setPage(value => Math.min(pageCount, value + 1))}>Siguiente</button>
      </nav>}
    </section>

    <section className="document-info-strip"><strong>Acceso controlado</strong><span>La búsqueda y las facetas se calculan sólo sobre publicaciones autorizadas. La descarga usa el backend institucional y nunca expone una URL pública de Object Storage.</span></section>
  </div>
}

function LibraryCard({ item, downloading, onDownload }: { item: LibraryCatalogItem; downloading: boolean; onDownload: () => void }) {
  const label = typeLabel(item.documentType)
  return <article className={`library-card library-card-${typeTone(item.documentType)}`}>
    <div className="library-card-cover" aria-hidden="true"><div className="library-icon"><InstitutionalIcon name="library" size={24} /></div><small>{label}</small></div>
    <div><span className="document-chip">{label}</span><h3>{item.title}</h3><p>{item.collectionName}</p></div>
    <dl><div><dt>Versión</dt><dd>v{item.versionNumber}</dd></div><div><dt>Formato</dt><dd>{contentTypeLabel(item.contentType)}</dd></div><div><dt>Tamaño</dt><dd>{formatBytes(item.sizeBytes)}</dd></div></dl>
    <small>Publicado {formatChile(item.publishedAtUtc)}</small>
    <div className="library-card-action"><button type="button" className="document-primary compact" disabled={downloading} onClick={onDownload}>{downloading ? 'Preparando…' : 'Descargar'}</button></div>
  </article>
}

const documentTypeLabels: Record<string, string> = {
  book: 'Libros',
  books: 'Libros',
  historical_publication: 'Historia',
  history: 'Historia',
  work_paper: 'Plancha de trabajo',
  working_paper: 'Plancha de trabajo',
  instruction_material: 'Docencia',
  instructional_material: 'Docencia',
  study_material: 'Material de estudio',
  publication: 'Publicación',
}

function typeLabel(value: string) {
  const normalized = value.trim().toLowerCase()
  return documentTypeLabels[normalized] ?? normalized.replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase())
}

function typeTone(value: string) {
  const normalized = value.toLowerCase()
  if (normalized.includes('histor') || normalized === 'history') return 'history'
  if (normalized.includes('work') || normalized.includes('plancha')) return 'work'
  if (normalized.includes('instruction') || normalized.includes('docencia')) return 'instruction'
  if (normalized.includes('book') || normalized.includes('libro')) return 'books'
  return 'general'
}

function createDemoShowcaseCatalog(collectionId: string): LibraryCatalogItem[] {
  const collectionName = 'Historia y formación'
  return [
    { id: 'demo-library-history-1', collectionId, title: 'Historia institucional — documento demostrativo', documentType: 'historical_publication', collectionName, versionNumber: 2, contentType: 'application/pdf', sizeBytes: 485000, publishedAtUtc: '2026-09-08T15:00:00Z' },
    { id: 'demo-library-book-1', collectionId, title: 'Ética y virtud — lectura demostrativa', documentType: 'book', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 812000, publishedAtUtc: '2026-09-07T15:00:00Z' },
    { id: 'demo-library-work-1', collectionId, title: 'Plancha de trabajo: simbolismo — demostrativa', documentType: 'work_paper', collectionName, versionNumber: 3, contentType: 'application/pdf', sizeBytes: 366000, publishedAtUtc: '2026-09-06T15:00:00Z' },
    { id: 'demo-library-instruction-1', collectionId, title: 'Docencia de Primer Grado — material demostrativo', documentType: 'instruction_material', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 624000, publishedAtUtc: '2026-09-05T15:00:00Z' },
    { id: 'demo-library-history-2', collectionId, title: 'Geografía e historia universal — selección demostrativa', documentType: 'historical_publication', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 941000, publishedAtUtc: '2026-09-04T15:00:00Z' },
    { id: 'demo-library-work-2', collectionId, title: 'Plancha de trabajo: el Aprendiz — demostrativa', documentType: 'work_paper', collectionName, versionNumber: 2, contentType: 'application/pdf', sizeBytes: 298000, publishedAtUtc: '2026-09-03T15:00:00Z' },
    { id: 'demo-library-history-3', collectionId, title: 'La evolución de la masonería moderna — demostrativa', documentType: 'historical_publication', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 772000, publishedAtUtc: '2026-09-02T15:00:00Z' },
    { id: 'demo-library-instruction-2', collectionId, title: 'Docencia de Segundo Grado — material demostrativo', documentType: 'instruction_material', collectionName, versionNumber: 1, contentType: 'application/pdf', sizeBytes: 558000, publishedAtUtc: '2026-09-01T15:00:00Z' },
  ]
}

function filterDemoCatalog(items: LibraryCatalogItem[], filters: { query: string; collectionId: string; documentType: string }) {
  const normalizedQuery = normalizeSearch(filters.query)
  return items.filter(item => {
    if (filters.collectionId && item.collectionId !== filters.collectionId) return false
    if (filters.documentType && item.documentType !== filters.documentType) return false
    if (!normalizedQuery) return true
    return normalizeSearch(`${item.title} ${item.documentType} ${item.collectionName}`).includes(normalizedQuery)
  })
}

function buildTypeFacets(items: LibraryCatalogItem[]): LibraryFacetItem[] {
  const counts = new Map<string, number>()
  for (const item of items) counts.set(item.documentType, (counts.get(item.documentType) ?? 0) + 1)
  return [...counts.entries()].map(([value, count]) => ({ value, label: value, count }))
}

function normalizeSearch(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function publicationCountLabel(total: number) { return `${total} ${total === 1 ? 'publicación' : 'publicaciones'}` }
function contentTypeLabel(value: string) { return value === 'application/pdf' ? 'PDF' : value.includes('wordprocessingml') ? 'Word' : value.includes('spreadsheetml') ? 'Excel' : value.includes('presentationml') ? 'PowerPoint' : value.split('/').at(-1)?.toUpperCase() ?? value }
function formatBytes(value: number) { if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`; return `${(value / 1024 / 1024).toFixed(1)} MB` }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value)) }
function downloadName(item: LibraryCatalogItem) { const base = item.title.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-zA-Z0-9._ -]+/g, '').trim().replace(/\s+/g, '-').slice(0, 90) || 'documento'; return `${base}.${extensionFor(item.contentType)}` }
function extensionFor(contentType: string) { if (contentType === 'application/pdf') return 'pdf'; if (contentType === 'image/png') return 'png'; if (contentType === 'image/jpeg') return 'jpg'; if (contentType.includes('wordprocessingml')) return 'docx'; if (contentType.includes('spreadsheetml')) return 'xlsx'; if (contentType.includes('presentationml')) return 'pptx'; if (contentType === 'text/csv') return 'csv'; if (contentType === 'text/markdown') return 'md'; return 'txt' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
