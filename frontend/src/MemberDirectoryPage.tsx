import { useEffect, useMemo, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import { type MemberDirectoryItem, type MemberProfile, type MembershipApiClient } from './api/membershipApi'
import './memberDirectory.css'

export default function MemberDirectoryPage({ api, membershipApi }: { api: PmgmApiClient; membershipApi: MembershipApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('')
  const [members, setMembers] = useState<MemberDirectoryItem[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [profile, setProfile] = useState<MemberProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
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
    if (!organizationId) { setMembers([]); setSelectedId(''); setProfile(null); return }
    let active = true
    setWorking(true); setError(null)
    const timer = window.setTimeout(() => {
      membershipApi.getMembers(organizationId, { query: query || undefined, status: status || undefined, limit: 150 })
        .then(response => {
          if (!active) return
          setMembers(response.items)
          setSelectedId(current => response.items.some(item => item.memberId === current) ? current : response.items[0]?.memberId ?? '')
        })
        .catch(reason => { if (active) setError(toMessage(reason)) })
        .finally(() => { if (active) setWorking(false) })
    }, 180)
    return () => { active = false; window.clearTimeout(timer) }
  }, [membershipApi, organizationId, query, status])

  useEffect(() => {
    if (!selectedId) { setProfile(null); return }
    let active = true
    setProfile(null)
    membershipApi.getProfile(selectedId)
      .then(response => { if (active) setProfile(response) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [membershipApi, selectedId])

  const metrics = useMemo(() => ({
    active: members.filter(item => item.membershipStatus === 'active').length,
    masters: members.filter(item => item.currentDegree === 'master').length,
    transferred: members.filter(item => item.membershipStatus === 'transferred').length,
    alerts: members.filter(item => item.institutionalStatus && item.institutionalStatus !== 'active').length,
  }), [members])

  const organization = organizations.find(item => item.id === organizationId)

  return <>
    <section className="page-heading member-heading">
      <div><p className="eyebrow">Padrón institucional</p><h1>Fichas de miembros</h1><p>Identidad institucional, Taller, grado, cargos e historial con acceso controlado por ámbito.</p></div>
      <span className="count-badge">{working ? 'actualizando…' : `${members.length} registros`}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la consulta.</strong><span>{error}</span></div>}

    <section className="member-toolbar panel">
      <label><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <label><span>Buscar</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Nombre o número institucional" /></label>
      <label><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="">Todos</option><option value="active">Activo</option><option value="transferred">Trasladado</option><option value="closed">Cerrado</option></select></label>
      <div className="member-context"><strong>{organization ? organizationLabel(organization) : 'Sin Taller'}</strong><small>La ficha conserva el historial del Taller de origen cuando existe un traslado.</small></div>
    </section>

    <section className="member-metrics">
      <Metric label="Activos" value={metrics.active} detail="Membresía vigente" />
      <Metric label="Maestros" value={metrics.masters} detail="Grado actual" />
      <Metric label="Traslados" value={metrics.transferred} detail="En este resultado" />
      <Metric label="Eventos de estado" value={metrics.alerts} detail="Requieren lectura contextual" />
    </section>

    <section className="member-layout">
      <article className="panel member-roster">
        <div className="panel-heading"><div><p className="eyebrow">Taller seleccionado</p><h2>Padrón</h2></div><span className="count-badge">{members.length}</span></div>
        {loading || working ? <Loading /> : members.length === 0 ? <div className="empty-state"><strong>No hay coincidencias.</strong><span>Pruebe otro filtro o Taller.</span></div> : <div className="member-list">{members.map(item => <button key={item.memberId} type="button" className={item.memberId === selectedId ? 'member-row selected' : 'member-row'} onClick={() => setSelectedId(item.memberId)}><span className="member-avatar">{initials(item.displayName)}</span><span className="member-row-main"><strong>{item.displayName}</strong><small>{degreeLabel(item.currentDegree)} · {item.institutionalNumber ?? 'Sin Nº institucional'}</small></span><span className={statusClass(item.membershipStatus)}>{membershipLabel(item.membershipStatus)}</span></button>)}</div>}
      </article>

      <article className="panel member-profile">
        {!selectedId ? <div className="empty-state"><strong>Seleccione un miembro.</strong></div> : !profile ? <Loading /> : <Profile profile={profile} />}
      </article>
    </section>
  </>
}

function Profile({ profile }: { profile: MemberProfile }) {
  const displayName = `${profile.member.firstNames} ${profile.member.lastNames}`.trim()
  const currentMembership = profile.current.membership
  return <>
    <div className="member-profile-hero">
      <span className="member-avatar large">{initials(displayName)}</span>
      <div><p className="eyebrow">Ficha institucional</p><h2>{displayName}</h2><p>{profile.member.institutionalNumber ?? 'Sin número institucional'} · {degreeLabel(profile.current.degree?.degree ?? null)}</p></div>
      <span className="profile-scope">{profile.scope === 'order' ? 'Vista Orden' : 'Vista autorizada'}</span>
    </div>

    <div className="profile-summary-grid">
      <Summary label="Taller actual" value={currentMembership?.organization ?? 'Sin afiliación vigente'} detail={currentMembership ? `Desde ${formatDate(currentMembership.startDate)}` : '—'} />
      <Summary label="Estado" value={institutionalStatusLabel(profile.current.institutionalStatus?.eventType)} detail={membershipLabel(currentMembership?.status ?? '')} />
      <Summary label="Cargos vigentes" value={String(profile.current.offices.length)} detail={profile.current.offices.map(item => officeLabel(item.officeType)).join(', ') || 'Sin cargos vigentes'} />
      <Summary label="Regularidad" value={regularityLabel(profile.regularity?.financial?.status)} detail={profile.regularity?.hospitalaria ? `Hospitalaria: ${regularityLabel(profile.regularity.hospitalaria.status)}` : 'Según permisos'} />
    </div>

    <section className="profile-block">
      <div className="profile-block-heading"><div><p className="eyebrow">Protección de datos</p><h3>Contacto</h3></div><span className={profile.contactVisible ? 'privacy-chip allowed' : 'privacy-chip protected'}>{profile.contactVisible ? 'Visible por rol' : 'Protegido'}</span></div>
      {profile.contactVisible ? <dl className="contact-grid"><div><dt>Correo</dt><dd>{profile.contact?.email ?? 'No registrado'}</dd></div><div><dt>Teléfono</dt><dd>{profile.contact?.phone ?? 'No registrado'}</dd></div><div><dt>Dirección</dt><dd>{profile.contact?.address ?? 'No registrada'}</dd></div></dl> : <p className="privacy-note">Los datos de contacto están minimizados para este rol. La ficha institucional sigue disponible sin exponer datos personales no necesarios.</p>}
    </section>

    <section className="profile-block">
      <div className="profile-block-heading"><div><p className="eyebrow">Trazabilidad</p><h3>Historial de membresía</h3></div><span className="count-badge">{profile.memberships.length}</span></div>
      <div className="history-list">{profile.memberships.map(item => <div key={item.id}><span className="history-dot" /><div><strong>{item.organization}</strong><small>{formatDate(item.startDate)}{item.endDate ? ` → ${formatDate(item.endDate)}` : ' → vigente'} · {membershipLabel(item.status)}</small>{item.endReason && <small>{item.endReason}</small>}</div></div>)}</div>
    </section>

    {profile.transfers.length > 0 && <section className="profile-block transfer-block"><div className="profile-block-heading"><div><p className="eyebrow">Continuidad institucional</p><h3>Cambios de Taller</h3></div></div>{profile.transfers.map(item => <article key={item.id} className="transfer-card"><strong>{item.sourceOrganization} → {item.targetOrganization}</strong><span>{formatDate(item.approvedEffectiveDate ?? item.proposedEffectiveDate)} · {transferStatusLabel(item.status)}</span>{item.resolution && <p>{item.resolution}</p>}</article>)}</section>}

    <section className="profile-block">
      <div className="profile-block-heading"><div><p className="eyebrow">Trayectoria</p><h3>Grados y cargos</h3></div></div>
      <div className="trajectory-grid"><div><strong>Grados</strong>{profile.degreeEvents.map(item => <span key={item.id}>{degreeLabel(item.degree)} · {formatDate(item.effectiveDate)}</span>)}</div><div><strong>Cargos</strong>{profile.offices.length === 0 ? <span>Sin cargos registrados.</span> : profile.offices.map(item => <span key={item.id}>{officeLabel(item.officeType)} · {item.period}</span>)}</div></div>
    </section>
  </>
}

function Metric({ label, value, detail }: { label: string; value: number; detail: string }) { return <article className="metric-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article> }
function Summary({ label, value, detail }: { label: string; value: string; detail: string }) { return <div className="profile-summary"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div> }
function Loading() { return <div className="loading-rows"><span /><span /><span /></div> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function initials(value: string) { return value.split(/\s+/).filter(Boolean).slice(0, 2).map(item => item[0]?.toUpperCase()).join('') }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function degreeLabel(value: string | null | undefined) { return value === 'master' ? 'Maestro/a' : value === 'fellowcraft' ? 'Compañero/a' : value === 'apprentice' ? 'Aprendiz' : value || 'Sin grado registrado' }
function officeLabel(value: string) { return value.replaceAll('_', ' ').replace(/\b\w/g, char => char.toUpperCase()) }
function membershipLabel(value: string) { return value === 'active' ? 'Activo' : value === 'transferred' ? 'Trasladado' : value === 'closed' ? 'Cerrado' : value || 'Sin estado' }
function institutionalStatusLabel(value?: string | null) { return value === 'active' ? 'Activo' : value === 'inactive' ? 'Inactivo' : value === 'voluntary_withdrawal' ? 'Retiro voluntario' : value === 'forced_withdrawal' ? 'Retiro forzoso' : value === 'reinstated' ? 'Reintegrado' : value === 'deceased' ? 'Fallecido' : value === 'workshop_transfer' ? 'Cambio de Taller' : value || 'Sin evento' }
function regularityLabel(value?: string | null) { return value === 'up_to_date' ? 'Al día' : value === 'delinquent' || value === 'overdue' ? 'Pendiente' : value === 'exempt' ? 'Exento' : value === 'pending' ? 'En revisión' : 'No visible' }
function transferStatusLabel(value: string) { return value === 'executed' ? 'Ejecutado' : value === 'approved' ? 'Aprobado' : value === 'requested' ? 'Solicitado' : value }
function statusClass(value: string) { return value === 'active' ? 'member-status active' : value === 'transferred' ? 'member-status transferred' : 'member-status closed' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
