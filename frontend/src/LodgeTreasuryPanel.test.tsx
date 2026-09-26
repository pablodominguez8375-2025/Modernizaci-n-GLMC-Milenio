import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import LodgeTreasuryPanel, { csvSafe } from './LodgeTreasuryPanel'
import { PmgmApiClient } from './api/pmgmApi'

const api = () => new PmgmApiClient({ useMocks: true })

describe('lodge treasury panel — segregación de funciones', () => {
  it('prevents formula execution from descriptive CSV fields without changing negative numeric amounts', () => {
    expect(csvSafe(' =HYPERLINK("https://example.test")', 4)).toBe("' =HYPERLINK(\"\"https://example.test\"\")")
    expect(csvSafe('-12500', 8)).toBe('-12500')
  })

  it('states the segregation rule: Tesorería registra, Venerable Maestro autoriza, Gran Tesorería solo concilia', () => {
    const html = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage canApproveExpenses={false} section="summary" />
    )
    expect(html).toContain('Tesorería registra; el Venerable Maestro autoriza los egresos. Gran Tesorería sólo recibe y concilia el Cuadro mensual.')
  })

  it('hides the expense-creation form for a read-only reviewer (canManage=false), but still shows the authorization ledger', () => {
    const reviewerOnly = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage={false} canApproveExpenses section="movements" />
    )
    expect(reviewerOnly).not.toContain('Registrar egreso')
    expect(reviewerOnly).toContain('Egresos y autorizaciones')

    const withManage = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage canApproveExpenses={false} section="movements" />
    )
    expect(withManage).toContain('Registrar egreso')
  })

  it('does not render the collection/cobranza tools outside canManage, even if section is requested as collection', () => {
    // A read-only Venerable Maestro session should never see the
    // cuotas/cobranza form regardless of which section prop is passed.
    const html = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage={false} canApproveExpenses section="collection" />
    )
    expect(html).not.toContain('Registrar pago de cuota')
  })

  it('renders the requested collection row columns and its direct payment action', () => {
    const html = renderToStaticMarkup(<LodgeTreasuryPanel api={api()} organizationId="org-1" canManage section="collection" />)
    expect(html).toContain('Saldo adeudado')
    expect(html).toContain('Semáforo')
    expect(html).toContain('Abre el registro desde la fila')
  })

  it('shows separate cash income and configurable category fields to the local treasurer', () => {
    const html = renderToStaticMarkup(<LodgeTreasuryPanel api={api()} organizationId="org-1" canManage section="movements" />)
    expect(html).toContain('Registrar otro ingreso')
    expect(html).toContain('Tipo de ingreso')
    expect(html).toContain('Enviar a autorización')
  })

  it('shows treasury reconciliation and CSV export in reports', () => {
    const html = renderToStaticMarkup(<LodgeTreasuryPanel api={api()} organizationId="org-1" canManage section="reports" />)
    expect(html).toContain('Cuadratura de caja')
    expect(html).toContain('Exportar CSV')
    expect(html).toContain('trazabilidad UTC de registro/autorización')
    expect(html).toContain('Guardar conciliación')
  })

  it('keeps repeated cash reconciliations as separate audit snapshots', async () => {
    const client=api()
    const first=await client.saveLodgeTreasuryReconciliation('org-1',{from:'2026-09-01',to:'2026-09-30',observedBalance:12000,evidenceReference:'arqueo-01'})
    const second=await client.saveLodgeTreasuryReconciliation('org-1',{from:'2026-09-01',to:'2026-09-30',observedBalance:12500,evidenceReference:'arqueo-02'})
    const report=await client.getLodgeTreasuryReport('org-1','2026-09-01','2026-09-30')
    expect(first.id).not.toBe(second.id)
    expect(report.reconciliationHistory).toHaveLength(2)
    expect(report.reconciliationHistory?.map(x=>x.evidenceReference)).toEqual(['arqueo-02','arqueo-01'])
    expect(report.reconciliationHistory?.[0].difference).toBe(12500-report.closingBalance)
  })
})
