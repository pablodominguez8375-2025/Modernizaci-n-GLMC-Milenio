import { useEffect, useMemo, useState } from 'react'
import { type GrandArchiveApiClient, type GrandArchiveCandidate, type GrandArchiveRecord, type GrandArchiveRecordType } from './api/grandArchiveApi'
import './grandArchive.css'

const TYPES: Array<{ value: GrandArchiveRecordType; label: string }> = [
  { value: 'decree', label: 'Decreto' }, { value: 'communication', label: 'Comunicado' }, { value: 'minutes', label: 'Acta' },
  { value: 'resolution', label: 'Resolución' }, { value: 'regulation', label: 'Reglamento' }, { value: 'correspondence', label: 'Correspondencia' },
  { value: 'historical_record', label: 'Registro histórico' }, { value: 'other', label: 'Otro' },
]

export default function GrandArchivePage({ archiveApi }: { archiveApi: GrandArchiveApiClient }) {
  const [items, setItems] = useState<GrandArchiveRecord[]>([])
  const [candidates, setCandidates] = useState<GrandArchiveCandidate[]>([])
  const [status, setStatus] = useState('active')
  const [recordType, setRecordType] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [showRegister, setShowRegister] = useState(false)

  useEffect(() => {
    let active = true
    setLoading(true); setError(null)
    Promise.all([
      archiveApi.list({ status: status || undefined, recordType: recordType || undefined, search: search || undefined, limit: 500 }),
      archiveApi.candidates(),
    ]).then(([records, candidateResponse]) => {
      if (!active) return
      setItems(records.items); setCandidates(candidateResponse.items)
    }).catch(reason => { if (active) setError(message(reason)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [archiveApi, status, recordType, search, refreshKey])

  const activeCount = useMemo(() => items.filter(x => x.status === 'active').length, [items])
  const refresh = () => setRefreshKey(value => value + 1)

  return <>
    <section className="page-heading archive-heading">
      <div><p className="eyebrow">Patrimonio documental · Gran Logia</p><h1>Gran Archivero</h1><p>Catálogo histórico institucional separado de Biblioteca Virtual. Referencia versiones documentales verificadas sin duplicar archivos.</p></div>
      <button type="button" onClick={() => setShowRegister(value => !value)}>{showRegister ? 'Cerrar incorporación' : 'Incorporar documento'}</button>
    </section>

    <section className="archive-kpis"><article className="metric-card"><span>Registros visibles</span><strong>{loading ? '…' : items.length}</strong></article><article className="metric-card"><span>Activos</span><strong>{loading ? '…' : activeCount}</strong></article><article className="metric-card"><span>Candidatos</span><strong>{loading ? '…' : candidates.length}</strong></article></section>

    {showRegister && <RegisterPanel archiveApi={archiveApi} candidates={candidates} onCreated={() => { setShowRegister(false); refresh() }} />}

    <section className="panel archive-filters">
      <label><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="">Todos</option><option value="active">Activos</option><option value="withdrawn">Retirados</option></select></label>
      <label><span>Tipo</span><select value={recordType} onChange={event => setRecordType(event.target.value)}><option value="">Todos</option>{TYPES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label className="archive-search"><span>Buscar</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Código, título, período…" /></label>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible consultar Gran Archivero.</strong><span>{error}</span></div>}
    <section className="panel">
      <div className="panel-heading"><div><p className="eyebrow">Catálogo archivístico</p><h2>Documentos históricos</h2></div><span className="count-badge">{loading ? '…' : items.length}</span></div>
      {loading ? <div className="loading-rows"><span /><span /><span /></div> : items.length === 0 ? <div className="empty-state"><strong>No hay registros para los filtros seleccionados.</strong></div> : <div className="archive-list">{items.map(item => <ArchiveCard key={item.id} item={item} archiveApi={archiveApi} onChanged={refresh} />)}</div>}
    </section>
  </>
}

function RegisterPanel({ archiveApi, candidates, onCreated }: { archiveApi: GrandArchiveApiClient; candidates: GrandArchiveCandidate[]; onCreated: () => void }) {
  const [selected, setSelected] = useState(candidates[0]?.documentVersionId ?? '')
  const [archiveCode, setArchiveCode] = useState('')
  const [recordType, setRecordType] = useState<GrandArchiveRecordType>('historical_record')
  const [documentDate, setDocumentDate] = useState('')
  const [originatingBody, setOriginatingBody] = useState('Gran Logia Mixta de Chile')
  const [historicalPeriod, setHistoricalPeriod] = useState('')
  const [description, setDescription] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const candidate = candidates.find(x => x.documentVersionId === selected)

  useEffect(() => { if (!selected && candidates[0]) setSelected(candidates[0].documentVersionId) }, [candidates, selected])

  const submit = async () => {
    if (!candidate) { setError('Seleccione una versión documental disponible.'); return }
    if (archiveCode.trim().length < 3) { setError('Ingrese un código archivístico válido.'); return }
    setBusy(true); setError(null)
    try {
      await archiveApi.register({ documentId: candidate.documentId, documentVersionId: candidate.documentVersionId, archiveCode: archiveCode.trim(), recordType, documentDate: documentDate || null, originatingBody: originatingBody.trim() || null, historicalPeriod: historicalPeriod.trim() || null, description: description.trim() || null })
      onCreated()
    } catch (reason) { setError(message(reason)) } finally { setBusy(false) }
  }

  return <section className="panel archive-register">
    <div className="panel-heading"><div><p className="eyebrow">Ingreso controlado</p><h2>Incorporar versión documental</h2></div></div>
    <p className="archive-policy-note">Sólo aparecen documentos de ámbito Gran Logia/Orden cuya versión está disponible después de integridad y antivirus. Las planchas de trabajo no son elegibles.</p>
    {candidates.length === 0 ? <div className="empty-state"><strong>No hay versiones documentales elegibles.</strong></div> : <div className="archive-form-grid">
      <label className="archive-wide"><span>Documento elegible</span><select value={selected} onChange={event => setSelected(event.target.value)}>{candidates.map(item => <option key={item.documentVersionId} value={item.documentVersionId}>{item.title} · v{item.versionNumber}</option>)}</select></label>
      <label><span>Código archivístico</span><input maxLength={120} value={archiveCode} onChange={event => setArchiveCode(event.target.value)} placeholder="GA-2026-001" /></label>
      <label><span>Tipo archivístico</span><select value={recordType} onChange={event => setRecordType(event.target.value as GrandArchiveRecordType)}>{TYPES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label><span>Fecha del documento</span><input type="date" value={documentDate} onChange={event => setDocumentDate(event.target.value)} /></label>
      <label><span>Órgano de origen</span><input maxLength={240} value={originatingBody} onChange={event => setOriginatingBody(event.target.value)} /></label>
      <label><span>Período histórico</span><input maxLength={160} value={historicalPeriod} onChange={event => setHistoricalPeriod(event.target.value)} placeholder="Ej.: 1980–1989" /></label>
      <label className="archive-wide"><span>Descripción archivística</span><textarea rows={3} maxLength={2000} value={description} onChange={event => setDescription(event.target.value)} /></label>
      {error && <p className="case-inline-error archive-wide" role="alert">{error}</p>}
      <div className="archive-wide archive-submit"><button type="button" disabled={busy} onClick={() => void submit()}>{busy ? 'Incorporando…' : 'Incorporar al Gran Archivero'}</button></div>
    </div>}
  </section>
}

function ArchiveCard({ item, archiveApi, onChanged }: { item: GrandArchiveRecord; archiveApi: GrandArchiveApiClient; onChanged: () => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const download = async () => { setBusy(true); setError(null); try { const blob = await archiveApi.download(item.id); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = item.originalFileName || `${item.archiveCode}.bin`; anchor.click(); URL.revokeObjectURL(url) } catch (reason) { setError(message(reason)) } finally { setBusy(false) } }
  const withdraw = async () => { const reason = window.prompt('Indique el motivo del retiro archivístico (mínimo 10 caracteres):'); if (!reason) return; setBusy(true); setError(null); try { await archiveApi.withdraw(item.id, reason); onChanged() } catch (cause) { setError(message(cause)) } finally { setBusy(false) } }
  return <article className={`archive-card ${item.status}`}>
    <div className="archive-card-top"><div><code>{item.archiveCode}</code><span className={`status-pill ${item.status === 'active' ? 'complete' : ''}`}>{item.status === 'active' ? 'Activo' : 'Retirado'}</span></div><span>{typeLabel(item.recordType)}</span></div>
    <h3>{item.title}</h3><p>{item.description ?? 'Sin descripción archivística adicional.'}</p>
    <dl className="archive-meta"><div><dt>Fecha documento</dt><dd>{dateLabel(item.documentDate)}</dd></div><div><dt>Origen</dt><dd>{item.originatingBody ?? '—'}</dd></div><div><dt>Período</dt><dd>{item.historicalPeriod ?? '—'}</dd></div><div><dt>Versión</dt><dd>{item.versionNumber ? `v${item.versionNumber}` : '—'}</dd></div></dl>
    {item.withdrawalReason && <div className="archive-withdrawal"><strong>Retirado del catálogo activo</strong><span>{item.withdrawalReason}</span></div>}
    {error && <p className="case-inline-error" role="alert">{error}</p>}
    <div className="archive-actions">{item.status === 'active' && <><button type="button" disabled={busy} onClick={() => void download()}>Abrir contenido</button><button type="button" className="secondary-action" disabled={busy} onClick={() => void withdraw()}>Retirar</button></>}<small>Archivado {dateTimeLabel(item.archivedAtUtc)}</small></div>
  </article>
}

function typeLabel(value: string) { return TYPES.find(x => x.value === value)?.label ?? value }
function dateLabel(value: string | null) { return value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) : '—' }
function dateTimeLabel(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
function message(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
