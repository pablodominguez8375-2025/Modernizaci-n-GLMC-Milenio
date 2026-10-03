import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
const page = readFileSync(new URL('./AdmissionsPage.tsx', import.meta.url), 'utf8')
const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
describe('PMGM-ADM-001 · afiliación e incorporación separadas de iniciación', () => {
  it('reutiliza Person/Member y publica ambas modalidades', () => {
    expect(page).toContain('personId: selected.personId')
    expect(page).toContain('admissionType: type')
    expect(page).toContain('searchAdmissionPeople')
    expect(page).not.toContain('getProfile(')
    expect(page).not.toContain('getMembers(')
    expect(page).toContain('Afiliación')
    expect(page).toContain('Incorporación')
    expect(page).toContain('withdrawalLetterGrantedDate')
    expect(page).toContain('derivedMode')
    expect(page).not.toContain('CandidateWorkshopIntakePage')
  })
  it('queda disponible en las vistas de Secretaría', () => {
    expect(app).toContain("label: 'Afiliación e incorporación'")
    expect(app).toContain("view === 'admissions'")
  })
  it('incluye panel documental vinculado al expediente y no bypass del pipeline', () => {
    expect(page).toContain('Evidencias del expediente')
    expect(page).toContain('uploadManagedFile')
    expect(page).toContain('addAdmissionEvidence')
    expect(page).toContain('reviewAdmissionEvidence')
    expect(page).toContain('management_only')
  })
})
