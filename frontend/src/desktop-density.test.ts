import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync(new URL('./desktop-density.css', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')
const calendar = readFileSync(new URL('./CalendarPage.tsx', import.meta.url), 'utf8')
const notifications = readFileSync(new URL('./NotificationsPage.tsx', import.meta.url), 'utf8')

describe('PMGM-UX densidad cómoda en escritorio', () => {
  it('se carga al final y solo actúa desde 1280 px', () => {
    expect(main.indexOf('./desktop-density.css')).toBeGreaterThan(main.indexOf('./common-views.css'))
    expect(css).toMatch(/@media \(min-width: 80rem\)/)
  })

  it('fase 1: márgenes de 2 rem, ancho de hasta 105 rem y encabezado compacto', () => {
    expect(css).toMatch(/\.content\s*\{[^}]*padding:\s*1\.5rem 2rem 3rem;[^}]*max-width:\s*105rem/s)
    expect(css).toMatch(/\.content h1\s*\{[^}]*font-size:\s*1\.75rem/s)
    expect(css).toMatch(/\.dashboard-page \.home-grid\s*\{\s*align-items:\s*start/s)
  })

  it('fase 2: Agenda y Avisos en dos zonas (lista + panel lateral de filtros y resumen)', () => {
    for (const page of [calendar, notifications]) {
      expect(page).toContain('<div className="workspace-split">')
      expect(page.indexOf('className="workspace-side"')).toBeLessThan(page.indexOf('className="workspace-main"'))
    }
    expect(css).toMatch(/\.workspace-split\s*\{[^}]*grid-template-columns:\s*minmax\(0, 1fr\) minmax\(17rem, 22rem\)/s)
  })

  it('sin tamaños de letra en px', () => {
    expect(css).not.toMatch(/font-size:\s*\d+px/)
  })
})
