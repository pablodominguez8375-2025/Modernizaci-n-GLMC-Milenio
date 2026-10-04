import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const css = readFileSync(new URL('./common-views.css', import.meta.url), 'utf8')
const groups = readFileSync(new URL('./navGroups.ts', import.meta.url), 'utf8')
const textSize = readFileSync(new URL('./TextSizeControl.tsx', import.meta.url), 'utf8')

describe('PMGM-UX menús simples en celular y PC (aprobado por el PO 03-10-2026)', () => {
  it('la hoja «Menú» del celular no repite lo que está en la barra inferior y usa tarjetas en 2 columnas', () => {
    expect(app).toContain('data-in-tabbar="true" onClick={() => setView(\'dashboard\')}')
    expect(app).toContain('label="Agenda" inTabbar')
    expect(app).toContain('label="Avisos" inTabbar')
    expect(app).toContain('label="Mi ficha" inTabbar={!operational}')
    expect(css).toMatch(/\.sidebar\.is-open \.nav-item\[data-in-tabbar="true"\],\s*\.sidebar\.is-open \.nav-section\[data-in-tabbar="true"\] \{ display: none; \}/)
    expect(css).toMatch(/\.sidebar\.is-open \{[^}]*grid-template-columns: repeat\(2, minmax\(0, 1fr\)\)/s)
  })

  it('en PC el tamaño de letra está en la cabecera y los grupos se pliegan solo si el menú no cabe', () => {
    expect(app).toContain('{effectiveProfile && <TextSizeMenu />}')
    expect(textSize).toContain('export function TextSizeMenu()')
    expect(css).toMatch(/\.sidebar \.text-size-control\.in-sidebar \{ display: none; \}/)
    expect(groups).toContain('if (contentHeight(nav) <= available) return')
    expect(groups).toContain('for (const group of [...groups].reverse())')
  })
})
