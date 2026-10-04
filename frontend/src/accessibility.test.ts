import { readdirSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const dir = new URL('./', import.meta.url)
const cssFiles = readdirSync(dir).filter(name => name.endsWith('.css'))
const read = (name: string) => readFileSync(new URL(`./${name}`, import.meta.url), 'utf8')
const a11y = read('accessibility.css')
const app = read('App.tsx')
const main = read('main.tsx')
const nav = read('role-navigation.css')

describe('PMGM-UX-004 legibilidad y menú claro', () => {
  it('ninguna hoja de estilos fija tamaños de letra en px (el tamaño del navegador debe funcionar)', () => {
    for (const name of cssFiles) {
      expect(read(name), name).not.toMatch(/font-size:\s*[^;}]*\d+px/)
      expect(read(name), name).not.toMatch(/font:\s*[^;}]*\d+px/)
    }
  })

  it('no hay letras menores a 12 px', () => {
    for (const name of cssFiles) {
      for (const match of read(name).matchAll(/font-size:\s*([0-9.]+)rem/g)) {
        expect(Number(match[1]) * 16, `${name}: ${match[0]}`).toBeGreaterThanOrEqual(12)
      }
    }
  })

  it('ofrece Normal, Grande y Muy grande y lo aplica antes de pintar', () => {
    expect(a11y).toMatch(/html\[data-text-size='large'\]\s*\{\s*font-size:\s*112\.5%/)
    expect(a11y).toMatch(/html\[data-text-size='xlarge'\]\s*\{\s*font-size:\s*125%/)
    expect(main.indexOf('applyTextSize(readTextSize())')).toBeLessThan(main.indexOf('createRoot(root).render('))
    expect(main.indexOf("import './accessibility.css'")).toBeGreaterThan(main.indexOf("import './action-kit.css'"))
  })

  it('menú de 16 px, opciones de 48 px y grupos sin mayúsculas diminutas', () => {
    expect(a11y).toMatch(/\.sidebar \.nav-item\s*\{[^}]*min-height:\s*48px[^}]*font-size:\s*1rem/s)
    expect(a11y).toMatch(/\.sidebar \.nav-section\s*\{[^}]*letter-spacing:\s*0[^}]*text-transform:\s*none/s)
  })

  it('usa grupos con nombres simples y una ayuda por opción', () => {
    for (const group of ['Mi espacio', 'Trámites', 'Mi Taller', 'Biblioteca y documentos']) expect(app).toContain(`<div className="nav-section">${group}</div>`)
    expect(app).toContain("'Insinuaciones e Iniciación': 'Publicaciones y seguimiento de la iniciación según tus permisos'")
    expect(app).toContain('<span className="nav-text" data-hint={hint}>{label}</span>')
  })

  it('tablet y celular usan el mismo patrón: barra inferior y hoja de menú (sin grilla superior)', () => {
    expect(nav).not.toContain('@media (max-width: 720px)')
    expect(nav).toMatch(/@media \(max-width: 980px\)\s*\{[^@]*\.sidebar\s*\{\s*display:\s*none/s)
  })
})
