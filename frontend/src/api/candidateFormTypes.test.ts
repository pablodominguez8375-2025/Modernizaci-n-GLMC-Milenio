import { describe, expect, it } from 'vitest'
import {
  coreCandidateFormFields, mockCandidateFormSchema, prepareCandidateFormCapture,
  visibleCandidateFormFields, isActiveDependency, type FormFieldDefinition,
} from './candidateFormTypes'

describe('Issue #37 — contrato privado de campos configurables', () => {
  const rows = mockCandidateFormSchema.fields

  it('does not send canonical identity, workshop, RUT or workflow state as configurable values', () => {
    const result = prepareCandidateFormCapture(rows, {
      first_names: 'Persona no autorizada',
      rut_or_institutional_id: 'RUT-NO',
      process_status: 'published',
      custom_motivation: 'Motivación sintética CI',
      custom_interest: 'Docencia',
      custom_volunteer: false,
    })
    expect(result.errors).toEqual([])
    expect(result.values).toEqual({
      custom_motivation: 'Motivación sintética CI',
      custom_interest: 'Docencia',
      custom_volunteer: false,
    })
    expect(Object.keys(result.values).some(key => coreCandidateFormFields.some(f => f.key === key))).toBe(false)
  })

  it('retains false as an explicit valid answer and requires the additional mandatory field', () => {
    const missing = prepareCandidateFormCapture(rows, { custom_volunteer: false })
    expect(missing.errors.join(' ')).toContain('Motivación')
    const complete = prepareCandidateFormCapture(rows, {
      custom_motivation: 'Solo un ejemplo ficticio', custom_volunteer: false,
    })
    expect(complete.errors).toEqual([])
    expect(complete.values.custom_volunteer).toBe(false)
  })

  it('excludes disabled, hidden, read-only, and unauthorized documents', () => {
    const extras: FormFieldDefinition[] = [
      ...rows,
      { key: 'custom_private_doc', label: 'Documento', typeCode: 'document', isCore: false,
        enabled: true, visible: true, required: false, editable: true, classification: 'restricted', sortOrder: 20 },
      { key: 'custom_secret', label: 'Oculto', typeCode: 'text', isCore: false,
        enabled: true, visible: false, required: false, editable: true, classification: 'restricted', sortOrder: 21 },
    ]
    expect(visibleCandidateFormFields(extras).map(f => f.key)).not.toContain('custom_secret')
    const result = prepareCandidateFormCapture(extras, {
      custom_motivation: 'Datos ficticios',
      custom_private_doc: '11111111-1111-4111-8111-111111111111',
      custom_secret: 'No debe transmitirse',
    })
    expect(Object.keys(result.values)).toEqual(['custom_motivation'])
    expect(JSON.stringify(result.values)).not.toContain('11111111-1111')
  })

  it('validates enumerations and conditional visibility without overriding server policy', () => {
    const dependent: FormFieldDefinition = {
      key: 'custom_followup', label: 'Detalle condicionado', typeCode: 'text', isCore: false,
      enabled: true, visible: true, required: true, editable: true, classification: 'sensitive',
      sortOrder: 30, dependsOnKey: 'custom_volunteer',
    }
    const schema = [...rows, dependent]
    expect(isActiveDependency(dependent, { custom_volunteer: false })).toBe(false)
    expect(isActiveDependency(dependent, { custom_volunteer: true })).toBe(true)
    expect(prepareCandidateFormCapture(schema, {
      custom_motivation: 'Solo QA', custom_volunteer: false,
    }).errors).toEqual([])
    expect(prepareCandidateFormCapture(schema, {
      custom_motivation: 'Solo QA', custom_volunteer: true,
    }).errors).toContain('Complete el campo obligatorio: Detalle condicionado')
    expect(prepareCandidateFormCapture(rows, {
      custom_motivation: 'Solo QA', custom_interest: 'Opción inexistente',
    }).errors.join(' ')).toContain('Seleccione una opción válida')
  })

  it('coerces a numeric UI value to JSON number and rejects non-finite numbers', () => {
    const fields: FormFieldDefinition[] = [...rows,
      { key: 'custom_budget', label: 'Importe', typeCode: 'currency',
        isCore: false, enabled: true, visible: true, required: false, editable: true,
        classification: 'internal', sortOrder: 30 }]
    expect(prepareCandidateFormCapture(fields, {
      custom_motivation: 'Ficticio', custom_budget: '12000.50',
    }).values.custom_budget).toBe(12000.5)
    expect(prepareCandidateFormCapture(fields, {
      custom_motivation: 'Ficticio', custom_budget: 'not-a-number',
    }).errors.join(' ')).toContain('número válido')
  })
})
