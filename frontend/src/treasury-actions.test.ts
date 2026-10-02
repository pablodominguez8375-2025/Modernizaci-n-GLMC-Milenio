import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const panel = readFileSync(new URL('./LodgeTreasuryPanel.tsx', import.meta.url), 'utf8')
const adjustments = readFileSync(new URL('./LodgeReceiptAdjustmentsPanel.tsx', import.meta.url), 'utf8')
const statement = readFileSync(new URL('./TreasuryStatementPage.tsx', import.meta.url), 'utf8')
const css = readFileSync(new URL('./lodgeTreasury.css', import.meta.url), 'utf8')

describe('PMGM-UX Tesorería · lista primero y acciones bajo demanda', () => {
  it('ningún formulario de Tesorería del Taller queda abierto: todos van dentro de un panel', () => {
    const forms = panel.match(/<form /g)?.length ?? 0
    const drawers = panel.match(/<ActionDrawer /g)?.length ?? 0
    expect(forms).toBe(7)
    expect(drawers).toBe(forms)
    expect(adjustments).toContain('<ActionDrawer label="Corregir o anular un registro"')
  })

  it('las acciones que no se pueden deshacer piden confirmación', () => {
    for (const label of ['Registrar pago', 'Imputar saldo a favor', 'Registrar egreso', 'Editar parámetros contables', 'Configurar cuota mensual', 'Registrar cuadratura de caja']) {
      const start = panel.indexOf(`<ActionDrawer label="${label}"`)
      expect(start).toBeGreaterThan(-1)
      expect(panel.slice(start, panel.indexOf('>', start))).toContain('confirmMessage=')
    }
    expect(statement).toContain('<ConfirmAction label="Enviar a Gran Tesorería"')
    expect(statement).toContain('<ConfirmAction label="Confirmar recepción bancaria y conciliar"')
    expect(statement).toContain('<ActionDrawer label="Registrar transferencia o depósito"')
  })

  it('la fila del hermano abre el panel de pago y los filtros del libro quedan contraídos', () => {
    expect(panel).toContain('setPaymentOpen(true)')
    expect(panel).toContain('open={paymentOpen} onOpenChange={setPaymentOpen}')
    expect(panel).toContain('<details className="regularity-form treasury-report-filters treasury-movement-filters treasury-filters-toggle">')
  })

  it('usa unidades relativas', () => {
    const added = css.slice(css.indexOf('PMGM-UX Tesorería'))
    expect(added).not.toMatch(/\d+px/)
  })
})
