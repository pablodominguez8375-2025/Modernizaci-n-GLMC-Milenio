import { lazy, Suspense, useEffect, useState, useSyncExternalStore } from 'react'
import '../app-styles'
import { LodgeCouncilApiProvider } from '../LodgeCouncilApiContext'
import { createDefaultBootstrapApiClient } from '../api/bootstrapApi'
import { createDefaultCalendarApiClient } from '../api/calendarApi'
import { createDefaultCandidateIntakeApiClient } from '../api/candidateIntakeApi'
import { createDefaultDataQualityCaseApiClient } from '../api/dataQualityCaseApi'
import { createDefaultDocumentApiClient } from '../api/documentApi'
import { createDefaultGrandArchiveApiClient } from '../api/grandArchiveApi'
import { createDefaultInternalAffairsApiClient } from '../api/internalAffairsApi'
import { createDefaultLodgeApiClient } from '../api/lodgeApi'
import { createDefaultLodgeCouncilApiClient } from '../api/lodgeCouncilApi'
import { createDefaultMembershipApiClient } from '../api/membershipApi'
import { createDefaultNotificationApiClient } from '../api/notificationApi'
import { createDefaultOrganizationProfileApiClient } from '../api/organizationProfileApi'
import { createDefaultPmgmApiClient } from '../api/pmgmApi'
import { createDefaultReportingApiClient } from '../api/reportingApi'
import AccessScreen from './AccessScreen'
import { readAuthConfig } from './config'
import { createUserManager, OidcSession } from './session'

// Keep the internal views out of the public access screen's initial download.
const App = lazy(() => import('../App').catch(() => ({ default: ApplicationLoadError })))

function ApplicationLoadError() {
  return <AccessScreen tone="error" message="No se pudo cargar la plataforma. Recargue para intentar nuevamente." loginLabel="Recargar plataforma" onLogin={() => window.location.reload()} />
}

function createRuntime() {
  try {
    const config = readAuthConfig(import.meta.env, window.location.origin)
    const session = config ? new OidcSession(createUserManager(config)) : null
    const api = createDefaultPmgmApiClient(session?.getAccessToken, session?.invalidate)
    const bootstrapApi = createDefaultBootstrapApiClient(session?.getAccessToken, session?.invalidate)
    const lodgeApi = createDefaultLodgeApiClient(session?.getAccessToken, session?.invalidate)
    const lodgeCouncilApi = createDefaultLodgeCouncilApiClient(session?.getAccessToken, session?.invalidate)
    const membershipApi = createDefaultMembershipApiClient(session?.getAccessToken, session?.invalidate)
    const organizationProfileApi = createDefaultOrganizationProfileApiClient(session?.getAccessToken, session?.invalidate)
    const reportingApi = createDefaultReportingApiClient(session?.getAccessToken, session?.invalidate)
    const internalAffairsApi = createDefaultInternalAffairsApiClient(session?.getAccessToken, session?.invalidate)
    const dataQualityCaseApi = createDefaultDataQualityCaseApiClient(session?.getAccessToken, session?.invalidate)
    const documentApi = createDefaultDocumentApiClient(session?.getAccessToken, session?.invalidate)
    const grandArchiveApi = createDefaultGrandArchiveApiClient(session?.getAccessToken, session?.invalidate)
    const calendarApi = createDefaultCalendarApiClient(session?.getAccessToken, session?.invalidate)
    const notificationApi = createDefaultNotificationApiClient(session?.getAccessToken, session?.invalidate)
    const candidateIntakeApi = createDefaultCandidateIntakeApiClient(session?.getAccessToken, session?.invalidate)
    return { session, api, bootstrapApi, lodgeApi, lodgeCouncilApi, membershipApi, organizationProfileApi, reportingApi, internalAffairsApi, dataQualityCaseApi, documentApi, grandArchiveApi, calendarApi, notificationApi, candidateIntakeApi, error: '' }
  } catch {
    return { session: null, api: null, bootstrapApi: null, lodgeApi: null, lodgeCouncilApi: null, membershipApi: null, organizationProfileApi: null, reportingApi: null, internalAffairsApi: null, dataQualityCaseApi: null, documentApi: null, grandArchiveApi: null, calendarApi: null, notificationApi: null, candidateIntakeApi: null, error: 'El acceso institucional no está configurado. Contacte a la administración.' }
  }
}
const runtime = createRuntime()

function runtimeReady() {
  return runtime.api && runtime.bootstrapApi && runtime.lodgeApi && runtime.lodgeCouncilApi && runtime.membershipApi && runtime.organizationProfileApi && runtime.reportingApi && runtime.internalAffairsApi && runtime.dataQualityCaseApi && runtime.documentApi && runtime.grandArchiveApi && runtime.calendarApi && runtime.notificationApi && runtime.candidateIntakeApi
}

function RuntimeApp({ onLogout }: { onLogout?: () => void }) {
  return <LodgeCouncilApiProvider api={runtime.lodgeCouncilApi!}>
    <Suspense fallback={<AccessScreen message="Preparando acceso…" />}>
      <App api={runtime.api!} bootstrapApi={runtime.bootstrapApi!} lodgeApi={runtime.lodgeApi!} membershipApi={runtime.membershipApi!} organizationProfileApi={runtime.organizationProfileApi!} reportingApi={runtime.reportingApi!} internalAffairsApi={runtime.internalAffairsApi!} dataQualityCaseApi={runtime.dataQualityCaseApi!} documentApi={runtime.documentApi!} grandArchiveApi={runtime.grandArchiveApi!} calendarApi={runtime.calendarApi!} notificationApi={runtime.notificationApi!} candidateIntakeApi={runtime.candidateIntakeApi!} onLogout={onLogout} />
    </Suspense>
  </LodgeCouncilApiProvider>
}

export default function AuthRoot() {
  if (!runtimeReady()) return <AccessScreen tone="error" message={runtime.error} />
  if (!runtime.session) return <DemoRoot />
  return <AuthenticatedApp session={runtime.session} />
}

function AuthenticatedApp({ session }: { session: OidcSession }) {
  const state = useSyncExternalStore(session.subscribe, session.getSnapshot)
  useEffect(() => { void session.initialize() }, [session])
  if (state.status === 'authenticated' && runtimeReady()) {
    return <RuntimeApp onLogout={() => { void session.logout() }} />
  }
  return <AccessScreen tone={state.status === 'error' ? 'error' : 'info'} message={state.message ?? (state.status === 'loading' ? 'Preparando acceso…' : 'Ingrese con su cuenta institucional.')} onLogin={state.status === 'loading' ? undefined : () => { void session.login() }} />
}


/**
 * Demo pública (sin sesión): muestra la aplicación. Solo para revisión de QA, la pantalla de ingreso se ve en
 * `#acceso`, `#acceso-cargando` y `#acceso-error`; nunca existe en el sistema real (allí manda la sesión).
 */
function DemoRoot() {
  const [hash, setHash] = useState(window.location.hash)
  useEffect(() => {
    const onChange = () => setHash(window.location.hash)
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  const enter = () => { window.location.hash = '' }
  if (hash === '#acceso') return <AccessScreen message="Ingrese con su cuenta institucional." onLogin={enter} />
  if (hash === '#acceso-cargando') return <AccessScreen message="Preparando acceso…" />
  if (hash === '#acceso-error') return <AccessScreen tone="error" message="No se pudo completar el ingreso. Intente nuevamente." onLogin={enter} loginLabel="Intentar de nuevo" />
  return <RuntimeApp />
}
