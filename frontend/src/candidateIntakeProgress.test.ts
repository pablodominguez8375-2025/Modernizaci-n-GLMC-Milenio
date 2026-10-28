import { describe, expect, it } from 'vitest'
import { intakeProgress } from './candidateIntakeProgress'

const base = { form: {}, presentersText: '', sponsorRegistered: false, hasPhoto: false }

describe('avance de la ficha de insinuado', () => {
  it('ficha vacía: 0 de 4 y lista lo que falta', () => {
    const result = intakeProgress(base)
    expect(result.done).toBe(0)
    expect(result.total).toBe(4)
    expect(result.firstPending).toBe('personal')
    expect(result.sections[0].missing).toContain('RUT o ID')
    expect(result.sections[2].missing).toEqual(['fecha de insinuación', 'presentantes', 'patrocinio formal'])
  })

  it('datos personales completos con teléfono o con correo', () => {
    const personal = { firstNames: 'Ana', paternalSurname: 'Soto', rutOrInstitutionalId: '1-9', birthDate: '1980-01-01' }
    expect(intakeProgress({ ...base, form: { ...personal, phone: '+56 9 1' } }).sections[0].complete).toBe(true)
    expect(intakeProgress({ ...base, form: { ...personal, email: 'a@b.cl' } }).sections[0].complete).toBe(true)
    expect(intakeProgress({ ...base, form: personal }).sections[0].missing).toEqual(['teléfono o correo'])
  })

  it('espacios en blanco no cuentan como dato', () => {
    expect(intakeProgress({ ...base, form: { occupation: '   ' } }).sections[1].complete).toBe(false)
  })

  it('todo completo: 4 de 4 y sin pendiente', () => {
    const result = intakeProgress({
      form: { firstNames: 'Ana', paternalSurname: 'Soto', rutOrInstitutionalId: '1-9', birthDate: '1980-01-01', email: 'a@b.cl', occupation: 'Contadora', insinuationDate: '2026-10-01' },
      presentersText: 'Juan Pérez', sponsorRegistered: true, hasPhoto: true,
    })
    expect(result.done).toBe(4)
    expect(result.firstPending).toBeNull()
  })
})
