import { describe, expect, it } from 'vitest'
import type { OwnReceiptDocument } from './membershipApi'
import { ownReceiptSummary, renderOwnReceiptPdf } from './ownReceiptPdf'

const own: OwnReceiptDocument = {
  kind: 'tesoreria', receiptNumber: 'REC-2026-001', organization: 'Taller Ñuble Nº 5',
  currency: 'CLP', paymentDate: '2026-09-30', receivedAmount: 25000, paymentMethod: 'transferencia',
  unallocatedAmount: 2000, lines: [{ description: 'Cuota mensual', amount: 23000, periodMonth: 9, periodYear: 2026 }],
  corrections: [{ kind: 'void', date: '2026-10-01', cashAmount: 0 }],
}

describe('comprobante PDF', () => {
  it('resume el pago en una frase y genera un PDF válido', async () => {
    expect(ownReceiptSummary(own)).toContain('Taller Ñuble Nº 5')
    expect(ownReceiptSummary(own)).toContain('30/09/2026')
    const blob = renderOwnReceiptPdf(own)
    expect(blob.type).toBe('application/pdf')
    const text = await blob.text()
    expect(text.startsWith('%PDF-1.4')).toBe(true)
    expect(text.trimEnd().endsWith('%%EOF')).toBe(true)
    expect(text).toContain('/Count 1')
  })
  it('pagina cuando hay muchas imputaciones y no rompe con caracteres raros', async () => {
    const lines = Array.from({ length: 80 }, (_, i) => ({ description: `Cuota ${i} 😀`, amount: 1000, periodMonth: (i % 12) + 1, periodYear: 2026 }))
    const text = await renderOwnReceiptPdf({ ...own, lines }).text()
    expect(Number(/\/Count (\d+)/.exec(text)?.[1])).toBeGreaterThan(1)
  })
})
