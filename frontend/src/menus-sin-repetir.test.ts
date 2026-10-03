import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const userMenu = readFileSync(new URL('./UserMenu.tsx', import.meta.url), 'utf8')

describe('PMGM-UX menús sin funciones repetidas (aprobado por el PO 03-10-2026)', () => {
  it('usa un solo nombre por función: Agenda y Avisos', () => {
    expect(app).toContain('<ModuleAccess icon="calendar" label="Agenda"')
    expect(app).toContain('<ModuleAccess icon="bell" label="Avisos"')
    expect(app).not.toMatch(/label="Mi calendario"|label="Notificaciones"/)
  })

  it('el menú de usuario no repite opciones del menú', () => {
    expect(userMenu).not.toContain('Mi calendario')
    expect(userMenu).not.toContain('TextSizeControl')
  })

  it('agrupa módulos relacionados como pestañas de una sola opción', () => {
    expect(app).toContain('label="Secciones de Sistema"')
    expect(app).toContain('label="Secciones de Calidad de datos"')
    expect(app).toContain('label="Secciones del Taller"')
    expect(app).not.toContain('<ModuleAccess icon="check" label="Cola de corroboración"')
  })
})
