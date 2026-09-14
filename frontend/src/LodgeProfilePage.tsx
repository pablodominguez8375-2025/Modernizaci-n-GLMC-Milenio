import { useEffect, useMemo, useState } from 'react'
import { type OrganizationProfile, type OrganizationProfileApiClient } from './api/organizationProfileApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './lodgeProfile.css'

export default function LodgeProfilePage({ api, organizationProfileApi }: { api: PmgmApiClient; organizationProfileApi: OrganizationProfileApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [profile, setProfile] = useState<OrganizationProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => {
        if (!active) return
        const workshops = response.items.filter(item => item.type.toLowerCase() !== 'order')
        setOrganizations(workshops)
        setOrganizationId(workshops[0]?.id ?? '')
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    if (!organizationId) { setProfile(null); return }
    let active = true
    setLoading(true); setError(null)
    organizationProfileApi.getProfile(organizationId)
      .then(response => { if (active) setProfile(response) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [organizationId, organizationProfileApi])

  const degrees = useMemo(() => {
    const entries = Object.entries(profile?.members.degreeDistribution ?? {})
    return entries.sort((a, b) => b[1] - a[1])
  }, [profile])

  return <>
    <section className="page-heading lodge-profile-heading">
      <div><p className="eyebrow">Identidad y gobierno del Taller</p><h1>Ficha de Taller</h1><p>Autoridades, padrón agregado, regularidad y actividad reciente en una sola proyección institucional.</p></div>
      <label className="lodge-profile-selector"><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible cargar la ficha.</strong><span>{error}</span></div>}
    {loading ? <div className="panel"><Loading /></div> : !profile ? <div className="panel empty-state"><strong>Seleccione un Taller.</strong></div> : <>
      <section className="lodge-identity panel">
        <div className="lodge-emblem" aria-hidden="true">M</div>
        <div><p className="eyebrow">Taller autorizado</p><h2>{profile.organization.name}</h2><p>{profile.organization.number ? `Nº ${profile.organization.number} · ` : ''}{profile.organization.type}</p></div>
        <div className="lodge-identity-meta"><span>Registro institucional</span><strong>{profile.organization.number ?? 'Sin número'}</strong><small>Creado {formatDateTime(profile.organization.createdAtUtc)}</small></div>
      </section>

      <section className="lodge-kpi-grid">
        <Kpi label="Miembros activos" value={String(profile.members.active)} detail="Afiliación vigente" />
        <Kpi label="Autoridades vigentes" value={String(profile.authorities.length)} detail="Según período actual" />
        <Kpi label="Gran Tesorería" value={regularityLabel(profile.regularity?.financial?.status)} detail={regularityDate(profile.regularity?.financial?.asOfDate)} />
        <Kpi label="Gran Hospitalaria" value={regularityLabel(profile.regularity?.hospitalaria?.status)} detail={regularityDate(profile.regularity?.hospitalaria?.asOfDate)} />
      </section>

      <section className="lodge-profile-grid">
        <article className="panel authorities-panel">
          <div className="panel-heading"><div><p className="eyebrow">Gobierno</p><h2>Autoridades y cargos</h2></div><span className="count-badge">{profile.authorities.length}</span></div>
          {profile.authorities.length === 0 ? <Empty text="Sin cargos vigentes registrados." /> : <div className="authority-list">{profile.authorities.map(item => <div key={item.id}><span className="authority-avatar">{initials(item.displayName)}</span><div><strong>{item.displayName}</strong><small>{officeLabel(item.officeType)} · período {item.period}</small></div><span>{formatDate(item.startDate)}{item.endDate ? ` – ${formatDate(item.endDate)}` : ''}</span></div>)}</div>}
        </article>

        <article className="panel degree-panel">
          <div className="panel-heading"><div><p className="eyebrow">Composición</p><h2>Distribución por grado</h2></div></div>
          <div className="degree-distribution">{degrees.length === 0 ? <Empty text="Sin grados registrados." /> : degrees.map(([degree, count]) => <div key={degree}><div className="degree-line"><strong>{degreeLabel(degree)}</strong><span>{count}</span></div><div className="degree-track"><span style={{ width: `${profile.members.active ? Math.min(100, Math.round(count / profile.members.active * 100)) : 0}%` }} /></div></div>)}</div>
          <div className="degree-total"><span>Total con afiliación activa</span><strong>{profile.members.active}</strong></div>
        </article>
      </section>

      <section className="lodge-profile-grid activity-grid">
        <article className="panel">
          <div className="panel-heading"><div><p className="eyebrow">Actividad logial</p><h2>Tenidas recientes</h2></div><span className="count-badge">{profile.activity.recentMeetings.length}</span></div>
          {profile.activity.recentMeetings.length === 0 ? <Empty text="Sin Tenidas registradas." /> : <div className="activity-list">{profile.activity.recentMeetings.map(item => <div key={item.id}><span className="activity-date"><strong>{day(item.meetingDate)}</strong><small>{month(item.meetingDate)}</small></span><div><strong>{item.title || meetingTypeLabel(item.meetingType)}</strong><small>{degreeLabel(item.grade)} · {formatDate(item.meetingDate)}</small></div><span className={activityStatusClass(item.status)}>{activityStatusLabel(item.status)}</span></div>)}</div>}
        </article>

        <article className="panel">
          <div className="panel-heading"><div><p className="eyebrow">Docencia</p><h2>Formación reciente</h2></div><span className="count-badge">{profile.activity.recentInstruction.length}</span></div>
          {profile.activity.recentInstruction.length === 0 ? <Empty text="Sin docencias registradas." /> : <div className="instruction-list">{profile.activity.recentInstruction.map(item => <div key={item.id}><span className="instruction-icon">◇</span><div><strong>{item.topic}</strong><small>{degreeLabel(item.grade)} · {formatDate(item.instructionDate)} · {officeLabel(item.responsibleOffice)}</small></div><span className={activityStatusClass(item.status)}>{activityStatusLabel(item.status)}</span></div>)}</div>}
        </article>
      </section>

      <article className="panel lodge-transfers">
        <div className="panel-heading"><div><p className="eyebrow">Continuidad institucional</p><h2>Movimientos de miembros</h2></div><span className="count-badge">{profile.activity.recentTransfers.length}</span></div>
        {profile.activity.recentTransfers.length === 0 ? <Empty text="No hay traslados recientes." /> : <div className="transfer-table"><div className="transfer-table-head"><span>Miembro</span><span>Movimiento</span><span>Fecha</span><span>Estado</span></div>{profile.activity.recentTransfers.map(item => <div key={item.id}><strong>{item.memberDisplayName}</strong><span>{item.direction === 'incoming' ? `Ingreso desde ${item.sourceOrganization}` : `Salida hacia ${item.targetOrganization}`}</span><span>{formatDate(item.approvedEffectiveDate ?? item.requestedDate)}</span><span className="transfer-status">{transferStatusLabel(item.status)}</span></div>)}</div>}
        <p className="lodge-privacy-note">La ficha del Taller muestra información institucional necesaria para gestión y gobierno. No expone correo, teléfono ni dirección de los miembros.</p>
      </article>
    </>}
  </>
}

function Kpi({ label, value, detail }: { label: string; value: string; detail: string }) { return <article className="metric-card"><span>{label}</span><strong className={value === 'Pendiente' ? 'kpi-warning' : undefined}>{value}</strong><small>{detail}</small></article> }
function Empty({ text }: { text: string }) { return <div className="empty-state compact"><strong>{text}</strong></div> }
function Loading() { return <div className="loading-rows"><span /><span /><span /></div> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function initials(value: string) { return value.split(/\s+/).filter(Boolean).slice(0, 2).map(item => item[0]?.toUpperCase()).join('') }
function officeLabel(value: string) { return value.replaceAll('_', ' ').replace(/\b\w/g, char => char.toUpperCase()) }
function degreeLabel(value: string) { return value === 'master' || value === 'third' ? 'Maestro/a' : value === 'fellowcraft' || value === 'second' ? 'Compañero/a' : value === 'apprentice' || value === 'first' ? 'Aprendiz' : value.replaceAll('_', ' ') }
function meetingTypeLabel(value: string) { return value === 'regular' ? 'Tenida regular' : value === 'instruction' ? 'Tenida de instrucción' : value.replaceAll('_', ' ') }
function regularityLabel(value?: string | null) { return value === 'up_to_date' ? 'Al día' : value === 'pending' ? 'Pendiente' : value === 'delinquent' || value === 'overdue' ? 'Morosidad' : value === 'exempt' ? 'Exento' : 'Sin dato' }
function regularityDate(value?: string | null) { return value ? `Estado al ${formatDate(value)}` : 'Según permisos y datos disponibles' }
function activityStatusLabel(value: string) { return value === 'closed' || value === 'completed' ? 'Realizada' : value === 'scheduled' ? 'Programada' : value }
function activityStatusClass(value: string) { return value === 'closed' || value === 'completed' ? 'activity-status done' : value === 'scheduled' ? 'activity-status scheduled' : 'activity-status' }
function transferStatusLabel(value: string) { return value === 'executed' ? 'Ejecutado' : value === 'approved' ? 'Aprobado' : value === 'requested' ? 'Solicitado' : value }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value)) }
function day(value: string) { return new Date(`${value}T12:00:00Z`).getUTCDate().toString().padStart(2, '0') }
function month(value: string) { return new Intl.DateTimeFormat('es-CL', { month: 'short', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)).replace('.', '').toUpperCase() }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
