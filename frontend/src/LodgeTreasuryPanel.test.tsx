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
    expect(html).not.toContain('Registrar recepción e imputaciones')
    expect(html).not.toContain('Imputar saldo a favor')
  })

  it('renders multi-period collection and credit-allocation tools', () => {
    const html = renderToStaticMarkup(<LodgeTreasuryPanel api={api()} organizationId="org-1" canManage section="collection" />)
    expect(html).toContain('Saldo adeudado')
    expect(html).toContain('Semáforo')
    expect(html).toContain('Registrar recepción e imputaciones')
    expect(html).toContain('Imputar a períodos con saldo')
    expect(html).toContain('Crédito disponible')
    expect(html).toContain('Imputar saldo a favor')
    expect(html).toContain('Fecha de recepción del pago')
    expect(html).toContain('La fecha registra cuándo se recibió el dinero')
    expect(html).toContain('sin crear otro ingreso')
  })

  it('records one receipt across periods, exposes the remainder as credit, and does not duplicate cash when later allocated', async () => {
    const client = api()
    const charges = await client.getLodgeTreasuryCharges('org-1', 2026, 9)
    const memberCharges = charges.items.filter(item => item.memberId === 'member-demo-002' || item.memberId === 'member-demo-003')
    const partiallyPaid = memberCharges.find(item => item.memberId === 'member-demo-002')!
    const nextPeriod = (await client.getLodgeTreasuryCharges('org-1',2026,10)).items.find(item => item.memberId === partiallyPaid.memberId)!

    const receipt = await client.recordLodgeMemberReceipt('org-1', {
      memberId: partiallyPaid.memberId,
      amount: 20000,
      paymentMethod: 'transfer',
      paymentDate: '2026-09-29',
      reference: 'TRX-MULTI-DEMO',
      idempotencyKey: 'receipt-multiperiod-test',
      currency: 'CLP',
      allocations: [{ chargeId: partiallyPaid.id, amount: 7000 }],
    })
    expect(receipt.allocatedAmount).toBe(7000)
    expect(receipt.unappliedBalance).toBe(13000)

    const credit = await client.getLodgeMemberReceipts('org-1', true)
    expect(credit.items.find(item => item.id === receipt.id)?.unappliedBalance).toBe(13000)
    const incomeBeforeReallocation = (await client.getLodgeTreasuryReport('org-1', '2026-09-01', '2026-09-30')).income
    await client.allocateLodgeMemberReceipt(receipt.id, [{ chargeId: nextPeriod.id, amount: 3000 }])

    const after = await client.getLodgeTreasuryReport('org-1', '2026-09-01', '2026-09-30')
    expect(after.income).toBe(incomeBeforeReallocation)
    expect(after.movements.filter(item => item.transactionId === receipt.id)).toHaveLength(1)
    expect(after.movements.find(item => item.transactionId === receipt.id)?.amount).toBe(20000)
    expect((await client.getLodgeMemberReceipts('org-1', true)).items.find(item => item.id === receipt.id)?.unappliedBalance).toBe(10000)
  })

  it('does not offer Past Active as an ordinary monthly fee', () => {
    const html = renderToStaticMarkup(<LodgeTreasuryPanel api={api()} organizationId="org-1" canManage section="settings" />)
    expect(html).not.toContain('Past Activo · aporte GT $0')
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
