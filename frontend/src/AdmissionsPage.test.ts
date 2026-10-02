import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
const page = readFileSync(new URL('./AdmissionsPage.tsx', import.meta.url), 'utf8')
const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
describe('PMGM-ADM-001 · afiliación e incorporación separadas de iniciación', () => {
  it('reutiliza Person/Member y publica ambas modalidades', () => {
    expect(page).toContain('profile.member.personId')
    expect(page).toContain('admissionType:type')
    expect(page).toContain('Afiliación')
    expect(page).toContain('Incorporación')
    expect(page).not.toContain('CandidateWorkshopIntakePage')
  })
  it('queda disponible en las vistas de Secretaría', () => {
    expect(app).toContain("label: 'Afiliación e incorporación'")
    expect(app).toContain("view === 'admissions'")
  })
})
