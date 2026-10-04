/// <reference types="node" />

import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('./role-navigation.css', import.meta.url), 'utf8')
const memberPortalCss = readFileSync(new URL('./member-portal.css', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')
const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const homeCss = readFileSync(new URL('./home-consistency.css', import.meta.url), 'utf8')

describe('PMGM-UX-002 contrato de navegación por rol', () => {
  it('se carga después de la capa institucional', () => {
    expect(main.indexOf("import './role-navigation.css'")).toBeGreaterThan(main.indexOf("import './institutional-theme.css'"))
  })

  it('en móvil oculta el panel de 158 px y abre el menú completo como hoja con secciones', () => {
    const mobile = css.slice(css.indexOf('@media (max-width: 980px)'))
    expect(mobile).toMatch(/\.sidebar\s*\{\s*display:\s*none/)
    expect(mobile).toMatch(/\.sidebar\.is-open\s*\{[^}]*position:\s*fixed[^}]*overflow-y:\s*auto/s)
    expect(mobile).toMatch(/\.sidebar\.is-open \.nav-section\s*\{[^}]*display:\s*block/s)
    expect(mobile).toMatch(/\.mobile-tabbar\s*\{[^}]*position:\s*fixed[^}]*repeat\(5/s)
  })

  it('mantiene objetivos táctiles de al menos 44 px y texto de al menos 12 px', () => {
    const sizes = [...css.matchAll(/font-size:\s*(\d+)px/g)].map(match => Number(match[1]))
    expect(Math.min(...sizes)).toBeGreaterThanOrEqual(12)
    expect(css).toMatch(/\.mobile-tab\s*\{[^}]*min-height:\s*56px/s)
    expect(css).toMatch(/\.sidebar\.is-open \.nav-item\s*\{[^}]*min-height:\s*48px/s)
  })

  it('usa solo tokens institucionales para los acentos', () => {
    expect(css).toContain('var(--brand-navy)')
    expect(css).toContain('var(--brand-gold)')
    expect(css).not.toMatch(/font-family:(?!\s*var\()/)
  })

  it('pinta los contadores del menú sin alterar el texto de la opción', () => {
    expect(css).toMatch(/\.nav-item\[data-badge\]::after\s*\{[^}]*content:\s*attr\(data-badge\)/s)
    expect(app).toContain('data-badge={count}')
  })

  it('entra a Inicio y no a Mi ficha', () => {
    expect(app).toContain("useState<View>('dashboard')")
  })

  it('el número del Taller queda dentro del sello en Mi ficha', () => {
    expect(memberPortalCss).toMatch(/\.member-lodge-line \.member-lodge-seal\s*\{[^}]*display:\s*grid[^}]*place-items:\s*center[^}]*font-size:\s*1\.375rem/s) // PMGM-UX-004: rem (22 px)
  })
  it('en perfiles operativos «Mis pendientes» va antes de los indicadores', () => {
    const dashboard = readFileSync(new URL('./DashboardPage.tsx', import.meta.url), 'utf8')
    // PMGM-UX-C: accesos compactos arriba y «Mis pendientes» como primer bloque de la grilla.
    expect(dashboard.indexOf('home-shortcuts')).toBeLessThan(dashboard.indexOf('id="mis-pendientes"'))
    expect(dashboard.indexOf('id="mis-pendientes"')).toBeLessThan(dashboard.indexOf('home-agenda-titulo'))
  })

  it('en móvil no duplica la campana de la cabecera y compacta los indicadores', () => {
    expect(css).toMatch(/\.topbar \.topbar-icon-button\s*\{[^}]*display:\s*none/s)
    expect(homeCss).toMatch(/\.home-shortcuts\.metric-grid\s*\{[^}]*repeat\(3, minmax\(0, 1fr\)\)/s)
  })
})
