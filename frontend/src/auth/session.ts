import { InMemoryWebStorage, UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'
import type { AuthConfig } from './config'

export type SessionStatus = 'loading' | 'anonymous' | 'authenticated' | 'error'
export interface SessionSnapshot { status: SessionStatus; message?: string }

export function createUserManager(config: AuthConfig) {
  return new UserManager({
    authority: config.authority,
    client_id: config.clientId,
    redirect_uri: config.redirectUri,
    post_logout_redirect_uri: config.logoutUri,
    response_type: 'code',
    response_mode: 'query',
    scope: config.scope,
    disablePKCE: false,
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    stateStore: new WebStorageStateStore({ store: window.sessionStorage, prefix: 'pmgm.oidc.' }),
    staleStateAgeInSeconds: 600,
    automaticSilentRenew: false,
    monitorSession: false,
    loadUserInfo: false,
    revokeTokensOnSignout: false,
  })
}

// No token or identity claims are exposed through React state.
export class OidcSession {
  private snapshot: SessionSnapshot = { status: 'loading' }
  private listeners = new Set<() => void>()
  private initialization?: Promise<void>
  private generation = 0

  constructor(private readonly manager: UserManager) {
    manager.events.addAccessTokenExpired(() => { void this.invalidate() })
    manager.events.addUserUnloaded(() => this.set({ status: 'anonymous' }))
  }

  subscribe = (listener: () => void) => {
    this.listeners.add(listener)
    return () => { this.listeners.delete(listener) }
  }
  getSnapshot = () => this.snapshot
  private set(snapshot: SessionSnapshot) {
    this.snapshot = snapshot
    this.listeners.forEach(listener => listener())
  }
  initialize = () => this.initialization ??= this.initializeOnce()

  private async initializeOnce() {
    const callbackUrl = window.location.href
    const pathname = window.location.pathname
    const callback = pathname === '/auth/callback' || pathname === '/auth/logout'
    // Capture once, then remove authorization codes/errors before any API calls.
    if (callback) window.history.replaceState(null, '', '/')
    try {
      await this.manager.clearStaleState()
      if (pathname === '/auth/callback') {
        const user = await this.manager.signinRedirectCallback(callbackUrl)
        if (!this.valid(user)) throw new Error('Invalid session')
        // Even an unsolicited refresh token remains unnecessary for this policy.
        user.refresh_token = undefined
        await this.manager.storeUser(user)
        this.set({ status: 'authenticated' })
      } else {
        if (pathname === '/auth/logout') await this.manager.signoutRedirectCallback(callbackUrl)
        await this.manager.removeUser()
        this.set({ status: 'anonymous' })
      }
    } catch {
      await this.manager.removeUser()
      this.set({ status: 'error', message: 'No se pudo completar el ingreso. Intente nuevamente.' })
    }
  }

  private valid(user: User | null): user is User {
    return !!user?.access_token && user.token_type.toLowerCase() === 'bearer' &&
      typeof user.expires_at === 'number' && user.expires_at > Date.now() / 1000 + 5
  }

  getAccessToken = async (): Promise<string | null> => {
    const generation = this.generation
    const user = await this.manager.getUser()
    if (generation !== this.generation || this.snapshot.status !== 'authenticated') return null
    if (!this.valid(user)) { await this.invalidate(); return null }
    return user.access_token
  }

  invalidate = async () => {
    this.generation++
    this.set({ status: 'anonymous', message: 'La sesión terminó. Ingrese nuevamente.' })
    await this.manager.removeUser()
  }

  login = async () => {
    this.set({ status: 'loading' })
    try {
      await this.manager.clearStaleState()
      await this.manager.signinRedirect({ nonce: crypto.randomUUID() })
    } catch {
      this.set({ status: 'error', message: 'No se pudo iniciar el ingreso institucional.' })
    }
  }

  logout = async () => {
    const user = await this.manager.getUser()
    await this.invalidate()
    try {
      await this.manager.signoutRedirect({ id_token_hint: user?.id_token })
    } catch {
      this.set({ status: 'anonymous', message: 'Sesión local cerrada. No se pudo cerrar la sesión del proveedor.' })
    }
  }
}
