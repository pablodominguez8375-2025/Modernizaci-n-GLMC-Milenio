import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { normalizeSearch, rankEntries, type SearchEntry } from './GlobalSearch'
import { initialsFor } from './UserMenu'

const css = readFileSync(new URL('./header-p2.css', import.meta.url), 'utf8')
const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const main = readFileSync(new URL('./main.tsx', import.meta.url), 'utf8')
const noop = () => undefined
const entries: SearchEntry[] = [
  { id: 'lodgeTreasury', label: 'Tesorería', section: 'Taller', icon: 'treasury', keywords: 'egresos cuotas', onSelect: noop },
  { id: 'treasury', label: 'Gran Tesorería', section: 'Gestión institucional', icon: 'treasury', onSelect: noop },
  { id: 'calendar', label: 'Mi calendario', section: 'Portal del Hermano', icon: 'calendar', keywords: 'agenda tenidas', onSelect: noop },
]

describe('PMGM-UX-003 cabecera P2', () => {
  it('busca sin tildes y prioriza coincidencias al inicio del nombre', () => {
    expect(normalizeSearch(' TESORERÍA ')).toBe('tesoreria')
    expect(rankEntries(entries, 'tesoreria').map(entry => entry.id)).toEqual(['lodgeTreasury', 'treasury'])
    expect(rankEntries(entries, 'egresos').map(entry => entry.id)).toEqual(['lodgeTreasury'])
    expect(rankEntries(entries, 'tenidas').map(entry => entry.id)).toEqual(['calendar'])
    expect(rankEntries(entries, '   ')).toEqual([])
  })

  it('genera iniciales del usuario sin el sufijo de demostración', () => {
    expect(initialsFor('Venerable Maestro · Demostración')).toBe('VM')
    expect(initialsFor('Hermano')).toBe('H')
  })

  it('separa los controles de demo en una franja que solo existe con datos ficticios', () => {
    expect(app).toContain('{api.useMocks && <div className="demo-strip"')
    expect(app).toContain("'UI QA v0.73'")
    expect(app).not.toMatch(/<span className="demo-badge">QA demostración<\/span><DemoProfileSwitcher[^\n]*<\/div>\s*<\/header>/)
  })

  it('la búsqueda solo ofrece módulos permitidos al perfil', () => {
    expect(app).toContain('allowed ? [{ id, label, section, icon, keywords, onSelect: () => setView(id) }] : []')
    expect(app).toContain('onSearchMembers={canMembers ? openMembersSearch : undefined}')
  })

  it('colapsa la barra lateral solo en escritorio y la búsqueda no aparece en celular', () => {
    expect(css).toMatch(/@media \(min-width: 981px\)\s*\{\s*\.workspace\.is-sidebar-collapsed\s*\{\s*grid-template-columns: 76px/s)
    expect(css).toMatch(/@media \(max-width: 720px\)[^@]*\.global-search\s*\{\s*display: none/s)
    expect(main.indexOf("import './header-p2.css'")).toBeGreaterThan(main.indexOf("import './role-navigation.css'"))
  })
})
