import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import LodgeTreasuryPanel from './LodgeTreasuryPanel'
import { PmgmApiClient } from './api/pmgmApi'

const api = () => new PmgmApiClient({ useMocks: true })

describe('lodge treasury panel — segregación de funciones', () => {
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
  })
})
