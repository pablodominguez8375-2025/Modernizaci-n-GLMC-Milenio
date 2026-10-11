import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const read = (file: string) => readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')

describe('Mi mesa de trabajo (PO 10-10-2026)', () => {
  const app = read('App.tsx')
  it('agrupa las funciones de gestión en una sola opción del menú con vuelta a la mesa', () => {
    expect(app).toContain('label="Mi mesa de trabajo"')
    expect(app).toContain('← Volver a Mi mesa de trabajo')
    expect(app).toContain('const useWorkbench = workbenchItems.length >= 5')
    expect(app).toContain('{!useWorkbench && <ModuleAccess icon="candidate"')
  })
  it('la mesa muestra «Lo de hoy», grupos y tarjetas con íconos', () => {
    const page = read('WorkbenchPage.tsx')
    expect(page).toContain('Lo de hoy')
    expect(page).toContain('workbench-icon')
    for (const group of ['Personas', 'Ceremonias y régimen', 'Finanzas', 'Secretaría y Orden']) expect(app).toContain(`group: '${group}'`)
  })
})
