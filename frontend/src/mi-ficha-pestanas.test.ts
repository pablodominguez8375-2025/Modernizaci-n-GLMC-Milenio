import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const page = readFileSync(new URL('./MemberPortalPage.tsx', import.meta.url), 'utf8')
const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const payments = readFileSync(new URL('./MemberPayments.tsx', import.meta.url), 'utf8')
const css = readFileSync(new URL('./common-views.css', import.meta.url), 'utf8')

/* Sustituye la decisión del 06-10-2026 (tres pestañas dentro de Mi ficha) por el pedido del PO del 07-10-2026:
   Mis pagos y Mis asistencias son opciones del menú lateral bajo Mi ficha; Mis datos es Mi ficha. Sin menú dentro de la vista. */
describe('PMGM-UX Mi ficha sin pestañas: Mis pagos y Mis asistencias bajo Mi ficha en el menú lateral (pedido del PO 07-10-2026)', () => {
  it('la vista no dibuja pestañas internas; la sección llega desde el menú', () => {
    expect(page).not.toContain('WorkspaceTabs')
    expect(page).not.toContain('Secciones de Mi ficha')
    expect(page).toContain("section = 'datos'")
    for (const id of ['datos', 'pagos', 'asistencias']) expect(page).toContain(`<WorkspacePanel id="${id}" active={section === '${id}'}`)
  })

  it('el menú lateral muestra Mi ficha seguida de Mis pagos y Mis asistencias, con el mismo permiso del Portal del Hermano', () => {
    const fichaAt = app.indexOf('label="Mi ficha"')
    const pagosAt = app.indexOf('label="Mis pagos" sub')
    const asistAt = app.indexOf('label="Mis asistencias" sub')
    const agendaAt = app.indexOf('label="Agenda" inTabbar')
    expect(fichaAt).toBeGreaterThan(-1)
    expect(fichaAt).toBeLessThan(pagosAt)
    expect(pagosAt).toBeLessThan(asistAt)
    expect(asistAt).toBeLessThan(agendaAt)
    expect(app).toContain("memberPayments:'member', memberAttendance:'member'")
    expect(app).toContain("section={view === 'memberPayments' ? 'pagos' : view === 'memberAttendance' ? 'asistencias' : 'datos'}")
    expect(css).toContain('.nav-item.nav-subitem')
  })

  it('incluye historial de cargos, reposiciones de Hospitalaria y detalle de tenidas y ceremonias', () => {
    expect(page).toContain('Historial de cargos en el Taller')
    expect(payments).toContain('Reposiciones por hermanos fallecidos')
    expect(page).toContain('Tenidas y ceremonias')
  })

  it('separa el crédito pendiente de la Cartola personal en Mis pagos (#366)', () => {
    expect(css).toMatch(/\.member-treasury-account > section\[aria-label="Crédito pendiente de imputación"\]\s*\{[^}]*margin-bottom:\s*1\.5rem/)
  })

  it('la edición de datos se abre en panel y no hay formulario abierto', () => {
    expect(page).toContain('<ActionDrawer label="Editar mis datos"')
    expect(page).toContain("{section === 'datos' && <ActionDrawer")
    expect(page).not.toContain('{editing && <section')
  })
})
