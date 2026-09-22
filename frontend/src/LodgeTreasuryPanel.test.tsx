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

  it('hides the expense-creation form for a read-only reviewer (canManage=false), but still shows the ledger', () => {
    const reviewerOnly = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage={false} canApproveExpenses section="expenses" />
    )
    expect(reviewerOnly).not.toContain('Registrar egreso')
    expect(reviewerOnly).toContain('Egresos y autorizaciones')

    const withManage = renderToStaticMarkup(
      <LodgeTreasuryPanel api={api()} organizationId="org-1" canManage canApproveExpenses={false} section="expenses" />
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
})
