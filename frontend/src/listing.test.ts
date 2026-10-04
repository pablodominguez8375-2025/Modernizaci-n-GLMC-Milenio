import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { buildCsv } from './listing'

const read = (file: string) => readFileSync(new URL(file, import.meta.url), 'utf8')

describe('PMGM-UX-003 · listados y vistas operativas', () => {
  it('genera CSV compatible con Excel en español: BOM, separador «;» y escapado', () => {
    const csv = buildCsv([{ header: 'Nombre', value: (row: { n: string }) => row.n }, { header: 'Nota', value: () => 'a;b "c"' }], [{ n: 'Ana' }], 'pie')
    expect(csv.startsWith('\uFEFFNombre;Nota\r\n')).toBe(true)
    expect(csv).toContain('Ana;"a;b ""c"""')
    expect(csv.endsWith('\r\n\r\npie')).toBe(true)
  })

  it('Fichas de miembros usa la barra común, carga incremental, exportación y vista tabla por permiso', () => {
    const page = read('./MemberDirectoryPage.tsx')
    expect(page).toContain('<ListingToolbar')
    expect(page).toContain('<LoadMore')
    expect(page).toContain('onExport={canExport ? exportMembers : undefined}')
    expect(page).toContain('allowTableView && viewMode === \'table\'')
    const app = read('./App.tsx')
    expect(app).toContain('canExport={canSecretariat || canManageLodgeSecretariat || canConfigureSystem}')
    expect(app).toContain('allowTableView={canSecretariat || canConfigureSystem}')
  })

  it('la tabla de miembros tiene encabezado fijo y scroll propio', () => {
    const css = read('./listing.css')
    expect(css).toMatch(/\.listing-table thead th\s*\{[^}]*position:\s*sticky/s)
    expect(css).toMatch(/\.listing-table-wrap\s*\{[^}]*overflow-x:\s*auto/s)
  })

  it('Parámetros del sistema usa un solo menú de secciones accesible (WorkspaceTabs, 04-10-2026)', () => {
    const page = read('./SystemConfigurationPage.tsx')
    expect(page).toContain('<WorkspaceTabs label="Secciones de Sistema"')
    expect(page).toContain('<WorkspacePanel id="settings"')
    expect(page).not.toContain('system-page-tabs')
  })

  it('Gestión Logial usa pestañas (PMGM-UX-004) y Carga de insinuados ofrece índice de secciones', () => {
    expect(read('./LodgeManagementPage.tsx')).toContain('<WorkspaceTabs')
    expect(read('./CandidateWorkshopIntakePage.tsx')).toContain('<SectionIndex')
  })

  it('el sello de Gestión Logial muestra el número del Taller y no una letra fija', () => {
    const page = read('./LodgeManagementPage.tsx')
    expect(page).not.toContain('<span className="lodge-seal">M</span>')
    expect(page).toContain('{lodgeSealText}')
  })

  it('el selector de Taller se reemplaza por texto cuando el perfil opera un solo Taller', () => {
    for (const file of ['./MemberDirectoryPage.tsx', './LodgeManagementPage.tsx', './LodgeTreasuryPage.tsx']) expect(read(file)).toContain('single-organization')
  })
})
