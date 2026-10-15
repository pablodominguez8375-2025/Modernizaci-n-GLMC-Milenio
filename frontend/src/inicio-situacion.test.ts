import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { degreeLabel } from './memberIdentity'

const dashboard = readFileSync('src/DashboardPage.tsx', 'utf8')
const app = readFileSync('src/App.tsx', 'utf8')
const lodge = readFileSync('src/LodgeManagementPage.tsx', 'utf8')
const hospitalaria = readFileSync('src/HospitalariaPage.tsx', 'utf8')

describe('Inicio: tu situación, títulos y celular compacto', () => {
  it('muestra Taller, grado y cargo del hermano y ya no dice «Autenticado»', () => {
    expect(dashboard).toContain('home-identity')
    expect(dashboard).not.toContain("'Autenticado'")
    expect(app).toContain('<DashboardPage membershipApi={membershipApi}')
  })
  it('nombra el grado en palabras', () => {
    expect(degreeLabel(1)).toBe('Aprendiz (1°)')
    expect(degreeLabel(2)).toBe('Compañero (2°)')
    expect(degreeLabel(3)).toBe('Maestro (3°)')
    expect(degreeLabel(undefined, '3°')).toBe('Maestro (3°)')
    expect(degreeLabel(0, null)).toBeNull()
  })
  it('el título de la pantalla es el mismo que el del menú', () => {
    expect(app).toContain("isLodgeSecretaryWorkspace ? 'Secretaría del Taller'")
    expect(lodge).toContain('{pageTitle} <span>›</span> Vista general')
    expect(hospitalaria).toContain('<h1>Hospitalaria del Taller</h1>')
  })
})
