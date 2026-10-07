import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const page = readFileSync(new URL('./MemberPortalPage.tsx', import.meta.url), 'utf8')

describe('PMGM-UX Mi ficha en tres pestañas (pedido del PO 06-10-2026)', () => {
  it('separa Mis datos, Mis pagos y Mis asistencias', () => {
    expect(page).toContain("tabs={[{ id: 'datos', label: 'Mis datos' }, { id: 'pagos', label: 'Mis pagos' }, { id: 'asistencias', label: 'Mis asistencias' }]}")
    for (const id of ['datos', 'pagos', 'asistencias']) expect(page).toContain(`<WorkspacePanel id="${id}"`)
  })

  it('incluye historial de cargos, reposiciones de Hospitalaria y detalle de tenidas y ceremonias', () => {
    expect(page).toContain('Historial de cargos en el Taller')
    expect(page).toContain('Reposiciones por hermanos fallecidos')
    expect(page).toContain('Tenidas y ceremonias')
  })

  it('la edición de datos se abre en panel y no hay formulario abierto', () => {
    expect(page).toContain('<ActionDrawer label="Editar mis datos"')
    expect(page).not.toContain('{editing && <section')
  })
})
