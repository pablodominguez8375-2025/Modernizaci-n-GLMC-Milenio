export interface AdmissionPersonOption { personId: string; memberId: string | null; displayName: string; institutionalNumber: string | null }
export interface AdmissionPersonSearch { organizationId: string; admissionType: 'affiliation' | 'incorporation'; query: string }
export interface AdmissionPersonSearchResponse { returned: number; items: AdmissionPersonOption[] }
const workshop23 = '23232323-2323-2323-2323-232323232323'
const workshop1 = '11111111-1111-1111-1111-111111111111'
const records = [
  { personId: 'cccccccc-1111-1111-1111-111111111111', memberId: 'dddddddd-1111-1111-1111-111111111111', displayName: 'Hermano Retirado Demostrativo', institutionalNumber: 'DEMO-CRV-23', organizationId: workshop23, admissionType: 'affiliation' },
  { personId: 'cccccccc-2222-2222-2222-222222222222', memberId: 'dddddddd-2222-2222-2222-222222222222', displayName: 'Hermana Otro Taller Demostrativa', institutionalNumber: 'DEMO-CRV-01', organizationId: workshop1, admissionType: 'affiliation' },
  { personId: 'cccccccc-3333-3333-3333-333333333333', memberId: null, displayName: 'Hermana Externa Demostrativa', institutionalNumber: null, organizationId: workshop23, admissionType: 'incorporation' },
]
export function lookupDemoAdmissionPeople(input: AdmissionPersonSearch): AdmissionPersonSearchResponse {
  const query = input.query.trim()
  if (query.length < 3 || query.length > 80) throw new Error('Ingrese entre 3 y 80 caracteres para buscar.')
  const term = query.toLocaleUpperCase('es-CL')
  const items = records.filter(x => x.admissionType === input.admissionType && (
    (input.admissionType === 'affiliation' && x.institutionalNumber === term) ||
    (x.organizationId === input.organizationId && x.displayName.toLocaleUpperCase('es-CL').includes(term))
  )).slice(0, 20).map(({ personId, memberId, displayName, institutionalNumber }) => ({ personId, memberId, displayName, institutionalNumber }))
  return { returned: items.length, items }
}
