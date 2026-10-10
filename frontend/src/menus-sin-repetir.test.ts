import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
const read = (file: string) => readFileSync(new URL(file, import.meta.url), 'utf8')
const userMenu = readFileSync(new URL('./UserMenu.tsx', import.meta.url), 'utf8')

describe('PMGM-UX menús sin funciones repetidas (aprobado por el PO 03-10-2026)', () => {
  it('usa un solo nombre por función: Agenda y Avisos', () => {
    expect(app).toContain('<ModuleAccess icon="calendar" label="Agenda"')
    expect(app).toContain('<ModuleAccess icon="bell" label="Avisos"')
    expect(app).not.toMatch(/label="Mi calendario"|label="Notificaciones"/)
  })

  it('el menú de usuario no repite opciones del menú', () => {
    expect(userMenu).not.toContain('Mi calendario')
    expect(userMenu).not.toContain('TextSizeControl')
  })

  it('agrupa módulos relacionados como pestañas de una sola opción', () => {
    expect(read('./SystemConfigurationPage.tsx')).toContain('label="Secciones de Sistema"')
    expect(app).toContain('bootstrapSlot={canBootstrap ? <BootstrapPage')
    expect(app).toContain('label="Secciones de Calidad de datos"')
    expect(app).toContain("initialTab={view === 'lodgeProfile' ? 'ficha' : undefined}")
    expect(read('./LodgeManagementPage.tsx')).toContain("{ id: 'ficha' as const, label: 'Ficha del Taller', icon: 'lodge' as const }")
    expect(app).not.toContain('<ModuleAccess icon="check" label="Cola de corroboración"')
  })

  it('reúne las tres vistas de iniciación en una sola entrada y búsqueda', () => {
    const sidebar = app.slice(app.indexOf('id="navegacion-principal"'), app.indexOf('<main className="content"'))
    expect(sidebar.match(/label="Insinuaciones e Iniciación"/g)).toHaveLength(1)
    expect(sidebar).not.toMatch(/label="Circuito de Iniciación"|label="Insinuados publicados"|label="Carga de insinuados"|label="Revisión de insinuados"/)
    expect(app).toContain("['candidates', 'candidateProfile', 'initiationCircuit']")
    const search = app.slice(app.indexOf('const searchEntries'), app.indexOf('const changeDemoProfile'))
    expect(search.match(/'Insinuaciones e Iniciación'/g)).toHaveLength(1)
    expect(search).not.toContain("'Procesos', 'ceremony'")
  })

  it('mantiene publicados para todos y pestañas privadas sólo por capacidades existentes', () => {
    expect(app).toContain('isInitiationView && (canCandidateProfile || canCeremonies) && <WorkspaceTabs')
    expect(app).toContain("{ id: 'candidates' as View, label: 'Publicados' }")
    expect(app).toContain("...(canCandidateProfile ? [{ id: 'candidateProfile' as View, label: canSecretariat ? 'Revisión' : 'Carga'")
    expect(app).toContain("...((canCandidateProfile || canCeremonies) && canView('initiationcircuit') ? [{ id: 'initiationCircuit' as View")
    expect(app).toContain("view === 'candidateProfile' && canCandidateProfile")
    expect(app).toContain("view === 'initiationCircuit' && (canCandidateProfile || canCeremonies)")
  })
})
