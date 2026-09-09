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

it('uses bearer-only boundary for data quality filters', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ asOf: '2026-09-09', total: 0, returned: 0, summary: { errors: 0, warnings: 0, affectedMembers: 0, byCode: {} }, items: [] }), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  const client = new InternalAffairsApiClient({ getAccessToken: async () => 'quality-token' })
  await client.getDataQuality({ asOf: '2026-09-09', organizationId: '11111111-1111-1111-1111-111111111111', severity: 'error', code: 'active_membership_after_death', search: 'demo', limit: 80 })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/regimen-interior/data-quality?asOf=2026-09-09&organizationId=11111111-1111-1111-1111-111111111111&severity=error&code=active_membership_after_death&search=demo&limit=80')
  expect(options.headers.get('Authorization')).toBe('Bearer quality-token')
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

it('demo mode exposes explainable data quality findings without network calls', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new InternalAffairsApiClient({ useMocks: true })
  const response = await client.getDataQuality({ severity: 'error' })
  expect(response.items.length).toBeGreaterThan(0)
  expect(response.items.every(item => item.severity === 'error')).toBe(true)
  expect(response.summary.errors).toBeGreaterThan(0)
  expect(response.summary.affectedMembers).toBeGreaterThan(0)
  expect(response.items.every(item => item.suggestedAction.length > 0)).toBe(true)
  expect(fetch).not.toHaveBeenCalled()
})
