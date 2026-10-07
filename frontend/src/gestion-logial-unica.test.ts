import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const page = readFileSync(new URL('./LodgeManagementPage.tsx', import.meta.url), 'utf8')

describe('PMGM-UX Gestión Logial sin menús repetidos (aprobado por el PO 06-10-2026)', () => {
  it('un solo menú: sin carrusel por cargo ni pestañas «Secciones del Taller»', () => {
    expect(app).not.toContain('<SecretariatRoleNavigation')
    expect(app).not.toContain('label="Secciones del Taller"')
  })

  it('el Resumen no repite ficha, miembros, agenda, avisos ni tarjetas de otros módulos', () => {
    for (const id of ['lodge-profile-summary', 'lodge-member-summary', 'id="lodge-areas"', 'id="lodge-activity"', 'lodge-inspiration-card']) expect(page).not.toContain(id)
    expect(page).toContain('lodge-officers-card')
  })

  it('Secretaría abre afiliación, fichas, documentos y ceremonias desde el menú lateral', () => {
    expect(app).toContain('label="Afiliación e incorporación" allowed active={view === \'admissions\'}')
    expect(app).toContain("allowed={(canMembers || isLodgeSecretaryWorkspace || isGrandSecretaryWorkspace) && canView('members')}")
    expect(app).toContain("allowed={(canDocuments || isLodgeSecretaryWorkspace || isGrandSecretaryWorkspace) && canView('documentmanager')}")
    expect(app).toContain("allowed={(canCeremonies || isGrandSecretaryWorkspace) && canView('ceremonies')}")
  })

  it('las autoridades de la Orden ven «Talleres (supervisión)» (opción B del PO)', () => {
    expect(app).toContain("isGrandSupervision ? 'Talleres (supervisión)'")
    expect(app).toContain('canManageSecretariat={canManageLodgeSecretariat && !isGrandSupervision}')
  })
})
