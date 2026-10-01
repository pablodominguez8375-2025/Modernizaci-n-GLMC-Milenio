/* PMGM-UX-004 · Lista primero, acción bajo demanda en Secretaría del Taller y Gran Secretaría. */
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8')
const formsOutsideDrawer = (source: string) => {
  // Cuenta formularios de registro que no están dentro de un <ActionDrawer>.
  let depth = 0; let outside = 0
  for (const match of source.matchAll(/<ActionDrawer\b|<\/ActionDrawer>|<form\b[^>]*onSubmit=\{(\w+)\}/g)) {
    if (match[0] === '</ActionDrawer>') depth -= 1
    else if (match[0].startsWith('<ActionDrawer')) depth += 1
    else if (depth === 0) outside += 1
  }
  return outside
}

describe('PMGM-UX-004 · Secretarías con listas primero y acciones bajo demanda', () => {
  it('Secretaría del Taller: correspondencia, pendientes y agenda se abren desde botones', () => {
    expect(formsOutsideDrawer(read('./OperationalSecretariatPanel.tsx'))).toBe(0)
    expect(read('./OperationalSecretariatPanel.tsx')).toContain('label="Registrar correspondencia"')
  })

  it('Secretaría del Taller: Cuadro, reuniones, Consejo y cartas de retiro no muestran formularios abiertos', () => {
    expect(formsOutsideDrawer(read('./LodgeSecretariatPanel.tsx'))).toBe(0)
    expect(formsOutsideDrawer(read('./LodgeCouncilPanel.tsx'))).toBe(0)
    expect(formsOutsideDrawer(read('./LodgeWithdrawalsPanel.tsx'))).toBe(0)
  })

  it('cerrar Tenida y remitir extracto piden confirmación', () => {
    const panel = read('./LodgeSecretariatPanel.tsx')
    expect(panel).toMatch(/<ConfirmAction label="Cerrar Tenida"/)
    expect(panel).toMatch(/<ConfirmAction tone="secondary" label="Remitir extracto a Gran Secretaría"/)
  })

  it('Gestión Logial se divide en pestañas, con Secretaría separada en Correspondencia, Cuadro y Archivo', () => {
    const page = read('./LodgeManagementPage.tsx')
    expect(page).toContain('<WorkspaceTabs label="Secciones de Gestión Logial"')
    for (const label of ['Tenidas y actas', 'Correspondencia', 'Cuadro y reuniones', 'Archivo y cierre', 'Consejo', 'Docencia', 'Cartas de retiro']) expect(page).toContain(`label: '${label}'`)
    expect(page).toContain('label="Registrar tenida"')
  })

  it('Gran Secretaría: pestañas, formularios en panel, Plancha con confirmación y acciones secundarias en «⋯»', () => {
    const page = read('./GrandSecretariatPage.tsx')
    expect(page).toContain('<WorkspaceTabs label="Secciones de Gran Secretaría"')
    expect(page).toMatch(/<ActionDrawer label="Reservar espacio"[^>]*confirmMessage=/)
    expect(page).toMatch(/<ActionDrawer label="Emitir documento oficial"[^>]*confirmMessage=/)
    expect(page).toContain('confirmLabel="Sí, emitir Plancha"')
    expect(page).toContain('<RowMenu label="Más acciones del extracto"')
  })
})
