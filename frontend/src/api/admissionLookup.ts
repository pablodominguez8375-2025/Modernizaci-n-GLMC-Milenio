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
export function validateDemoAdmissionIdentity(input: {
  organizationId: string; admissionType: string; personId: string; memberId?: string | null;
  withdrawalLetterGrantedDate?: string | null; affiliationMode?: string | null;
  originObedience?: string | null; degree?: string | null;
}, extra: (AdmissionPersonOption & { organizationId: string; admissionType: string })[] = []): void {
  if (![workshop23, workshop1].includes(input.organizationId)) throw new Error('El Taller destino no existe en esta demostración.')
  const person = [...records, ...extra].find(x => x.personId === input.personId)
  if (input.admissionType === 'affiliation') {
    if (!input.memberId || !person?.memberId || person.memberId !== input.memberId) throw new Error('El hermano y la persona deben corresponder al mismo registro maestro.')
  } else if (input.admissionType === 'incorporation') {
    if (input.memberId) throw new Error('Una incorporación no debe indicar membresía GLMCh antes de su resolución.')
    if (!person || person.organizationId !== input.organizationId) throw new Error('La persona no está disponible en el alcance autorizado de esta demostración.')
    if (person.memberId) throw new Error('La persona ya tiene registro de hermano GLMCh; corresponde revisar su afiliación.')
    if (input.withdrawalLetterGrantedDate != null) throw new Error('La fecha de Carta de Retiro Voluntario sólo aplica a afiliaciones.')
    if (input.affiliationMode?.trim()) throw new Error('La modalidad simple/con activación sólo aplica a afiliaciones.')
    if (!input.originObedience?.trim() || !input.degree?.trim()) throw new Error('Registre Obediencia de origen y grado declarado para su posterior acreditación documental.')
  } else throw new Error('El tipo de expediente debe ser afiliación o incorporación.')
}
export function lookupDemoAdmissionPeople(input: AdmissionPersonSearch, extra: (AdmissionPersonOption & { organizationId: string; admissionType: string })[] = []): AdmissionPersonSearchResponse {
  const query = input.query.trim()
  if (query.length < 3 || query.length > 80) throw new Error('Ingrese entre 3 y 80 caracteres para buscar.')
  const term = query.toLocaleUpperCase('es-CL')
  const items = [...records, ...extra].filter(x => x.admissionType === input.admissionType && (
    (input.admissionType === 'affiliation' && x.institutionalNumber === term) ||
    (x.organizationId === input.organizationId && x.displayName.toLocaleUpperCase('es-CL').includes(term))
  )).slice(0, 20).map(({ personId, memberId, displayName, institutionalNumber }) => ({ personId, memberId, displayName, institutionalNumber }))
  return { returned: items.length, items }
}
