import { describe, expect, it, vi, afterEach } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { validateExternalIncorporation, type ExternalIncorporationRequest } from './externalIncorporation'
afterEach(() => vi.unstubAllGlobals())
const input: ExternalIncorporationRequest = { organizationId: '23232323-2323-2323-2323-232323232323', firstNames: 'Hermana', lastNames: 'Externa sintética', rutOrInstitutionalId: 'EXT-TEST-99', originObedience: 'Obediencia sintética', degree: 'master' }
describe('alta externa de incorporación', () => {
  it('crea identidad sin Member, permite búsqueda local y rechaza repetir identificación normalizada', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const created = await api.createExternalIncorporation(input)
    expect(created).toMatchObject({ admissionType: 'incorporation', memberId: null, status: 'under_review' })
    const own = await api.searchAdmissionPeople({ organizationId: input.organizationId, admissionType: 'incorporation', query: input.lastNames })
    expect(own.items).toContainEqual({ personId: created.personId, memberId: null, displayName: 'Hermana Externa sintética', institutionalNumber: null })
    expect((await api.searchAdmissionPeople({ organizationId: '11111111-1111-1111-1111-111111111111', admissionType: 'incorporation', query: input.lastNames })).items).toEqual([])
    await expect(api.createExternalIncorporation({ ...input, rutOrInstitutionalId: ' ext.test.99 ' })).rejects.toThrow(/área autorizada/)
  })
  it.each([{ firstNames: '' }, { lastNames: ' ' }, { rutOrInstitutionalId: 'A'.repeat(17) }, { rutOrInstitutionalId: '!!!' }, { originObedience: '' }])('rechaza datos incompletos %j', patch => {
    expect(() => validateExternalIncorporation({ ...input, ...patch })).toThrow()
  })
  it('envía sólo el contrato de incorporación al endpoint propio', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'case', personId: 'person', memberId: null, admissionType: 'incorporation' })))
    vi.stubGlobal('fetch', fetch)
    await new PmgmApiClient({ getAccessToken: async () => 'test' }).createExternalIncorporation(input)
    expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/incorporaciones/persona-nueva')
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual(input)
    expect(fetch.mock.calls[0][1].cache).toBe('no-store')
  })
})
