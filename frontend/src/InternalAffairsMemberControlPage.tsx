import { useEffect, useMemo, useState } from 'react'
import { type HistoricalIntakeReviewItem, type InternalAffairsApiClient, type MemberControlRow } from './api/internalAffairsApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './internalAffairsControl.css'

export default function InternalAffairsMemberControlPage({ api, internalAffairsApi }: { api: PmgmApiClient; internalAffairsApi: InternalAffairsApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [asOf, setAsOf] = useState(todayInChile())
  const [status, setStatus] = useState('')
  const [degree, setDegree] = useState('')
  const [financialStatus, setFinancialStatus] = useState('')
  const [search, setSearch] = useState('')
  const [pastActiveOnly, setPastActiveOnly] = useState(false)
  const [pendingTransferOnly, setPendingTransferOnly] = useState(false)
  const [rows, setRows] = useState<MemberControlRow[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [historicalIntakes, setHistoricalIntakes] = useState<HistoricalIntakeReviewItem[]>([])
  const [historicalNotes, setHistoricalNotes] = useState<Record<string,string>>({})
  const [reviewingId, setReviewingId] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => { if (active) setOrganizations(response.items.filter(item => item.type.toLowerCase() !== 'order')) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    internalAffairsApi.getPendingHistoricalIntakes(organizationId || undefined)
      .then(response => { if(active) setHistoricalIntakes(response.items) })
      .catch(reason => { if(active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [internalAffairsApi, organizationId])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      setLoading(true); setError(null)
      internalAffairsApi.getMembers({ asOf, organizationId: organizationId || undefined, status: status || undefined, degree: degree || undefined, financialStatus: financialStatus || undefined, search: search || undefined, pastActiveOnly, pendingTransferOnly, limit: 500 })
        .then(response => { if (active) { setRows(response.items); setTotal(response.total) } })
        .catch(reason => { if (active) setError(toMessage(reason)) })
        .finally(() => { if (active) setLoading(false) })
    }, 180)
    return () => { active = false; window.clearTimeout(timer) }
  }, [internalAffairsApi, asOf, organizationId, status, degree, financialStatus, search, pastActiveOnly, pendingTransferOnly])

  const resolveHistorical = async (item: HistoricalIntakeReviewItem, decision:'approve'|'observe'|'reject') => {
    setReviewingId(item.id); setError(null)
    try {
      await internalAffairsApi.reviewHistoricalIntake(item.id,decision,historicalNotes[item.id]?.trim()||null)
      const response=await internalAffairsApi.getPendingHistoricalIntakes(organizationId||undefined)
      setHistoricalIntakes(response.items)
      const control=await internalAffairsApi.getMembers({asOf,organizationId:organizationId||undefined,limit:500})
      setRows(control.items); setTotal(control.total)
    } catch(reason){ setError(toMessage(reason)) } finally { setReviewingId(null) }
  }

  const counters = useMemo(() => ({
    current: rows.filter(row => row.relation === 'current').length,
    historical: rows.filter(row => row.relation === 'historical').length,
    delinquent: rows.filter(row => row.financialStatus === 'delinquent').length,
    pastActive: rows.filter(row => row.pastActive).length,
    pendingTransfers: rows.filter(row => row.pendingTransfer).length,
  }), [rows])

  return <>
    <section className="page-heading">
      <div><p className="eyebrow">Régimen Interior · control operativo</p><h1>Control de miembros</h1><p>Fechas, estados y trayectoria institucional para revisión autorizada de la Orden.</p></div>
      <span className="count-badge">{loading ? 'consultando…' : `${total} registros`}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible cargar el control de miembros.</strong><span>{error}</span></div>}

    <section className="internal-control-filters panel">
      <label><span>Corte</span><input type="date" value={asOf} onChange={event => setAsOf(event.target.value)} /></label>
      <label><span>Taller relacionado</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Toda la Orden</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <label><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="">Todos</option>{statusOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      <label><span>Grado</span><select value={degree} onChange={event => setDegree(event.target.value)}><option value="">Todos</option><option value="apprentice">Aprendiz</option><option value="fellowcraft">Compañero</option><option value="master">Maestro</option></select></label>
      <label><span>Finanzas</span><select value={financialStatus} onChange={event => setFinancialStatus(event.target.value)}><option value="">Todos</option><option value="up_to_date">Al día</option><option value="delinquent">Moroso</option><option value="pending">Pendiente</option><option value="exempt">Exento</option><option value="no_status">Sin estado</option></select></label>
      <label className="internal-control-search"><span>Buscar</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Nombre, Nº institucional o Taller" /></label>
      <label className="internal-control-check"><input type="checkbox" checked={pastActiveOnly} onChange={event => setPastActiveOnly(event.target.checked)} /><span>Sólo Past Active</span></label>
      <label className="internal-control-check"><input type="checkbox" checked={pendingTransferOnly} onChange={event => setPendingTransferOnly(event.target.checked)} /><span>Traslado pendiente</span></label>
    </section>

    <section className="internal-control-summary" aria-label="Resumen del listado">
      <Summary label="Vigentes" value={counters.current} />
      <Summary label="Relación histórica" value={counters.historical} />
      <Summary label="Morosos" value={counters.delinquent} attention={counters.delinquent > 0} />
      <Summary label="Past Active" value={counters.pastActive} />
      <Summary label="Traslados pendientes" value={counters.pendingTransfers} attention={counters.pendingTransfers > 0} />
    </section>

    <section className="panel internal-control-table-panel">
      <div className="panel-heading"><div><p className="eyebrow">Puesta en marcha · Cuadro del Taller</p><h2>Cargas históricas pendientes de validación</h2></div><span className="count-badge">{historicalIntakes.length} pendientes</span></div>
      <p className="form-note">Secretaría del Taller propone altas o actualizaciones de hermanos ya activos. La información sólo pasa al Cuadro oficial después de esta validación de Régimen Interior.</p>
      <div className="historical-review-list">
        {historicalIntakes.map(item => <article key={item.id} className="historical-review-card">
          <div><strong>{item.firstNames} {item.lastNames}</strong><span>{degreeLabel(item.currentDegree)} · corte {formatDate(item.cutoffDate)}</span><small>{item.institutionalNumber || 'Sin Nº institucional'}{item.rut ? ` · RUT ${item.rut}` : ''}</small><small>Fuente: {item.evidenceReference}</small><small>Hitos: iniciación {formatDateOptional(item.initiationDate)} · aumento {formatDateOptional(item.wageIncreaseDate)} · exaltación {formatDateOptional(item.exaltationDate)}</small>{item.offices.length>0 && <small>Cargos: {item.offices.map(o=>`${o.officeType} (${o.period})`).join(', ')}</small>}</div>
          <label><span>Observación de RI</span><textarea value={historicalNotes[item.id] ?? ''} onChange={e=>setHistoricalNotes(current=>({...current,[item.id]:e.target.value}))} placeholder="Obligatoria al observar o rechazar" /></label>
          <div className="historical-review-actions">
            <button type="button" disabled={reviewingId!==null} onClick={()=>void resolveHistorical(item,'approve')}>Aprobar y actualizar Cuadro</button>
            <button type="button" disabled={reviewingId!==null || !(historicalNotes[item.id]??'').trim()} onClick={()=>void resolveHistorical(item,'observe')}>Observar</button>
            <button type="button" disabled={reviewingId!==null || !(historicalNotes[item.id]??'').trim()} onClick={()=>void resolveHistorical(item,'reject')}>Rechazar</button>
          </div>
        </article>)}
        {historicalIntakes.length===0 && <div className="empty-state"><strong>No hay cargas históricas pendientes de validación.</strong></div>}
      </div>
    </section>

    <section className="panel internal-control-table-panel">
      <div className="panel-heading"><div><p className="eyebrow">Trazabilidad institucional</p><h2>Miembros relacionados</h2></div><small>Máximo 500 filas por consulta · sin datos de contacto</small></div>
      <div className="internal-control-table-wrap">
        <table className="internal-control-table">
          <thead><tr><th>Hermano/a</th><th>Relación / Taller</th><th>Estado</th><th>Grado</th><th>Fechas masónicas</th><th>Retiros / reintegro</th><th>Finanzas</th><th>Historial</th></tr></thead>
          <tbody>{rows.map(row => <MemberRow key={row.memberId} row={row} />)}</tbody>
        </table>
      </div>
      {!loading && rows.length === 0 && <div className="empty-state"><strong>No hay registros que coincidan con los filtros.</strong></div>}
    </section>
  </>
}

function MemberRow({ row }: { row: MemberControlRow }) {
  const workshop = row.currentWorkshop ?? row.lastWorkshop
  return <tr>
    <td><strong>{row.displayName}</strong><small>{row.institutionalNumber || 'Sin Nº institucional'}</small></td>
    <td><span className={row.relation === 'current' ? 'internal-control-pill good' : 'internal-control-pill neutral'}>{row.relation === 'current' ? 'Vigente' : 'Histórica'}</span><strong>{workshop.name}</strong><small>{workshop.number ? `Nº ${workshop.number}` : ''}{row.currentWorkshop ? ` · desde ${formatDateOptional(row.currentWorkshop.startDate)}` : row.lastWorkshop.endDate ? ` · hasta ${formatDate(row.lastWorkshop.endDate)}` : ''}</small></td>
    <td><span className={statusClass(row.currentStatus)}>{statusLabel(row.currentStatus)}</span>{row.statusEffectiveDate && <small>desde {formatDate(row.statusEffectiveDate)}</small>}{row.pendingTransfer && <span className="internal-control-pill attention">Traslado pendiente</span>}</td>
    <td><strong>{degreeLabel(row.currentDegree)}</strong>{row.pastActive && <span className="internal-control-pill accent">Past Active</span>}</td>
    <td><Milestone label="Iniciación" value={row.milestones.initiation} /><Milestone label="Aumento" value={row.milestones.wageIncrease} /><Milestone label="Exaltación" value={row.milestones.exaltation} /></td>
    <td><Milestone label={withdrawalLabel(row.milestones.withdrawalType)} value={row.milestones.withdrawal} /><Milestone label="Reintegro" value={row.milestones.reinstatement} /><Milestone label="Defunción" value={row.milestones.death} /><Milestone label="Traslado" value={row.milestones.transfer} /></td>
    <td><FinancialStatus value={row.financialStatus} /></td>
    <td><strong>{row.membershipHistoryCount}</strong><small>afiliación(es) registradas</small></td>
  </tr>
}

function Summary({ label, value, attention }: { label: string; value: number; attention?: boolean }) { return <article className={attention ? 'panel internal-control-summary-card attention' : 'panel internal-control-summary-card'}><span>{label}</span><strong>{value}</strong></article> }
function Milestone({ label, value }: { label: string; value: string | null }) { if (!value) return null; return <span className="internal-control-date"><small>{label}</small>{formatDate(value)}</span> }
function FinancialStatus({ value }: { value: string | null }) { const good = value === 'up_to_date' || value === 'exempt'; return <span className={good ? 'internal-control-pill good' : value ? 'internal-control-pill attention' : 'internal-control-pill neutral'}>{financialLabel(value)}</span> }

const statusOptions = [['active', 'Activo'], ['inactive', 'Inactivo'], ['voluntary_withdrawal', 'Retiro voluntario'], ['forced_withdrawal', 'Retiro forzoso'], ['reinstated', 'Reintegrado'], ['deceased', 'Fallecido']] as const
function statusLabel(value: string) { return statusOptions.find(([code]) => code === value)?.[1] ?? value }
function statusClass(value: string) { return value === 'active' || value === 'reinstated' ? 'internal-control-pill good' : value === 'inactive' ? 'internal-control-pill neutral' : 'internal-control-pill attention' }
function degreeLabel(value: string | null) { return value === 'apprentice' ? 'Aprendiz' : value === 'fellowcraft' ? 'Compañero' : value === 'master' ? 'Maestro' : value || 'Sin grado registrado' }
function financialLabel(value: string | null) { return value === 'up_to_date' ? 'Al día' : value === 'delinquent' ? 'Moroso' : value === 'pending' ? 'Pendiente' : value === 'exempt' ? 'Exento' : 'Sin estado' }
function withdrawalLabel(value: string | null) { return value === 'forced_withdrawal' ? 'Retiro forzoso' : value === 'voluntary_withdrawal' ? 'Retiro voluntario' : 'Retiro' }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function formatDateOptional(value: string | null) { return value ? formatDate(value) : 'sin fecha exacta' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: string) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'Error inesperado al consultar Régimen Interior.' }
