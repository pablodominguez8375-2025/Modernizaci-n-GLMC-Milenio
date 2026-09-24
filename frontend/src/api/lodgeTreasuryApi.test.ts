import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

describe('Tesorería del Taller en demostración', () => {
  it('alinea resumen, cargos ficticios, decreto y cuota de cónyuge configurable', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const plans = await api.getLodgeFeePlans(organizationId)
    expect(plans.items.map(item => item.feeType)).toEqual(['normal', 'student', 'senior', 'spouse'])
    expect(plans.items.find(item=>item.feeType==='student')?.grandTreasuryAmount).toBe(8000)
    expect(plans.items.find(item=>item.feeType==='spouse')?.grandTreasuryAmount).toBe(13000)
    expect(plans.items.find(item=>item.feeType==='spouse')?.memberAmount).toBe(15000)

    const initial = await api.getLodgeTreasurySummary(organizationId, 2026, 9)
    expect(initial).toMatchObject({ members: 3, memberExpected: 67000, collected: 39000, receivable: 28000, grandTreasuryExpected: 55000, workshopMarginProjected: 12000, paid: 1, partial: 1, overdue: 1 })
    const charges = await api.getLodgeTreasuryCharges(organizationId, 2026, 9)
    const spouse = charges.items.find(item => item.feeType === 'spouse')
    expect(spouse).toMatchObject({ memberAmount: 15000, monthlyFeeAmount: 15000, paidAmount: 0, balance: 15000, status: 'pending' })

    const generated = await api.generateLodgeCharges(organizationId, 2026, 9)
    expect(generated).toEqual(initial)
    expect(await api.getLodgeTreasurySummary(organizationId, 2026, 9)).toEqual(initial)
    expect((await api.getLodgeTreasuryReport(organizationId, '2026-09-01', '2026-09-30')).income).toBe(39000)
  })

  it('reconcilia el resumen después de un pago completo de cónyuge', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const charges = await api.getLodgeTreasuryCharges(organizationId, 2026, 9)
    const spouse = charges.items.find(item => item.feeType === 'spouse')!
    const firstPayment = await api.addLodgeTreasuryPayment(spouse.id, { amount: 15000, paymentMethod: 'transfer', paymentDate: '2026-09-21', reference: 'TRX-SPOUSE-001', idempotencyKey: 'spouse-payment-001' })
    const replay = await api.addLodgeTreasuryPayment(spouse.id, { amount: 15000, paymentMethod: 'transfer', paymentDate: '2026-09-21', reference: 'TRX-SPOUSE-001', idempotencyKey: 'spouse-payment-001' })
    expect(replay.receiptNumber).toBe(firstPayment.receiptNumber)
    expect(spouse.payments).toHaveLength(1)
    expect(await api.getLodgeTreasurySummary(organizationId, 2026, 9)).toMatchObject({ members: 3, memberExpected: 67000, collected: 54000, receivable: 13000, grandTreasuryExpected: 55000, paid: 2, partial: 1, overdue: 0 })
  })

  it('registra cobranza, comprobante y autorización separada del egreso', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const charges = await api.getLodgeTreasuryCharges(organizationId, 2026, 9)
    const pending = charges.items.find(item => item.balance > 0)!
    const payment = await api.addLodgeTreasuryPayment(pending.id, { amount: pending.balance, paymentMethod: 'transfer', paymentDate: '2026-09-21', reference: 'TRX-QA-001', idempotencyKey: 'qa-payment-001' })
    expect(payment.balance).toBe(0)
    expect(payment.receiptNumber).toContain('REC-DEMO')

    const expense = await api.createLodgeTreasuryExpense(organizationId, { category: 'Servicios', amount: 15000, expenseDate: '2026-09-21', description: 'Egreso QA', evidenceReference: 'PDF-QA-001' })
    expect(expense.approvalStatus).toBe('pending_approval')
    expect((await api.approveLodgeTreasuryExpense(expense.id)).approvalStatus).toBe('approved')
  })

  it('permite configurar la cuota de cónyuge por vigencia sin mezclar su monto con Gran Tesorería', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const plan = (await api.getLodgeFeePlans(organizationId)).items.find(item=>item.feeType==='spouse')!
    expect(plan.memberAmount).toBe(15000)
    expect(plan.grandTreasuryAmount).toBe(13000)
    expect(plan.workshopAmount).toBe(2000)
    expect(plan.effectiveFrom).toBe('2026-01-01')
  })

  it('recalcula el aporte institucional por Oriente sin cambiar la cuota local del Taller', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    await api.setTreasuryTerritory(organizationId,'other_oriente')
    const plans=(await api.getLodgeFeePlans(organizationId)).items
    expect(plans.find(item=>item.feeType==='normal')?.grandTreasuryAmount).toBe(15000)
    expect(plans.find(item=>item.feeType==='spouse')?.grandTreasuryAmount).toBe(10000)
    expect(plans.find(item=>item.feeType==='spouse')?.memberAmount).toBe(15000)
    expect(plans.find(item=>item.feeType==='spouse')?.workshopAmount).toBe(5000)
  })

  it('cierra un ejercicio, conserva el arrastre y bloquea movimientos retroactivos', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const year = new Date().getFullYear() - 1
    const closure = await api.closeLodgeTreasuryYear(organizationId, year)
    expect(closure.accountingYear).toBe(year)
    expect(closure.closingBalance).toBe(closure.openingBalance + closure.income - closure.authorizedExpenses)
    expect((await api.getLodgeTreasuryYearClosures(organizationId)).items).toHaveLength(1)
    await expect(api.createLodgeTreasuryIncome(organizationId, {
      category: 'Ajuste', amount: 1000, incomeDate: year + '-12-31', description: 'Ajuste de demostración'
    })).rejects.toThrow('ejercicio cerrado')
    await expect(api.closeLodgeTreasuryYear(organizationId, year)).rejects.toThrow('ya está cerrado')
  })
})
