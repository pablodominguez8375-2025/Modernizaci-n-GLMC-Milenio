import { afterEach, expect, it, vi } from 'vitest'
import { InternalAffairsApiClient } from './internalAffairsApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary for member control filters', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ asOf: '2026-09-09', total: 0, returned: 0, items: [] }), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  const client = new InternalAffairsApiClient({ getAccessToken: async () => 'regimen-token' })
  await client.getMembers({ asOf: '2026-09-09', status: 'inactive', financialStatus: 'delinquent', pastActiveOnly: true, limit: 200 })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/regimen-interior/members?asOf=2026-09-09&status=inactive&financialStatus=delinquent&pastActiveOnly=true&limit=200')
  expect(options.headers.get('Authorization')).toBe('Bearer regimen-token')
  expect(options).toMatchObject({ method: 'GET', credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('demo mode can filter historical and financial member data without network calls', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new InternalAffairsApiClient({ useMocks: true })
  const response = await client.getMembers({ financialStatus: 'delinquent', pastActiveOnly: true })
  expect(response.items.length).toBeGreaterThan(0)
  expect(response.items.every(item => item.financialStatus === 'delinquent')).toBe(true)
  expect(response.items.every(item => item.pastActive)).toBe(true)
  expect(response.items.some(item => item.membershipHistoryCount > 1)).toBe(true)
  expect(fetch).not.toHaveBeenCalled()
})
