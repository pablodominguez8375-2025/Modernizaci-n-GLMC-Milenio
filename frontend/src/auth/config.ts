export interface AuthConfig {
  authority: string
  clientId: string
  scope: string
  redirectUri: string
  logoutUri: string
}

export function readAuthConfig(env: Record<string, unknown>, origin: string): AuthConfig | null {
  if (env.VITE_USE_MOCKS === 'true') return null
  const authority = String(env.VITE_OIDC_AUTHORITY ?? '').trim()
  const clientId = String(env.VITE_OIDC_CLIENT_ID ?? '').trim()
  const scope = String(env.VITE_OIDC_SCOPE ?? 'openid profile').trim()
  const url = new URL(authority)
  const local = env.DEV === true && ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
  if ((url.protocol !== 'https:' && !(local && url.protocol === 'http:')) || url.username || url.password || url.search || url.hash) {
    throw new Error('La autoridad OIDC debe usar HTTPS.')
  }
  if (!clientId || !scope.split(/\s+/).includes('openid') || scope.split(/\s+/).includes('offline_access')) {
    throw new Error('Configure el cliente público y scopes OIDC sin offline_access.')
  }
  return { authority, clientId, scope, redirectUri: `${origin}/auth/callback`, logoutUri: `${origin}/auth/logout` }
}
