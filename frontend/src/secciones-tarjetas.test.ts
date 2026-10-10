import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const read = (file: string) => readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')

describe('Secciones como tarjetas (opción C, PO 09-10-2026)', () => {
  const kit = read('actionKit.tsx')
  it('WorkspaceTabs tiene modo tarjetas con «← Volver a …» y sin pestañas en ese modo', () => {
    expect(kit).toContain('export type WorkspaceHub<T extends string>')
    expect(kit).toContain('section-hub-card')
    expect(kit).toContain('← Volver a {hub.title}')
  })
  it('lo usan Gestión Logial, Gran Secretaría, Hospitalaria (Gran y del Taller) y Parámetros del sistema', () => {
    for (const [file, title] of [['LodgeManagementPage.tsx', 'Gestión Logial'], ['GrandSecretariatPage.tsx', 'Gran Secretaría'], ['HospitalariaPage.tsx', 'Gran Hospitalaria'], ['HospitalariaPage.tsx', 'Hospitalaria del Taller'], ['SystemConfigurationPage.tsx', 'Parámetros del sistema']]) {
      expect(read(file)).toContain(`hub={{ title: '${title}'`)
    }
  })
  it('un acceso directo a una sección (initialTab / docencia) abre la sección y no las tarjetas', () => {
    expect(read('LodgeManagementPage.tsx')).toContain('startOpen: Boolean(initialTab) || focusInstructions')
  })
  it('el modo tarjetas oculta el contenido del módulo hasta elegir y usa todo el ancho en PC', () => {
    expect(read('action-kit.css')).toContain('.workspace-tabs.is-hub ~ * { display: none !important; }')
    expect(read('desktop-density.css')).toContain('nav.workspace-tabs:is(.is-hub, .is-open-section)')
  })
  it('las tarjetas muestran íconos institucionales y cada módulo con tarjetas los asigna', () => {
    expect(kit).toContain('section-hub-icon')
    expect(kit).toContain('InstitutionalIcon')
    for (const file of ['LodgeManagementPage.tsx', 'GrandSecretariatPage.tsx', 'HospitalariaPage.tsx', 'SystemConfigurationPage.tsx']) {
      expect(read(file)).toMatch(/icon: ?'/)
    }
  })
})
