import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { CEREMONY_TYPE_LABELS, ceremonyTypeLabel, isKnownCeremonyType } from './ceremonyTypes'

describe('ceremonyTypes — PMGM-GAP-001 · GAP-001', () => {
  it('labels every institutional ceremony type correctly', () => {
    expect(ceremonyTypeLabel('initiation')).toBe('Iniciación')
    expect(ceremonyTypeLabel('affiliation')).toBe('Afiliación')
    expect(ceremonyTypeLabel('wage_increase')).toBe('Aumento de salario')
    expect(ceremonyTypeLabel('exaltation')).toBe('Exaltación')
    expect(ceremonyTypeLabel('incorporation')).toBe('Incorporación')
  })

  it('never disguises Afiliación or Incorporación as Exaltación (the original defect)', () => {
    expect(ceremonyTypeLabel('affiliation')).not.toBe('Exaltación')
    expect(ceremonyTypeLabel('incorporation')).not.toBe('Exaltación')
  })

  it('surfaces unknown codes explicitly instead of mapping them to a real grade', () => {
    expect(ceremonyTypeLabel('other')).toBe('Tipo no reconocido (other)')
    expect(ceremonyTypeLabel(null)).toBe('Sin tipo de ceremonia')
    expect(isKnownCeremonyType('other')).toBe(false)
  })

  // Excepción temporal y explícita: LodgeSecretariatPanel.tsx conserva su copia
  // porque PR #116 inserta código inmediatamente debajo de ella y eliminarla aquí
  // generaría un conflicto evitable. Su copia NO tiene el defecto de GAP-001
  // (muestra el código crudo en vez de rotular "Exaltación"). Retirar esta
  // excepción y la copia al integrar PR #116.
  const PENDING_AFTER_PR_116 = ['LodgeSecretariatPanel.tsx']

  it('has no local ceremonyTypeLabel copies left in the frontend (single source of truth)', () => {
    const dir = fileURLToPath(new URL('.', import.meta.url))
    const offenders = readdirSync(dir)
      .filter(name => /\.tsx?$/.test(name) && !name.includes('.test.') && name !== 'ceremonyTypes.ts')
      .filter(name => /function ceremonyTypeLabel/.test(readFileSync(`${dir}/${name}`, 'utf-8')))
    expect(offenders).toEqual(PENDING_AFTER_PR_116)
  })

  it('the remaining local copy never falls back to "Exaltación" for unknown types', () => {
    const dir = fileURLToPath(new URL('.', import.meta.url))
    const source = readFileSync(`${dir}/LodgeSecretariatPanel.tsx`, 'utf-8')
    const fn = source.slice(source.indexOf('function ceremonyTypeLabel'), source.indexOf('\n', source.indexOf('function ceremonyTypeLabel')))
    expect(fn).toMatch(/:value\}$/)
  })

  // Paridad de contrato con el backend. Se omite (no falla) en builds que sólo
  // disponen de frontend/ — p. ej. Dockerfile.showcase — y se ejecuta en el CI
  // del repositorio completo.
  const backendCodes = fileURLToPath(new URL('../../backend/src/PMGM.Api/Modules/Ceremonies/CeremonyCodes.cs', import.meta.url))
  it.skipIf(!existsSync(backendCodes))('matches CeremonyCodes.Type in the backend exactly', () => {
    const source = readFileSync(backendCodes, 'utf-8')
    const typeBlock = source.slice(source.indexOf('class Type'), source.indexOf('}', source.indexOf('class Type')))
    const backendTypes = [...typeBlock.matchAll(/=\s*"([a-z_]+)"/g)].map(match => match[1]).sort()
    expect(backendTypes.length).toBeGreaterThan(0)
    expect(Object.keys(CEREMONY_TYPE_LABELS).sort()).toEqual(backendTypes)
  })
})
