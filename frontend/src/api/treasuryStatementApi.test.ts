import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

describe('Gran Tesorería monthly statement demo', () => {
  it('generates, pays, submits and reconciles the official-style statement', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    let statement = await api.createTreasuryStatement(organizationId, { periodYear: 2026, periodMonth: 9, cutoffDate: '2026-09-12' })
    statement = await api.generateTreasuryStatementLines(statement.id, { apprenticeAmount: 21000, fellowcraftAmount: 21000, masterAmount: 21000 })
    expect(statement.lines).toHaveLength(7)
    expect(statement.lines.some(line => line.authorizationReference?.includes('Plancha'))).toBe(true)
    expect(statement.differenceAmount).toBe(statement.expectedAmount)

    statement = await api.addTreasuryStatementPayment(statement.id, { paymentMethod: 'transfer', paymentDate: '2026-09-12', amount: statement.differenceAmount })
    expect(statement.differenceAmount).toBe(0)
    statement = await api.submitTreasuryStatement(statement.id)
    expect(statement.status).toBe('submitted')
    statement = await api.reconcileTreasuryStatement(statement.id)
    expect(statement.status).toBe('reconciled')
  })
})
