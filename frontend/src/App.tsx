import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useCollapsibleNavGroups } from './navGroups'
import AdmissionsPage from './AdmissionsPage'
import BootstrapPage from './BootstrapPage'
import CalendarPage from './CalendarPage'
import CandidateProfilePage from './CandidateProfilePage'
import CandidateWorkshopIntakePage from './CandidateWorkshopIntakePage'
import CeremoniesPage from './CeremoniesPage'
import DashboardPage from './DashboardPage'
import DataQualityCaseQueuePage from './DataQualityCaseQueuePage'
import DemoProfileSwitcher from './DemoProfileSwitcher'
import DocumentManagementPage from './DocumentManagementPage'
import GlobalSearch, { type SearchEntry } from './GlobalSearch'
import ExecutiveReportingPage from './ExecutiveReportingPage'
import GrandArchivePage from './GrandArchivePage'
import GrandSecretariatPage from './GrandSecretariatPage'
import GrandTreasuryPage from './GrandTreasuryPage'
import HospitalariaPage from './HospitalariaPage'
import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'
import InternalAffairsDataQualityPage from './InternalAffairsDataQualityPage'
import InternalAffairsMemberControlPage from './InternalAffairsMemberControlPage'
import InitiationCircuitPage from './InitiationCircuitPage'
import LibraryPage from './LibraryPage'
import LodgeManagementPage from './LodgeManagementPage'
import LodgeInstructionPage from './LodgeInstructionPage'
import OrderInstructionReportPage from './OrderInstructionReportPage'
import LodgeProfilePage from './LodgeProfilePage'
import LodgeTreasuryPage from './LodgeTreasuryPage'
import MemberDirectoryPage from './MemberDirectoryPage'
import MemberPortalPage from './MemberPortalPage'
import MobileTabBar, { type MobileTabId } from './MobileTabBar'
import NotificationsPage from './NotificationsPage'
import PublishedCandidatePhoto from './PublishedCandidatePhoto'
import RegimenInteriorPage from './RegimenInteriorPage'
import RegularityPage from './RegularityPage'
import SystemConfigurationPage from './SystemConfigurationPage'
import UserMenu from './UserMenu'
import { WorkspaceTabs } from './actionKit'
import TextSizeControl, { TextSizeMenu } from './TextSizeControl'
import SecretariatRoleNavigation, { type SecretariatSection } from './SecretariatRoleNavigation'
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
import { organizationDisplayName } from './displayFormat'
import { buildPendingTasks, isOperationalProfile, isSystemAdministrator, navigationBadges, totalPending, type PendingCounts, type PendingTarget, type PendingTaskFlags } from './rolePendingTasks'

type View = 'admissions' | 'memberPortal' | 'dashboard' | 'bootstrap' | 'system' | 'candidates' | 'candidateProfile' | 'initiationCircuit' | 'members' | 'lodgeProfile' | 'reporting' | 'memberControl' | 'dataQuality' | 'caseQueue' | 'calendar' | 'notifications' | 'ceremonies' | 'regimen' | 'treasury' | 'lodgeTreasury' | 'hospitalaria' | 'secretariat' | 'lodge' | 'lodgeInstruction' | 'orderInstructionReport' | 'library' | 'documents' | 'grandArchive'
type ExtendedCapabilities = SessionProfile['capabilities'] & { canBootstrapInstitutional?: boolean; canConfigureSystem?: boolean; canManageLodgeOperations?: boolean; canReadLodgeSecretariat?: boolean; canManageLodgeSecretariat?: boolean; canManageDocuments?: boolean; canReadLibrary?: boolean; canManageGrandArchive?: boolean; canReadLodgeHospitalaria?: boolean; canManageLodgeHospitalaria?: boolean; canApproveLodgeExpenses?: boolean; canReadLodgeCouncilSummary?: boolean; canManageLodgeCouncilSummaryAccess?: boolean; canManageAnyWorkshopProfile?: boolean }

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
  const [view, setView] = useState<View>('dashboard')
  const [menuOpen, setMenuOpen] = useState(false)
  const [memberQuery, setMemberQuery] = useState('')
  const [memberQueryRevision, setMemberQueryRevision] = useState(0)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(readSidebarPreference)
  const [pendingCounts, setPendingCounts] = useState<PendingCounts>({})
  const [unreadCount, setUnreadCount] = useState(0)
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
  const canConfigureSystem = capabilities?.canConfigureSystem ?? canBootstrap
  const canCalendar = effectiveProfile !== null
  const canNotifications = effectiveProfile !== null
  const canLodgeOperations = capabilities?.canManageLodgeOperations ?? false
  const canLodgeTreasury = capabilities?.canManageLodgeTreasury ?? false
  const isLodgeTreasurerWorkspace = canLodgeTreasury && !canLodgeOperations
  const canMembers = hasInstitutionalScope && !isLodgeTreasurerWorkspace
  const canManageWorkshopProfile = capabilities?.canManageAnyWorkshopProfile ?? false
  const canLodgeProfile = (capabilities?.canReadLodgeCouncilSummary ?? false) || canManageWorkshopProfile
  const canManageLodgeSummaryAccess = capabilities?.canManageLodgeCouncilSummaryAccess ?? false
  const canCeremonies = capabilities?.canReviewCeremonies ?? false
  const canRegimen = capabilities?.canRunRegimenInteriorReports ?? false
  const canReporting = canRegimen
  const canMemberControl = canRegimen
  const canDataQuality = canRegimen
  const canCaseQueue = canRegimen
  const canTreasury = capabilities?.canManageTreasuryRegularity ?? false
  const canHospitalaria = capabilities?.canManageHospitalariaRegularity ?? false
  const canReadLodgeHospitalaria = capabilities?.canReadLodgeHospitalaria ?? false
  const canManageLodgeHospitalaria = capabilities?.canManageLodgeHospitalaria ?? false
  const canApproveLodgeExpenses = capabilities?.canApproveLodgeExpenses ?? false
  const canHospitalariaWorkspace = canHospitalaria || canReadLodgeHospitalaria
  const canSecretariat = capabilities?.canManageGrandSecretariat ?? false
  const canLodge = canLodgeOperations
  const instructionGrades = useMemo(() => [
    ...(capabilities?.canManageApprenticeInstruction ? ['apprentice' as const] : []),
    ...(capabilities?.canManageFellowcraftInstruction ? ['fellowcraft' as const] : []),
    ...(capabilities?.canManageMasterInstruction ? ['master' as const] : []),
  ], [capabilities?.canManageApprenticeInstruction, capabilities?.canManageFellowcraftInstruction, capabilities?.canManageMasterInstruction])
  const canManageAnyInstruction = instructionGrades.length > 0
  const orderInstructionGrades = useMemo(() => [
    ...(capabilities?.canReadOrderApprenticeInstructions ? ['apprentice' as const] : []),
    ...(capabilities?.canReadOrderFellowcraftInstructions ? ['fellowcraft' as const] : []),
    ...(capabilities?.canReadOrderMasterInstructions ? ['master' as const] : []),
  ], [capabilities?.canReadOrderApprenticeInstructions, capabilities?.canReadOrderFellowcraftInstructions, capabilities?.canReadOrderMasterInstructions])
  const canReadOrderInstructions = orderInstructionGrades.length > 0
  const canReadAllOrderInstructions = capabilities?.canReadAllOrderInstructions ?? false
  const canReadLodgeSecretariat = capabilities?.canReadLodgeSecretariat ?? false
  const canManageLodgeSecretariat = capabilities?.canManageLodgeSecretariat ?? false
  const canCandidateProfile = canSecretariat || canLodge
  const initiationViews: View[] = ['candidates', 'candidateProfile', 'initiationCircuit']
  const isInitiationView = initiationViews.includes(view)
  const canLibrary = effectiveProfile !== null && (capabilities?.canReadLibrary ?? false)
  const canDocuments = capabilities?.canManageDocuments ?? false
  const canGrandArchive = capabilities?.canManageGrandArchive ?? false
  const isLodgeSecretaryWorkspace = canManageLodgeSecretariat && !canSecretariat
  const isGrandSecretaryWorkspace = canSecretariat
  const localSecretariatViews: View[] = ['lodge', 'candidateProfile', 'initiationCircuit', 'admissions', 'members', 'documents']
  const grandSecretariatViews: View[] = ['secretariat', 'candidateProfile', 'initiationCircuit', 'admissions', 'ceremonies', 'members', 'documents']
  const localSecretariatSections: SecretariatSection[] = [
    { id: 'lodge', label: 'Tenidas y actas', description: 'Agenda, asistencia, extractos, correspondencia y pendientes' },
    { id: 'candidateProfile', label: 'Insinuados', description: 'Nuevo insinuado y seguimiento del expediente privado' },
    { id: 'initiationCircuit', label: 'Circuito de iniciación', description: 'Entrevistas, balotaje y solicitud de Plancha' },
    { id: 'admissions', label: 'Afiliación e incorporación', description: 'Expedientes de hermanos ya iniciados, separados de la iniciación' },
    { id: 'members', label: 'Cuadro del Taller', description: 'Fichas e historial de los hermanos del Taller' },
    { id: 'documents', label: 'Documentos', description: 'Carga y consulta de respaldos firmados' },
  ]
  const grandSecretariatSections: SecretariatSection[] = [
    { id: 'secretariat', label: 'Bandeja institucional', description: 'Planchas PDF firmadas, extractos, templos y salas' },
    { id: 'candidateProfile', label: 'Revisión de insinuados', description: 'Control previo a publicación institucional' },
    { id: 'initiationCircuit', label: 'Circuito de iniciación', description: 'Control administrativo y Plancha de autorización' },
    { id: 'admissions', label: 'Afiliación e incorporación', description: 'Expedientes de hermanos ya iniciados, separados de la iniciación' },
    { id: 'ceremonies', label: 'Ceremonias', description: 'Solicitudes, requisitos y autorizaciones' },
    { id: 'members', label: 'Cuadro General', description: 'Consulta mínima de fichas institucionales' },
    { id: 'documents', label: 'Documentos', description: 'Carga y consulta de PDF oficiales firmados' },
  ]
  const hasInstitutionalManagement = canMembers || canReporting || canMemberControl || canDataQuality || canCaseQueue || canCeremonies || canRegimen || canTreasury || canHospitalaria || canSecretariat || canGrandArchive || canManageAnyInstruction || canReadOrderInstructions

  const pendingFlags: PendingTaskFlags = useMemo(() => ({ canApproveLodgeExpenses, canLodgeTreasury, canSecretariat, canCeremonies, canLodge, canManageLodgeSecretariat, canConfigureSystem, canBootstrap }), [canApproveLodgeExpenses, canLodgeTreasury, canSecretariat, canCeremonies, canLodge, canManageLodgeSecretariat, canConfigureSystem, canBootstrap])
  const operational = isOperationalProfile(pendingFlags)
  const navRef = useRef<HTMLElement>(null)
  useCollapsibleNavGroups(navRef, [view, effectiveProfile?.displayName, sidebarCollapsed, operational])
  const pendingTasks = useMemo(() => buildPendingTasks(pendingFlags, pendingCounts), [pendingFlags, pendingCounts])
  const badges = useMemo(() => navigationBadges(pendingTasks), [pendingTasks])
  const pendingTotal = totalPending(pendingTasks)

  useEffect(() => {
    if (!effectiveProfile) return
    let active = true
    const today = new Date().toISOString().slice(0, 10)
    const firstOrganization = (canApproveLodgeExpenses || canLodgeTreasury || canLodge || canManageLodgeSecretariat)
      ? api.getOrganizationOptions().then(response => response.items[0]?.id ?? null)
      : Promise.resolve(null)
    Promise.allSettled([
      notificationApi.getMine({ unreadOnly: true, limit: 50 }),
      firstOrganization.then(id => id && (canApproveLodgeExpenses || canLodgeTreasury) ? api.getLodgeTreasuryExpenses(id, `${today.slice(0, 4)}-01-01`, today).then(response => response.items.filter(item => item.approvalStatus === 'pending_approval').length) : null),
      canSecretariat ? candidateIntakeApi.getGrandSecretariatQueue('pending_grand_secretariat').then(response => response.total) : Promise.resolve(null),
      canCeremonies ? api.getCeremonyReviewQueue().then(response => response.total) : Promise.resolve(null),
      canLodge && !canSecretariat ? candidateIntakeApi.getWorkshopQueue().then(response => response.total) : Promise.resolve(null),
      firstOrganization.then(id => id && (canLodge || canManageLodgeSecretariat) ? lodgeApi.getSecretariatTasks(id).then(response => response.items.filter(item => item.status === 'pending' || item.status === 'in_progress').length) : null),
    ]).then(([unread, expenses, secretariat, ceremonies, workshop, tasks]) => {
      if (!active) return
      const value = <T,>(result: PromiseSettledResult<T>) => (result.status === 'fulfilled' ? result.value : null)
      const unreadItems = value(unread)
      setUnreadCount(Array.isArray(unreadItems) ? unreadItems.filter(item => item.readAtUtc === null).length : 0)
      setPendingCounts({ expensesToApprove: value(expenses), secretariatReviews: value(secretariat), ceremonyReviews: value(ceremonies), workshopCandidates: value(workshop), secretariatTasks: value(tasks) })
    })
    return () => { active = false }
  }, [api, candidateIntakeApi, lodgeApi, notificationApi, effectiveProfile, canApproveLodgeExpenses, canLodgeTreasury, canSecretariat, canCeremonies, canLodge, canManageLodgeSecretariat])

  useEffect(() => { setMenuOpen(false) }, [view])
  useEffect(() => {
    if (!menuOpen) return
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenuOpen(false) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [menuOpen])

  const openPendingTarget = useCallback((target: PendingTarget) => { setView(target) }, [])
  const openPendingInbox = () => {
    setView('dashboard')
    setMenuOpen(false)
    window.setTimeout(() => document.getElementById('mis-pendientes')?.scrollIntoView({ block: 'start', behavior: 'smooth' }), 60)
  }
  const activeTab: MobileTabId | null = view === 'dashboard' ? 'home' : view === 'calendar' ? 'calendar' : view === 'notifications' ? 'notifications' : view === 'memberPortal' && !operational ? 'second' : null

  const toggleSidebar = () => setSidebarCollapsed(current => { const next = !current; writeSidebarPreference(next); return next })
  const openMembersSearch = (query: string) => { setMemberQuery(query); setMemberQueryRevision(revision => revision + 1); setView('members') }

  /** PMGM-UX-003 · Catálogo para la búsqueda global: solo módulos que el perfil puede abrir. */
  const searchEntries: SearchEntry[] = useMemo(() => {
    const entry = (allowed: boolean, id: View, label: string, section: string, icon: InstitutionalIconName, keywords = ''): SearchEntry[] => allowed ? [{ id, label, section, icon, keywords, onSelect: () => setView(id) }] : []
    return [
      ...entry(true, 'dashboard', 'Inicio', 'General', 'home', 'pendientes resumen'),
      ...entry(canMemberPortal, 'memberPortal', 'Mi ficha', 'Portal del Hermano', 'member', 'perfil datos personales'),
      ...entry(canCalendar, 'calendar', 'Agenda', 'Portal del Hermano', 'calendar', 'agenda tenidas eventos'),
      ...entry(canNotifications, 'notifications', 'Avisos', 'Portal del Hermano', 'bell', 'avisos alertas'),
      ...entry(canConfigureSystem, 'system', 'Parámetros del sistema', 'Sistema', 'settings', 'configuracion plazos reglas'),
      ...entry(canBootstrap && !canConfigureSystem, 'bootstrap', 'Configuración inicial', 'Sistema', 'settings', 'carga institucional'),
      ...entry(true, 'candidates', 'Insinuaciones e Iniciación', 'Procesos', 'candidate', `insinuados publicados publicacion${canCandidateProfile ? ' carga revision insinuacion expediente' : ''}${canCandidateProfile || canCeremonies ? ' circuito iniciacion entrevistas balotaje plancha' : ''}`),
      ...entry(canMembers, 'members', 'Fichas de miembros', 'Gestión institucional', 'members', 'hermanos cuadro'),
      ...entry(canReporting, 'reporting', 'Reportería Ejecutiva', 'Gestión institucional', 'report', 'reportes'),
      ...entry(canMemberControl, 'memberControl', 'Control de miembros', 'Gestión institucional', 'memberControl'),
      ...entry(canDataQuality, 'dataQuality', 'Calidad de datos', 'Gestión institucional', 'dataQuality'),
      ...entry(canCaseQueue && !canDataQuality, 'caseQueue', 'Cola de corroboración', 'Gestión institucional', 'check'),
      ...entry(canCeremonies, 'ceremonies', 'Ceremonias', 'Gestión institucional', 'ceremony', 'autorizaciones'),
      ...entry(canRegimen, 'regimen', 'Régimen Interior', 'Gestión institucional', 'shield'),
      ...entry(canTreasury, 'treasury', 'Gran Tesorería', 'Gestión institucional', 'treasury', 'cuotas pagos'),
      ...entry(canHospitalariaWorkspace, 'hospitalaria', canHospitalaria ? 'Gran Hospitalaria' : 'Hospitalaria del Taller', canHospitalaria ? 'Gestión institucional' : 'Taller', 'hospitalaria'),
      ...entry(canSecretariat, 'secretariat', 'Gran Secretaría', 'Gestión institucional', 'secretariat', 'planchas templos salas'),
      ...entry(canGrandArchive, 'grandArchive', 'Gran Archivero', 'Gestión institucional', 'archive', 'archivo'),
      ...entry(canReadOrderInstructions, 'orderInstructionReport', 'Docencia de la Orden', 'Gestión institucional', 'library'),
      ...entry(canLodgeProfile && !canLodge, 'lodgeProfile', 'Ficha del Taller', 'Taller', 'lodge'),
      ...entry(canLodge, 'lodge', isLodgeSecretaryWorkspace ? 'Secretaría' : 'Gestión Logial', 'Taller', 'lodge', 'tenidas actas asistencia'),
      ...entry(canManageAnyInstruction, 'lodgeInstruction', 'Docencia', 'Taller', 'library', 'instruccion'),
      ...entry(canLodgeTreasury || canApproveLodgeExpenses, 'lodgeTreasury', 'Tesorería', 'Taller', 'treasury', 'egresos cuotas recibos'),
      ...entry(canLibrary, 'library', 'Biblioteca Virtual', 'Conocimiento', 'library', 'libros planchas'),
      ...entry(canDocuments, 'documents', 'Gestor Documental', 'Conocimiento', 'documents', 'pdf archivos'),
    ]
  }, [canMemberPortal, canCalendar, canNotifications, canConfigureSystem, canBootstrap, canCandidateProfile, canSecretariat, canCeremonies, canMembers, canReporting, canMemberControl, canDataQuality, canCaseQueue, canRegimen, canTreasury, canHospitalariaWorkspace, canHospitalaria, canGrandArchive, canReadOrderInstructions, canLodgeProfile, canLodge, isLodgeSecretaryWorkspace, canManageAnyInstruction, canLodgeTreasury, canApproveLodgeExpenses, canLibrary, canDocuments])

  const changeDemoProfile = (next: DemoProfileKey) => {
    setDemoProfileKey(next)
    setPendingCounts({})
    setView('dashboard')
  }

  const openNotificationAction = (actionUrl: string) => {
    const path = actionUrl.split(/[?#]/, 1)[0]
    if (path === '/initiation-circuit') return setView('initiationCircuit')
    if (path === '/candidates') return setView('candidates')
    if (path === '/calendar' && canCalendar) return setView('calendar')
    if (path === '/ceremonies' && canCeremonies) return setView('ceremonies')
    if (path === '/lodge' && canLodge) return setView('lodge')
    if (path === '/documents' && canDocuments) return setView('documents')
  }

  const versionLabel = api.useMocks ? 'UI QA v0.88' : `API v${systemInfo?.version ?? '—'}`
  return <div className="app-shell">
    {api.useMocks && <div className="demo-strip" role="region" aria-label="Controles de la demostración"><span className="demo-badge">QA demostración</span><DemoProfileSwitcher value={demoProfileKey} onChange={changeDemoProfile} /><span className="demo-version">{versionLabel}</span></div>}
    <header className="topbar">
      <button className="brand" type="button" onClick={() => setView('dashboard')} aria-label="Proyecto Centenario — Gran Logia Mixta de Chile; ir a Inicio"><span className="brand-mark brand-mark-reduced"><img className="brand-logo" src={`${import.meta.env.BASE_URL}brand/logo-glmch-reducido-v2-azul.svg`} alt="Gran Logia Mixta de Chile" /></span><span><strong>Proyecto Centenario</strong></span></button>
      <span className="product-motto">Camino al centenario 1929-2029</span>
      <div className="topbar-meta">{effectiveProfile && <GlobalSearch entries={searchEntries} onSearchMembers={canMembers ? openMembersSearch : undefined} />}{effectiveProfile && <span className="environment-badge role-chip">{effectiveProfile.displayName}</span>}{effectiveProfile && <TextSizeMenu />}{canNotifications && <button className="topbar-icon-button" type="button" aria-label="Abrir notificaciones" title="Notificaciones" data-badge={unreadCount > 0 ? (unreadCount > 99 ? '99+' : String(unreadCount)) : undefined} onClick={() => setView('notifications')}><InstitutionalIcon name="bell" size={18} /></button>}{effectiveProfile ? <UserMenu displayName={effectiveProfile.displayName} versionLabel={versionLabel} onOpenProfile={canMemberPortal ? () => setView('memberPortal') : undefined} onLogout={onLogout} /> : onLogout && <button type="button" onClick={onLogout}>Cerrar sesión</button>}</div>
    </header>
    <div className={sidebarCollapsed ? 'workspace is-sidebar-collapsed' : 'workspace'}>
      <nav ref={navRef} className={menuOpen ? 'sidebar is-open' : 'sidebar'} id="navegacion-principal" aria-label="Navegación principal" onClick={event => { if ((event.target as HTMLElement).closest('button')) setMenuOpen(false) }}>
        <button className="sidebar-collapse" type="button" aria-pressed={sidebarCollapsed} aria-label={sidebarCollapsed ? 'Expandir menú lateral' : 'Contraer menú lateral'} title={sidebarCollapsed ? 'Expandir menú' : 'Contraer menú'} onClick={toggleSidebar}><InstitutionalIcon name={sidebarCollapsed ? 'sidebarExpand' : 'sidebarCollapse'} size={18} /><span>{sidebarCollapsed ? 'Expandir' : 'Contraer menú'}</span></button>
        <button className={view === 'dashboard' ? 'nav-item active' : 'nav-item'} type="button" title="Inicio" data-in-tabbar="true" onClick={() => setView('dashboard')}><NavIcon name="home" /> <span className="nav-text" data-hint={navHint('Inicio')}>Inicio</span></button>
        <div className="nav-section" data-in-tabbar={operational ? undefined : 'true'}>Mi espacio</div>
        <ModuleAccess icon="member" label="Mi ficha" inTabbar={!operational} allowed={canMemberPortal} active={view === 'memberPortal'} onOpen={canMemberPortal ? () => setView('memberPortal') : undefined} />
        <ModuleAccess icon="calendar" label="Agenda" inTabbar allowed={canCalendar} active={view === 'calendar'} onOpen={canCalendar ? () => setView('calendar') : undefined} />
        <ModuleAccess icon="bell" label="Avisos" inTabbar badge={unreadCount} allowed={canNotifications} active={view === 'notifications'} onOpen={canNotifications ? () => setView('notifications') : undefined} />
        {(canBootstrap||canConfigureSystem) && <><div className="nav-section">Sistema</div><ModuleAccess icon="settings" label={canConfigureSystem ? 'Parámetros del sistema' : 'Configuración inicial'} allowed active={view === 'system' || view === 'bootstrap'} onOpen={() => setView(canConfigureSystem ? 'system' : 'bootstrap')} /></>}
        <div className="nav-section">Trámites</div>
        <ModuleAccess icon="candidate" label="Insinuaciones e Iniciación" allowed active={isInitiationView} onOpen={() => setView('candidates')} />
        {hasInstitutionalManagement && <>
          <div className="nav-section">Gestión y consultas</div>
          {!isLodgeSecretaryWorkspace && !isGrandSecretaryWorkspace && <ModuleAccess icon="members" label="Fichas de miembros" allowed={canMembers} active={view === 'members'} onOpen={canMembers ? () => setView('members') : undefined} />}
          <ModuleAccess icon="report" label="Reportería Ejecutiva" allowed={canReporting} active={view === 'reporting'} onOpen={canReporting ? () => setView('reporting') : undefined} />
          <ModuleAccess icon="memberControl" label="Control de miembros" allowed={canMemberControl} active={view === 'memberControl'} onOpen={canMemberControl ? () => setView('memberControl') : undefined} />
          <ModuleAccess icon="dataQuality" label={canDataQuality ? 'Calidad de datos' : 'Cola de corroboración'} allowed={canDataQuality || canCaseQueue} active={view === 'dataQuality' || view === 'caseQueue'} onOpen={() => setView(canDataQuality ? 'dataQuality' : 'caseQueue')} />
          {!isGrandSecretaryWorkspace && <ModuleAccess icon="ceremony" label="Ceremonias" badge={badges.ceremonies} allowed={canCeremonies} active={view === 'ceremonies'} onOpen={canCeremonies ? () => setView('ceremonies') : undefined} />}
          <ModuleAccess icon="shield" label="Régimen Interior" allowed={canRegimen} active={view === 'regimen'} onOpen={canRegimen ? () => setView('regimen') : undefined} />
          <ModuleAccess icon="treasury" label="Gran Tesorería" allowed={canTreasury} active={view === 'treasury'} onOpen={canTreasury ? () => setView('treasury') : undefined} />
          <ModuleAccess icon="hospitalaria" label="Gran Hospitalaria" allowed={canHospitalaria} active={view === 'hospitalaria'} onOpen={canHospitalaria ? () => setView('hospitalaria') : undefined} />
          <ModuleAccess icon="secretariat" label="Gran Secretaría" allowed={canSecretariat} active={!isInitiationView && (isGrandSecretaryWorkspace ? grandSecretariatViews.includes(view) : view === 'secretariat')} onOpen={canSecretariat ? () => setView('secretariat') : undefined} />
          <ModuleAccess icon="archive" label="Gran Archivero" allowed={canGrandArchive} active={view === 'grandArchive'} onOpen={canGrandArchive ? () => setView('grandArchive') : undefined} />
          <ModuleAccess icon="library" label="Docencia de la Orden" allowed={canReadOrderInstructions} active={view === 'orderInstructionReport'} onOpen={canReadOrderInstructions ? () => setView('orderInstructionReport') : undefined} />
        </>}
        {(canLodgeProfile || canLodge || canLodgeTreasury || canApproveLodgeExpenses || canReadLodgeHospitalaria || canManageAnyInstruction) && <>
          <div className="nav-section">Mi Taller</div>
          <ModuleAccess icon="lodge" label="Ficha del Taller" allowed={canLodgeProfile && !canLodge} active={view === 'lodgeProfile'} onOpen={canLodgeProfile ? () => setView('lodgeProfile') : undefined} />
          {isLodgeSecretaryWorkspace ? <ModuleAccess icon="secretariat" label="Secretaría" badge={badges.lodge} allowed active={!isInitiationView && (localSecretariatViews.includes(view) || view === 'lodgeProfile')} onOpen={() => setView('lodge')} /> : <ModuleAccess icon="lodge" label="Gestión Logial" badge={badges.lodge} allowed={canLodge} active={view === 'lodge' || view === 'lodgeProfile'} onOpen={canLodge ? () => setView('lodge') : undefined} />}
          {!isLodgeSecretaryWorkspace && <ModuleAccess icon="library" label="Docencia" allowed={canManageAnyInstruction} active={view === 'lodgeInstruction'} onOpen={canManageAnyInstruction ? () => setView('lodgeInstruction') : undefined} />}
          <ModuleAccess icon="treasury" label="Tesorería" badge={badges.lodgeTreasury} allowed={canLodgeTreasury || canApproveLodgeExpenses} active={view === 'lodgeTreasury'} onOpen={canLodgeTreasury || canApproveLodgeExpenses ? () => setView('lodgeTreasury') : undefined} />
          {canReadLodgeHospitalaria && !canHospitalaria && <ModuleAccess icon="hospitalaria" label="Hospitalaria del Taller" allowed active={view === 'hospitalaria'} onOpen={() => setView('hospitalaria')} />}
        </>}
        {(canLibrary || canDocuments) && <>
          <div className="nav-section">Biblioteca y documentos</div>
          <ModuleAccess icon="library" label="Biblioteca Virtual" allowed={canLibrary} active={view === 'library'} onOpen={canLibrary ? () => setView('library') : undefined} />
          {!isLodgeSecretaryWorkspace && !isGrandSecretaryWorkspace && <ModuleAccess icon="documents" label="Gestor Documental" allowed={canDocuments} active={view === 'documents'} onOpen={canDocuments ? () => setView('documents') : undefined} />}
        </>}
        <TextSizeControl className="in-sidebar" />
      </nav>
      <main className="content" id="contenido-principal">
        {error && <ErrorBanner message={error} />}
        {isLodgeSecretaryWorkspace && !isInitiationView && localSecretariatViews.includes(view) && <SecretariatRoleNavigation title="Secretaría" sections={localSecretariatSections} active={view} onChange={id => setView(id as View)} />}
        {isGrandSecretaryWorkspace && !isInitiationView && grandSecretariatViews.includes(view) && <SecretariatRoleNavigation title="Gran Secretaría" sections={grandSecretariatSections} active={view} onChange={id => setView(id as View)} />}
        {view === 'memberPortal' && canMemberPortal && <MemberPortalPage profile={effectiveProfile} useMocks={api.useMocks} membershipApi={membershipApi} documentApi={documentApi} onOpenCalendar={canCalendar ? () => setView('calendar') : undefined} onOpenNotifications={canNotifications ? () => setView('notifications') : undefined} onOpenLibrary={canLibrary ? () => setView('library') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {view === 'dashboard' && <DashboardPage pendingTasks={pendingTasks} operational={operational} administrator={isSystemAdministrator(pendingFlags)} onOpenPending={openPendingTarget} portal={portal} systemInfo={systemInfo} profile={effectiveProfile} loading={loading} calendarApi={calendarApi} notificationApi={notificationApi} onOpenCandidates={() => setView('candidates')} onOpenCalendar={() => setView('calendar')} onOpenNotifications={() => setView('notifications')} onOpenSecretariat={canSecretariat ? () => setView('secretariat') : undefined} onOpenLodge={canLodge ? () => setView('lodge') : undefined} />}
        {/* PMGM-UX menús sin repetir: módulos relacionados como pestañas de una sola opción del menú. */}
        {(view === 'system' || view === 'bootstrap') && canConfigureSystem && <SystemConfigurationPage api={api} bootstrapSlot={canBootstrap ? <BootstrapPage bootstrapApi={bootstrapApi} /> : undefined} />}
        {view === 'bootstrap' && canBootstrap && !canConfigureSystem && <BootstrapPage bootstrapApi={bootstrapApi} />}
        {isInitiationView && (canCandidateProfile || canCeremonies) && <WorkspaceTabs label="Secciones de Insinuaciones e Iniciación" active={view} onChange={setView} tabs={[
          { id: 'candidates' as View, label: 'Publicados' },
          ...(canCandidateProfile ? [{ id: 'candidateProfile' as View, label: canSecretariat ? 'Revisión' : 'Carga', badge: badges.candidateProfile }] : []),
          ...(canCandidateProfile || canCeremonies ? [{ id: 'initiationCircuit' as View, label: 'Circuito de Iniciación' }] : []),
        ]} />}
        {view === 'candidates' && <CandidatePortal portal={portal} loading={loading} api={candidateIntakeApi} />}
        {view === 'candidateProfile' && canCandidateProfile && (canSecretariat ? <CandidateProfilePage api={candidateIntakeApi} canReview={canSecretariat} onBack={() => setView('candidates')} /> : <CandidateWorkshopIntakePage api={candidateIntakeApi} onBack={() => setView('candidates')} />)}
        {view === 'initiationCircuit' && (canCandidateProfile || canCeremonies) && <InitiationCircuitPage api={api} demoProfileKey={api.useMocks ? demoProfileKey : undefined} />}
        {view === 'admissions' && (canManageLodgeSecretariat || canSecretariat) && <AdmissionsPage demoProfileKey={api.useMocks ? demoProfileKey : undefined} api={api} membershipApi={membershipApi} documentApi={documentApi} />}
        {view === 'members' && canMembers && <MemberDirectoryPage key={memberQueryRevision} api={api} membershipApi={membershipApi} initialQuery={memberQuery} canExport={canSecretariat || canManageLodgeSecretariat || canConfigureSystem} allowTableView={canSecretariat || canConfigureSystem} />}
        {(view === 'lodge' || view === 'lodgeProfile') && canLodge && canLodgeProfile && <WorkspaceTabs label="Secciones del Taller" active={view} onChange={setView} tabs={[{ id: 'lodge' as View, label: isLodgeSecretaryWorkspace ? 'Secretaría' : 'Gestión Logial' }, { id: 'lodgeProfile' as View, label: 'Ficha del Taller' }]} />}
        {view === 'lodgeProfile' && canLodgeProfile && <LodgeProfilePage api={api} organizationProfileApi={organizationProfileApi} canManageAccess={canManageLodgeSummaryAccess} canEditWorkshopProfile={canManageWorkshopProfile} />}
        {view === 'reporting' && canReporting && <ExecutiveReportingPage reportingApi={reportingApi} />}
        {view === 'memberControl' && canMemberControl && <InternalAffairsMemberControlPage api={api} internalAffairsApi={internalAffairsApi} />}
        {(view === 'dataQuality' || view === 'caseQueue') && canDataQuality && canCaseQueue && <WorkspaceTabs label="Secciones de Calidad de datos" active={view} onChange={setView} tabs={[{ id: 'dataQuality' as View, label: 'Hallazgos' }, { id: 'caseQueue' as View, label: 'Cola de corroboración' }]} />}
        {view === 'dataQuality' && canDataQuality && <InternalAffairsDataQualityPage api={api} internalAffairsApi={internalAffairsApi} caseApi={dataQualityCaseApi} onOpenCases={() => setView('caseQueue')} />}
        {view === 'caseQueue' && canCaseQueue && <DataQualityCaseQueuePage api={api} caseApi={dataQualityCaseApi} />}
        {view === 'notifications' && canNotifications && <NotificationsPage notificationApi={notificationApi} onAction={openNotificationAction} />}
        {view === 'calendar' && canCalendar && <CalendarPage api={api} calendarApi={calendarApi} canManage={canSecretariat} />}
        {view === 'ceremonies' && canCeremonies && <CeremoniesPage api={api} />}
        {view === 'regimen' && canRegimen && <RegimenInteriorPage api={api} />}
        {view === 'treasury' && canTreasury && <GrandTreasuryPage api={api} />}
        {view === 'lodgeTreasury' && (canLodgeTreasury || canApproveLodgeExpenses) && <LodgeTreasuryPage api={api} canManage={canLodgeTreasury} canApproveExpenses={canApproveLodgeExpenses} />}
        {view === 'hospitalaria' && canHospitalariaWorkspace && <HospitalariaPage api={api} canReadLocal={canReadLodgeHospitalaria} canManageLocal={canManageLodgeHospitalaria} canApproveExpenses={canApproveLodgeExpenses} canManageGrand={canHospitalaria} regularitySlot={canHospitalaria ? <RegularityPage api={api} kind="hospitalaria" /> : undefined} />}
        {view === 'secretariat' && canSecretariat && <GrandSecretariatPage api={api} />}
        {view === 'grandArchive' && canGrandArchive && <GrandArchivePage archiveApi={grandArchiveApi} />}
        {view === 'lodge' && canLodge && <LodgeManagementPage api={api} lodgeApi={lodgeApi} documentApi={documentApi} canReadSecretariat={canReadLodgeSecretariat} canManageSecretariat={canManageLodgeSecretariat} instructionGrades={instructionGrades} />}
        {view === 'lodgeInstruction' && canManageAnyInstruction && <LodgeInstructionPage api={api} lodgeApi={lodgeApi} allowedGrades={instructionGrades} />}
        {view === 'orderInstructionReport' && canReadOrderInstructions && <OrderInstructionReportPage lodgeApi={lodgeApi} allowedGrades={orderInstructionGrades} canReadAll={canReadAllOrderInstructions} />}
        {view === 'library' && canLibrary && <LibraryPage documentApi={documentApi} />}
        {view === 'documents' && canDocuments && <DocumentManagementPage api={api} documentApi={documentApi} />}
      </main>
    </div>
    {effectiveProfile && <MobileTabBar active={activeTab} menuOpen={menuOpen} operational={operational} pendingCount={pendingTotal} unreadCount={unreadCount} onHome={() => setView('dashboard')} onSecond={operational ? openPendingInbox : () => setView('memberPortal')} onCalendar={() => setView('calendar')} onNotifications={() => setView('notifications')} onToggleMenu={() => setMenuOpen(open => !open)} />}
  </div>
}

function NavIcon({ name }: { name: InstitutionalIconName }) {
  return <span className="nav-icon" aria-hidden="true"><InstitutionalIcon name={name} size={18} /></span>
}

function ModuleAccess({ icon, label, allowed, active = false, onOpen, badge, inTabbar = false }: { icon: InstitutionalIconName; label: string; allowed: boolean; active?: boolean; onOpen?: () => void; badge?: number; inTabbar?: boolean }) {
  if (!allowed || !onOpen) return null
  const count = badge && badge > 0 ? (badge > 99 ? '99+' : String(badge)) : undefined
  const hint = navHint(label)
  return <button className={active ? 'nav-item active' : 'nav-item'} type="button" onClick={onOpen} data-badge={count} data-in-tabbar={inTabbar ? 'true' : undefined} title={[label, hint, count ? `${count} pendientes` : ''].filter(Boolean).join(' — ')}><NavIcon name={icon} /> <span className="nav-text" data-hint={hint}>{label}</span></button>
}

/** PMGM-UX-004 · Una línea de ayuda en lenguaje simple por opción del menú (visible en tablet y celular; tooltip en escritorio). */
const NAV_HINTS: Record<string, string> = {
  'Insinuaciones e Iniciación': 'Publicaciones y seguimiento de la iniciación según tus permisos',
  'Inicio': 'Resumen del día y tus pendientes',
  'Mi ficha': 'Tus datos personales y masónicos',
  'Agenda': 'Tenidas, reuniones y actividades',
  'Avisos': 'Avisos y mensajes para ti',
  'Parámetros del sistema': 'Ajustes generales, respaldos y perfiles',
  'Configuración inicial': 'Puesta en marcha del sistema',
  'Fichas de miembros': 'Buscar y consultar hermanos',
  'Reportería Ejecutiva': 'Indicadores y reportes de la Orden',
  'Control de miembros': 'Altas, bajas y cambios de estado',
  'Calidad de datos': 'Datos incompletos por corregir',
  'Cola de corroboración': 'Casos por verificar',
  'Ceremonias': 'Solicitar y autorizar ceremonias',
  'Régimen Interior': 'Normas y disciplina',
  'Gran Tesorería': 'Cuotas y finanzas de la Orden',
  'Gran Hospitalaria': 'Ayuda solidaria de la Orden',
  'Gran Secretaría': 'Planchas, documentos y templos',
  'Gran Archivero': 'Archivo histórico de la Orden',
  'Docencia de la Orden': 'Instrucción en todos los Talleres',
  'Ficha del Taller': 'Datos y cargos de tu Taller',
  'Secretaría': 'Tenidas, actas y correspondencia',
  'Gestión Logial': 'Administración del Taller',
  'Docencia': 'Instrucción de tu Taller',
  'Tesorería': 'Cuotas, pagos y gastos del Taller',
  'Hospitalaria del Taller': 'Ayuda solidaria del Taller',
  'Biblioteca Virtual': 'Libros y trabajos para leer',
  'Gestor Documental': 'Documentos y respaldos firmados',
}

function navHint(label: string): string | undefined {
  return NAV_HINTS[label]
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
  return <article className="candidate-card"><div className="candidate-avatar" style={{ overflow: 'hidden' }}><PublishedCandidatePhoto candidate={candidate} api={api} /></div><div className="candidate-main"><div className="candidate-title-row"><div><h3>{candidate.displayName}</h3><p>{organizationDisplayName(candidate.workshopName, candidate.workshopNumber)}</p></div><span className={complete ? 'status-pill complete' : 'status-pill active'}>{complete ? 'Plazo cumplido' : 'En publicación'}</span></div><div className="progress-row"><div className="progress-track" role="progressbar" aria-valuenow={percentage} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${percentage}%` }} /></div><strong>{candidate.elapsedDays}/{candidate.requiredDays} días</strong></div><dl className="candidate-meta"><div><dt>Publicado</dt><dd>{formatDate(candidate.publishedFromUtc)}</dd></div><div><dt>Cumplimiento</dt><dd>{formatDate(candidate.complianceDateUtc)}</dd></div></dl></div></article>
}

function LoadingRows() { return <div className="loading-rows"><span /><span /><span /></div> }
function ErrorBanner({ message }: { message: string }) { return <div className="error-banner" role="alert"><strong>No fue posible conectar con la información institucional.</strong><span>{message}</span></div> }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(date) }

const SIDEBAR_PREFERENCE_KEY = 'pmgm.sidebarCollapsed'
function readSidebarPreference(): boolean {
  try { return window.localStorage.getItem(SIDEBAR_PREFERENCE_KEY) === '1' } catch { return false }
}
function writeSidebarPreference(collapsed: boolean) {
  try { window.localStorage.setItem(SIDEBAR_PREFERENCE_KEY, collapsed ? '1' : '0') } catch { /* preferencia opcional */ }
}
