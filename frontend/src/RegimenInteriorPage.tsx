import { type FormEvent, useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient, type RegimenInteriorSummary } from './api/pmgmApi'
import type { OrderRejectionAlert, RePresentationReviewRow, RePresentationReviewDecision } from './api/pmgmApi'
import './regimen.css'
import { organizationDisplayName } from './displayFormat'
import AssemblyPadronPanel from './AssemblyPadronPanel'
import InstitutionalIcon from './InstitutionalIcon'

type RegimenSection = 'home' | 'informe' | 'padron'

export default function RegimenInteriorPage({ api, initialSection = 'home' }: { api: PmgmApiClient; initialSection?: RegimenSection }) {
  const [section, setSection] = useState<RegimenSection>(initialSection)
  const today = chileDate(new Date())
  const [organizationId, setOrganizationId] = useState('')
  const [from, setFrom] = useState(`${today.slice(0, 4)}-01-01`)
  const [asOf, setAsOf] = useState(today)
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [summary, setSummary] = useState<RegimenInteriorSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [rejectionAlerts, setRejectionAlerts] = useState<OrderRejectionAlert[]>([])
  const [rePresentationRows, setRePresentationRows] = useState<RePresentationReviewRow[]>([])
  const [reviewId, setReviewId] = useState('')
  const [reviewReference, setReviewReference] = useState('')
  const [reviewNotes, setReviewNotes] = useState('')
  const [reviewBusy, setReviewBusy] = useState(false)
  const [reviewMessage, setReviewMessage] = useState<string|null>(null)

  const load = async (filters = { organizationId, from, asOf }) => {
    setLoading(true); setError(null)
    try { setSummary(await api.getRegimenInteriorSummary(filters)) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible cargar el reporte.') }
    finally { setLoading(false) }
  }

  useEffect(() => {
    let active = true
    Promise.all([api.getOrganizationOptions(), api.getRegimenInteriorSummary({ from: `${today.slice(0, 4)}-01-01`, asOf: today }), api.getOrderRejectionAlerts(), api.getRePresentationReviews()])
      .then(([orgs, report, alerts, reReviews]) => { if (active) { setOrganizations(orgs.items); setSummary(report); setRejectionAlerts(alerts.items); setRePresentationRows(reReviews.items); setReviewId(reReviews.items[0]?.requestId ?? '') } })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar Régimen Interior.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, today])

  const submit = (event: FormEvent) => { event.preventDefault(); void load() }
  async function resolveReview(status:RePresentationReviewDecision['status']){
    if(!reviewId || reviewBusy)return
    setReviewBusy(true);setReviewMessage(null);setError(null)
    try{
      await api.resolveRePresentation(reviewId,{status,sourceReference:reviewReference.trim(),notes:reviewNotes.trim()||null})
      const queue=await api.getRePresentationReviews()
      setRePresentationRows(queue.items)
      setReviewMessage('Resolución de Régimen Interior registrada con trazabilidad. La autorización continúa sujeta a los demás vistos buenos.')
      setReviewReference('');setReviewNotes('')
    }catch(reason){setError(reason instanceof Error?reason.message:'No fue posible registrar la resolución.')}
    finally{setReviewBusy(false)}
  }

  return <>
    <section className="page-heading"><div><p className="eyebrow">Control institucional agregado</p><h1>Régimen Interior</h1><p>Estado de membresía, movimientos, grados y regularidad financiera, por Orden o Taller.</p></div><span className="count-badge">Corte {formatDateOnly(summary?.asOf ?? asOf)}</span></section>
    {error && <div className="error-banner" role="alert"><strong>No fue posible obtener el reporte.</strong><span>{error}</span></div>}
    {section === 'home' ? <nav className="workspace-tabs is-hub" aria-label="Secciones de Régimen Interior">
      <p className="section-hub-intro">Elige qué quieres hacer.</p>
      <div className="section-hub-grid">
        <button type="button" className="section-hub-card" onClick={() => { setSection('informe'); window.scrollTo({ top: 0 }) }}><span className="section-hub-icon" aria-hidden="true"><InstitutionalIcon name="report" size={26} /></span><strong>Informe y revisiones</strong><small>Membresía, alertas de rechazo y reinsinuaciones</small></button>
        <button type="button" className="section-hub-card" onClick={() => { setSection('padron'); window.scrollTo({ top: 0 }) }}><span className="section-hub-icon" aria-hidden="true"><InstitutionalIcon name="members" size={26} /></span><strong>Padrón de la Gran Asamblea</strong><small>Asambleístas, sufragantes habilitados y saldos de los Talleres</small></button>
      </div>
    </nav> : <nav className="workspace-tabs is-open-section" aria-label="Sección de Régimen Interior">
      <button type="button" className="secondary section-hub-back" onClick={() => { setSection('home'); window.scrollTo({ top: 0 }) }}>← Volver a Régimen Interior</button>
      <strong className="section-hub-current">{section === 'padron' ? 'Padrón de la Gran Asamblea' : 'Informe y revisiones'}</strong>
    </nav>}
    {section === 'padron' && <AssemblyPadronPanel api={api} />}
    {section === 'informe' && <>
    <section className="panel report-filter"><form onSubmit={submit} className="report-filter-form">
      <Field label="Ámbito"><select value={organizationId} onChange={e => setOrganizationId(e.target.value)}><option value="">Toda la Orden</option>{organizations.map(o => <option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></Field>
      <Field label="Desde"><input type="date" required value={from} onChange={e => setFrom(e.target.value)} /></Field>
      <Field label="Fecha de corte"><input type="date" required value={asOf} onChange={e => setAsOf(e.target.value)} /></Field>
      <button className="primary-action" disabled={loading}>Actualizar reporte</button>
    </form></section>
    {loading && !summary ? <div className="panel"><p>Cargando reporte institucional…</p></div> : summary && <Report summary={summary} />}
    <section className="panel report-wide"><div className="panel-heading"><div><p className="eyebrow">Antecedentes transversales · acceso restringido</p><h2>Alertas de candidatos rechazados en Cámara del Medio</h2><p>Consulta para detectar una ficha previa en otro Taller antes de autorizar una nueva presentación.</p></div><span className="count-badge">{rejectionAlerts.length} alertas</span></div>{rejectionAlerts.length === 0 ? <p className="muted">No hay rechazos registrados.</p> : <div className="table-wrap"><table><thead><tr><th>Candidato</th><th>Taller que rechazó</th><th>Fecha</th><th>Referencia</th></tr></thead><tbody>{rejectionAlerts.map((alert, index) => <tr key={`${alert.personId}-${alert.rejectionDate}-${index}`}><td><strong>{alert.firstNames} {alert.lastNames}</strong><small>ID transversal protegido</small></td><td>{organizationDisplayName(alert.workshopName, alert.workshopNumber)}</td><td>{formatDateOnly(alert.rejectionDate)}</td><td>{alert.sourceReference ?? 'Sin referencia'}</td></tr>)}</tbody></table></div>}</section>
    <section className="panel report-wide" aria-label="Revisión de nuevas presentaciones">
      <div className="panel-heading">
        <div><p className="eyebrow">Régimen Interior · acceso restringido</p><h2>Revisión de reinsinuaciones</h2>
          <p>Examine la constancia de subsanación de Secretaría. Sólo una resolución formal aprobada habilita el requisito; observar o rechazar mantiene bloqueada la autorización.</p></div>
        <span className="count-badge">{rePresentationRows.filter(x=>x.status!=='approved').length} sin conformidad</span>
      </div>
      {rePresentationRows.length===0?<p className="muted">No hay constancias de nueva presentación pendientes de revisión.</p>:<>
        <div className="table-wrap"><table><thead><tr><th>Persona</th><th>Taller</th><th>Estado RI</th><th>Respaldo</th><th>Revisar</th></tr></thead>
        <tbody>{rePresentationRows.map(row=><tr key={row.requestId}>
          <td>{row.personName}</td><td>{row.workshopName}</td>
          <td><span className={row.status==='approved'?'status-pill complete':'status-pill attention'}>{row.status==='approved'?'Aprobada':row.status==='rejected'?'Rechazada':'Observada'}</span></td>
          <td>{row.originalSourceReference??row.sourceReference??'Sin referencia'}{row.originalSourceReference&&row.sourceReference!==row.originalSourceReference?<small>Última resolución: {row.sourceReference}</small>:null}</td>
          <td><button className="secondary-action" type="button" aria-label={`Revisar ${row.personName}`} onClick={()=>{setReviewId(row.requestId);setReviewReference('');setReviewNotes('');setReviewMessage(null)}}>Revisar</button></td>
        </tr>)}</tbody></table></div>
        {reviewId&&<article className="panel"><h3>Resolución institucional del expediente seleccionado</h3>
          <p className="muted">{rePresentationRows.find(x=>x.requestId===reviewId)?.personName} · Las decisiones anteriores permanecen auditadas.</p>
          <div className="report-filter-form">
            <Field label="Referencia de resolución o acta *"><input maxLength={240} required value={reviewReference} onChange={e=>setReviewReference(e.target.value)} placeholder="ACTA-RI-2026-001"/></Field>
            <Field label="Observaciones"><input maxLength={1000} value={reviewNotes} onChange={e=>setReviewNotes(e.target.value)} placeholder="Fundamento breve, sin datos sensibles"/></Field>
          </div>
          <div className="notification-actions">
            <button type="button" disabled={reviewBusy||!reviewReference.trim()} onClick={()=>void resolveReview('approved')}>Aprobar subsanación</button>
            <button type="button" disabled={reviewBusy||!reviewReference.trim()} onClick={()=>void resolveReview('observed')}>Observar</button>
            <button type="button" disabled={reviewBusy||!reviewReference.trim()} onClick={()=>void resolveReview('rejected')}>Rechazar</button>
          </div>
          {reviewMessage&&<p role="status">{reviewMessage}</p>}
        </article>}
      </>}
    </section>
    </>}
  </>
}

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
function organizationLabel(o: OrganizationOption) { return `${organizationDisplayName(o.name, o.number)}` }
function degreeLabel(value: string) { const normalized = value.toLowerCase(); if (normalized === 'apprentice') return 'Aprendiz'; if (normalized === 'fellowcraft') return 'Compañero'; if (normalized === 'master') return 'Maestro'; if (normalized === 'past_active') return 'PAS activo'; return value }
function formatDateOnly(value: string) { const [y, m, d] = value.split('-').map(Number); return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(y, m - 1, d, 12))) }
function chileDate(date: Date) { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(date); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
