import { describe, expect, it, vi } from 'vitest'
import { MembershipApiClient, type OwnReceiptDocument } from './api/membershipApi'
import { ownReceiptHtml } from './ownReceiptDocument'

describe('comprobantes propios de Mi ficha', () => {
  const own: OwnReceiptDocument = {
    kind: 'tesoreria', receiptNumber: 'REC-2026-001', organization: 'Taller ficticio',
    currency: 'USD', paymentDate: '2025-12-31', receivedAmount: 30, paymentMethod: 'transferencia',
    unallocatedAmount: 5, lines: [
      { description: 'Cuota de enero', amount: 10, periodMonth: 1, periodYear: 2026 },
      { description: 'Cuota de febrero', amount: 15, periodMonth: 2, periodYear: 2026 },
    ], corrections: [{ kind: 'correction', date: '2026-01-02', cashAmount: 0 }],
  }

  it('respeta fecha original, importes y períodos separados y evita HTML activo', () => {
    const html = ownReceiptHtml({ ...own, organization: '<img src=x onerror=alert(1)>' })
    expect(html).toContain('31/12/2025')
    expect(html).toContain('01/2026')
    expect(html).toContain('02/2026')
    expect(html).toContain('Ajustes posteriores')
    expect(html).toContain('Content-Security-Policy')
    expect(html).toContain('&lt;img src=x onerror=alert(1)&gt;')
    expect(html).not.toContain('<img src=x')
    expect(html).not.toContain('fictitious-bank-reference')
  })

  it('usa la identidad del token, no acepta memberId y desactiva cache', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => own })
    vi.stubGlobal('fetch', fetch)
    const api = new MembershipApiClient({ getAccessToken: async () => 'own-token' })
    const returned = await api.getOwnReceipt('tesoreria', 'receipt-1')
    expect(returned.receivedAmount).toBe(30)
    const [url, init] = fetch.mock.calls[0]
    expect(url).toBe('/api/membership/me/comprobantes/tesoreria/receipt-1')
    expect(url).not.toContain('memberId=')
    expect(init.cache).toBe('no-store')
    expect(init.credentials).toBe('omit')
    expect(init.headers.get('Authorization')).toBe('Bearer own-token')
    vi.unstubAllGlobals()
  })

  it('la demostración permite recibos ficticios y rechaza identificadores ajenos', async () => {
    const api = new MembershipApiClient({ useMocks: true })
    const treasury = await api.getOwnReceipt('tesoreria', 'payment-demo-2026-09')
    expect(treasury.receivedAmount).toBe(25000)
    expect(treasury.lines[0].periodMonth).toBe(9)
    const hospitalaria = await api.getOwnReceipt('hospitalaria', 'hospitalaria-payment-demo')
    expect(hospitalaria.receivedAmount).toBe(1500)
    const credit = await api.getOwnReceipt('tesoreria', 'credit-demo-usd')
    expect(credit.currency).toBe('USD')
    expect(credit.unallocatedAmount).toBe(3.5)
    await expect(api.getOwnReceipt('tesoreria', 'not-own')).rejects.toThrow('no existe')
  })
})
