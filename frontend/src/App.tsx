import { useEffect, useMemo, useState } from 'react'
import BootstrapPage from './BootstrapPage'
import CalendarPage from './CalendarPage'
import CandidateProfilePage from './CandidateProfilePage'
import CandidateWorkshopIntakePage from './CandidateWorkshopIntakePage'
import CeremoniesPage from './CeremoniesPage'
import DashboardPage from './DashboardPage'
import DataQualityCaseQueuePage from './DataQualityCaseQueuePage'
import DemoProfileSwitcher from './DemoProfileSwitcher'
import DocumentManagementPage from './DocumentManagementPage'
import ExecutiveReportingPage from './ExecutiveReportingPage'
import GrandArchivePage from './GrandArchivePage'
import GrandSecretariatPage from './GrandSecretariatPage'
import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'
import InternalAffairsDataQualityPage from './InternalAffairsDataQualityPage'
import InternalAffairsMemberControlPage from './InternalAffairsMemberControlPage'
import InitiationCircuitPage from './InitiationCircuitPage'
import LibraryPage from './LibraryPage'
import LodgeManagementPage from './LodgeManagementPage'
import LodgeProfilePage from './LodgeProfilePage'
import MemberDirectoryPage from './MemberDirectoryPage'
import MemberPortalPage from './MemberPortalPage'
import NotificationsPage from './NotificationsPage'
import PublishedCandidatePhoto from './PublishedCandidatePhoto'
import RegimenInteriorPage from './RegimenInteriorPage'
import RegularityPage from './RegularityPage'
import { type BootstrapApiClient } from './api/bootstrapApi'
import { type CalendarApiClient } from './api/calendarApi'
import { type CandidateIntakeApiClient } from './api/candidateIntakeApi'
import { type DataQualityCaseApiClient } from './api/dataQualityCaseApi'
import { type DocumentApiClient } from './api/documentApi'
import { type GrandArchiveApiClient } from './api/grandArchiveApi'
import { type InternalAffairsApiClient } from './api/internalAffairsApi'
import { type LodgeApiClient } from './api/lodgeApi'
import { type MembershipApiClient } from './api/membershipApi'
import { type NotificationApiClient } from './api/notificationApi'
import { type OrganizationProfileApiClient } from './api/organizationProfileApi'
import { type CandidatePublication, type CandidatePortalResponse, type PmgmApiClient, type SessionProfile, type SystemInfo } from './api/pmgmApi'
import { type ReportingApiClient } from './api/reportingApi'
import { getDemoProfile, type DemoProfileKey } from './demoProfiles'

type View = 'memberPortal' | 'dashboard' | 'bootstrap' | 'candidates' | 'candidateProfile' | 'initiationCircuit' | 'members' | 'lodgeProfile' | 'reporting' | 'memberControl' | 'dataQuality' | 'caseQueue' | 'calendar' | 'notifications' | 'ceremonies' | 'regimen' | 'treasury' | 'hospitalaria' | 'secretariat' | 'lodge' | 'library' | 'documents' | 'grandArchive'
type ExtendedCapabilities = SessionProfile['capabilities'] & { canBootstrapInstitutional?: boolean; canManageLodgeOperations?: boolean; canManageDocuments?: boolean; canReadLibrary?: boolean; canManageGrandArchive?: boolean }

interface AppProps {
  api: PmgmApiClient
  bootstrapApi: BootstrapApiClient
  lodgeApi: LodgeApiClient
  membershipApi: MembershipApiClient
  organizationProfileApi: OrganizationProfileApiClient
  reportingApi: ReportingApiClient
  internalAffairsApi: InternalAffairsApiClient
  dataQualityCaseApi: DataQualityCaseApiClient
  documentApi: DocumentApiClient
  grandArchiveApi: GrandArchiveApiClient
  calendarApi: CalendarApiClient
  notificationApi: NotificationApiClient
  candidateIntakeApi: CandidateIntakeApiClient
  onLogout?: () => void
}

export default function App({ api, bootstrapApi, lodgeApi, membershipApi, organizationProfileApi, reportingApi, internalAffairsApi, dataQualityCaseApi, documentApi, grandArchiveApi, calendarApi, notificationApi, candidateIntakeApi, onLogout }: AppProps) {
  const [view, setView] = useState<View>('memberPortal')
  const [portal, setPortal] = useState<CandidatePortalResponse | null>(null)
  const [systemInfo, setSystemInfo] = useState<SystemInfo | null>(null)
  const [profile, setProfile] = useState<SessionProfile | null>(null)
  const [demoProfileKey, setDemoProfileKey] = useState<DemoProfileKey>('brother')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    Promise.all([candidateIntakeApi.getPublishedCandidates(), api.getSystemInfo(), api.getSessionProfile()])
      .then(([portalResponse, systemResponse, session]) => { if (!active) return; setPortal(portalResponse); setSystemInfo(systemResponse); setProfile(session) })
      .catch((reason: unknown) => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar la plataforma.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, candidateIntakeApi])

  const effectiveProfile = api.useMocks ? getDemoProfile(demoProfileKey) : profile
  const capabilities = effectiveProfile?.capabilities as ExtendedCapabilities | undefined
  const hasInstitutionalScope = effectiveProfile?.accessScope === 'organization' || effectiveProfile?.accessScope === 'order'
  const canMemberPortal = effectiveProfile !== null
  const canBootstrap = capabilities?.canBootstrapInstitutional ?? false
  const canCalendar = effectiveProfile !== null
  const canNotifications = effectiveProfile !== null
  const canMembers = hasInstitutionalScope
  const canLodgeProfile = hasInstitutionalScope
  const canCeremonies = capabilities?.canReviewCeremonies ?? false
  const canRegimen = capabilities?.canRunRegimenInteriorReports ?? false
  const canReporting = canRegimen
  const canMemberControl = canRegimen
  const canDataQuality = canRegimen
  const canCaseQueue = canRegimen
  const canTreasury = capabilities?.canManageTreasuryRegularity ?? false
  const canHospitalaria = capabilities?.canManageHospitalariaRegularity ?? false
  const canSecretariat = capabilities?.canManageGrandSecretariat ?? false
  const canLodge = capabilities?.canManageLodgeOperations ?? false
  const canCandidateProfile = canSecretariat || canLodge
  const canLibrary = effectiveProfile !== null && (capabilities?.canReadLibrary ?? false)
  const canDocuments = capabilities?.canManageDocuments ?? false
  const canGrandArchive = capabilities?.canManageGrandArchive ?? false
  const hasInstitutionalManagement = canMembers || canReporting || canMemberControl || canDataQuality || canCaseQueue || canCeremonies || canRegimen || canTreasury || canHospitalaria || canSecretariat || canGrandArchive

  const changeDemoProfile = (next: DemoProfileKey) => {
    setDemoProfileKey(next)
    setView('memberPortal')
  }

  const openNotificationAction = (actionUrl: string) => {
    const path = actionUrl.split(/[?#]/, 1)[0]
    if (path === '/candidates') return setView('candidates')
    if (path === '/calendar' && canCalendar) return setView('calendar')
    if (path === '/ceremonies' && canCeremonies) return setView('ceremonies')
    if (path === '/lodge' && canLodge) return setView('lodge')
    if (path === '/documents' && canDocuments) return setView('documents')
  }

  return <div className="app-shell">
    <header className="topbar">
      <button className="brand" type="button" onClick={() => setView('memberPortal')} aria-label="Ir a Mi ficha"><span className="brand-mark" aria-hidden="true">C</span><span><strong>Proyecto Centenario</strong><small>Gran Logia Mixta de Chile</small></span></button>
      <span className="product-motto">100 años de historia · Un legado hacia el futuro</span>
      <div className="topbar-meta">{api.useMocks && <><span className="demo-badge">QA demostración</span><DemoProfileSwitcher value={demoProfileKey} onChange={changeDemoProfile} /></>}{effectiveProfile && <span className="environment-badge">{effectiveProfile.displayName}</span>}{canNotifications && <button className="topbar-icon-button" type="button" aria-label="Abrir notificaciones" title="Notificaciones" onClick={() => setView('notifications')}><InstitutionalIcon name="bell" size={18} /></button>}{onLogout && <button type="button" onClick={onLogout}>Cerrar sesión</button>}<span className="environment-badge">{api.useMocks ? 'UI QA v0.39' : `API v${systemInfo?.version ?? '—'}`}</span></div>
    </header>
    <div className="workspace">
      <nav className="sidebar" aria-label="Navegación principal">
        <button className={view === 'dashboard' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('dashboard')}><NavIcon name="home" /> Inicio</button>
        <div className="nav-section">Portal del Hermano</div>
        <ModuleAccess icon="member" label="Mi ficha" allowed={canMemberPortal} active={view === 'memberPortal'} onOpen={canMemberPortal ? () => setView('memberPortal') : undefined} />
        <ModuleAccess icon="calendar" label="Mi calendario" allowed={canCalendar} active={view === 'calendar'} onOpen={canCalendar ? () => setView('calendar') : undefined} />
        <ModuleAccess icon="bell" label="Notificaciones" allowed={canNotifications} active={view === 'notifications'} onOpen={canNotifications ? () => setView('notifications') : undefined} />
        {canBootstrap && <><div className="nav-section">Plataforma</div><ModuleAccess icon="settings" label="Configuración inicial" allowed={canBootstrap} active={view === 'bootstrap'} onOpen={() => setView('bootstrap')} /></>}
        <div className="nav-section">Procesos</div>
        <button className={view === 'candidates' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('candidates')}><NavIcon name="candidate" /> Insinuados publicados</button>
        <ModuleAccess icon="candidate" label={canSecretariat ? 'Revisión de insinuados' : 'Carga de insinuados'} allowed={canCandidateProfile} active={view === 'candidateProfile'} onOpen={canCandidateProfile ? () => setView('candidateProfile') : undefined} />
        <ModuleAccess icon="ceremony" label="Circuito de Iniciación" allowed={canCandidateProfile || canCeremonies} active={view === 'initiationCircuit'} onOpen={canCandidateProfile || canCeremonies ? () => setView('initiationCircuit') : undefined} />
        {hasInstitutionalManagement && <>
          <div className="nav-section">Gestión institucional</div>
          <ModuleAccess icon="members" label="Fichas de miembros" allowed={canMembers} active={view === 'members'} onOpen={canMembers ? () => setView('members') : undefined} />
          <ModuleAccess icon="report" label="Reportería Ejecutiva" allowed={canReporting} active={view === 'reporting'} onOpen={canReporting ? () => setView('reporting') : undefined} />
          <ModuleAccess icon="memberControl" label="Control de miembros" allowed={canMemberControl} active={view === 'memberControl'} onOpen={canMemberControl ? () => setView('memberControl') : undefined} />
          <ModuleAccess icon="dataQuality" label="Calidad de datos" allowed={canDataQuality} active={view === 'dataQuality'} onOpen={canDataQuality ? () => setView('dataQuality') : undefined} />
          <ModuleAccess icon="check" label="Cola de corroboración" allowed={canCaseQueue} active={view === 'caseQueue'} onOpen={canCaseQueue ? () => setView('caseQueue') : undefined} />
          <ModuleAccess icon="ceremony" label="Ceremonias" allowed={canCeremonies} active={view === 'ceremonies'} onOpen={canCeremonies ? () => setView('ceremonies') : undefined} />
          <ModuleAccess icon="shield" label="Régimen Interior" allowed={canRegimen} active={view === 'regimen'} onOpen={canRegimen ? () => setView('regimen') : undefined} />
          <ModuleAccess icon="treasury" label="Gran Tesorería" allowed={canTreasury} active={view === 'treasury'} onOpen={canTreasury ? () => setView('treasury') : undefined} />
          <ModuleAccess icon="hospitalaria" label="Gran Hospitalaria" allowed={canHospitalaria} active={view === 'hospitalaria'} onOpen={canHospitalaria ? () => setView('hospitalaria') : undefined} />
          <ModuleAccess icon="secretariat" label="Gran Secretaría" allowed={canSecretariat} active={view === 'secretariat'} onOpen={canSecretariat ? () => setView('secretariat') : undefined} />
          <ModuleAccess icon="archive" label="Gran Archivero" allowed={canGrandArchive} active={view === 'grandArchive'} onOpen={canGrandArchive ? () => setView('grandArchive') : undefined} />
        </>}
        {(canLodgeProfile || canLodge) && <>
          <div className="nav-section">Taller</div>
          <ModuleAccess icon="lodge" label="Ficha de Taller" allowed={canLodgeProfile} active={view === 'lodgeProfile'} onOpen={canLodgeProfile ? () => setView('lodgeProfile') : undefined} />
          <ModuleAccess icon="lodge" label="Gestión Logial" allowed={canLodge} active={view === 'lodge'} onOpen={canLodge ? () => setView('lodge') : undefined} />
        </>}
        {(canLibrary || canDocuments) && <>
          <div className="nav-section">Conocimiento</div>
          <ModuleAccess icon="library" label="Biblioteca Virtual" allowed={canLibrary} active={view === 'library'} onOpen={canLibrary ? () => setView('library') : undefined} />
          <ModuleAccess icon="documents" label="Gestor Documental" allowed={canDocuments} active={view === 'documents'} onOpen={canDocuments ? () => setView('documents') : undefined} />
        </>}
      </nav>
      <main className="content" id="contenido-principal">
        {error && <ErrorBanner message={error} />}
        {view === 'memberPortal' && canMemberPortal && <MemberPortalPage profile={effectiveProfile} useMocks={api.useMocks} membershipApi={membershipApi} onOpenCalendar={canCalendar ? () => setView('calendar') : undefined} onOpenNotifications={canNotifications ? () => setView('notifications') : undefined} onOpenLibrary={canLibrary ? () => setView('library') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {view === 'dashboard' && <DashboardPage portal={portal} systemInfo={systemInfo} profile={effectiveProfile} loading={loading} calendarApi={calendarApi} notificationApi={notificationApi} onOpenCandidates={() => setView('candidates')} onOpenCalendar={() => setView('calendar')} onOpenNotifications={() => setView('notifications')} onOpenSecretariat={canSecretariat ? () => setView('secretariat') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {view === 'bootstrap' && canBootstrap && <BootstrapPage bootstrapApi={bootstrapApi} />}
        {view === 'candidates' && <CandidatePortal portal={portal} loading={loading} api={candidateIntakeApi} />}
        {view === 'candidateProfile' && canCandidateProfile && (canSecretariat ? <CandidateProfilePage api={candidateIntakeApi} canReview={canSecretariat} onBack={() => setView('candidates')} /> : <CandidateWorkshopIntakePage api={candidateIntakeApi} onBack={() => setView('candidates')} />)}
        {view === 'initiationCircuit' && (canCandidateProfile || canCeremonies) && <InitiationCircuitPage />}
        {view === 'members' && canMembers && <MemberDirectoryPage api={api} membershipApi={membershipApi} />}
        {view === 'lodgeProfile' && canLodgeProfile && <LodgeProfilePage api={api} organizationProfileApi={organizationProfileApi} />}
        {view === 'reporting' && canReporting && <ExecutiveReportingPage reportingApi={reportingApi} />}
        {view === 'memberControl' && canMemberControl && <InternalAffairsMemberControlPage api={api} internalAffairsApi={internalAffairsApi} />}
        {view === 'dataQuality' && canDataQuality && <InternalAffairsDataQualityPage api={api} internalAffairsApi={internalAffairsApi} caseApi={dataQualityCaseApi} onOpenCases={() => setView('caseQueue')} />}
        {view === 'caseQueue' && canCaseQueue && <DataQualityCaseQueuePage api={api} caseApi={dataQualityCaseApi} />}
        {view === 'notifications' && canNotifications && <NotificationsPage notificationApi={notificationApi} onAction={openNotificationAction} />}
        {view === 'calendar' && canCalendar && <CalendarPage api={api} calendarApi={calendarApi} canManage={canSecretariat} />}
        {view === 'ceremonies' && canCeremonies && <CeremoniesPage api={api} />}
        {view === 'regimen' && canRegimen && <RegimenInteriorPage api={api} />}
        {view === 'treasury' && canTreasury && <RegularityPage api={api} kind="treasury" />}
        {view === 'hospitalaria' && canHospitalaria && <RegularityPage api={api} kind="hospitalaria" />}
        {view === 'secretariat' && canSecretariat && <GrandSecretariatPage api={api} />}
        {view === 'grandArchive' && canGrandArchive && <GrandArchivePage archiveApi={grandArchiveApi} />}
        {view === 'lodge' && canLodge && <LodgeManagementPage api={api} lodgeApi={lodgeApi} />}
        {view === 'library' && canLibrary && <LibraryPage documentApi={documentApi} />}
        {view === 'documents' && canDocuments && <DocumentManagementPage api={api} documentApi={documentApi} />}
      </main>
    </div>
  </div>
}

function NavIcon({ name }: { name: InstitutionalIconName }) {
  return <span className="nav-icon" aria-hidden="true"><InstitutionalIcon name={name} size={18} /></span>
}

function ModuleAccess({ icon, label, allowed, active = false, onOpen }: { icon: InstitutionalIconName; label: string; allowed: boolean; active?: boolean; onOpen?: () => void }) {
  if (!allowed || !onOpen) return null
  return <button className={active ? 'nav-item active' : 'nav-item'} type="button" onClick={onOpen}><NavIcon name={icon} /> {label}</button>
}

function CandidatePortal({ portal, loading, api }: { portal: CandidatePortalResponse | null; loading: boolean; api: CandidateIntakeApiClient }) {
  const [query, setQuery] = useState('')
  const filtered = useMemo(() => {
    const normalized = normalize(query)
    return !normalized ? portal?.items ?? [] : (portal?.items ?? []).filter(item => normalize(`${item.displayName} ${item.workshopName} ${item.workshopNumber ?? ''}`).includes(normalized))
  }, [portal, query])
  return <><section className="page-heading"><div><p className="eyebrow">Transparencia institucional controlada</p><h1>Insinuados en período de publicación</h1><p>Publicaciones vigentes según el plazo configurado para solicitudes de iniciación.</p></div><span className="count-badge">{loading ? '…' : `${filtered.length} registros`}</span></section><section className="panel"><label className="search-field"><span>Buscar por nombre o Taller</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Ej.: Taller 23" /></label>{loading ? <LoadingRows /> : filtered.length === 0 ? <div className="empty-state"><strong>No hay coincidencias.</strong></div> : <div className="candidate-list">{filtered.map(candidate => <CandidateCard key={`${candidate.displayName}-${candidate.publishedFromUtc}`} candidate={candidate} api={api} />)}</div>}</section></>
}

function CandidateCard({ candidate, api }: { candidate: CandidatePublication; api: CandidateIntakeApiClient }) {
  const percentage = Math.min(100, Math.round(candidate.elapsedDays / Math.max(1, candidate.requiredDays) * 100))
  const complete = candidate.elapsedDays >= candidate.requiredDays
  return <article className="candidate-card"><div className="candidate-avatar" style={{ overflow: 'hidden' }}><PublishedCandidatePhoto candidate={candidate} api={api} /></div><div className="candidate-main"><div className="candidate-title-row"><div><h3>{candidate.displayName}</h3><p>{candidate.workshopName}{candidate.workshopNumber ? ` · Nº ${candidate.workshopNumber}` : ''}</p></div><span className={complete ? 'status-pill complete' : 'status-pill active'}>{complete ? 'Plazo cumplido' : 'En publicación'}</span></div><div className="progress-row"><div className="progress-track" role="progressbar" aria-valuenow={percentage} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${percentage}%` }} /></div><strong>{candidate.elapsedDays}/{candidate.requiredDays} días</strong></div><dl className="candidate-meta"><div><dt>Publicado</dt><dd>{formatDate(candidate.publishedFromUtc)}</dd></div><div><dt>Cumplimiento</dt><dd>{formatDate(candidate.complianceDateUtc)}</dd></div></dl></div></article>
}

function LoadingRows() { return <div className="loading-rows"><span /><span /><span /></div> }
function ErrorBanner({ message }: { message: string }) { return <div className="error-banner" role="alert"><strong>No fue posible conectar con la información institucional.</strong><span>{message}</span></div> }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(date) }
