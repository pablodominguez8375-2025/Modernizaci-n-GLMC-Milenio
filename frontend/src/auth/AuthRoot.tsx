import { useEffect, useSyncExternalStore } from 'react'
import App from '../App'
import { createDefaultLodgeApiClient } from '../api/lodgeApi'
import { createDefaultPmgmApiClient } from '../api/pmgmApi'
import { readAuthConfig } from './config'
import { createUserManager, OidcSession } from './session'

function createRuntime() {
  try {
    const config = readAuthConfig(import.meta.env, window.location.origin)
    const session = config ? new OidcSession(createUserManager(config)) : null
    const api = createDefaultPmgmApiClient(session?.getAccessToken, session?.invalidate)
    const lodgeApi = createDefaultLodgeApiClient(session?.getAccessToken, session?.invalidate)
    return { session, api, lodgeApi, error: '' }
  } catch {
    return { session: null, api: null, lodgeApi: null, error: 'El acceso institucional no está configurado. Contacte a la administración.' }
  }
}
const runtime = createRuntime()

export default function AuthRoot() {
  if (!runtime.api || !runtime.lodgeApi) return <AccessScreen message={runtime.error} />
  if (!runtime.session) return <App api={runtime.api} lodgeApi={runtime.lodgeApi} />
  return <AuthenticatedApp session={runtime.session} />
}

function AuthenticatedApp({ session }: { session: OidcSession }) {
  const state = useSyncExternalStore(session.subscribe, session.getSnapshot)
  useEffect(() => { void session.initialize() }, [session])
  if (state.status === 'authenticated' && runtime.api && runtime.lodgeApi) {
    return <App api={runtime.api} lodgeApi={runtime.lodgeApi} onLogout={() => { void session.logout() }} />
  }
  return <AccessScreen message={state.message ?? (state.status === 'loading' ? 'Preparando acceso…' : 'Ingrese con su cuenta institucional.')} onLogin={state.status === 'loading' ? undefined : () => { void session.login() }} />
}

function AccessScreen({ message, onLogin }: { message: string; onLogin?: () => void }) {
  return <main className="content"><section className="panel"><h1>Proyecto Milenio</h1><p role="status">{message}</p>{onLogin && <button type="button" onClick={onLogin}>Ingresar</button>}</section></main>
}
