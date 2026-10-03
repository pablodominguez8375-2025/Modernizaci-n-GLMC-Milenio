import { afterEach, describe, expect, it, vi } from 'vitest'
import { lookupDemoAdmissionPeople } from './admissionLookup'
import { PmgmApiClient } from './pmgmApi'
const organizationId = '23232323-2323-2323-2323-232323232323'
afterEach(() => vi.unstubAllGlobals())
describe('búsqueda mínima de identidad para admisiones', () => {
  it('incluye retirados de la historia del Taller y conserva Person/Member', () => {
    const result = lookupDemoAdmissionPeople({ organizationId, admissionType: 'affiliation', query: 'Retirado' })
    expect(result.returned).toBe(1)
    expect(result.items[0]).toMatchObject({ personId: 'cccccccc-1111-1111-1111-111111111111', memberId: 'dddddddd-1111-1111-1111-111111111111' })
    expect(Object.keys(result.items[0]).sort()).toEqual(['displayName', 'institutionalNumber', 'memberId', 'personId'])
  })
  it('otro Taller requiere número exacto, no nombre o número parcial', () => {
    expect(lookupDemoAdmissionPeople({ organizationId, admissionType: 'affiliation', query: 'Otro Taller' }).returned).toBe(0)
    expect(lookupDemoAdmissionPeople({ organizationId, admissionType: 'affiliation', query: 'DEMO-CRV' }).returned).toBe(0)
    expect(lookupDemoAdmissionPeople({ organizationId, admissionType: 'affiliation', query: 'demo-crv-01' }).returned).toBe(1)
  })
  it('incorporación reutiliza Persona sin crear Member', () => {
    const result = lookupDemoAdmissionPeople({ organizationId, admissionType: 'incorporation', query: 'Externa' })
    expect(result.returned).toBe(1); expect(result.items[0].memberId).toBeNull()
    expect(lookupDemoAdmissionPeople({ organizationId: '11111111-1111-1111-1111-111111111111', admissionType: 'incorporation', query: 'Externa' }).returned).toBe(0)
  })
  it.each(['', 'ab', 'x'.repeat(81)])('impide búsquedas sin límite %s', query => {
    expect(() => lookupDemoAdmissionPeople({ organizationId, admissionType: 'affiliation', query })).toThrow()
  })
  it('envía sólo contexto de expediente y término al endpoint propio', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ returned: 0, items: [] }))); vi.stubGlobal('fetch', fetch)
    await new PmgmApiClient({ getAccessToken: async () => 'test' }).searchAdmissionPeople({ organizationId, admissionType: 'affiliation', query: ' DEMO-CRV-01 ' })
    expect(fetch.mock.calls[0][0]).toBe(`/api/admisiones/personas-busqueda?organizationId=${organizationId}&admissionType=affiliation&query=DEMO-CRV-01`)
    expect(fetch.mock.calls[0][1]).toMatchObject({ cache: 'no-store', credentials: 'omit' })
  })
})
