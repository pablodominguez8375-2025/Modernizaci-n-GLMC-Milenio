import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('./home-consistency.css', import.meta.url), 'utf8')
const dashboard = readFileSync(new URL('./DashboardPage.tsx', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')

describe('PMGM-UX-C+D · Inicio uniforme y consistencia', () => {
  it('se carga después de las hojas de tema', () => {
    expect(main.indexOf("./home-consistency.css")).toBeGreaterThan(main.indexOf("./accessibility.css"))
  })

  it('Inicio usa franja de saludo, 4 accesos de igual tamaño y bloques de igual altura', () => {
    expect(dashboard).toContain('className="home-strip"')
    expect(dashboard.match(/<Shortcut /g)?.length).toBe(4)
    expect(css).toMatch(/\.home-grid\s*\{[^}]*align-items:\s*stretch/s)
    expect(css).toMatch(/\.home-grid\.has-inbox\s*\{[^}]*repeat\(3, minmax\(0, 1fr\)\)/s)
  })

  it('agenda y avisos muestran máximo 4 ítems con «Ver todo» y no repiten indicadores grandes', () => {
    expect(dashboard).toContain('upcoming.slice(0, 4)')
    expect(dashboard.match(/>Ver todo</g)?.length).toBe(2)
    expect(dashboard).not.toContain('MetricCard')
    expect(dashboard).not.toContain('executive-hero')
  })

  it('las etiquetas superiores no usan mayúsculas diminutas', () => {
    expect(css).toMatch(/\.eyebrow,[^{]*\{[^}]*text-transform:\s*none[^}]*letter-spacing:\s*0[^}]*font-size:\s*0\.875rem/s)
  })

  it('los estados vacíos explican qué hacer', () => {
    expect(dashboard).toContain('aparecerá aquí')
    expect(dashboard).toContain('Estás al día')
  })

  it('usa solo unidades relativas en tamaños de letra', () => {
    expect(css).not.toMatch(/font-size:\s*\d+px/)
  })
})
