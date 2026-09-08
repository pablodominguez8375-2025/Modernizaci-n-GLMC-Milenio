import { afterEach, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

afterEach(() => vi.unstubAllGlobals())

it('sends only the access token, with no cookies, caching or redirect following', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response('{}')); vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'access-token' }).getSystemInfo()
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/system/info'); expect(options.headers.get('Authorization')).toBe('Bearer access-token'); expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('reads effective session capabilities from the API instead of decoding the JWT', async () => {
  const response = { displayName: 'Hermana Institucional', accessScope: 'order', capabilities: { canApproveTransfers: false, canRunRegimenInteriorReports: true, canManageGrandSecretariat: true, canManageTreasuryRegularity: true, canManageHospitalariaRegularity: true, canEvaluateCeremonies: true, canManagePrivacy: false } }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response))); vi.stubGlobal('fetch', fetch)
  const session = await new PmgmApiClient({ getAccessToken: async () => 'token' }).getSessionProfile()
  expect(session.capabilities.canManageTreasuryRegularity).toBe(true); expect(session.capabilities.canManageHospitalariaRegularity).toBe(true); expect(fetch.mock.calls[0][0]).toBe('/api/session/me')
})

it('uses purpose-minimized organization selector endpoint', async () => {
  const response = { total: 1, items: [{ id: '1', name: 'Taller 1', number: '1', type: 'workshop' }] }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response))); vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'token' }).getOrganizationOptions()
  expect(fetch.mock.calls[0][0]).toBe('/api/institutional/organizations/options')
})

it('builds Regimen Interior aggregate query without personal identifiers', async () => {
  const report = { scope: 'order', organizationId: null, asOf: '2026-09-08', period: { from: '2026-01-01', to: '2026-09-08' }, members: {}, events: {}, financialRegularity: {}, degreeDistribution: {}, pendingTransfers: 0 }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(report))); vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'token' }).getRegimenInteriorSummary({ asOf: '2026-09-08', from: '2026-01-01' })
  expect(fetch.mock.calls[0][0]).toBe('/api/regimen-interior/summary?asOf=2026-09-08&from=2026-01-01')
})

it('uses only workshop-level Treasury regularity endpoints', async () => {
  const current = { id: 't1', organizationId: 'o1', memberId: null, scope: 'organization', status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'TES-1', notes: null, recordedAtUtc: '2026-09-08T12:00:00Z' }
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(current))).mockResolvedValueOnce(new Response(JSON.stringify({ ...current, status: 'pending' }))); vi.stubGlobal('fetch', fetch)
  const client = new PmgmApiClient({ getAccessToken: async () => 'token' })
  await client.getTreasuryWorkshopRegularity('o1', '2026-09-08')
  await client.setTreasuryWorkshopRegularity('o1', { status: 'pending', asOfDate: '2026-09-08', sourceReference: 'TES-2', notes: null })
  expect(fetch.mock.calls[0][0]).toBe('/api/tesoreria/talleres/o1/regularidad?asOf=2026-09-08')
  expect(fetch.mock.calls[1][0]).toBe('/api/tesoreria/talleres/o1/regularidad')
  expect(String(fetch.mock.calls[0][0])).not.toContain('/miembros/')
  expect(String(fetch.mock.calls[1][0])).not.toContain('/miembros/')
})

it('uses Hospitalaria workshop regularity independently from Treasury', async () => {
  const current = { id: 'h1', organizationId: 'o1', status: 'overdue', asOfDate: '2026-09-08', sourceReference: 'HOSP-1', notes: null, recordedAtUtc: '2026-09-08T12:00:00Z' }
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(current))).mockResolvedValueOnce(new Response(JSON.stringify({ ...current, status: 'up_to_date' }))); vi.stubGlobal('fetch', fetch)
  const client = new PmgmApiClient({ getAccessToken: async () => 'token' })
  expect((await client.getHospitalariaWorkshopRegularity('o1', '2026-09-08'))?.status).toBe('overdue')
  await client.setHospitalariaWorkshopRegularity('o1', { status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-2', notes: null })
  expect(fetch.mock.calls[0][0]).toBe('/api/hospitalaria/talleres/o1/regularidad?asOf=2026-09-08')
  expect(fetch.mock.calls[1][0]).toBe('/api/hospitalaria/talleres/o1/regularidad')
})

it('treats missing workshop regularity as no record instead of an application failure', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ message: 'No existe una validación.' }), { status: 404, headers: { 'Content-Type': 'application/json' } })); vi.stubGlobal('fetch', fetch)
  const client = new PmgmApiClient({ getAccessToken: async () => 'token' })
  await expect(client.getTreasuryWorkshopRegularity('o1', '2026-09-08')).resolves.toBeNull()
  await expect(client.getHospitalariaWorkshopRegularity('o1', '2026-09-08')).resolves.toBeNull()
})

it('posts Gran Secretaria reservations with an optional ceremony link', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'r1', status: 'reserved' }))); vi.stubGlobal('fetch', fetch)
  await new PmgmApiClient({ getAccessToken: async () => 'token' }).createSecretariatReservation({ spaceId: 's1', organizationId: 'o1', ceremonyRequestId: 'c1', purpose: 'Aumento de salario', startsAtUtc: '2026-09-08T18:00:00Z', endsAtUtc: '2026-09-08T20:00:00Z' })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/gran-secretaria/reservas'); expect(options.method).toBe('POST'); expect(options.headers.get('Content-Type')).toBe('application/json'); expect(JSON.parse(options.body as string)).toMatchObject({ organizationId: 'o1', ceremonyRequestId: 'c1', spaceId: 's1' })
})

it('uses the minimized ceremony queue and formal authorization endpoints', async () => {
  const queue = { total: 1, items: [{ id: 'c1', organizationId: 'o1', organizationName: 'Taller 1', organizationNumber: '1', ceremonyType: 'wage_increase', proposedDate: '2026-09-18', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: 'r1', spaceName: 'Templo', reservationStartsAtUtc: '2026-09-18T22:00:00Z', reservationEndsAtUtc: '2026-09-19T01:00:00Z', createdAtUtc: '2026-09-08T12:00:00Z' }] }
  const document = { id: 'd1', documentType: 'ceremony_authorization', documentCode: 'AUT-CER-1', title: 'Autorización', content: 'Contenido', organizationId: 'o1', relatedCeremonyRequestId: 'c1', spaceReservationId: 'r1', status: 'issued', issuedAtUtc: '2026-09-08T12:00:00Z', issuedBySubject: 'subject' }
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(queue))).mockResolvedValueOnce(new Response(JSON.stringify(document))); vi.stubGlobal('fetch', fetch)
  const client = new PmgmApiClient({ getAccessToken: async () => 'token' })
  const response = await client.getSecretariatCeremonyQueue(); expect(response.items[0].formalAuthorizationIssued).toBe(false); expect(fetch.mock.calls[0][0]).toBe('/api/institutional/gran-secretaria/ceremonias-autorizadas')
  await client.issueSecretariatCeremonyAuthorization('c1', 'r1'); const [url, options] = fetch.mock.calls[1]; expect(url).toBe('/api/gran-secretaria/ceremonias/c1/autorizacion'); expect(options.method).toBe('POST'); expect(JSON.parse(options.body as string)).toEqual({ spaceReservationId: 'r1' })
})

it('never sends an institutional request without a token', async () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch); await expect(new PmgmApiClient().getCandidatePortal()).rejects.toThrow('Debe ingresar'); expect(fetch).not.toHaveBeenCalled()
})

it.each([401, 403])('handles HTTP %s without retrying or demo fallback', async status => {
  const fetch = vi.fn().mockResolvedValue(new Response(null, { status })); const onUnauthorized = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('fetch', fetch)
  await expect(new PmgmApiClient({ getAccessToken: async () => 'token', onUnauthorized }).getCandidatePortal()).rejects.toThrow(); expect(fetch).toHaveBeenCalledTimes(1); expect(onUnauthorized).toHaveBeenCalledTimes(status === 401 ? 1 : 0)
})

it('demo does not request token or network', async () => {
  const fetch = vi.fn(), token = vi.fn(); vi.stubGlobal('fetch', fetch); const client = new PmgmApiClient({ useMocks: true, getAccessToken: token })
  await client.getSystemInfo(); await client.getRegimenInteriorSummary(); await client.getTreasuryWorkshopRegularity('11111111-1111-1111-1111-111111111111'); await client.getHospitalariaWorkshopRegularity('11111111-1111-1111-1111-111111111111'); await client.getSecretariatAvailability('2026-09-08T18:00:00Z', '2026-09-08T20:00:00Z'); await client.getSecretariatCeremonyQueue()
  expect(fetch).not.toHaveBeenCalled(); expect(token).not.toHaveBeenCalled()
})
