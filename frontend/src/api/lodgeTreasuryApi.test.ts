import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

describe('Tesorería del Taller en demostración', () => {
  it('separa cobro, obligación a Gran Tesorería y saldo mensual', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const plans = await api.getLodgeFeePlans(organizationId)
    expect(plans.items.map(item => item.feeType)).toEqual(['normal', 'student', 'senior'])
    expect(plans.items[0].memberAmount).toBeGreaterThan(plans.items[0].grandTreasuryAmount)

    const summary = await api.generateLodgeCharges(organizationId, 2026, 9)
    expect(summary.members).toBe(22)
    expect(summary.memberExpected).toBeGreaterThan(summary.grandTreasuryExpected)
    expect(summary.receivable).toBe(summary.memberExpected - summary.collected)
    expect(summary.workshopMarginProjected).toBe(summary.memberExpected - summary.grandTreasuryExpected)
    expect(summary.trafficLight).toBe('amber')
  })

  it('registra cobranza, comprobante y autorización separada del egreso', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const charges = await api.getLodgeTreasuryCharges(organizationId, 2026, 9)
    const pending = charges.items.find(item => item.balance > 0)!
    const payment = await api.addLodgeTreasuryPayment(pending.id, { amount: pending.balance, paymentMethod: 'transfer', paymentDate: '2026-09-21', reference: 'TRX-QA-001' })
    expect(payment.balance).toBe(0)
    expect(payment.receiptNumber).toContain('REC-DEMO')

    const expense = await api.createLodgeTreasuryExpense(organizationId, { category: 'Servicios', amount: 15000, expenseDate: '2026-09-21', description: 'Egreso QA', evidenceReference: 'PDF-QA-001' })
    expect(expense.approvalStatus).toBe('pending_approval')
    expect((await api.approveLodgeTreasuryExpense(expense.id)).approvalStatus).toBe('approved')
  })
})
