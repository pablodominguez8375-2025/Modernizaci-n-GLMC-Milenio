import { useEffect, useMemo, useState } from 'react'
import { type DataQualityIssue, type DataQualityResponse, type InternalAffairsApiClient } from './api/internalAffairsApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './internalAffairsDataQuality.css'

export default function InternalAffairsDataQualityPage({ api, internalAffairsApi }: { api: PmgmApiClient; internalAffairsApi: InternalAffairsApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [asOf, setAsOf] = useState('2026-09-09')
  const [severity, setSeverity] = useState('')
  const [code, setCode] = useState('')
  const [search, setSearch] = useState('')
  const [response, setResponse] = useState<DataQualityResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(result => { if (active) setOrganizations(result.items.filter(item => item.type.toLowerCase() !== 'order')) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      setLoading(true); setError(null)
      internalAffairsApi.getDataQuality({
        asOf,
        organizationId: organizationId || undefined,
        severity: severity || undefined,
        code: code || undefined,
        search: search.trim() || undefined,
        limit: 250,
      })
        .then(result => { if (active) setResponse(result) })
        .catch(reason => { if (active) setError(toMessage(reason)) })
        .finally(() => { if (active) setLoading(false) })
    }, 180)
    return () => { active = false; window.clearTimeout(timer) }
  }, [asOf, organizationId, severity, code, search, internalAffairsApi])

  const codes = useMemo(() => Object.entries(response?.summary.byCode ?? {}).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])), [response])

  return <>
    <section className="page-heading data-quality-heading">
      <div>
        <p className="eyebrow">Régimen Interior · control preventivo</p>
        <h1>Calidad de datos institucionales</h1>
        <p>Detecta secuencias y fechas que requieren corroboración. El módulo es sólo lectura: ninguna observación modifica el historial automáticamente.</p>
      </div>
      <span className="data-quality-readonly">Sólo revisión</span>
    </section>

    <section className="panel data-quality-filters" aria-label="Filtros de calidad de datos">
      <label><span>Fecha de corte</span><input type="date" value={asOf} onChange={event => setAsOf(event.target.value)} /></label>
      <label><span>Taller relacionado</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Toda la Orden</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <label><span>Severidad</span><select value={severity} onChange={event => setSeverity(event.target.value)}><option value="">Todas</option><option value="error">Error</option><option value="warning">Advertencia</option></select></label>
      <label><span>Regla</span><select value={code} onChange={event => setCode(event.target.value)}><option value="">Todas las reglas</option>{codes.map(([item, count]) => <option key={item} value={item}>{ruleLabel(item)} ({count})</option>)}</select></label>
      <label className="data-quality-search"><span>Buscar</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Nombre, número institucional, Taller o regla" /></label>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible ejecutar la revisión.</strong><span>{error}</span></div>}

    <section className="data-quality-kpis" aria-label="Resumen de observaciones">
      <Metric label="Errores" value={loading ? '…' : String(response?.summary.errors ?? 0)} detail="Inconsistencias de alta prioridad" tone="error" />
      <Metric label="Advertencias" value={loading ? '…' : String(response?.summary.warnings ?? 0)} detail="Casos que requieren corroboración" tone="warning" />
      <Metric label="Miembros afectados" value={loading ? '…' : String(response?.summary.affectedMembers ?? 0)} detail="Personas con al menos una observación" />
      <Metric label="Resultados visibles" value={loading ? '…' : String(response?.returned ?? 0)} detail={`${response?.total ?? 0} coincidencias con los filtros`} />
    </section>

    <section className="data-quality-layout">
      <article className="panel data-quality-results">
        <div className="panel-heading"><div><p className="eyebrow">Observaciones detectadas</p><h2>Casos para corroborar</h2></div><span className="count-badge">{loading ? '…' : response?.total ?? 0}</span></div>
        {loading ? <Loading /> : !response || response.items.length === 0 ? <div className="empty-state"><strong>No se detectaron observaciones con estos filtros.</strong><p>Esto no certifica por sí solo la integridad del padrón; indica que las reglas automáticas no encontraron inconsistencias en el alcance consultado.</p></div> : <div className="data-quality-list">{response.items.map((item, index) => <IssueCard key={`${item.memberId}-${item.code}-${item.primaryDate ?? 'none'}-${index}`} item={item} />)}</div>}
      </article>

      <aside className="panel data-quality-rules">
        <div className="panel-heading"><div><p className="eyebrow">Cobertura</p><h2>Reglas activas</h2></div></div>
        {codes.length === 0 ? <div className="empty-state compact"><strong>Sin reglas activadas por los datos del alcance.</strong></div> : <div className="rule-list">{codes.slice(0, 12).map(([item, count]) => <button key={item} type="button" className={code === item ? 'active' : ''} onClick={() => setCode(code === item ? '' : item)}><span>{ruleLabel(item)}</span><strong>{count}</strong></button>)}</div>}
        <p className="data-quality-note"><strong>Principio de control:</strong> la observación indica qué revisar y sugiere una acción, pero la corrección requiere validación humana y respaldo institucional.</p>
      </aside>
    </section>
  </>
}

function IssueCard({ item }: { item: DataQualityIssue }) {
  return <article className={`data-quality-issue ${item.severity === 'error' ? 'error' : 'warning'}`}>
    <div className="issue-topline"><span className={`issue-severity ${item.severity}`}>{item.severity === 'error' ? 'Error' : 'Advertencia'}</span><code>{item.code}</code></div>
    <div className="issue-main">
      <div><h3>{item.title}</h3><p>{item.description}</p></div>
      <div className="issue-person"><strong>{item.displayName}</strong><span>{item.institutionalNumber ?? 'Sin número institucional'}</span><small>{item.organizationName ?? 'Ámbito Orden'}</small></div>
    </div>
    <div className="issue-dates"><span>Fecha observada <strong>{dateLabel(item.primaryDate)}</strong></span>{item.relatedDate && <span>Fecha relacionada <strong>{dateLabel(item.relatedDate)}</strong></span>}</div>
    <div className="issue-action"><span aria-hidden="true">✓</span><div><small>Acción sugerida</small><strong>{item.suggestedAction}</strong></div></div>
  </article>
}

function Metric({ label, value, detail, tone }: { label: string; value: string; detail: string; tone?: 'error' | 'warning' }) { return <article className={`metric-card data-quality-metric ${tone ?? ''}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></article> }
function Loading() { return <div className="loading-rows"><span /><span /><span /></div> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function dateLabel(value: string | null) { return value ? new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) : '—' }
function ruleLabel(value: string) { return value.split('_').map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(' ') }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
