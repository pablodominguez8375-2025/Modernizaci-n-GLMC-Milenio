/** Institutionally private configurable extras. Core person/ceremony fields remain in their domain APIs. */
export type FormFieldType =
  | 'text' | 'multiline' | 'number' | 'date' | 'time' | 'currency' | 'percentage'
  | 'boolean' | 'single_select' | 'multi_select' | 'email' | 'phone'
  | 'document' | 'photo' | 'reference'
export type FormClassification = 'internal' | 'sensitive' | 'restricted'
export type FormValue = string | number | boolean | string[] | null
export type FormValues = Record<string, FormValue>

export interface FormFieldDefinition {
  key: string
  label: string
  typeCode: FormFieldType
  isCore: boolean
  enabled: boolean
  visible: boolean
  required: boolean
  editable: boolean
  classification: FormClassification
  sortOrder: number
  helpText?: string | null
  dependsOnKey?: string | null
  options?: string[] | null
}

export interface FormSchemaSnapshot {
  formCode: 'candidate_profile'
  version: number
  effectiveFromUtc: string
  fields: FormFieldDefinition[]
}
export interface ActiveCandidateFormSchema {
  formCode: 'candidate_profile'
  schemaVersion: number
  scopeKey: string
  effectiveFromUtc: string
  expectedCaptureRevision: number
  fields: FormFieldDefinition[]
}
export interface CandidateFormCaptureInfo {
  captureRevision: number
  schemaVersion: number
  scopeKey: string
  capturedAtUtc: string
  payloadSha256: string
}
export interface CandidateFormCaptureHistory {
  requestId: string
  total: number
  captures: CandidateFormCaptureInfo[]
}
export interface CandidateFormCaptureDetail {
  ceremonyRequestId: string
  captureRevision: number
  schemaVersion: number
  scopeKey: string
  capturedAtUtc: string
  values: FormValues
}
export interface SaveCandidateFormCapture {
  schemaVersion: number
  expectedCaptureRevision: number
  values: FormValues
}
export interface FormSchemaVersionSummary {
  version: number
  effectiveFromUtc: string
  payloadSha256: string
  recordedAtUtc: string
}
export interface FormSchemaVersionsResponse {
  formCode: 'candidate_profile'
  organizationId: string | null
  total: number
  versions: FormSchemaVersionSummary[]
}
export interface PublishFormSchema {
  version: number
  expectedPreviousVersion: number
  effectiveFromUtc: string
  fields: FormFieldDefinition[]
}

export const coreCandidateFormFields: ReadonlyArray<FormFieldDefinition> = [
  { key: 'candidate_id', label: 'Expediente', typeCode: 'reference', isCore: true, enabled: true, visible: true, required: true, editable: false, classification: 'sensitive', sortOrder: 0 },
  { key: 'organization_id', label: 'Taller', typeCode: 'reference', isCore: true, enabled: true, visible: true, required: true, editable: false, classification: 'internal', sortOrder: 1 },
  { key: 'first_names', label: 'Nombres', typeCode: 'text', isCore: true, enabled: true, visible: true, required: true, editable: true, classification: 'sensitive', sortOrder: 2 },
  { key: 'paternal_surname', label: 'Apellido paterno', typeCode: 'text', isCore: true, enabled: true, visible: true, required: true, editable: true, classification: 'sensitive', sortOrder: 3 },
  { key: 'maternal_surname', label: 'Apellido materno', typeCode: 'text', isCore: true, enabled: true, visible: true, required: false, editable: true, classification: 'sensitive', sortOrder: 4 },
  { key: 'rut_or_institutional_id', label: 'RUT o identificación', typeCode: 'text', isCore: true, enabled: true, visible: true, required: true, editable: true, classification: 'restricted', sortOrder: 5 },
  { key: 'process_status', label: 'Estado del proceso', typeCode: 'text', isCore: true, enabled: true, visible: true, required: true, editable: false, classification: 'internal', sortOrder: 6 },
]
export const mockCandidateFormSchema: FormSchemaSnapshot = {
  formCode: 'candidate_profile',
  version: 1,
  effectiveFromUtc: '2026-10-01T00:00:00Z',
  fields: [
    ...coreCandidateFormFields,
    { key: 'custom_motivation', label: 'Motivación personal complementaria', typeCode: 'multiline', isCore: false, enabled: true, visible: true, required: true, editable: true, classification: 'sensitive', sortOrder: 10, helpText: 'Respuesta privada para el expediente, no para publicación.' },
    { key: 'custom_interest', label: 'Área de interés', typeCode: 'single_select', isCore: false, enabled: true, visible: true, required: false, editable: true, classification: 'internal', sortOrder: 11, options: ['Docencia', 'Solidaridad', 'Historia institucional'] },
    { key: 'custom_volunteer', label: 'Disponibilidad para actividades solidarias', typeCode: 'boolean', isCore: false, enabled: true, visible: true, required: false, editable: true, classification: 'internal', sortOrder: 12 },
  ],
}

export const capturedValueTypes = new Set<FormFieldType>([
  'text', 'multiline', 'number', 'date', 'time', 'currency', 'percentage',
  'boolean', 'single_select', 'multi_select', 'email', 'phone',
])

export function isActiveDependency(field: FormFieldDefinition, values: FormValues): boolean {
  if (!field.dependsOnKey) return true
  const parent = values[field.dependsOnKey]
  if (parent == null || parent === false || parent === '' || parent === 0) return false
  if (Array.isArray(parent) && parent.length === 0) return false
  return true
}

export function isProvided(value: FormValue | undefined): boolean {
  return value !== undefined && value !== null &&
    (typeof value !== 'string' || value.trim().length > 0) &&
    (!Array.isArray(value) || value.length > 0)
}

export function visibleCandidateFormFields(fields: FormFieldDefinition[]): FormFieldDefinition[] {
  return fields
    .filter(field => !field.isCore && field.enabled && field.visible)
    .sort((left, right) => left.sortOrder - right.sortOrder || left.key.localeCompare(right.key))
}

/** Client convenience only. Server schema, permissions and privacy checks remain authoritative. */
export function prepareCandidateFormCapture(
  fields: FormFieldDefinition[], draft: FormValues,
): { values: FormValues; errors: string[] } {
  const values: FormValues = {}
  const errors: string[] = []
  const allowed = visibleCandidateFormFields(fields)
  for (const field of allowed) {
    if (!isActiveDependency(field, draft)) continue
    if (!field.editable || !capturedValueTypes.has(field.typeCode)) continue
    const value = draft[field.key]
    if (field.required && !isProvided(value)) errors.push('Complete el campo obligatorio: ' + field.label)
    if (!isProvided(value)) continue
    if (field.typeCode === 'number' || field.typeCode === 'currency' || field.typeCode === 'percentage') {
      const numeric = typeof value === 'number' ? value : Number(value)
      if (!Number.isFinite(numeric)) errors.push('Ingrese un número válido en ' + field.label)
      else values[field.key] = numeric
      continue
    }
    if (field.typeCode === 'multi_select') {
      if (!Array.isArray(value) || value.some(option => !field.options?.includes(option)))
        errors.push('Seleccione opciones válidas en ' + field.label)
      else values[field.key] = value
      continue
    }
    if (field.typeCode === 'single_select' && (typeof value !== 'string' || !field.options?.includes(value))) {
      errors.push('Seleccione una opción válida en ' + field.label)
      continue
    }
    if (field.typeCode === 'boolean' && typeof value !== 'boolean') {
      errors.push('Ingrese una respuesta válida en ' + field.label)
      continue
    }
    if (typeof value === 'string') values[field.key] = value.trim()
    else if (typeof value === 'boolean') values[field.key] = value
    else errors.push('El tipo de dato no corresponde a ' + field.label)
  }
  return { values, errors }
}
