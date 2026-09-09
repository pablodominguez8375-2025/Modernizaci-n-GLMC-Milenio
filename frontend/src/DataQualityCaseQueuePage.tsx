import { useEffect, useMemo, useState } from 'react'
import { type DataQualityCase, type DataQualityCaseApiClient } from './api/dataQualityCaseApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './dataQualityCaseQueue.css'

const STATUS_LABEL: Record<string, string> = { open: 'Abierto', under_review: 'En revisión', resolved_confirmed: 'Confirmado', dismissed: 'Descartado' }

export default function DataQualityCaseQueuePage({ api, caseApi }: { api: PmgmApiClient; caseApi: DataQualityCaseApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [status, setStatus] = useState('')
  const [organizationId, setOrganizationId] = useState('')
  const [assignedToMe, setAssignedToMe] = useState(false)
  const [items, setItems] = useState<DataQualityCase[]>([])
  const [allItems, setAllItems] = useState<DataQualityCase[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions().then(result => { if (active) setOrganizations(result.items.filter(x => x.type.toLowerCase() !== 'order')) }).catch(reason => { if (active) setError(message(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    setLoading(true); setError(null)
    Promise.all([
      caseApi.listCases({ status: status || undefined, organizationId: organizationId || undefined, assignedToMe, limit: 500 }),
      caseApi.listCases({ limit: 500 }),
    ]).then(([filtered, complete]) => {
      if (!active) return
      setItems(filtered.items); setAllItems(complete.items)
    }).catch(reason => { if (active) setError(message(reason)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [caseApi, status, organizationId, assignedToMe, refreshKey])

  const summary = useMemo(() => ({
    open: allItems.filter(x => x.status === 'open').length,
    review: allItems.filter(x => x.status === 'under_review').length,
    resolved: allItems.filter(x => x.status === 'resolved_confirmed').length,
    dismissed: allItems.filter(x => x.status === 'dismissed').length,
  }), [allItems])

  const refresh = () => setRefreshKey(value => value + 1)

  return <>
    <section className="page-heading case-queue-heading">
      <div><p className="eyebrow">Régimen Interior · trazabilidad</p><h1>Cola de corroboración</h1><p>Convierte observaciones de calidad en casos de revisión humana. Resolver un caso documenta la decisión; no modifica el dato fuente.</p></div>
      <button type="button" className="secondary-action" onClick={refresh}>Actualizar</button>
    </section>

    <section className="case-queue-kpis">
      <Metric label="Abiertos" value={summary.open} tone="open" />
      <Metric label="En revisión" value={summary.review} tone="review" />
      <Metric label="Confirmados" value={summary.resolved} tone="resolved" />
      <Metric label="Descartados" value={summary.dismissed} />
    </section>

    <section className="panel case-queue-filters">
      <label><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="">Todos</option><option value="open">Abiertos</option><option value="under_review">En revisión</option><option value="resolved_confirmed">Confirmados</option><option value="dismissed">Descartados</option></select></label>
      <label><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Toda la Orden</option>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}{item.number ? ` · Nº ${item.number}` : ''}</option>)}</select></label>
      <label className="case-checkbox"><input type="checkbox" checked={assignedToMe} onChange={event => setAssignedToMe(event.target.checked)} /><span>Asignados a mí</span></label>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible operar la cola.</strong><span>{error}</span></div>}

    <section className="panel case-queue-panel">
      <div className="panel-heading"><div><p className="eyebrow">Casos institucionales</p><h2>Revisión y resolución</h2></div><span className="count-badge">{loading ? '…' : items.length}</span></div>
      {loading ? <div className="loading-rows"><span /><span /><span /></div> : items.length === 0 ? <div className="empty-state"><strong>No hay casos para los filtros seleccionados.</strong><p>Los casos se abren explícitamente desde un hallazgo de Calidad de datos.</p></div> : <div className="case-queue-list">{items.map(item => <CaseCard key={item.id} item={item} caseApi={caseApi} onChanged={refresh} />)}</div>}
    </section>
  </>
}

function CaseCard({ item, caseApi, onChanged }: { item: DataQualityCase; caseApi: DataQualityCaseApiClient; onChanged: () => void }) {
  const [resolving, setResolving] = useState(false)
  const [outcome, setOutcome] = useState<'confirmed' | 'dismissed'>('confirmed')
  const [resolutionSummary, setResolutionSummary] = useState('')
  const [evidenceReference, setEvidenceReference] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const claim = async () => {
    setBusy(true); setError(null)
    try { await caseApi.claimCase(item.id); onChanged() } catch (reason) { setError(message(reason)) } finally { setBusy(false) }
  }
  const resolve = async () => {
    if (resolutionSummary.trim().length < 10) { setError('Describe el resultado de la corroboración con al menos 10 caracteres.'); return }
    setBusy(true); setError(null)
    try { await caseApi.resolveCase(item.id, { outcome, resolutionSummary: resolutionSummary.trim(), evidenceReference: evidenceReference.trim() || undefined }); setResolving(false); onChanged() } catch (reason) { setError(message(reason)) } finally { setBusy(false) }
  }

  return <article className={`case-card status-${item.status}`}>
    <div className="case-card-top"><div><span className={`case-status ${item.status}`}>{STATUS_LABEL[item.status] ?? item.status}</span><span className={`case-severity ${item.severity}`}>{item.severity === 'error' ? 'Error' : 'Advertencia'}</span></div><code>{item.ruleCode}</code></div>
    <div className="case-card-main">
      <div><h3>{ruleLabel(item.ruleCode)}</h3><p><strong>{item.displayName}</strong>{item.institutionalNumber ? ` · ${item.institutionalNumber}` : ''}</p><small>{item.organizationName ?? 'Ámbito Orden'} · detectado al {dateLabel(item.detectionAsOf)}</small></div>
      <dl><div><dt>Fecha observada</dt><dd>{dateLabel(item.primaryDate)}</dd></div><div><dt>Relacionada</dt><dd>{dateLabel(item.relatedDate)}</dd></div><div><dt>Asignación</dt><dd>{item.assignedToDisplayName ?? (item.assignedToSubject ? 'Revisor asignado' : 'Sin asignar')}</dd></div></dl>
    </div>

    {item.resolutionSummary && <div className="case-resolution"><small>Resolución</small><strong>{item.resolutionSummary}</strong>{item.evidenceReference && <span>Respaldo: {item.evidenceReference}</span>}</div>}
    {error && <p className="case-inline-error" role="alert">{error}</p>}

    <div className="case-actions">
      {item.status === 'open' && <button type="button" disabled={busy} onClick={() => void claim()}>{busy ? 'Tomando…' : 'Tomar caso'}</button>}
      {item.status === 'under_review' && <button type="button" disabled={busy} onClick={() => setResolving(value => !value)}>{resolving ? 'Cerrar resolución' : 'Resolver caso'}</button>}
      <span>{item.events.length > 0 ? `${item.events.length} movimientos registrados` : `Actualizado ${dateTimeLabel(item.updatedAtUtc)}`}</span>
    </div>

    {resolving && <div className="case-resolve-form">
      <label><span>Resultado</span><select value={outcome} onChange={event => setOutcome(event.target.value as 'confirmed' | 'dismissed')}><option value="confirmed">Hallazgo confirmado</option><option value="dismissed">Hallazgo descartado</option></select></label>
      <label className="case-resolution-text"><span>Resultado de la corroboración</span><textarea rows={3} maxLength={2000} value={resolutionSummary} onChange={event => setResolutionSummary(event.target.value)} placeholder="Indique qué se corroboró y qué acción institucional corresponde." /></label>
      <label><span>Referencia de respaldo</span><input maxLength={500} value={evidenceReference} onChange={event => setEvidenceReference(event.target.value)} placeholder="Ej.: ACTA-023-2026" /></label>
      <button type="button" disabled={busy} onClick={() => void resolve()}>{busy ? 'Guardando…' : 'Registrar resolución'}</button>
    </div>}
  </article>
}

function Metric({ label, value, tone }: { label: string; value: number; tone?: string }) { return <article className={`metric-card case-metric ${tone ?? ''}`}><span>{label}</span><strong>{value}</strong><small>casos</small></article> }
function dateLabel(value: string | null) { return value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) : '—' }
function dateTimeLabel(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
function ruleLabel(value: string) { return value.split('_').map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(' ') }
function message(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
