import { afterEach, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

afterEach(() => vi.unstubAllGlobals())
it('sends only the access token, with no cookies, caching or redirect following', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response('{}'))
  vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'access-token' }).getSystemInfo()
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/system/info')
  expect(options.headers.get('Authorization')).toBe('Bearer access-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})
it('reads effective session capabilities from the API instead of decoding the JWT', async () => {
  const response = {
    displayName: 'Hermana Institucional',
    accessScope: 'order',
    capabilities: {
      canApproveTransfers: false,
      canRunRegimenInteriorReports: false,
      canManageGrandSecretariat: true,
      canManageTreasuryRegularity: false,
      canManageHospitalariaRegularity: false,
      canEvaluateCeremonies: true,
      canManagePrivacy: false,
    },
  }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response), { headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetch)

  const profile = await new PmgmApiClient({ getAccessToken: async () => 'token' }).getSessionProfile()

  expect(fetch.mock.calls[0][0]).toBe('/api/session/me')
  expect(profile).toEqual(response)
})
it('never sends an institutional request without a token', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  await expect(new PmgmApiClient().getCandidatePortal()).rejects.toThrow('Debe ingresar')
  expect(fetch).not.toHaveBeenCalled()
})
it.each([401, 403])('handles HTTP %s without retrying or falling back to demo', async status => {
  const fetch = vi.fn().mockResolvedValue(new Response(null, { status }))
  const onUnauthorized = vi.fn().mockResolvedValue(undefined)
  vi.stubGlobal('fetch', fetch)
  await expect(new PmgmApiClient({ getAccessToken: async () => 'token', onUnauthorized }).getCandidatePortal()).rejects.toThrow()
  expect(fetch).toHaveBeenCalledTimes(1)
  expect(onUnauthorized).toHaveBeenCalledTimes(status === 401 ? 1 : 0)
})
it('demo does not request a token or call the network', async () => {
  const fetch = vi.fn(), getAccessToken = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new PmgmApiClient({ useMocks: true, getAccessToken })
  await client.getSystemInfo()
  const profile = await client.getSessionProfile()
  expect(profile.accessScope).toBe('order')
  expect(fetch).not.toHaveBeenCalled()
  expect(getAccessToken).not.toHaveBeenCalled()
})
