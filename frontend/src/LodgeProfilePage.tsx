import { useEffect, useMemo, useState } from 'react'
import { type LodgeSummaryAccess, type OrganizationProfile, type OrganizationProfileApiClient } from './api/organizationProfileApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import './lodgeProfile.css'

export default function LodgeProfilePage({ api, organizationProfileApi, canManageAccess = false, canEditWorkshopProfile = false }: { api: PmgmApiClient; organizationProfileApi: OrganizationProfileApiClient; canManageAccess?: boolean; canEditWorkshopProfile?: boolean }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [profile, setProfile] = useState<OrganizationProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [accessManagement, setAccessManagement] = useState<LodgeSummaryAccess | null>(null)
  const [selectedMasterId, setSelectedMasterId] = useState('')
  const [grantReason, setGrantReason] = useState('')
  const [savingGrant, setSavingGrant] = useState(false)
  const [savingWorkshopProfile, setSavingWorkshopProfile] = useState(false)
  const [workshopProfileDraft, setWorkshopProfileDraft] = useState({ name: '', establishedOn: '', city: '', country: '' })
  const [logoUrl, setLogoUrl] = useState<string | null>(null)
  const [logoFile, setLogoFile] = useState<File | null>(null)
  const [savingLogo, setSavingLogo] = useState(false)
  const [logoRevision, setLogoRevision] = useState(0)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => {
        if (!active) return
        const workshops = response.items.filter(item => item.type.toLowerCase() === 'workshop')
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
      .then(response => {
        if (!active) return
        setProfile(response)
        setWorkshopProfileDraft({ name: response.organization.name, establishedOn: response.organization.establishedOn ?? '', city: response.organization.city ?? '', country: response.organization.country ?? '' })
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [organizationId, organizationProfileApi])

  useEffect(() => {
    let active = true
    let url: string | null = null
    setLogoUrl(null); setLogoFile(null)
    if (organizationId) void organizationProfileApi.getWorkshopLogo(organizationId).then(blob => { if (active && blob) { url = URL.createObjectURL(blob); setLogoUrl(url) } }).catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false; if (url) URL.revokeObjectURL(url) }
  }, [organizationId, organizationProfileApi, profile?.organization.hasLogo, logoRevision])

  useEffect(() => {
    if (!canManageAccess || !organizationId) { setAccessManagement(null); return }
    let active = true
    organizationProfileApi.getSummaryAccess(organizationId)
      .then(response => { if (active) setAccessManagement(response) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [canManageAccess, organizationId, organizationProfileApi])

  const grantSummaryAccess = async () => {
    if (!organizationId || !selectedMasterId || !grantReason.trim()) return
    setSavingGrant(true); setError(null)
    try {
      await organizationProfileApi.grantSummaryAccess(organizationId, selectedMasterId, grantReason.trim())
      setAccessManagement(await organizationProfileApi.getSummaryAccess(organizationId))
      setSelectedMasterId(''); setGrantReason('')
    } catch (reason) { setError(toMessage(reason)) }
    finally { setSavingGrant(false) }
  }

  const revokeSummaryAccess = async (grantId: string) => {
    if (!organizationId) return
    setError(null)
    try {
      await organizationProfileApi.revokeSummaryAccess(organizationId, grantId)
      setAccessManagement(await organizationProfileApi.getSummaryAccess(organizationId))
    } catch (reason) { setError(toMessage(reason)) }
  }

  const saveWorkshopProfile = async () => {
    if (!organizationId || !profile) return
    setSavingWorkshopProfile(true); setError(null)
    try {
      await organizationProfileApi.updateWorkshopMetadata(organizationId, {
        name: workshopProfileDraft.name.trim(),
        establishedOn: workshopProfileDraft.establishedOn || null,
        city: workshopProfileDraft.city.trim() || null,
        country: workshopProfileDraft.country.trim() || null,
      })
      setProfile(await organizationProfileApi.getProfile(organizationId))
    } catch (reason) { setError(toMessage(reason)) }
    finally { setSavingWorkshopProfile(false) }
  }

  const saveWorkshopLogo = async () => {
    if (!organizationId || !logoFile) return
    setSavingLogo(true); setError(null)
    try { await organizationProfileApi.uploadWorkshopLogo(organizationId, logoFile); setLogoFile(null); setProfile(await organizationProfileApi.getProfile(organizationId)); setLogoRevision(value => value + 1) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setSavingLogo(false) }
  }
  const removeWorkshopLogo = async () => {
    if (!organizationId) return
    setSavingLogo(true); setError(null)
    try { await organizationProfileApi.removeWorkshopLogo(organizationId); setProfile(await organizationProfileApi.getProfile(organizationId)); setLogoRevision(value => value + 1) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setSavingLogo(false) }
  }

  const degrees = useMemo(() => {
    const entries = Object.entries(profile?.members.degreeDistribution ?? {})
    return entries.sort((a, b) => b[1] - a[1])
  }, [profile])

  return <>
    <section className="page-heading lodge-profile-heading">
      <div><p className="eyebrow">Identidad y administración</p><h1>Ficha del Taller</h1><p>Datos de identidad, origen, autoridades, regularidad y actividad logial.</p></div>
      <label className="lodge-profile-selector"><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible completar la consulta o guardar la ficha.</strong><span>{error}</span></div>}
    {loading ? <div className="panel"><Loading /></div> : !profile ? <div className="panel empty-state"><strong>Seleccione un Taller.</strong></div> : <>
      <section className="lodge-identity panel">
        <div className="lodge-emblem">{logoUrl ? <img src={logoUrl} alt={`Logo de ${profile.organization.name}`} /> : <span aria-hidden="true">M</span>}</div>
        <div><p className="eyebrow">Taller autorizado</p><h2>{profile.organization.name}</h2><p>{profile.organization.number ? `Nº ${profile.organization.number} · ` : ''}{profile.organization.type}</p></div>
        <div className="lodge-identity-meta"><span>Registro institucional</span><strong>{profile.organization.number ?? 'Sin número'}</strong><small>Registro creado el {formatDateTime(profile.organization.createdAtUtc)}</small></div>
      </section>

      <section className="panel workshop-origin-panel" aria-labelledby="workshop-origin-heading">
        <div className="panel-heading"><div><p className="eyebrow">Identidad institucional</p><h2 id="workshop-origin-heading">Origen y pertenencia del Taller</h2></div></div>
        {canEditWorkshopProfile ? <div className="workshop-origin-form">
          <label className="workshop-name-field"><span>Nombre del Taller</span><input required maxLength={200} value={workshopProfileDraft.name} onChange={event => setWorkshopProfileDraft(value => ({ ...value, name: event.target.value }))} /></label>
          <label><span>Fecha de iniciación</span><input type="date" value={workshopProfileDraft.establishedOn} onChange={event => setWorkshopProfileDraft(value => ({ ...value, establishedOn: event.target.value }))} /></label>
          <label><span>Ciudad / Oriente</span><input maxLength={120} value={workshopProfileDraft.city} onChange={event => setWorkshopProfileDraft(value => ({ ...value, city: event.target.value }))} /></label>
          <label><span>País</span><input maxLength={120} value={workshopProfileDraft.country} onChange={event => setWorkshopProfileDraft(value => ({ ...value, country: event.target.value }))} /></label>
          <label className="workshop-logo-field"><span>Logo personalizado (opcional, PNG/JPEG hasta 2 MiB)</span><input type="file" accept="image/png,image/jpeg" onChange={event => { const file = event.target.files?.[0] ?? null; setLogoFile(file); if (file) { const url = URL.createObjectURL(file); setLogoUrl(current => { if (current) URL.revokeObjectURL(current); return url }) } }} /></label>
          <div className="workshop-profile-actions"><button type="button" className="primary-action" disabled={savingWorkshopProfile || !workshopProfileDraft.name.trim()} onClick={() => void saveWorkshopProfile()}>{savingWorkshopProfile ? 'Guardando…' : 'Guardar ficha'}</button>{logoFile && <button type="button" className="secondary-action" disabled={savingLogo} onClick={() => void saveWorkshopLogo()}>{savingLogo ? 'Subiendo…' : 'Guardar logo'}</button>}{profile.organization.hasLogo && <button type="button" className="secondary-action" disabled={savingLogo} onClick={() => void removeWorkshopLogo()}>Quitar logo</button>}</div>
        </div> : <dl className="workshop-origin-readonly"><div><dt>Nombre del Taller</dt><dd>{profile.organization.name}</dd></div><div><dt>Fecha de iniciación</dt><dd>{profile.organization.establishedOn ? formatDate(profile.organization.establishedOn) : 'Sin registrar'}</dd></div><div><dt>Ciudad / Oriente</dt><dd>{profile.organization.city ?? 'Sin registrar'}</dd></div><div><dt>País</dt><dd>{profile.organization.country ?? 'Sin registrar'}</dd></div></dl>}
        <p className="workshop-origin-note">La clasificación de cuotas se administra aparte por Gran Tesorería: {treasuryTerritoryLabel(profile.organization.treasuryTerritory)}. No se deduce de la ciudad ni del país.</p>
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

      {canManageAccess && <article className="panel lodge-summary-access">
        <div className="panel-heading"><div><p className="eyebrow">Permiso de consulta</p><h2>Accesos delegados</h2></div><span className="count-badge">{accessManagement?.activeGrants.length ?? 0}</span></div>
        <p>Como Venerable Maestro puede delegar y revocar el acceso de solo lectura a un Maestro activo de este Taller. La delegación no lo integra al Consejo ni habilita otros módulos.</p>
        <div className="lodge-summary-grant-form">
          <label><span>Hermano Maestro</span><select value={selectedMasterId} onChange={event => setSelectedMasterId(event.target.value)}><option value="">Seleccione un Maestro activo…</option>{accessManagement?.eligibleMasters.map(item => <option key={item.memberId} value={item.memberId}>{item.displayName}</option>)}</select></label>
          <label><span>Fundamento breve</span><input maxLength={500} value={grantReason} onChange={event => setGrantReason(event.target.value)} placeholder="Motivo de la delegación" /></label>
          <button type="button" className="primary-action" disabled={savingGrant || !selectedMasterId || !grantReason.trim()} onClick={() => void grantSummaryAccess()}>{savingGrant ? 'Guardando…' : 'Otorgar acceso'}</button>
        </div>
        {!accessManagement ? <div className="empty-state compact"><strong>Cargando permisos…</strong></div> : accessManagement.activeGrants.length === 0 ? <div className="empty-state compact"><strong>No hay accesos delegados vigentes.</strong></div> : <div className="summary-grant-list">{accessManagement.activeGrants.map(item => <div key={item.id}><div><strong>{item.displayName}</strong><small>Desde {formatDateTime(item.grantedAtUtc)} · {item.reason}</small><small className={item.isCurrentlyEligible ? 'summary-grant-effective' : 'summary-grant-ineffective'}>{item.isCurrentlyEligible ? 'Acceso vigente' : 'Acceso no efectivo: debe mantener membresía activa y grado de Maestro'}</small></div><button type="button" className="secondary-action" onClick={() => void revokeSummaryAccess(item.id)}>Revocar</button></div>)}</div>}
      </article>}
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
function treasuryTerritoryLabel(value: string | null) { return value === 'santiago' ? 'Santiago' : value === 'other_oriente' ? 'Otro Oriente' : value === 'peru' ? 'Perú' : 'Sin clasificar' }
function activityStatusLabel(value: string) { return value === 'closed' || value === 'completed' ? 'Realizada' : value === 'scheduled' ? 'Programada' : value }
function activityStatusClass(value: string) { return value === 'closed' || value === 'completed' ? 'activity-status done' : value === 'scheduled' ? 'activity-status scheduled' : 'activity-status' }
function transferStatusLabel(value: string) { return value === 'executed' ? 'Ejecutado' : value === 'approved' ? 'Aprobado' : value === 'requested' ? 'Solicitado' : value }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(value)) }
function day(value: string) { return new Date(`${value}T12:00:00Z`).getUTCDate().toString().padStart(2, '0') }
function month(value: string) { return new Intl.DateTimeFormat('es-CL', { month: 'short', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)).replace('.', '').toUpperCase() }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
