import { useEffect, useMemo, useState } from 'react'
import { type DataQualityCaseApiClient } from './api/dataQualityCaseApi'
import { type DataQualityIssue, type DataQualityResponse, type InternalAffairsApiClient } from './api/internalAffairsApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './internalAffairsDataQuality.css'

const RULE_LABELS: Record<string, string> = {
  invalid_membership_range: 'Rango de afiliación inválido', multiple_current_memberships: 'Más de una afiliación vigente', overlapping_workshop_memberships: 'Afiliaciones de Taller superpuestas', active_status_without_current_membership: 'Estado activo sin afiliación vigente',
  missing_initiation_before_wage_increase: 'Aumento sin iniciación registrada', missing_wage_increase_before_exaltation: 'Exaltación sin aumento registrado', wage_increase_before_initiation: 'Aumento anterior a iniciación', exaltation_before_wage_increase: 'Exaltación anterior al aumento', duplicate_degree_milestone: 'Hito de grado duplicado',
  reinstatement_without_prior_withdrawal: 'Reintegro sin retiro previo', consecutive_withdrawals_without_reinstatement: 'Retiros consecutivos sin reintegro', status_event_after_death: 'Evento institucional posterior a defunción', degree_event_after_death: 'Grado posterior a defunción', office_after_death: 'Cargo posterior a defunción', active_membership_after_death: 'Afiliación vigente tras defunción',
  invalid_office_range: 'Período de cargo inválido', transfer_same_source_and_target: 'Traslado con mismo origen y destino', transfer_effective_before_request: 'Traslado efectivo antes de solicitud', approved_transfer_before_request: 'Traslado aprobado antes de solicitud', executed_transfer_without_target_membership: 'Traslado sin afiliación destino', target_membership_date_mismatch: 'Fecha de destino no coincide con traslado', transfer_outside_source_membership: 'Traslado fuera de afiliación origen',
}

export default function InternalAffairsDataQualityPage({ api, internalAffairsApi, caseApi, onOpenCases }: { api: PmgmApiClient; internalAffairsApi: InternalAffairsApiClient; caseApi: DataQualityCaseApiClient; onOpenCases?: () => void }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [asOf, setAsOf] = useState(todayInSantiago())
  const [severity, setSeverity] = useState('')
  const [code, setCode] = useState('')
  const [search, setSearch] = useState('')
  const [response, setResponse] = useState<DataQualityResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [openingKey, setOpeningKey] = useState<string | null>(null)
  const [caseNotice, setCaseNotice] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions().then(result => { if (active) setOrganizations(result.items.filter(item => item.type.toLowerCase() !== 'order')) }).catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      setLoading(true); setError(null)
      internalAffairsApi.getDataQuality({ asOf, organizationId: organizationId || undefined, severity: severity || undefined, code: code || undefined, search: search.trim() || undefined, limit: 250 })
        .then(result => { if (active) setResponse(result) }).catch(reason => { if (active) setError(toMessage(reason)) }).finally(() => { if (active) setLoading(false) })
    }, 180)
    return () => { active = false; window.clearTimeout(timer) }
  }, [asOf, organizationId, severity, code, search, internalAffairsApi])

  const codes = useMemo(() => Object.entries(response?.summary.byCode ?? {}).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])), [response])
  const openCase = async (item: DataQualityIssue) => {
    const key = `${item.memberId}-${item.code}-${item.organizationId ?? 'order'}`
    setOpeningKey(key); setCaseNotice(null); setError(null)
    try {
      const result = await caseApi.openCase({ detectionAsOf: asOf, issue: item })
      setCaseNotice(`Caso ${result.status === 'under_review' ? 'ya en revisión' : 'disponible en la cola'}: ${ruleLabel(result.ruleCode)}.`)
    } catch (reason) { setError(toMessage(reason)) } finally { setOpeningKey(null) }
  }

  return <>
    <section className="page-heading data-quality-heading"><div><p className="eyebrow">Régimen Interior · control preventivo</p><h1>Calidad de datos institucionales</h1><p>Detecta secuencias y fechas que requieren corroboración. El motor no modifica el historial; un hallazgo puede transformarse explícitamente en un caso de revisión.</p></div><span className="data-quality-readonly">Detección read-only</span></section>
    <section className="panel data-quality-filters" aria-label="Filtros de calidad de datos">
      <label><span>Fecha de corte</span><input type="date" value={asOf} onChange={event => setAsOf(event.target.value)} /></label>
      <label><span>Taller relacionado</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Toda la Orden</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <label><span>Severidad</span><select value={severity} onChange={event => setSeverity(event.target.value)}><option value="">Todas</option><option value="error">Error</option><option value="warning">Advertencia</option></select></label>
      <label><span>Regla</span><select value={code} onChange={event => setCode(event.target.value)}><option value="">Todas las reglas</option>{codes.map(([item, count]) => <option key={item} value={item}>{ruleLabel(item)} ({count})</option>)}</select></label>
      <label className="data-quality-search"><span>Buscar</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Nombre, número institucional, Taller o regla" /></label>
    </section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    {caseNotice && <div className="data-quality-case-notice" role="status"><span>{caseNotice}</span>{onOpenCases && <button type="button" onClick={onOpenCases}>Ir a Cola de corroboración</button>}</div>}
    <section className="data-quality-kpis" aria-label="Resumen de observaciones">
      <Metric label="Errores" value={loading ? '…' : String(response?.summary.errors ?? 0)} detail="Inconsistencias de alta prioridad" tone="error" /><Metric label="Advertencias" value={loading ? '…' : String(response?.summary.warnings ?? 0)} detail="Casos que requieren corroboración" tone="warning" /><Metric label="Miembros afectados" value={loading ? '…' : String(response?.summary.affectedMembers ?? 0)} detail="Personas con al menos una observación" /><Metric label="Resultados visibles" value={loading ? '…' : String(response?.returned ?? 0)} detail={`${response?.total ?? 0} coincidencias con los filtros`} />
    </section>
    <section className="data-quality-layout">
      <article className="panel data-quality-results"><div className="panel-heading"><div><p className="eyebrow">Observaciones detectadas</p><h2>Casos para corroborar</h2></div><span className="count-badge">{loading ? '…' : response?.total ?? 0}</span></div>{loading ? <Loading /> : !response || response.items.length === 0 ? <div className="empty-state"><strong>No se detectaron observaciones con estos filtros.</strong><p>Esto no certifica por sí solo la integridad del padrón; indica que las reglas automáticas no encontraron inconsistencias en el alcance consultado.</p></div> : <div className="data-quality-list">{response.items.map((item, index) => { const key = `${item.memberId}-${item.code}-${item.organizationId ?? 'order'}`; return <IssueCard key={`${key}-${item.primaryDate ?? 'none'}-${index}`} item={item} busy={openingKey === key} onOpen={() => void openCase(item)} /> })}</div>}</article>
      <aside className="panel data-quality-rules"><div className="panel-heading"><div><p className="eyebrow">Cobertura</p><h2>Reglas activas</h2></div></div>{codes.length === 0 ? <div className="empty-state compact"><strong>Sin reglas activadas por los datos del alcance.</strong></div> : <div className="rule-list">{codes.slice(0, 12).map(([item, count]) => <button key={item} type="button" className={code === item ? 'active' : ''} onClick={() => setCode(code === item ? '' : item)}><span>{ruleLabel(item)}</span><strong>{count}</strong></button>)}</div>}<p className="data-quality-note"><strong>Principio de control:</strong> detectar no equivale a corregir. La modificación de la fuente requiere revisión humana y respaldo institucional.</p></aside>
    </section>
  </>
}

function IssueCard({ item, busy, onOpen }: { item: DataQualityIssue; busy: boolean; onOpen: () => void }) {
  return <article className={`data-quality-issue ${item.severity === 'error' ? 'error' : 'warning'}`}><div className="issue-topline"><span className={`issue-severity ${item.severity}`}>{item.severity === 'error' ? 'Error' : 'Advertencia'}</span><code>{item.code}</code></div><div className="issue-main"><div><h3>{item.title}</h3><p>{item.description}</p></div><div className="issue-person"><strong>{item.displayName}</strong><span>{item.institutionalNumber ?? 'Sin número institucional'}</span><small>{item.organizationName ?? 'Ámbito Orden'}</small></div></div><div className="issue-dates"><span>Fecha observada <strong>{dateLabel(item.primaryDate)}</strong></span>{item.relatedDate && <span>Fecha relacionada <strong>{dateLabel(item.relatedDate)}</strong></span>}</div><div className="issue-action"><span aria-hidden="true">✓</span><div><small>Acción sugerida</small><strong>{item.suggestedAction}</strong></div></div><div className="issue-case-action"><button type="button" onClick={onOpen} disabled={busy}>{busy ? 'Abriendo caso…' : 'Abrir caso de corroboración'}</button><span>Se validará que el hallazgo siga vigente antes de crear el caso.</span></div></article>
}

function Metric({ label, value, detail, tone }: { label: string; value: string; detail: string; tone?: 'error' | 'warning' }) { return <article className={`metric-card data-quality-metric ${tone ?? ''}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></article> }
function Loading() { return <div className="loading-rows"><span /><span /><span /></div> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function dateLabel(value: string | null) { return value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) : '—' }
function ruleLabel(value: string) { return RULE_LABELS[value] ?? value.split('_').map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(' ') }
function todayInSantiago() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const map = Object.fromEntries(parts.map(part => [part.type, part.value])); return `${map.year}-${map.month}-${map.day}` }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
