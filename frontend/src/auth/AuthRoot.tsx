import { useEffect, useSyncExternalStore } from 'react'
import App from '../App'
import { createDefaultBootstrapApiClient } from '../api/bootstrapApi'
import { createDefaultCalendarApiClient } from '../api/calendarApi'
import { createDefaultCandidateIntakeApiClient } from '../api/candidateIntakeApi'
import { createDefaultDataQualityCaseApiClient } from '../api/dataQualityCaseApi'
import { createDefaultDocumentApiClient } from '../api/documentApi'
import { createDefaultGrandArchiveApiClient } from '../api/grandArchiveApi'
import { createDefaultInternalAffairsApiClient } from '../api/internalAffairsApi'
import { createDefaultLodgeApiClient } from '../api/lodgeApi'
import { createDefaultMembershipApiClient } from '../api/membershipApi'
import { createDefaultNotificationApiClient } from '../api/notificationApi'
import { createDefaultOrganizationProfileApiClient } from '../api/organizationProfileApi'
import { createDefaultPmgmApiClient } from '../api/pmgmApi'
import { createDefaultReportingApiClient } from '../api/reportingApi'
import { readAuthConfig } from './config'
import { createUserManager, OidcSession } from './session'

function createRuntime() {
  try {
    const config = readAuthConfig(import.meta.env, window.location.origin)
    const session = config ? new OidcSession(createUserManager(config)) : null
    const api = createDefaultPmgmApiClient(session?.getAccessToken, session?.invalidate)
    const bootstrapApi = createDefaultBootstrapApiClient(session?.getAccessToken, session?.invalidate)
    const lodgeApi = createDefaultLodgeApiClient(session?.getAccessToken, session?.invalidate)
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
    return { session, api, bootstrapApi, lodgeApi, membershipApi, organizationProfileApi, reportingApi, internalAffairsApi, dataQualityCaseApi, documentApi, grandArchiveApi, calendarApi, notificationApi, candidateIntakeApi, error: '' }
  } catch {
    return { session: null, api: null, bootstrapApi: null, lodgeApi: null, membershipApi: null, organizationProfileApi: null, reportingApi: null, internalAffairsApi: null, dataQualityCaseApi: null, documentApi: null, grandArchiveApi: null, calendarApi: null, notificationApi: null, candidateIntakeApi: null, error: 'El acceso institucional no está configurado. Contacte a la administración.' }
  }
}
const runtime = createRuntime()

function runtimeReady() {
  return runtime.api && runtime.bootstrapApi && runtime.lodgeApi && runtime.membershipApi && runtime.organizationProfileApi && runtime.reportingApi && runtime.internalAffairsApi && runtime.dataQualityCaseApi && runtime.documentApi && runtime.grandArchiveApi && runtime.calendarApi && runtime.notificationApi && runtime.candidateIntakeApi
}

export default function AuthRoot() {
  if (!runtimeReady()) return <AccessScreen message={runtime.error} />
  if (!runtime.session) return <App api={runtime.api!} bootstrapApi={runtime.bootstrapApi!} lodgeApi={runtime.lodgeApi!} membershipApi={runtime.membershipApi!} organizationProfileApi={runtime.organizationProfileApi!} reportingApi={runtime.reportingApi!} internalAffairsApi={runtime.internalAffairsApi!} dataQualityCaseApi={runtime.dataQualityCaseApi!} documentApi={runtime.documentApi!} grandArchiveApi={runtime.grandArchiveApi!} calendarApi={runtime.calendarApi!} notificationApi={runtime.notificationApi!} candidateIntakeApi={runtime.candidateIntakeApi!} />
  return <AuthenticatedApp session={runtime.session} />
}

function AuthenticatedApp({ session }: { session: OidcSession }) {
  const state = useSyncExternalStore(session.subscribe, session.getSnapshot)
  useEffect(() => { void session.initialize() }, [session])
  if (state.status === 'authenticated' && runtimeReady()) {
    return <App api={runtime.api!} bootstrapApi={runtime.bootstrapApi!} lodgeApi={runtime.lodgeApi!} membershipApi={runtime.membershipApi!} organizationProfileApi={runtime.organizationProfileApi!} reportingApi={runtime.reportingApi!} internalAffairsApi={runtime.internalAffairsApi!} dataQualityCaseApi={runtime.dataQualityCaseApi!} documentApi={runtime.documentApi!} grandArchiveApi={runtime.grandArchiveApi!} calendarApi={runtime.calendarApi!} notificationApi={runtime.notificationApi!} candidateIntakeApi={runtime.candidateIntakeApi!} onLogout={() => { void session.logout() }} />
  }
  return <AccessScreen message={state.message ?? (state.status === 'loading' ? 'Preparando acceso…' : 'Ingrese con su cuenta institucional.')} onLogin={state.status === 'loading' ? undefined : () => { void session.login() }} />
}

function AccessScreen({ message, onLogin }: { message: string; onLogin?: () => void }) {
  return <main className="content"><section className="panel"><h1>Proyecto Centenario</h1><p role="status">{message}</p>{onLogin && <button type="button" onClick={onLogin}>Ingresar</button>}</section></main>
}
