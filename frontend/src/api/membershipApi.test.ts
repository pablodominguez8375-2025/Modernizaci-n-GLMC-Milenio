import { afterEach, expect, it, vi } from 'vitest'
import { MembershipApiClient } from './membershipApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary for the member directory', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ organization: { id: 'org-1', name: 'Taller', number: '1', type: 'workshop' }, total: 0, returned: 0, items: [] })))
  vi.stubGlobal('fetch', fetch)
  await new MembershipApiClient({ getAccessToken: async () => 'member-token' }).getMembers('org-1', { query: 'Ana', status: 'active', limit: 25 })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/members?organizationId=org-1&query=Ana&status=active&limit=25')
  expect(options.headers.get('Authorization')).toBe('Bearer member-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('loads one institutional profile from the dedicated endpoint', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ member: { id: 'member-1' } })))
  vi.stubGlobal('fetch', fetch)
  await new MembershipApiClient({ getAccessToken: async () => 'token' }).getProfile('member-1')
  expect(fetch.mock.calls[0][0]).toBe('/api/members/member-1/profile')
})

it('demo mode exposes a populated roster and transfer history without network calls', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new MembershipApiClient({ useMocks: true })
  const roster = await client.getMembers('11111111-1111-1111-1111-111111111111')
  expect(roster.items.length).toBeGreaterThanOrEqual(4)
  const transferred = await client.getMembers('23232323-2323-2323-2323-232323232323')
  const profile = await client.getProfile(transferred.items[0].memberId)
  expect(profile.transfers).toHaveLength(1)
  expect(profile.memberships).toHaveLength(2)
  expect(fetch).not.toHaveBeenCalled()
})
