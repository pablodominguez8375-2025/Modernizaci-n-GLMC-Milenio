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
  await new PmgmApiClient({ useMocks: true, getAccessToken }).getSystemInfo()
  expect(fetch).not.toHaveBeenCalled()
  expect(getAccessToken).not.toHaveBeenCalled()
})
