import { describe, expect, it } from 'vitest'
import { docenciaDegreesForProfile, functionalMenuDefinitions, functionalMenuNames } from './functionalAccessModel'

describe('functional access model', () => {
  it('exposes one operational menu per institutional function', () => {
    expect(functionalMenuNames).toContain('Secretaría')
    expect(functionalMenuNames).toContain('Tesorería')
    expect(functionalMenuNames).toContain('Hospitalaria')
    expect(functionalMenuNames.filter(item => item === 'Docencia')).toHaveLength(1)
  })

  it('limits Docencia to the degree assigned to each office', () => {
    expect(docenciaDegreesForProfile('Segundo Vigilante')).toEqual([1])
    expect(docenciaDegreesForProfile('Primer Vigilante')).toEqual([2])
    expect(docenciaDegreesForProfile('Inmediato Ex-Venerable Maestro')).toEqual([3])
  })

  it('uses Cuadro terminology outside Gran Asamblea', () => {
    const copy = functionalMenuDefinitions.map(item => `${item.menu} ${item.description}`).join(' ')
    expect(copy).toContain('Cuadro General de la Orden')
    expect(copy.toLowerCase()).not.toContain('padrón')
  })
})
