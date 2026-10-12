/** Volatile, synthetic-only showcase state. No storage, network calls or personal data. */
import {
  coreCandidateFormFields, mockCandidateFormSchema,
  type FormSchemaSnapshot, type FormSchemaVersionsResponse, type PublishFormSchema,
} from './candidateFormTypes'

const published: FormSchemaSnapshot[] = [structuredClone(mockCandidateFormSchema)]

export function mockActiveCandidateSchema(): FormSchemaSnapshot | null {
  const now = new Date().toISOString()
  return structuredClone(published.filter(row => row.effectiveFromUtc <= now).at(-1) ?? null)
}
export function mockFormHistory(): FormSchemaVersionsResponse {
  return {
    formCode: 'candidate_profile', organizationId: null,
    total: published.length,
    versions: [...published].reverse().map(row => ({
      version: row.version, effectiveFromUtc: row.effectiveFromUtc,
      payloadSha256: 'a'.repeat(64), recordedAtUtc: row.effectiveFromUtc,
    })),
  }
}
export function mockFormVersion(version: number): FormSchemaSnapshot {
  const match = published.find(row => row.version === version)
  if (!match) throw new Error('La versión demostrativa solicitada no existe.')
  return structuredClone(match)
}

export function publishMockFormVersion(input: PublishFormSchema): FormSchemaSnapshot {
  const last = published.at(-1)!
  if (input.expectedPreviousVersion !== last.version || input.version !== last.version + 1)
    throw new Error('La configuración cambió. Recargue su historial antes de publicar.')
  if (input.effectiveFromUtc <= new Date().toISOString() ||
      input.effectiveFromUtc <= last.effectiveFromUtc)
    throw new Error('La vigencia debe ser futura y posterior a la versión anterior.')
  const fields = input.fields
  const byKey = new Map(fields.map(field => [field.key, field]))
  if (byKey.size !== fields.length) throw new Error('No se permiten claves duplicadas.')
  for (const core of coreCandidateFormFields) {
    const field = byKey.get(core.key)
    if (!field || !field.isCore || !field.enabled || !field.visible ||
        field.typeCode !== core.typeCode || (core.required && !field.required) ||
        (core.editable === false && field.editable) ||
        field.classification !== core.classification)
      throw new Error('No se pueden modificar los campos institucionales obligatorios.')
  }
  for (const old of last.fields) {
    const next = byKey.get(old.key)
    if (!next || next.typeCode !== old.typeCode || next.isCore !== old.isCore)
      throw new Error('La nueva versión debe conservar el identificador y tipo de todos los campos anteriores.')
    const rank = { internal: 1, sensitive: 2, restricted: 3 }
    if (rank[next.classification] < rank[old.classification])
      throw new Error('No puede reducirse la clasificación de un dato privado.')
  }
  const row: FormSchemaSnapshot = {
    formCode: 'candidate_profile', version: input.version,
    effectiveFromUtc: input.effectiveFromUtc,
    fields: structuredClone(fields),
  }
  published.push(row)
  return structuredClone(row)
}
