import { afterEach, expect, it, vi } from 'vitest'
import { AdmissionApiClient } from './admissionApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only admission endpoints and organization-scoped queue', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, items: [] })))
  vi.stubGlobal('fetch', fetch)
  const client = new AdmissionApiClient({ getAccessToken: async () => 'admission-token' })

  await client.listCases('o1')

  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/admisiones/expedientes?organizationId=o1')
  expect(options.headers.get('Authorization')).toBe('Bearer admission-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('uses a purpose-minimized person selector without RUT or contact fields', async () => {
  const response = { total: 1, items: [{ personId: 'p1', displayName: 'Hermana Ejemplo', memberId: 'm1' }] }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)))
  vi.stubGlobal('fetch', fetch)
  const client = new AdmissionApiClient({ getAccessToken: async () => 'token' })

  const result = await client.searchPeople('o1', 'Ejemplo')

  expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/personas/opciones?organizationId=o1&query=Ejemplo')
  expect(result.items[0]).toEqual({ personId: 'p1', displayName: 'Hermana Ejemplo', memberId: 'm1' })
  expect(JSON.stringify(result)).not.toContain('rut')
  expect(JSON.stringify(result)).not.toContain('email')
  expect(JSON.stringify(result)).not.toContain('phone')
})

it('demo reentry requires a three-Master commission before third degree', async () => {
  const client = new AdmissionApiClient({ useMocks: true })
  const reentry = (await client.listCases()).items.find(item => item.admissionCase.affiliationProcedure === 'reentry')
  expect(reentry).toBeDefined()

  const initial = await client.getEligibility(reentry!.admissionCase.id)
  expect(initial.eligibility.canProceed).toBe(false)
  expect(initial.eligibility.requirements.find(x => x.code === 'information_commission')?.status).toBe('rejected')

  await client.appointCommission(reentry!.admissionCase.id, {
    memberIds: ['m1', 'm2', 'm3'],
    appointmentDate: '2026-09-17',
    sourceReference: 'ACTA-COMISION-QA',
  })
  await client.completeCommission(reentry!.admissionCase.id, {
    completed: true,
    asOfDate: '2026-09-18',
    sourceReference: 'INFORME-COMISION-QA',
  })
  await client.recordThirdDegree(reentry!.admissionCase.id, {
    asOfDate: '2026-09-18',
    sourceReference: 'ACTA-3G-QA',
    presentMasters: 6,
    votesInFavor: 4,
    votesAgainst: 2,
    abstentions: 0,
  })
  await client.recordFirstDegreeBallot(reentry!.admissionCase.id, {
    asOfDate: '2026-09-19',
    sourceReference: 'ACTA-BALOTAJE-QA',
    ballots: [{ procedureNumber: 1, eligibleVoters: 6, whiteBallots: 5, blackBallots: 1 }],
    ballotApproved: true,
  })

  const final = await client.getEligibility(reentry!.admissionCase.id)
  expect(final.eligibility.canProceed).toBe(true)
})

it('demo transfer creation requires a distinct source workshop', async () => {
  const client = new AdmissionApiClient({ useMocks: true })
  const person = (await client.searchPeople('23232323-2323-2323-2323-232323232323', 'Traslado')).items[0]
  expect(person?.memberId).toBeTruthy()

  await expect(client.createCase({
    organizationId: '23232323-2323-2323-2323-232323232323',
    admissionType: 'affiliation',
    affiliationMode: 'simple',
    affiliationProcedure: 'transfer',
    memberId: person.memberId,
    personId: person.personId,
  })).rejects.toThrow('Taller de origen')

  await expect(client.createCase({
    organizationId: '23232323-2323-2323-2323-232323232323',
    admissionType: 'affiliation',
    affiliationMode: 'simple',
    affiliationProcedure: 'transfer',
    memberId: person.memberId,
    personId: person.personId,
    originOrganizationId: '23232323-2323-2323-2323-232323232323',
  })).rejects.toThrow('distinto')
})

it('demo permits commission waiver only for affiliation with transfer', async () => {
  const client = new AdmissionApiClient({ useMocks: true })
  const rows = (await client.listCases()).items
  const transfer = rows.find(item => item.admissionCase.affiliationProcedure === 'transfer')
  const reentry = rows.find(item => item.admissionCase.affiliationProcedure === 'reentry')
  expect(transfer).toBeDefined()
  expect(reentry).toBeDefined()

  await expect(client.waiveTransferCommission(reentry!.admissionCase.id, {
    asOfDate: '2026-09-18',
    sourceReference: 'ACTA-CAMARA-QA',
  })).rejects.toThrow('sólo aplica')

  await expect(client.waiveTransferCommission(transfer!.admissionCase.id, {
    asOfDate: '2026-09-18',
    sourceReference: 'ACTA-CAMARA-QA',
  })).resolves.toBeUndefined()
})

it('demo stores only aggregate first-degree ballot counts', async () => {
  const client = new AdmissionApiClient({ useMocks: true })
  const standard = (await client.listCases()).items.find(item => item.admissionCase.affiliationProcedure === 'standard')
  expect(standard).toBeDefined()

  await client.recordFirstDegreeBallot(standard!.admissionCase.id, {
    asOfDate: '2026-09-19',
    sourceReference: 'ACTA-BALOTAJE-QA',
    ballots: [{ procedureNumber: 1, eligibleVoters: 6, whiteBallots: 6, blackBallots: 0 }],
    ballotApproved: true,
  })

  const detail = await client.getCase(standard!.admissionCase.id)
  expect(detail.decisions.some(x => x.decisionType === 'lodge_first_degree_ballot')).toBe(true)
  expect(JSON.stringify(detail)).not.toContain('voterId')
  expect(JSON.stringify(detail)).not.toContain('memberVote')
})

it('demo completion is idempotent after an authorized transfer ceremony', async () => {
  const client = new AdmissionApiClient({ useMocks: true })
  const transfer = (await client.listCases()).items.find(item => item.admissionCase.affiliationProcedure === 'transfer')
  expect(transfer).toBeDefined()

  const detail = await client.getCase(transfer!.admissionCase.id)
  const requestId = detail.decisions.find(x => x.decisionType === 'ceremony_request_created')?.sourceReference
  expect(requestId).toBeTruthy()

  const payload = { meetingId: 'bbbbbbbb-2309-0018-0000-000000000006', ceremonyDate: '2026-09-18' }
  await client.completeCeremony(requestId!, payload)
  await client.completeCeremony(requestId!, payload)

  const after = await client.getCase(transfer!.admissionCase.id)
  expect(after.admissionCase.status).toBe('resolved')
  expect(after.decisions.filter(x => x.decisionType === 'ceremony_completed')).toHaveLength(1)
})

it('demo admission client never requests a token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new AdmissionApiClient({ useMocks: true, getAccessToken: token })

  await client.listCases()
  await client.searchPeople('23232323-2323-2323-2323-232323232323', 'Demo')

  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})
