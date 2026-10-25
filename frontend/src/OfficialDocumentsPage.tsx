import { useEffect, useMemo, useState } from 'react'
import { audienceLabel, type GrandArchiveApiClient, type GrandArchiveRecord } from './api/grandArchiveApi'
import './grandArchive.css'

const TYPES = [
  { value: 'decree', label: 'Decretos' }, { value: 'communication', label: 'Comunicados' }, { value: 'resolution', label: 'Resoluciones' },
  { value: 'regulation', label: 'Reglamentos' }, { value: 'minute', label: 'Actas' }, { value: 'other', label: 'Otros' },
]
const TYPE_ONE: Record<string, string> = { decree: 'Decreto', communication: 'Comunicado', resolution: 'Resolución', regulation: 'Reglamento', minute: 'Acta', other: 'Documento' }

/** Vista de solo lectura para todos los Hermanos: el servidor entrega únicamente lo publicado para su grado (#653). */
export default function OfficialDocumentsPage({ archiveApi }: { archiveApi: GrandArchiveApiClient }) {
  const [items, setItems] = useState<GrandArchiveRecord[]>([])
  const [recordType, setRecordType] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    setLoading(true); setError(null)
    archiveApi.listOfficial({ recordType: recordType || undefined, search: search || undefined, limit: 500 })
      .then(response => { if (active) setItems(response.items) })
      .catch(reason => { if (active) setError(message(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [archiveApi, recordType, search])

  const byYear = useMemo(() => {
    const groups = new Map<string, GrandArchiveRecord[]>()
    for (const item of items) { const year = item.documentDate?.slice(0, 4) ?? 'Sin fecha'; groups.set(year, [...(groups.get(year) ?? []), item]) }
    return [...groups.entries()]
  }, [items])

  const open = async (item: GrandArchiveRecord) => {
    setBusyId(item.id); setError(null)
    try { const blob = await archiveApi.downloadOfficial(item.id); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = item.originalFileName || `${item.archiveCode}.pdf`; anchor.click(); URL.revokeObjectURL(url) }
    catch (reason) { setError(message(reason)) } finally { setBusyId(null) }
  }

  return <div className="archive-page official-docs">
    <section className="page-heading archive-heading">
      <div><p className="eyebrow">Gran Archivero · Gran Logia</p><h1>Documentos oficiales</h1><p>Decretos, comunicados, reglamentos y resoluciones de la Orden publicados para usted según su grado.</p></div>
    </section>
    <section className="panel archive-filters">
      <label><span>Tipo de documento</span><select value={recordType} onChange={event => setRecordType(event.target.value)}><option value="">Todos</option>{TYPES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label className="archive-search"><span>Buscar</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Título o código…" /></label>
    </section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible consultar los documentos.</strong><span>{error}</span></div>}
    {loading ? <section className="panel"><div className="loading-rows"><span /><span /><span /></div></section>
      : items.length === 0 ? <section className="panel"><div className="empty-state"><strong>No hay documentos oficiales publicados para usted.</strong><span>Cuando el Gran Archivero publique uno para su grado, aparecerá aquí.</span></div></section>
      : byYear.map(([year, docs]) => <section className="panel official-year" key={year}>
        <div className="panel-heading"><div><p className="eyebrow">Año</p><h2>{year}</h2></div><span className="count-badge">{docs.length}</span></div>
        <ul className="official-list">{docs.map(item => <li key={item.id} className="official-row">
          <div className="official-main">
            <span className="official-type">{TYPE_ONE[item.recordType] ?? 'Documento'} · <code>{item.archiveCode}</code></span>
            <strong>{item.title}</strong>
            {item.description && <span className="official-desc">{item.description}</span>}
            <span className="official-meta">{dateLabel(item.documentDate)} · <span className={`audience-chip degree-${item.publishedMinimumDegree ?? 0}`}>{audienceLabel(item.publishedMinimumDegree)}</span></span>
          </div>
          <button type="button" className="primary-action" disabled={busyId === item.id} onClick={() => void open(item)}>{busyId === item.id ? 'Abriendo…' : 'Abrir documento'}</button>
        </li>)}</ul>
      </section>)}
  </div>
}

function dateLabel(value: string | null) { return value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) : 'Sin fecha' }
function message(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
