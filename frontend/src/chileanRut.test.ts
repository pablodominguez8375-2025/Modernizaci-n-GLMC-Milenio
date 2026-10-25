import { describe, expect, it } from 'vitest'
import { formatChileanRut, isValidChileanRut, parseChileanRut } from './chileanRut'

describe('RUT de Chile: módulo 11 y formato canónico', () => {
  it('acepta K minúscula, compacto, con guion y con separadores', () => {
    const formatted = '10.000.013-K' // ejemplo matemático sintético, no una persona
    for (const value of ['10000013-k', '10000013K', '10.000.013-K', ' 10.000.013-k ']) {
      expect(isValidChileanRut(value)).toBe(true)
      expect(formatChileanRut(value)).toBe(formatted)
      expect(parseChileanRut(value)?.normalized).toBe('10000013-K')
    }
  })
  it('acepta DV numérico, DV cero y cuerpo de siete dígitos', () => {
    expect(formatChileanRut('123456785')).toBe('12.345.678-5')
    expect(formatChileanRut('10000004-0')).toBe('10.000.004-0')
    expect(formatChileanRut('9.876.543-3')).toBe('9.876.543-3')
  })
  it('rechaza dígito incorrecto, cuerpo cero y entradas malformadas', () => {
    for (const value of ['12.345.678-K', '10.000.013-0', '0-0', '12345678',
      '12.34.567-5', '12--345678-5', '123456789-0', '12.345.678-X', '', null]) {
      expect(isValidChileanRut(value)).toBe(false)
      expect(formatChileanRut(value)).toBeNull()
    }
  })
})
