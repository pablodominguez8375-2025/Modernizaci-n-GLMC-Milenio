import { useEffect, useMemo, useState } from 'react'
import { type DocumentApiClient, type LibraryDocument } from './api/documentApi'
import './documents.css'

export default function LibraryPage({ documentApi }: { documentApi: DocumentApiClient }) {
  const [items, setItems] = useState<LibraryDocument[]>([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    documentApi.getLibrary()
      .then(response => { if (active) setItems(response.items) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [documentApi])

  const filtered = useMemo(() => {
    const needle = normalize(query)
    return needle ? items.filter(item => normalize(`${item.title} ${item.documentType} ${item.collectionName}`).includes(needle)) : items
  }, [items, query])

  return <>
    <section className="page-heading">
      <div><p className="eyebrow">Conocimiento institucional</p><h1>Biblioteca Virtual</h1><p>Documentos publicados expresamente para su consulta según alcance y política de acceso.</p></div>
      <span className="count-badge">{loading ? 'cargando…' : `${filtered.length} publicaciones`}</span>
    </section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible abrir la Biblioteca.</strong><span>{error}</span></div>}
    <section className="panel">
      <label className="search-field"><span>Buscar en Biblioteca</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Título, colección o tipo documental" /></label>
      {loading ? <div className="loading-rows"><span /><span /><span /></div> : filtered.length === 0 ? <div className="empty-state"><strong>No hay publicaciones visibles.</strong><p>La Biblioteca sólo muestra documentos con una versión disponible y publicada.</p></div> : <div className="document-library-grid">{filtered.map(item => <LibraryCard key={item.id} item={item} />)}</div>}
    </section>
    <section className="document-info-strip"><strong>Acceso controlado</strong><span>La Biblioteca no expone nombres físicos de archivo, claves de almacenamiento, hash SHA-256 ni referencias técnicas de escaneo.</span></section>
  </>
}

function LibraryCard({ item }: { item: LibraryDocument }) {
  return <article className="library-card">
    <div className="library-icon" aria-hidden="true">▥</div>
    <div><span className="document-chip">{typeLabel(item.documentType)}</span><h3>{item.title}</h3><p>{item.collectionName}</p></div>
    <dl><div><dt>Versión</dt><dd>v{item.versionNumber}</dd></div><div><dt>Formato</dt><dd>{contentTypeLabel(item.contentType)}</dd></div><div><dt>Tamaño</dt><dd>{formatBytes(item.sizeBytes)}</dd></div></dl>
    <small>Publicado {formatChile(item.publishedAtUtc)}</small>
    <div className="document-pending-action">Lectura/descarga se habilitará al conectar el almacenamiento documental.</div>
  </article>
}

function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function typeLabel(value: string) { return value.replaceAll('_', ' ') }
function contentTypeLabel(value: string) { return value === 'application/pdf' ? 'PDF' : value.includes('word') ? 'Word' : value.split('/').at(-1)?.toUpperCase() ?? value }
function formatBytes(value: number) { if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`; return `${(value / 1024 / 1024).toFixed(1)} MB` }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value)) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
