import { afterEach, describe, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })
const identity = { organizationId: 'org-synthetic', memberId: 'member-synthetic', personId: 'person-synthetic', admissionType: 'affiliation' as const }
describe('contrato CRV del adaptador de demo y API', () => {
  it.each([
    ['2026-07-03', 'simple'], ['2026-07-02', 'activation'],
  ] as const)('conserva fecha y modalidad válidas %s', async (withdrawalLetterGrantedDate, affiliationMode) => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-10-03T15:00:00Z'))
    const result = await new PmgmApiClient({ useMocks: true }).createAdmissionCase({ ...identity, withdrawalLetterGrantedDate, affiliationMode })
    expect(result).toMatchObject({ ...identity, withdrawalLetterGrantedDate, affiliationMode, status: 'under_review' })
  })
  it.each([
    ['2026-07-02', 'simple'], ['2026-07-03', 'activation'],
    ['2026-10-04', 'simple'], ['2026-02-30', 'simple'], ['', 'simple'],
  ] as const)('rechaza fecha o modalidad inválida %s / %s', async (withdrawalLetterGrantedDate, affiliationMode) => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-10-03T15:00:00Z'))
    await expect(new PmgmApiClient({ useMocks: true }).createAdmissionCase({ ...identity, withdrawalLetterGrantedDate, affiliationMode })).rejects.toThrow()
  })
  it('transporta la fecha al backend y conserva su respuesta', async () => {
    const payload = { ...identity, affiliationMode: 'simple' as const, withdrawalLetterGrantedDate: '2026-07-03' }
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...payload, id: 'case-synthetic', status: 'under_review', createdAtUtc: '2026-10-03T15:00:00Z' })))
    vi.stubGlobal('fetch', fetch)
    const result = await new PmgmApiClient({ getAccessToken: async () => 'test-token' }).createAdmissionCase(payload)
    expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/expedientes')
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual(payload)
    expect(result.withdrawalLetterGrantedDate).toBe(payload.withdrawalLetterGrantedDate)
  })
})
