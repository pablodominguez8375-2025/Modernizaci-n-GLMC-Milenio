import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const read = (file: string) => readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')

describe('Menú simplificado (aprobado por el PO 09-10-2026)', () => {
  const app = read('App.tsx')
  const groups = read('navGroups.ts')
  const css = read('common-views.css')

  it('los grupos con una sola opción no muestran título ni flecha y «Mi espacio» solo se pliega si el menú no cabe', () => {
    expect(groups).toContain("group.items.length === 1 && group.name !== 'Mi espacio'")
    expect(groups).toContain("classList.add('is-solo')")
    expect(groups).toContain("const order = [...(mine ? [mine] : [])")
    expect(css).toContain('.sidebar .nav-section.is-solo { display: none !important; }')
  })

  it('las opciones propias del Taller llevan «del Taller» para no confundirse con las de la Orden', () => {
    for (const label of ['Tesorería del Taller', 'Secretaría del Taller', 'Docencia del Taller']) expect(app).toContain(`label="${label}"`)
  })

  it('Reportería, Control de miembros y Calidad de datos se reúnen en «Informes» con tarjetas, sin pestañas', () => {
    expect(app).toContain('label="Informes"')
    expect(app).toContain('← Volver a Informes')
    expect(read('ReportsHub.tsx')).toContain('reports-hub-card')
  })
})
