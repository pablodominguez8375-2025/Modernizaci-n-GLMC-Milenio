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
  const transferredMaster = transferred.items.find(item => item.currentDegree === 'master')
  expect(transferredMaster).toBeDefined()
  const profile = await client.getProfile(transferredMaster!.memberId)
  expect(profile.transfers).toHaveLength(1)
  expect(profile.memberships).toHaveLength(2)
  expect(profile.offices).toEqual(expect.arrayContaining([
    expect.objectContaining({ officeType: 'secretary', organization: 'Taller Demostrativo Nº 1', evidenceReference: 'ACTA-DEMO-2023-04' }),
    expect.objectContaining({ officeType: 'master_of_ceremonies', organization: 'Taller Demostrativo Nº 23', evidenceReference: 'ACTA-DEMO-2026-01' })
  ]))
  expect(fetch).not.toHaveBeenCalled()
})

it('demo mode models twenty workshops with the agreed operational composition', async () => {
  const client = new MembershipApiClient({ useMocks: true })
  const workshopIds = [1, ...Array.from({ length: 18 }, (_, index) => index + 2), 23]
  for (const number of workshopIds) {
    const id = number === 1 ? '11111111-1111-1111-1111-111111111111' : number === 23 ? '23232323-2323-2323-2323-232323232323' : `00000000-0000-0000-0000-${String(number).padStart(12, '0')}`
    const roster = await client.getMembers(id, { limit: 100 })
    expect(roster.total).toBe(24)
    expect(roster.items.filter(item => item.currentDegree === 'master').length).toBe(14)
    expect(roster.items.filter(item => item.currentDegree === 'fellowcraft').length).toBe(5)
    expect(roster.items.filter(item => item.currentDegree === 'apprentice').length).toBe(5)
    expect(roster.items.filter(item => item.institutionalStatus === 'past_active').length).toBe(2)
    expect(roster.items.filter(item => item.membershipType === 'regular').length).toBe(24)
  }
})

it('revealMemberRut envía el motivo por POST y la demo exige motivo (#609)', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ rut: '12.345.678-5', expiresInSeconds: 15 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  try {
    const api = new MembershipApiClient({ getAccessToken: async () => 'token' })
    await api.revealMemberRut('member-1', 'identity_verification')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/members/member-1/rut/reveal')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ reason: 'identity_verification' })
    await expect(new MembershipApiClient({ useMocks: true }).revealMemberRut('m', '')).rejects.toThrow(/motivo/)
  } finally { vi.unstubAllGlobals() }
})

it('foto de perfil: sube bytes con su tipo, cambia visibilidad y la demo respeta la elección (#609)', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ hasPhoto: true, visibleToMembers: false }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  try {
    const api = new MembershipApiClient({ getAccessToken: async () => 'token' })
    const photo = new Blob([new Uint8Array([1, 2, 3])], { type: 'image/jpeg' })
    await api.uploadOwnPhoto(photo)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/member-self/photo')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(new Headers(fetchMock.mock.calls[0][1].headers).get('Content-Type')).toBe('image/jpeg')
    await api.setOwnPhotoVisibility(true)
    expect(JSON.parse(fetchMock.mock.calls[1][1].body as string)).toEqual({ visible: true })
  } finally { vi.unstubAllGlobals() }
  const demo = new MembershipApiClient({ useMocks: true })
  await expect(demo.setOwnPhotoVisibility(true)).rejects.toThrow(/Primero suba/)
  expect(await demo.getOwnPhotoStatus()).toEqual({ hasPhoto: false, visibleToMembers: false })
})
