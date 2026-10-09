import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const nav = readFileSync(new URL('./navGroups.ts', import.meta.url), 'utf8')
const css = readFileSync(new URL('./common-views.css', import.meta.url), 'utf8')

/* Pedido del PO 08-10-2026: el menú del Administrador en PC se veía mal (grupos sin plegar y opciones fuera de pantalla). */
describe('PMGM-UX menú lateral del Administrador en PC', () => {
  it('vuelve a aplicar los grupos plegables cuando el menú se redibuja (cambio de perfil o llegada de permisos)', () => {
    expect(nav).toContain('new MutationObserver')
    expect(nav).toContain("observer.observe(nav, { childList: true })")
    expect(nav).toContain('observer.disconnect()')
  })

  it('el menú es compacto en PC y mantiene objetivos de 2,5 rem y letra de 16 px', () => {
    expect(css).toMatch(/\.workspace:not\(\.is-sidebar-collapsed\) \.sidebar \.nav-item \{[^}]*min-height: 2\.5rem/)
    expect(css).not.toMatch(/\.workspace:not\(\.is-sidebar-collapsed\) \.sidebar \.nav-item \{[^}]*font-size/)
  })
})
