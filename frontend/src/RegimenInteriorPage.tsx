import { type FormEvent, useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient, type RegimenInteriorSummary } from './api/pmgmApi'
import './regimen.css'

export default function RegimenInteriorPage({ api }: { api: PmgmApiClient }) {
  const today = chileDate(new Date())
  const [organizationId, setOrganizationId] = useState('')
  const [from, setFrom] = useState(`${today.slice(0, 4)}-01-01`)
  const [asOf, setAsOf] = useState(today)
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [summary, setSummary] = useState<RegimenInteriorSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = async (filters = { organizationId, from, asOf }) => {
    setLoading(true); setError(null)
    try { setSummary(await api.getRegimenInteriorSummary(filters)) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible cargar el reporte.') }
    finally { setLoading(false) }
  }

  useEffect(() => {
    let active = true
    Promise.all([api.getOrganizationOptions(), api.getRegimenInteriorSummary({ from: `${today.slice(0, 4)}-01-01`, asOf: today })])
      .then(([orgs, report]) => { if (active) { setOrganizations(orgs.items); setSummary(report) } })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar Régimen Interior.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, today])

  const submit = (event: FormEvent) => { event.preventDefault(); void load() }

  return <>
    <section className="page-heading"><div><p className="eyebrow">Control institucional agregado</p><h1>Régimen Interior</h1><p>Estado de membresía, movimientos, grados y regularidad financiera, por Orden o Taller.</p></div><span className="count-badge">Corte {formatDateOnly(summary?.asOf ?? asOf)}</span></section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible obtener el reporte.</strong><span>{error}</span></div>}
    <section className="panel report-filter"><form onSubmit={submit} className="report-filter-form">
      <Field label="Ámbito"><select value={organizationId} onChange={e => setOrganizationId(e.target.value)}><option value="">Toda la Orden</option>{organizations.map(o => <option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></Field>
      <Field label="Desde"><input type="date" required value={from} onChange={e => setFrom(e.target.value)} /></Field>
      <Field label="Fecha de corte"><input type="date" required value={asOf} onChange={e => setAsOf(e.target.value)} /></Field>
      <button className="primary-action" disabled={loading}>Actualizar reporte</button>
    </form></section>
    {loading && !summary ? <div className="panel"><p>Cargando reporte institucional…</p></div> : summary && <Report summary={summary} />}
    {api.useMocks && <AssemblyRosterDemo />}
  </>
}

const assemblyRosterDemo = [
  { id: 'asm-a', name: 'Persona Demostrativa A', lodge: 'Taller Demostrativo Nº 1', category: 'Asambleísta permanente', history: 'Período 2022 completado', alert: 'Sin observaciones', source: 'Historial institucional de cargos', attend: true, vote: true, status: 'enabled' },
  { id: 'asm-b', name: 'Persona Demostrativa B', lodge: 'Taller Demostrativo Nº 23', category: 'Representante', history: 'Primer período vigente', alert: 'Taller moroso · Gran Tesorería', source: 'Snapshot financiero de Gran Tesorería', attend: true, vote: false, status: 'observed' },
  { id: 'asm-c', name: 'Persona Demostrativa C', lodge: 'Taller Demostrativo Nº 8', category: 'Asambleísta permanente', history: 'Reelecto como Venerable Maestro', alert: 'Inhabilidad vigente de asistencia y sufragio', source: 'Efecto institucional reservado', attend: false, vote: false, status: 'disabled' },
  { id: 'asm-d', name: 'Persona Demostrativa D', lodge: 'Taller Demostrativo Nº 14', category: 'Asambleísta permanente', history: 'Período 2024 completado', alert: 'Designación pendiente de validar', source: 'Gestión de cargos del Taller', attend: false, vote: false, status: 'pending' },
]

type AssemblyRosterItem = typeof assemblyRosterDemo[number]
type AssemblyReviewStatus = 'enabled' | 'attendance_only' | 'observed' | 'disabled' | 'pending'

function AssemblyRosterDemo() {
  const [items, setItems] = useState<AssemblyRosterItem[]>(() => assemblyRosterDemo.map(item => ({ ...item })))
  const [selectedId, setSelectedId] = useState(items[1].id)
  const [closed, setClosed] = useState(false)
  const selected = items.find(item => item.id === selectedId) ?? items[0]
  const unresolved = items.filter(item => item.status === 'pending' || item.status === 'observed').length

  const decide = (status: AssemblyReviewStatus, alert: string, attend: boolean, vote: boolean) => {
    if (closed) return
    setItems(current => current.map(item => item.id === selected.id ? { ...item, status, alert, attend, vote } : item))
  }

  return <section className="panel report-wide"><div className="panel-heading"><div><p className="eyebrow">Gran Asamblea · datos ficticios</p><h2>Padrón preliminar de asambleístas</h2><p>Clasificación automática y alertas revisadas por Régimen Interior antes del cierre inmutable.</p></div><div><span className="count-badge">{unresolved} pendientes</span> <button className="primary-action" type="button" disabled={closed || unresolved > 0} onClick={() => setClosed(true)}>{closed ? 'Padrón cerrado · versión 1' : 'Cerrar padrón'}</button></div></div>
    <div className="table-wrap"><table><thead><tr><th>Integrante</th><th>Calidad calculada</th><th>Alerta u observación</th><th>Asiste</th><th>Vota</th><th></th></tr></thead><tbody>{items.map(item => <tr key={item.id}><td><strong>{item.name}</strong><small>{item.lodge}</small></td><td>{item.category}<small>{item.history}</small></td><td>{item.alert}</td><td><span className={item.attend ? 'status-pill complete' : 'status-pill attention'}>{item.attend ? 'Sí' : 'No'}</span></td><td><span className={item.vote ? 'status-pill complete' : 'status-pill attention'}>{item.vote ? 'Sí' : 'No'}</span></td><td><button className="secondary-action" type="button" onClick={() => setSelectedId(item.id)}>Revisar</button></td></tr>)}</tbody></table></div>
    <article className="panel"><p className="eyebrow">Detalle de revisión</p><h3>{selected.name}</h3><dl className="candidate-meta"><div><dt>Calidad</dt><dd>{selected.category}</dd></div><div><dt>Historial</dt><dd>{selected.history}</dd></div><div><dt>Fuente</dt><dd>{selected.source}</dd></div><div><dt>Resultado actual</dt><dd>{reviewStatusLabel(selected.status)}</dd></div></dl><p><strong>Alerta:</strong> {selected.alert}</p>
      <div className="notification-actions"><button type="button" disabled={closed} onClick={() => decide('enabled', 'Revisión conforme por Régimen Interior', true, true)}>Confirmar habilitación</button><button type="button" disabled={closed} onClick={() => decide('attendance_only', 'Habilitado únicamente para asistir', true, false)}>Solo asistencia</button><button type="button" disabled={closed} onClick={() => decide('observed', 'Corrección solicitada a la unidad responsable', false, false)}>Solicitar corrección</button><button type="button" disabled={closed} onClick={() => decide('pending', 'Validación derivada; respuesta pendiente', false, false)}>Derivar validación</button><button type="button" disabled={closed} onClick={() => decide('disabled', 'Inhabilidad confirmada por Régimen Interior', false, false)}>Confirmar inhabilidad</button></div>
    </article>
    {unresolved > 0 && <p className="report-footnote">El cierre está bloqueado hasta resolver {unresolved} observación(es). Una unidad externa debe corregir su propia fuente; Régimen Interior conserva la trazabilidad.</p>}
    <p className="report-footnote">El padrón identifica quién puede asistir y sufragar, pero nunca registra ni permite reconstruir cómo votó una persona.</p>
  </section>
}

function reviewStatusLabel(value: string) { return value === 'enabled' ? 'Habilitado' : value === 'attendance_only' ? 'Solo asistencia' : value === 'disabled' ? 'Inhabilitado' : value === 'observed' ? 'Observado' : 'Pendiente de respuesta' }

function Report({ summary }: { summary: RegimenInteriorSummary }) {
  const degrees = Object.entries(summary.degreeDistribution).sort(([a], [b]) => a.localeCompare(b, 'es'))
  return <div className="report-layout">
    <section className="metric-grid report-metrics" aria-label="Estado de membresía">
      <Metric label="Afiliados actuales" value={summary.members.currentlyAffiliated} detail={`${summary.members.totalRelated} relacionados históricamente`} />
      <Metric label="Activos" value={summary.members.active} detail="Activos o reintegrados" />
      <Metric label="Inactivos" value={summary.members.inactive} detail={`${summary.members.currentWithBlockingStatus} con estado bloqueante`} />
      <Metric label="Morosos" value={summary.financialRegularity.delinquentMembersDistinct} detail="Miembros distintos · Gran Tesorería" />
    </section>

    <section className="report-grid">
      <article className="panel"><p className="eyebrow">Movimientos del período</p><h2>Eventos institucionales</h2><div className="stat-list">
        <Stat label="Retiros voluntarios" value={summary.events.voluntaryWithdrawals} />
        <Stat label="Retiros forzosos" value={summary.events.forcedWithdrawals} />
        <Stat label="Reintegros" value={summary.events.reinstatements} />
        <Stat label="Defunciones" value={summary.events.deaths} />
        <Stat label="Traslados de Taller" value={summary.events.transfers} />
        <Stat label="Traslados pendientes" value={summary.pendingTransfers} />
      </div></article>

      <article className="panel"><p className="eyebrow">Gran Tesorería</p><h2>Regularidad financiera</h2><div className="stat-list">
        <Stat label="Al día" value={summary.financialRegularity.upToDate} tone="good" />
        <Stat label="Morosos" value={summary.financialRegularity.delinquent} tone="bad" />
        <Stat label="Pendientes" value={summary.financialRegularity.pending} />
        <Stat label="Exentos" value={summary.financialRegularity.exempt} />
        <Stat label="Sin estado informado" value={summary.financialRegularity.withoutStatus} />
        <Stat label="Afiliaciones evaluadas" value={summary.financialRegularity.currentAffiliations} />
      </div></article>

      <article className="panel report-wide"><div className="panel-heading"><div><p className="eyebrow">Grados</p><h2>Distribución actual</h2></div><span className="count-badge">{degrees.reduce((sum, [, value]) => sum + value, 0)} registros</span></div>
        {degrees.length === 0 ? <p className="muted">Sin eventos de grado para el ámbito seleccionado.</p> : <div className="degree-grid">{degrees.map(([degree, count]) => <div key={degree}><span>{degreeLabel(degree)}</span><strong>{count}</strong></div>)}</div>}
      </article>
    </section>
    <p className="report-footnote">Período: {formatDateOnly(summary.period.from)} a {formatDateOnly(summary.period.to)} · Fuente financiera: {summary.financialRegularity.source}. El reporte es agregado y no expone fichas personales.</p>
  </div>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="field"><span>{label}</span>{children}</label> }
function Metric({ label, value, detail }: { label: string; value: number; detail: string }) { return <article className="metric-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article> }
function Stat({ label, value, tone }: { label: string; value: number; tone?: 'good' | 'bad' }) { return <div className={`stat-row ${tone ?? ''}`}><span>{label}</span><strong>{value}</strong></div> }
function organizationLabel(o: OrganizationOption) { return `${o.name}${o.number ? ` · Nº ${o.number}` : ''}` }
function degreeLabel(value: string) { const normalized = value.toLowerCase(); if (normalized === 'apprentice') return 'Aprendiz'; if (normalized === 'fellowcraft') return 'Compañero'; if (normalized === 'master') return 'Maestro'; return value }
function formatDateOnly(value: string) { const [y, m, d] = value.split('-').map(Number); return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(y, m - 1, d, 12))) }
function chileDate(date: Date) { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(date); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
