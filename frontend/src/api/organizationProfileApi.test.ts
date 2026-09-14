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
