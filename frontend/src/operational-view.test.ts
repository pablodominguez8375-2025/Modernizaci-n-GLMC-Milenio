import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/* PMGM-UX patrón «vista operativa» (aprobado por el PO 02-10-2026):
   ningún formulario de acción queda abierto en la vista; todos se abren desde un botón en panel (ActionDrawer). */
const read = (file: string) => readFileSync(new URL(`./${file}`, import.meta.url), 'utf8')

function formsOutsideDrawer(source: string) {
  let outside = 0
  let depth = 0
  const tokens = source.match(/<ActionDrawer[\s>]|<\/ActionDrawer>|<form[\s>]/g) ?? []
  for (const token of tokens) {
    if (token.startsWith('<ActionDrawer')) depth += 1
    else if (token === '</ActionDrawer>') depth -= 1
    else if (depth === 0) outside += 1
  }
  return outside
}

describe('PMGM-UX vista operativa · formularios solo en panel', () => {
  for (const file of ['HospitalariaPage.tsx', 'CeremoniesPage.tsx', 'LodgeTreasuryPanel.tsx', 'LodgeReceiptAdjustmentsPanel.tsx', 'MemberWorkPapersPanel.tsx', 'RegularityPage.tsx', 'RegularityManagement.tsx', 'CeremonyRightsPage.tsx', 'LodgeInstructionPage.tsx', 'SystemOperationsPanel.tsx']) {
    it(`${file} no deja formularios abiertos`, () => {
      expect(formsOutsideDrawer(read(file))).toBe(0)
    })
  }

  it('Gestor Documental abre «Nueva colección» y «Registrar documento» en panel', () => {
    const source = read('DocumentManagementPage.tsx')
    expect(source).toMatch(/<ActionDrawer label="Nueva colección"[^>]*><CollectionForm /)
    expect(source).toMatch(/<ActionDrawer label="Registrar documento"[^>]*><DocumentForm /)
  })

  it('Hospitalaria abre movimiento y rendición en panel y deja visible «Enviar»', () => {
    const source = read('HospitalariaPage.tsx')
    expect(source).toContain('<ActionDrawer label="Registrar movimiento"')
    expect(source).toContain('<ActionDrawer label="Preparar rendición"')
    expect(source).toContain('<ActionDrawer label="Cambiar tarifa"')
  })

  it('Control de miembros: filtros secundarios plegados y observar/rechazar en panel', () => {
    const source = read('InternalAffairsMemberControlPage.tsx')
    expect(source).toContain('<details className="internal-control-more"><summary>Más filtros')
    expect(source).toContain('<ConfirmAction label="Aprobar y actualizar Cuadro"')
    expect(source).toContain('<ActionDrawer label="Observar o rechazar"')
  })
})

describe('PMGM-UX vista operativa · Calidad de datos con acción en lote', () => {
  const source = read('InternalAffairsDataQualityPage.tsx')
  it('permite seleccionar hallazgos y abrir casos en lote con confirmación', () => {
    expect(source).toContain('className="data-quality-bulk-bar"')
    expect(source).toContain('openSelectedCases')
    expect(source).toMatch(/<ConfirmAction label=\{bulkBusy \? 'Abriendo casos…'/)
    expect(source).toContain('caseApi.openCase({ detectionAsOf: asOf, issue: item })')
  })
})
