import { expect, it } from 'vitest'
import { readAuthConfig } from './config'

it('allows explicit HTTP OIDC only on loopback for local QA', () => {
  const config = readAuthConfig({
    VITE_OIDC_AUTHORITY: 'http://127.0.0.1:8180/realms/pmgm',
    VITE_OIDC_CLIENT_ID: 'pmgm-web',
    VITE_OIDC_SCOPE: 'openid profile',
    VITE_OIDC_ALLOW_HTTP_LOCAL: 'true',
  }, 'http://127.0.0.1:8081')
  expect(config?.authority).toBe('http://127.0.0.1:8180/realms/pmgm')
  expect(config?.redirectUri).toBe('http://127.0.0.1:8081/auth/callback')
})

it('rejects external HTTP even when local QA flag is enabled', () => {
  expect(() => readAuthConfig({
    VITE_OIDC_AUTHORITY: 'http://identity.example.test/realms/pmgm',
    VITE_OIDC_CLIENT_ID: 'pmgm-web',
    VITE_OIDC_SCOPE: 'openid profile',
    VITE_OIDC_ALLOW_HTTP_LOCAL: 'true',
  }, 'http://127.0.0.1:8081')).toThrow(/HTTPS/)
})

it('keeps offline_access forbidden', () => {
  expect(() => readAuthConfig({
    VITE_OIDC_AUTHORITY: 'https://identity.example.test/realms/pmgm',
    VITE_OIDC_CLIENT_ID: 'pmgm-web',
    VITE_OIDC_SCOPE: 'openid profile offline_access',
  }, 'https://pmgm.example.test')).toThrow(/offline_access/)
})
