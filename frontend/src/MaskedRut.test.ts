import { describe, expect, it } from 'vitest'
import { maskRut } from './MaskedRut'

describe('maskRut', () => {
  it('oculta los últimos tres dígitos del cuerpo y conserva el verificador', () => {
    expect(maskRut('12.345.678-5')).toBe('12.345.***-5')
    expect(maskRut('9.876.543-K')).toBe('9.876.***-K')
    expect(maskRut('7654321-0')).toBe('7.654.***-0')
  })
  it('no filtra nada si el valor es inválido', () => { expect(maskRut('')).toBe('***') })
})
