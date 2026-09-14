import { useEffect, useMemo, useState } from 'react'
import { type ExecutiveReport, type ExecutiveWorkshopRow, type ReportingApiClient } from './api/reportingApi'
import './reporting.css'

export default function ExecutiveReportingPage({ reportingApi }: { reportingApi: ReportingApiClient }) {
  const today = todayInChile()
  const [asOf, setAsOf] = useState(today)
  const [from, setFrom] = useState(`${today.slice(0, 4)}-01-01`)
  const [report, setReport] = useState<ExecutiveReport | null>(null)
  const [query, setQuery] = useState('')
  const [attentionOnly, setAttentionOnly] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!asOf || !from || from > asOf) return
    let active = true
    setLoading(true); setError(null)
    reportingApi.getExecutiveReport({ asOf, from })
      .then(value => { if (active) setReport(value) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [reportingApi, asOf, from])

  const workshops = useMemo(() => {
    const normalized = normalize(query)
    return (report?.workshops ?? [])
      .filter(item => !attentionOnly || item.attentionRequired)
      .filter(item => !normalized || normalize(`${item.name} ${item.number ?? ''}`).includes(normalized))
      .sort((a, b) => Number(b.attentionRequired) - Number(a.attentionRequired) || b.blockingMembers - a.blockingMembers || a.name.localeCompare(b.name, 'es'))
  }, [report, query, attentionOnly])

  return <>
    <section className="page-heading">
      <div>
        <p className="eyebrow">Régimen Interior · visión de la Orden</p>
        <h1>Reportería Ejecutiva</h1>
        <p>Comparativo consolidado por Taller, sin exponer datos personales innecesarios.</p>
      </div>
      <span className="count-badge">{loading ? 'calculando…' : `${workshops.length} Talleres`}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible generar el reporte.</strong><span>{error}</span></div>}
    {from > asOf && <div className="error-banner" role="alert"><strong>Período inválido.</strong><span>La fecha inicial debe ser anterior o igual a la fecha de corte.</span></div>}

    <section className="reporting-toolbar panel">
      <label><span>Desde</span><input type="date" value={from} onChange={event => setFrom(event.target.value)} /></label>
      <label><span>Corte</span><input type="date" value={asOf} onChange={event => setAsOf(event.target.value)} /></label>
      <label className="reporting-search"><span>Buscar Taller</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Nombre o número" /></label>
      <label className="reporting-check"><input type="checkbox" checked={attentionOnly} onChange={event => setAttentionOnly(event.target.checked)} /><span>Sólo con alertas</span></label>
    </section>

    {report && <>
      <section className="reporting-kpis" aria-label="Resumen ejecutivo de la Orden">
        <Kpi label="Miembros vigentes" value={report.overview.currentMembers} detail={`${report.overview.activeMembers} activos`} />
        <Kpi label="Inactivos" value={report.overview.inactiveMembers} detail={`${report.overview.blockingMembers} con estado bloqueante`} tone={report.overview.blockingMembers > 0 ? 'attention' : undefined} />
        <Kpi label="Past Active" value={report.overview.pastActive} detail="según trayectoria de cargos" />
        <Kpi label="Morosos" value={report.overview.financial.delinquentMembersDistinct} detail={`${report.overview.financial.delinquentAffiliations} afiliaciones`} tone={report.overview.financial.delinquentMembersDistinct > 0 ? 'attention' : undefined} />
        <Kpi label="Talleres con alertas" value={report.overview.workshopsRequiringAttention} detail={`de ${report.overview.workshops}`} tone={report.overview.workshopsRequiringAttention > 0 ? 'attention' : undefined} />
        <Kpi label="Traslados pendientes" value={report.overview.pendingTransfers} detail={`${report.overview.events.transfers} ejecutados en período`} tone={report.overview.pendingTransfers > 0 ? 'attention' : undefined} />
      </section>

      <section className="reporting-events panel">
        <div><span>Retiros voluntarios</span><strong>{report.overview.events.voluntaryWithdrawals}</strong></div>
        <div><span>Retiros forzosos</span><strong>{report.overview.events.forcedWithdrawals}</strong></div>
        <div><span>Reintegros</span><strong>{report.overview.events.reinstatements}</strong></div>
        <div><span>Defunciones</span><strong>{report.overview.events.deaths}</strong></div>
        <div><span>Talleres morosos</span><strong>{report.overview.financial.workshopsTreasuryDelinquent}</strong></div>
        <div><span>Hospitalaria vencida</span><strong>{report.overview.financial.workshopsHospitalariaOverdue}</strong></div>
      </section>

      <section className="panel reporting-table-panel">
        <div className="panel-heading">
          <div><p className="eyebrow">Comparativo institucional</p><h2>Estado por Taller</h2></div>
          <small>Corte {formatDate(report.asOf)} · período {formatDate(report.period.from)} a {formatDate(report.period.to)}</small>
        </div>
        <div className="reporting-table-wrap">
          <table className="reporting-table">
            <thead><tr><th>Taller</th><th>Miembros</th><th>Estado</th><th>Grados</th><th>Past Active</th><th>Tesorería</th><th>Hospitalaria</th><th>Actividad</th><th>Alertas</th></tr></thead>
            <tbody>
              {workshops.map(item => <WorkshopRow key={item.organizationId} item={item} />)}
            </tbody>
          </table>
        </div>
        {workshops.length === 0 && <div className="empty-state"><strong>No hay Talleres que coincidan con el filtro.</strong></div>}
      </section>

      <section className="panel reporting-note">
        <p><strong>Definición operativa de Past Active:</strong> {report.pastActiveDefinition}</p>
        <p>La reportería entrega indicadores agregados. No expone correo, teléfono, dirección ni antecedentes documentales del hermano.</p>
      </section>
    </>}
  </>
}

function Kpi({ label, value, detail, tone }: { label: string; value: number; detail: string; tone?: 'attention' }) {
  return <article className={tone === 'attention' ? 'panel reporting-kpi attention' : 'panel reporting-kpi'}><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>
}

function WorkshopRow({ item }: { item: ExecutiveWorkshopRow }) {
  return <tr>
    <td><strong>{item.name}</strong><small>{item.number ? `Nº ${item.number}` : 'Sin número'}</small></td>
    <td><strong>{item.currentMembers}</strong><small>{item.activeMembers} activos</small></td>
    <td><span className={item.blockingMembers > 0 ? 'reporting-status attention' : 'reporting-status good'}>{item.blockingMembers > 0 ? `${item.blockingMembers} bloqueantes` : 'Sin bloqueos'}</span><small>{item.inactiveMembers} inactivos</small></td>
    <td><span>{degreeSummary(item.degreeDistribution)}</span></td>
    <td><strong>{item.pastActive}</strong></td>
    <td><RegularityStatus value={item.financial.workshopTreasuryStatus} /><small>{item.financial.delinquentAffiliations} afiliaciones morosas</small></td>
    <td><RegularityStatus value={item.financial.workshopHospitalariaStatus} /></td>
    <td><span>{item.activity.meetings} Tenidas</span><small>{item.activity.instructionSessions} docencias · {item.activity.pendingTransfers} traslados pendientes</small></td>
    <td>{item.attentionRequired ? <div className="reporting-flags">{item.attentionFlags.map(flag => <span key={flag}>{flagLabel(flag)}</span>)}</div> : <span className="reporting-status good">Sin alertas</span>}</td>
  </tr>
}

function RegularityStatus({ value }: { value: string | null }) {
  const good = value === 'up_to_date' || value === 'exempt'
  const label = value === 'up_to_date' ? 'Al día' : value === 'delinquent' ? 'Moroso' : value === 'overdue' ? 'Vencido' : value === 'pending' ? 'Pendiente' : value === 'exempt' ? 'Exento' : 'Sin estado'
  return <span className={good ? 'reporting-status good' : value ? 'reporting-status attention' : 'reporting-status pending'}>{label}</span>
}

function degreeSummary(value: Record<string, number>) {
  const labels: Record<string, string> = { apprentice: 'A', fellowcraft: 'C', master: 'M' }
  const entries = Object.entries(value)
  if (entries.length === 0) return 'Sin grado registrado'
  return entries.map(([degree, total]) => `${labels[degree] ?? degree}: ${total}`).join(' · ')
}

function flagLabel(value: string) {
  if (value === 'treasury_delinquent') return 'Tesorería'
  if (value === 'hospitalaria_overdue') return 'Hospitalaria'
  if (value === 'blocking_member_status') return 'Estados'
  if (value === 'pending_transfers') return 'Traslados'
  return value
}

function todayInChile() {
  const parts = new Intl.DateTimeFormat('en', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const get = (type: string) => parts.find(item => item.type === type)?.value ?? ''
  return `${get('year')}-${get('month')}-${get('day')}`
}
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'Error inesperado al generar el reporte.' }
