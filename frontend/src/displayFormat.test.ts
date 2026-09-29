import { describe, expect, it } from 'vitest'
import { countLabel, formatDateOnlyCl, organizationDisplayName, organizationNumberOf } from './displayFormat'

describe('displayFormat', () => {
  it('no duplica el número cuando el nombre del Taller ya lo incluye', () => {
    expect(organizationDisplayName('Taller Demostrativo Nº 1', 1)).toBe('Taller Demostrativo Nº 1')
    expect(organizationDisplayName('Taller Demostrativo Nº 12', '1')).toBe('Taller Demostrativo Nº 12 · Nº 1')
    expect(organizationDisplayName('Luz del Sur', 23)).toBe('Luz del Sur · Nº 23')
    expect(organizationDisplayName('Luz del Sur', null)).toBe('Luz del Sur')
  })
  it('obtiene el número del sello del Taller', () => {
    expect(organizationNumberOf('Taller Demostrativo Nº 23')).toBe('23')
    expect(organizationNumberOf('Luz del Sur', 7)).toBe('7')
    expect(organizationNumberOf('Luz del Sur')).toBeNull()
  })
  it('concuerda singular y plural', () => {
    expect(countLabel(1, 'pendiente')).toBe('1 pendiente')
    expect(countLabel(0, 'pendiente')).toBe('0 pendientes')
    expect(countLabel(3, 'autorización', 'autorizaciones')).toBe('3 autorizaciones')
  })
  it('formatea fechas sin hora en es-CL sin corrimiento de día', () => {
    expect(formatDateOnlyCl('2026-09-01')).toMatch(/^(01-09-2026|1 sept?\.? 2026)$/)
    expect(formatDateOnlyCl(null)).toBe('—')
  })
})
