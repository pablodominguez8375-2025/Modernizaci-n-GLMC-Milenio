import { afterEach, expect, it, vi } from 'vitest'
import { OrganizationProfileApiClient } from './organizationProfileApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary for one organization profile', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ organization: { id: 'org-1' }, members: { active: 0, degreeDistribution: {} }, authorities: [], regularity: null, activity: { recentMeetings: [], recentInstruction: [], recentTransfers: [] } })))
  vi.stubGlobal('fetch', fetch)
  await new OrganizationProfileApiClient({ getAccessToken: async () => 'org-token' }).getProfile('org-1')
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/institutional/organizations/org-1/profile')
  expect(options.headers.get('Authorization')).toBe('Bearer org-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('demo profile includes authorities, degrees and activity without network calls', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new OrganizationProfileApiClient({ useMocks: true })
  const profile = await client.getProfile('11111111-1111-1111-1111-111111111111')
  expect(profile.members.active).toBeGreaterThan(0)
  expect(profile.authorities.length).toBeGreaterThanOrEqual(3)
  expect(profile.members.degreeDistribution.master).toBeGreaterThan(0)
  expect(profile.activity.recentMeetings.length).toBeGreaterThan(0)
  expect(fetch).not.toHaveBeenCalled()
})

it('scopes summary delegation grant and revocation to the selected workshop', async () => {
  const fetch = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'grant-1' }), { status: 201 }))
    .mockResolvedValueOnce(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetch)
  const client = new OrganizationProfileApiClient({ getAccessToken: async () => 'vm-token' })

  await client.grantSummaryAccess('workshop-23', 'master-9', 'Apoyo temporal al Consejo')
  await client.revokeSummaryAccess('workshop-23', 'grant-1')

  expect(fetch.mock.calls[0][0]).toBe('/api/institutional/organizations/workshop-23/summary-access')
  expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'POST', credentials: 'omit', cache: 'no-store' })
  expect(JSON.parse(fetch.mock.calls[0][1].body as string)).toEqual({ memberId: 'master-9', reason: 'Apoyo temporal al Consejo' })
  expect(fetch.mock.calls[1][0]).toBe('/api/institutional/organizations/workshop-23/summary-access/grant-1')
  expect(fetch.mock.calls[1][1]).toMatchObject({ method: 'DELETE', credentials: 'omit', cache: 'no-store' })
})
