import { afterEach, describe, expect, it, vi } from 'vitest'
import { InMemoryWebStorage, OidcClient, User, type UserManager } from 'oidc-client-ts'
import { createUserManager, OidcSession } from './session'
import { readAuthConfig } from './config'

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })
const config = { authority: 'https://identity.example', clientId: 'public-spa', scope: 'openid profile', redirectUri: 'https://app.example/auth/callback', logoutUri: 'https://app.example/auth/logout' }
function browser(path = '/auth/callback?code=private-code&state=state') {
  vi.stubGlobal('window', { location: new URL(path, 'https://app.example'), history: { replaceState: vi.fn() }, sessionStorage: new InMemoryWebStorage() })
}
function fixture() {
  browser()
  const user = new User({ access_token: 'access-only', id_token: 'id-only', refresh_token: 'discard-me', token_type: 'Bearer', expires_at: Date.now() / 1000 + 300, profile: { sub: 'subject', iss: config.authority, aud: config.clientId, exp: Date.now() / 1000 + 300, iat: Date.now() / 1000 } })
  const manager = {
    events: { addAccessTokenExpired: vi.fn(), addUserUnloaded: vi.fn() },
    clearStaleState: vi.fn().mockResolvedValue(undefined), signinRedirectCallback: vi.fn().mockResolvedValue(user),
    storeUser: vi.fn().mockResolvedValue(undefined), removeUser: vi.fn().mockResolvedValue(undefined),
    getUser: vi.fn().mockResolvedValue(user), signinRedirect: vi.fn().mockResolvedValue(undefined),
    signoutRedirect: vi.fn().mockResolvedValue(undefined), signoutRedirectCallback: vi.fn().mockResolvedValue(undefined),
  }
  return { user, manager, session: new OidcSession(manager as unknown as UserManager) }
}
describe('session boundary', () => {
  it('processes callback once under StrictMode, cleans URL and discards refresh tokens', async () => {
    const { session, manager, user } = fixture()
    await Promise.all([session.initialize(), session.initialize()])
    expect(manager.signinRedirectCallback).toHaveBeenCalledTimes(1)
    expect(window.history.replaceState).toHaveBeenCalledWith(null, '', '/')
    expect(user.refresh_token).toBeUndefined()
    expect(await session.getAccessToken()).toBe('access-only')
  })
  it('rejects failed callbacks without displaying provider error details', async () => {
    const { session, manager } = fixture()
    manager.signinRedirectCallback.mockRejectedValue(new Error('private-code'))
    await session.initialize()
    expect(session.getSnapshot().status).toBe('error')
    expect(session.getSnapshot().message).not.toContain('private-code')
    expect(await session.getAccessToken()).toBeNull()
  })
  it('expires tokens before a request and clears session', async () => {
    const { session, user, manager } = fixture()
    await session.initialize()
    user.expires_at = Date.now() / 1000 - 1
    expect(await session.getAccessToken()).toBeNull()
    expect(manager.removeUser).toHaveBeenCalled()
    expect(session.getSnapshot().status).toBe('anonymous')
  })
  it('clears local session even when provider logout fails', async () => {
    const { session, manager } = fixture()
    await session.initialize()
    manager.signoutRedirect.mockRejectedValue(new Error('unavailable'))
    await session.logout()
    expect(await session.getAccessToken()).toBeNull()
    expect(session.getSnapshot().message).toContain('Sesión local cerrada')
  })
  it('does not restore tokens on reload', async () => {
    const { session, manager } = fixture()
    browser('/')
    await session.initialize()
    expect(manager.signinRedirectCallback).not.toHaveBeenCalled()
    expect(await session.getAccessToken()).toBeNull()
  })
  it('validates logout callback state through the protocol library', async () => {
    const { session, manager } = fixture()
    browser('/auth/logout?state=logout-state')
    await session.initialize()
    expect(manager.signoutRedirectCallback).toHaveBeenCalledWith('https://app.example/auth/logout?state=logout-state')
  })
  it('isolates token storage from sessionStorage', async () => {
    browser('/')
    const manager = createUserManager(config)
    await manager.settings.userStore.set('test', 'token')
    expect(window.sessionStorage.length).toBe(0)
    expect(manager.settings.response_type).toBe('code')
    expect(manager.settings.disablePKCE).toBe(false)
    expect(manager.settings.client_secret).toBeUndefined()
  })
  it('rejects unsolicited callback state using the real library', async () => {
    browser()
    const session = new OidcSession(createUserManager(config))
    await session.initialize()
    expect(session.getSnapshot().status).toBe('error')
    expect(await session.getAccessToken()).toBeNull()
  })
  it('generates a real S256 authorization request without exposing the verifier', async () => {
    browser('/')
    const manager = createUserManager(config)
    const client = new OidcClient({ ...manager.settings, metadata: {
      issuer: config.authority, authorization_endpoint: `${config.authority}/authorize`,
      token_endpoint: `${config.authority}/token`,
    } })
    const request = await client.createSigninRequest({ nonce: 'unique-nonce' })
    const params = new URL(request.url).searchParams
    expect(params.get('response_type')).toBe('code')
    expect(params.get('code_challenge_method')).toBe('S256')
    expect(params.get('nonce')).toBe('unique-nonce')
    expect(params.get('state')).toBeTruthy()
    expect(params.has('code_verifier')).toBe(false)
    expect(params.has('client_secret')).toBe(false)
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(request.state.code_verifier))
    expect(params.get('code_challenge')).toBe(btoa(String.fromCharCode(...new Uint8Array(digest))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, ''))
  })
})
describe('configuration', () => {
  it('requires explicit demo mode', () => {
    expect(readAuthConfig({ VITE_USE_MOCKS: 'true' }, 'https://app.example')).toBeNull()
    expect(() => readAuthConfig({}, 'https://app.example')).toThrow()
  })
  it('rejects insecure authorities and refresh scopes', () => {
    const env = { VITE_OIDC_AUTHORITY: 'http://identity.example', VITE_OIDC_CLIENT_ID: 'spa' }
    expect(() => readAuthConfig(env, 'https://app.example')).toThrow()
    expect(() => readAuthConfig({ ...env, VITE_OIDC_AUTHORITY: config.authority, VITE_OIDC_SCOPE: 'openid offline_access' }, 'https://app.example')).toThrow()
  })
})
