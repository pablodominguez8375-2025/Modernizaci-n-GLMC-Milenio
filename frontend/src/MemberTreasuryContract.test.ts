import { describe, expect, it } from 'vitest'
import { memberTreasuryDemoStatement } from './api/membershipApi'

describe('Member personal treasury contract', () => {
  it('keeps the public showcase ledger explicitly fictitious', () => {
    expect(memberTreasuryDemoStatement.organization.name).toContain('Demostrativo')
    expect(memberTreasuryDemoStatement.statement.memberId).toContain('demo')
    expect(memberTreasuryDemoStatement.statement.payments.every(payment => payment.receiptNumber?.startsWith('DEMO-'))).toBe(true)
  })

  it('represents charges, partial allocations, payments and balance consistently', () => {
    const { statement } = memberTreasuryDemoStatement
    const totalCharges = statement.charges.reduce((sum, charge) => sum + charge.amount, 0)
    const totalPayments = statement.payments.reduce((sum, payment) => sum + payment.amount, 0)
    const outstanding = statement.charges.reduce((sum, charge) => sum + charge.outstandingAmount, 0)
    const partiallyPaid = statement.charges.find(charge => charge.status === 'open')

    expect(totalCharges).toBe(150000)
    expect(totalPayments).toBe(110000)
    expect(outstanding).toBe(40000)
    expect(partiallyPaid?.appliedAmount).toBe(20000)
    expect(partiallyPaid?.outstandingAmount).toBe(40000)
    expect(statement.summary.pendingChargeCount).toBe(1)
    expect(statement.summary.overdueChargeCount).toBe(0)
    expect(statement.summary.availableCredit).toBe(0)
    expect(statement.summary.netBalance).toBe(40000)
  })

  it('exposes receipt availability without exposing a document identifier in the self-service contract', () => {
    const payment = memberTreasuryDemoStatement.statement.payments[0]
    expect(payment.receiptAvailable).toBe(true)
    expect('receiptDocumentId' in payment).toBe(false)
  })
})
