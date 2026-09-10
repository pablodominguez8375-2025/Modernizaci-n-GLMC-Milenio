import { useEffect, useMemo, useState } from 'react'
import BootstrapPage from './BootstrapPage'
import CalendarPage from './CalendarPage'
import CandidateProfilePage from './CandidateProfilePage'
import CeremoniesPage from './CeremoniesPage'
import DashboardPage from './DashboardPage'
import DataQualityCaseQueuePage from './DataQualityCaseQueuePage'
import DocumentManagementPage from './DocumentManagementPage'
import ExecutiveReportingPage from './ExecutiveReportingPage'
import GrandArchivePage from './GrandArchivePage'
import GrandSecretariatPage from './GrandSecretariatPage'
import InternalAffairsDataQualityPage from './InternalAffairsDataQualityPage'
import InternalAffairsMemberControlPage from './InternalAffairsMemberControlPage'
import LibraryPage from './LibraryPage'
import LodgeManagementPage from './LodgeManagementPage'
import LodgeProfilePage from './LodgeProfilePage'
import LodgeSecretariatPage from './LodgeSecretariatPage'
import MemberDirectoryPage from './MemberDirectoryPage'
import MemberPortalPage from './MemberPortalPage'
import NotificationsPage from './NotificationsPage'
import RegimenInteriorPage from './RegimenInteriorPage'
import RegularityPage from './RegularityPage'
import { type BootstrapApiClient } from './api/bootstrapApi'
import { type CalendarApiClient } from './api/calendarApi'
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

type View = 'memberPortal' | 'dashboard' | 'bootstrap' | 'candidates' | 'candidateProfile' | 'members' | 'lodgeProfile' | 'reporting' | 'memberControl' | 'dataQuality' | 'caseQueue' | 'calendar' | 'notifications' | 'ceremonies' | 'regimen' | 'treasury' | 'hospitalaria' | 'secretariat' | 'lodge' | 'lodgeSecretariat' | 'library' | 'documents' | 'grandArchive'
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
  onLogout?: () => void
}

export default function App({ api, bootstrapApi, lodgeApi, membershipApi, organizationProfileApi, reportingApi, internalAffairsApi, dataQualityCaseApi, documentApi, grandArchiveApi, calendarApi, notificationApi, onLogout }: AppProps) {
  const [view, setView] = useState<View>('memberPortal')
  const [portal, setPortal] = useState<CandidatePortalResponse | null>(null)
  const [systemInfo, setSystemInfo] = useState<SystemInfo | null>(null)
  const [profile, setProfile] = useState<SessionProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    Promise.all([api.getCandidatePortal(), api.getSystemInfo(), api.getSessionProfile()])
      .then(([portalResponse, systemResponse, session]) => { if (!active) return; setPortal(portalResponse); setSystemInfo(systemResponse); setProfile(session) })
      .catch((reason: unknown) => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar la plataforma.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  const capabilities = profile?.capabilities as ExtendedCapabilities | undefined
  const canMemberPortal = api.useMocks || profile !== null
  const canBootstrap = api.useMocks || (capabilities?.canBootstrapInstitutional ?? false)
  const canCalendar = api.useMocks || profile !== null
  const canNotifications = api.useMocks || profile !== null
  const canMembers = api.useMocks || profile !== null
  const canLodgeProfile = api.useMocks || profile !== null
  const canCeremonies = capabilities?.canReviewCeremonies ?? false
  const canRegimen = capabilities?.canRunRegimenInteriorReports ?? false
  const canReporting = api.useMocks || canRegimen
  const canMemberControl = api.useMocks || canRegimen
  const canDataQuality = api.useMocks || canRegimen
  const canCaseQueue = api.useMocks || canRegimen
  const canTreasury = capabilities?.canManageTreasuryRegularity ?? false
  const canHospitalaria = capabilities?.canManageHospitalariaRegularity ?? false
  const canSecretariat = capabilities?.canManageGrandSecretariat ?? false
  const canLodge = api.useMocks || (capabilities?.canManageLodgeOperations ?? false)
  const canCandidateProfile = api.useMocks || canSecretariat || canLodge
  const canLibrary = api.useMocks || (capabilities?.canReadLibrary ?? false)
  const canDocuments = api.useMocks || (capabilities?.canManageDocuments ?? false)
  const canGrandArchive = api.useMocks || (capabilities?.canManageGrandArchive ?? false)

  return <div className="app-shell">
    <header className="topbar">
      <button className="brand" type="button" onClick={() => setView('memberPortal')} aria-label="Ir a Mi ficha"><span className="brand-mark" aria-hidden="true">M</span><span><strong>Proyecto Milenio</strong><small>Gran Logia Mixta de Chile</small></span></button>
      <span className="product-motto">Tradición · Igualdad · Humanismo</span>
      <div className="topbar-meta">{api.useMocks && <span className="demo-badge">QA demostración</span>}{profile && <span className="environment-badge">{profile.displayName}</span>}{canNotifications && <button className="topbar-icon-button" type="button" aria-label="Abrir notificaciones" title="Notificaciones" onClick={() => setView('notifications')}>✦</button>}{onLogout && <button type="button" onClick={onLogout}>Cerrar sesión</button>}<span className="environment-badge">{api.useMocks ? 'UI QA v0.36' : `API v${systemInfo?.version ?? '—'}`}</span></div>
    </header>
    <div className="workspace">
      <nav className="sidebar" aria-label="Navegación principal">
        <button className={view === 'dashboard' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('dashboard')}><span aria-hidden="true">⌂</span> Inicio</button>
        <div className="nav-section">Portal del Hermano</div>
        <ModuleAccess icon="♙" label="Mi ficha" allowed={canMemberPortal} active={view === 'memberPortal'} onOpen={canMemberPortal ? () => setView('memberPortal') : undefined} />
        <ModuleAccess icon="▣" label="Mi calendario" allowed={canCalendar} active={view === 'calendar'} onOpen={canCalendar ? () => setView('calendar') : undefined} />
        <ModuleAccess icon="✦" label="Notificaciones" allowed={canNotifications} active={view === 'notifications'} onOpen={canNotifications ? () => setView('notifications') : undefined} />
        {canBootstrap && <><div className="nav-section">Plataforma</div><ModuleAccess icon="⚙" label="Configuración inicial" allowed={canBootstrap} active={view === 'bootstrap'} onOpen={() => setView('bootstrap')} /></>}
        <div className="nav-section">Procesos</div>
        <button className={view === 'candidates' ? 'nav-item active' : 'nav-item'} type="button" onClick={() => setView('candidates')}><span aria-hidden="true">◎</span> Insinuados publicados</button>
        <ModuleAccess icon="▣" label="Ficha de insinuado" allowed={canCandidateProfile} active={view === 'candidateProfile'} onOpen={canCandidateProfile ? () => setView('candidateProfile') : undefined} />
        <div className="nav-section">Gestión institucional</div>
        <ModuleAccess icon="◫" label="Fichas de miembros" allowed={canMembers} active={view === 'members'} onOpen={canMembers ? () => setView('members') : undefined} />
        <ModuleAccess icon="▧" label="Reportería Ejecutiva" allowed={canReporting} active={view === 'reporting'} onOpen={canReporting ? () => setView('reporting') : undefined} />
        <ModuleAccess icon="⊙" label="Control de miembros" allowed={canMemberControl} active={view === 'memberControl'} onOpen={canMemberControl ? () => setView('memberControl') : undefined} />
        <ModuleAccess icon="△" label="Calidad de datos" allowed={canDataQuality} active={view === 'dataQuality'} onOpen={canDataQuality ? () => setView('dataQuality') : undefined} />
        <ModuleAccess icon="✓" label="Cola de corroboración" allowed={canCaseQueue} active={view === 'caseQueue'} onOpen={canCaseQueue ? () => setView('caseQueue') : undefined} />
        <ModuleAccess icon="◉" label="Ceremonias" allowed={canCeremonies} active={view === 'ceremonies'} onOpen={canCeremonies ? () => setView('ceremonies') : undefined} />
        <ModuleAccess icon="◇" label="Régimen Interior" allowed={canRegimen} active={view === 'regimen'} onOpen={canRegimen ? () => setView('regimen') : undefined} />
        <ModuleAccess icon="◈" label="Gran Tesorería" allowed={canTreasury} active={view === 'treasury'} onOpen={canTreasury ? () => setView('treasury') : undefined} />
        <ModuleAccess icon="✧" label="Gran Hospitalaria" allowed={canHospitalaria} active={view === 'hospitalaria'} onOpen={canHospitalaria ? () => setView('hospitalaria') : undefined} />
        <ModuleAccess icon="▤" label="Gran Secretaría" allowed={canSecretariat} active={view === 'secretariat'} onOpen={canSecretariat ? () => setView('secretariat') : undefined} />
        <ModuleAccess icon="▨" label="Gran Archivero" allowed={canGrandArchive} active={view === 'grandArchive'} onOpen={canGrandArchive ? () => setView('grandArchive') : undefined} />
        <div className="nav-section">Taller</div>
        <ModuleAccess icon="⌂" label="Ficha de Taller" allowed={canLodgeProfile} active={view === 'lodgeProfile'} onOpen={canLodgeProfile ? () => setView('lodgeProfile') : undefined} />
        <ModuleAccess icon="□" label="Gestión Logial" allowed={canLodge} active={view === 'lodge'} onOpen={canLodge ? () => setView('lodge') : undefined} />
        <ModuleAccess icon="▤" label="Secretaría Logial" allowed={canLodge} active={view === 'lodgeSecretariat'} onOpen={canLodge ? () => setView('lodgeSecretariat') : undefined} />
        <div className="nav-section">Conocimiento</div>
        <ModuleAccess icon="▥" label="Biblioteca Virtual" allowed={canLibrary} active={view === 'library'} onOpen={canLibrary ? () => setView('library') : undefined} />
        <ModuleAccess icon="▦" label="Gestor Documental" allowed={canDocuments} active={view === 'documents'} onOpen={canDocuments ? () => setView('documents') : undefined} />
      </nav>
      <main className="content" id="contenido-principal">
        {error && <ErrorBanner message={error} />}
        {view === 'memberPortal' && canMemberPortal && <MemberPortalPage profile={profile} useMocks={api.useMocks} onOpenCalendar={canCalendar ? () => setView('calendar') : undefined} onOpenNotifications={canNotifications ? () => setView('notifications') : undefined} onOpenLibrary={canLibrary ? () => setView('library') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {view === 'dashboard' && <DashboardPage portal={portal} systemInfo={systemInfo} profile={profile} loading={loading} calendarApi={calendarApi} notificationApi={notificationApi} onOpenCandidates={() => setView('candidates')} onOpenCalendar={() => setView('calendar')} onOpenNotifications={() => setView('notifications')} onOpenSecretariat={canSecretariat ? () => setView('secretariat') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {view === 'bootstrap' && canBootstrap && <BootstrapPage bootstrapApi={bootstrapApi} />}
        {view === 'candidates' && <CandidatePortal portal={portal} loading={loading} />}
        {view === 'candidateProfile' && canCandidateProfile && <CandidateProfilePage useMocks={api.useMocks} onBack={() => setView('candidates')} />}
        {view === 'members' && canMembers && <MemberDirectoryPage api={api} membershipApi={membershipApi} />}
        {view === 'lodgeProfile' && canLodgeProfile && <LodgeProfilePage api={api} organizationProfileApi={organizationProfileApi} />}
        {view === 'reporting' && canReporting && <ExecutiveReportingPage reportingApi={reportingApi} />}
        {view === 'memberControl' && canMemberControl && <InternalAffairsMemberControlPage api={api} internalAffairsApi={internalAffairsApi} />}
        {view === 'dataQuality' && canDataQuality && <InternalAffairsDataQualityPage api={api} internalAffairsApi={internalAffairsApi} caseApi={dataQualityCaseApi} onOpenCases={() => setView('caseQueue')} />}
        {view === 'caseQueue' && canCaseQueue && <DataQualityCaseQueuePage api={api} caseApi={dataQualityCaseApi} />}
        {view === 'notifications' && canNotifications && <NotificationsPage notificationApi={notificationApi} />}
        {view === 'calendar' && canCalendar && <CalendarPage api={api} calendarApi={calendarApi} canManage={canSecretariat} />}
        {view === 'ceremonies' && canCeremonies && <CeremoniesPage api={api} />}
        {view === 'regimen' && canRegimen && <RegimenInteriorPage api={api} />}
        {view === 'treasury' && canTreasury && <RegularityPage api={api} kind="treasury" />}
        {view === 'hospitalaria' && canHospitalaria && <RegularityPage api={api} kind="hospitalaria" />}
        {view === 'secretariat' && canSecretariat && <GrandSecretariatPage api={api} />}
        {view === 'grandArchive' && canGrandArchive && <GrandArchivePage archiveApi={grandArchiveApi} />}
        {view === 'lodge' && canLodge && <LodgeManagementPage api={api} lodgeApi={lodgeApi} />}
        {view === 'lodgeSecretariat' && canLodge && <LodgeSecretariatPage api={api} lodgeApi={lodgeApi} />}
        {view === 'library' && canLibrary && <LibraryPage documentApi={documentApi} />}
        {view === 'documents' && canDocuments && <DocumentManagementPage api={api} documentApi={documentApi} />}
      </main>
    </div>
  </div>
}

function ModuleAccess({ icon, label, allowed, active = false, onOpen }: { icon: string; label: string; allowed: boolean; active?: boolean; onOpen?: () => void }) {
  if (allowed && onOpen) return <button className={active ? 'nav-item active' : 'nav-item'} type="button" onClick={onOpen}><span aria-hidden="true">{icon}</span> {label}</button>
  return <span className={allowed ? 'nav-item' : 'nav-item disabled'}><span aria-hidden="true">{icon}</span> {label}{!allowed && <em>sin acceso</em>}</span>
}

function CandidatePortal({ portal, loading }: { portal: CandidatePortalResponse | null; loading: boolean }) {
  const [query, setQuery] = useState('')
  const filtered = useMemo(() => {
    const normalized = normalize(query)
    return !normalized ? portal?.items ?? [] : (portal?.items ?? []).filter(item => normalize(`${item.displayName} ${item.workshopName} ${item.workshopNumber ?? ''}`).includes(normalized))
  }, [portal, query])
  return <><section className="page-heading"><div><p className="eyebrow">Transparencia institucional controlada</p><h1>Insinuados en período de publicación</h1><p>Publicaciones vigentes según el plazo configurado para solicitudes de iniciación.</p></div><span className="count-badge">{loading ? '…' : `${filtered.length} registros`}</span></section><section className="panel"><label className="search-field"><span>Buscar por nombre o Taller</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Ej.: Taller 23" /></label>{loading ? <LoadingRows /> : filtered.length === 0 ? <div className="empty-state"><strong>No hay coincidencias.</strong></div> : <div className="candidate-list">{filtered.map(candidate => <CandidateCard key={`${candidate.displayName}-${candidate.publishedFromUtc}`} candidate={candidate} />)}</div>}</section></>
}

function CandidateCard({ candidate }: { candidate: CandidatePublication }) {
  const percentage = Math.min(100, Math.round(candidate.elapsedDays / Math.max(1, candidate.requiredDays) * 100))
  const complete = candidate.elapsedDays >= candidate.requiredDays
  return <article className="candidate-card"><div className="candidate-avatar">{initials(candidate.displayName)}</div><div className="candidate-main"><div className="candidate-title-row"><div><h3>{candidate.displayName}</h3><p>{candidate.workshopName}{candidate.workshopNumber ? ` · Nº ${candidate.workshopNumber}` : ''}</p></div><span className={complete ? 'status-pill complete' : 'status-pill active'}>{complete ? 'Plazo cumplido' : 'En publicación'}</span></div><div className="progress-row"><div className="progress-track" role="progressbar" aria-valuenow={percentage} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${percentage}%` }} /></div><strong>{candidate.elapsedDays}/{candidate.requiredDays} días</strong></div><dl className="candidate-meta"><div><dt>Publicado</dt><dd>{formatDate(candidate.publishedFromUtc)}</dd></div><div><dt>Cumplimiento</dt><dd>{formatDate(candidate.complianceDateUtc)}</dd></div></dl></div></article>
}

function LoadingRows() { return <div className="loading-rows"><span /><span /><span /></div> }
function ErrorBanner({ message }: { message: string }) { return <div className="error-banner" role="alert"><strong>No fue posible conectar con la información institucional.</strong><span>{message}</span></div> }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function initials(value: string) { return value.split(/\s+/).filter(Boolean).slice(0, 2).map(x => x[0]?.toUpperCase() ?? '').join('') }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(date) }