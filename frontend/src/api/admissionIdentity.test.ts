import { describe, expect, it } from 'vitest'
import { PmgmApiClient, type CreateAdmissionCaseRequest } from './pmgmApi'

const external: CreateAdmissionCaseRequest = { organizationId: '23232323-2323-2323-2323-232323232323',
  personId: 'cccccccc-3333-3333-3333-333333333333', admissionType: 'incorporation',
  originObedience: 'Obediencia sintética', degree: 'master' }
describe('identidad del alta directa de admisiones en demo', () => {
  it('preserva PersonId externo sin crear Member antes de resolución', async () => {
    expect(await new PmgmApiClient({ useMocks: true }).createAdmissionCase(external)).toMatchObject({ organizationId: external.organizationId, admissionType: external.admissionType,
      personId: external.personId, memberId: null, affiliationMode: null, withdrawalLetterGrantedDate: null, status: 'under_review' })
  })
  it.each([
    { personId: 'cccccccc-1111-1111-1111-111111111111' },
    { memberId: 'dddddddd-1111-1111-1111-111111111111' },
    { personId: 'persona-sin-expediente' },
    { organizationId: '11111111-1111-1111-1111-111111111111' },
    { organizationId: 'orden-sintetica' },
    { withdrawalLetterGrantedDate: '2026-10-03' },
    { affiliationMode: 'simple' as const },
    { originObedience: ' ' },
    { degree: '' },
  ])('rechaza la alteración de identidad/contexto %j', async patch => {
    await expect(new PmgmApiClient({ useMocks: true }).createAdmissionCase({ ...external, ...patch })).rejects.toThrow()
  })
  it('rechaza PersonId/MemberId incoherentes en afiliación', async () => {
    await expect(new PmgmApiClient({ useMocks: true }).createAdmissionCase({ ...external, admissionType: 'affiliation',
      memberId: 'dddddddd-1111-1111-1111-111111111111', affiliationMode: 'simple', withdrawalLetterGrantedDate: '2026-10-03' })).rejects.toThrow(/mismo registro maestro/)
  })
})
