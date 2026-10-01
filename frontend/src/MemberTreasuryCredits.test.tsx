import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import MemberTreasuryCredits from './MemberTreasuryCredits'
import { MembershipApiClient, type MemberTreasuryCredit } from './api/membershipApi'

describe('Crédito propio en la cartola', () => {
  it('muestra recibos y monedas separados sin publicar referencias bancarias ni IDs', () => {
    const credits: MemberTreasuryCredit[] = [
      { id: 'private-receipt-id', currency: 'CLP', receiptNumber: 'REC-CLP', paymentDate: '2026-09-28', amount: 5000, reference: 'PRIVATE-BANK-REFERENCE' },
      { id: 'private-usd-id', currency: 'USD', receiptNumber: 'REC-USD', paymentDate: '2026-09-29', amount: 3.5, reference: null },
    ]
    const html = renderToStaticMarkup(<MemberTreasuryCredits credits={credits} />)
    expect(html).toContain('REC-CLP')
    expect(html).toContain('REC-USD')
    expect(html).toContain('28/09/2026')
    expect(html).toContain('<td>CLP</td>')
    expect(html).toContain('<td>USD</td>')
    expect(html).toContain(new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(3.5))
    expect(html).not.toContain('PRIVATE-BANK-REFERENCE')
    expect(html).not.toContain('private-receipt-id')
    expect(html).toContain('no descuentan automáticamente el saldo de deuda')
  })

  it('admite cuentas sin crédito y respuestas anteriores sin el campo opcional', () => {
    for (const credits of [undefined, []]) {
      const html = renderToStaticMarkup(<MemberTreasuryCredits credits={credits} />)
      expect(html).toContain('Sin crédito pendiente de imputación.')
      expect(html).not.toContain('<table')
    }
  })

  it('usa el contrato real en la demo sintética sin compensar las deudas existentes', async () => {
    const profile = await new MembershipApiClient({ useMocks: true }).getSelfProfile()
    expect(profile.treasuryAccount.balance).toBe(25000)
    expect(profile.treasuryAccount.totalPaid).toBe(50000)
    const credits = profile.treasuryAccount.unappliedCredits!
    expect(credits.map(x => x.currency)).toEqual(['CLP', 'USD'])
    expect(credits.every(x => x.receiptNumber.startsWith('REC-DEMO-'))).toBe(true)
    const html = renderToStaticMarkup(<MemberTreasuryCredits credits={credits} />)
    expect(html).toContain('REC-DEMO-CREDITO-CLP')
    expect(html).toContain('REC-DEMO-CREDITO-USD')
  })
})
