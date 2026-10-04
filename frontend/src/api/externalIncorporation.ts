import type { AdmissionCaseResponse } from './pmgmApi'
import type { AdmissionPersonOption } from './admissionLookup'
export interface ExternalIncorporationRequest { organizationId: string; firstNames: string; lastNames: string; rutOrInstitutionalId: string; originObedience: string; degree: 'apprentice' | 'fellowcraft' | 'master'; originLodgeName?: string | null; originLodgeNumber?: string | null }
export type ExternalDemoPerson = AdmissionPersonOption & { organizationId: string; admissionType: string }
export function validateExternalIncorporation(input: ExternalIncorporationRequest): string {
  const identifier = input.rutOrInstitutionalId.trim().replace(/[.\- ]/g, '').toLocaleUpperCase('es-CL')
  if (!input.firstNames.trim() || input.firstNames.trim().length > 160 || !input.lastNames.trim() || input.lastNames.trim().length > 160 ||
      !identifier || identifier.length > 16 || !/^[\p{L}\p{N}]+$/u.test(identifier) || !input.originObedience.trim() || input.originObedience.trim().length > 240 ||
      (input.originLodgeName?.trim().length ?? 0) > 240 || (input.originLodgeNumber?.trim().length ?? 0) > 80 || !['apprentice', 'fellowcraft', 'master'].includes(input.degree))
    throw new Error('Revise nombres, apellidos, identificación (hasta 16 caracteres), Obediencia y grado declarado.')
  return identifier
}
export class ExternalIncorporationDemo {
  readonly people: ExternalDemoPerson[] = []
  private readonly identifiers = new Set(['DEMOCRV23', 'DEMOCRV01'])
  create(input: ExternalIncorporationRequest): AdmissionCaseResponse {
    const identifier = validateExternalIncorporation(input)
    if (!['23232323-2323-2323-2323-232323232323', '11111111-1111-1111-1111-111111111111'].includes(input.organizationId)) throw new Error('El Taller destino no existe en esta demostración.')
    if (this.identifiers.has(identifier)) throw new Error('No fue posible registrar esta identidad. Revise el expediente existente con el área autorizada antes de repetir el alta.')
    const personId = crypto.randomUUID(); const result: AdmissionCaseResponse = { id: crypto.randomUUID(), organizationId: input.organizationId,
      admissionType: 'incorporation', personId, memberId: null, affiliationMode: null, withdrawalLetterGrantedDate: null, status: 'under_review', createdAtUtc: new Date().toISOString() }
    this.identifiers.add(identifier)
    this.people.push({ personId, memberId: null, displayName: `${input.firstNames.trim()} ${input.lastNames.trim()}`, institutionalNumber: null, organizationId: input.organizationId, admissionType: 'incorporation' })
    return result
  }
}
