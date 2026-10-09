import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { treasuryReceipts } from './MemberPayments'
import type { MemberTreasuryCharge } from './api/membershipApi'

const page = readFileSync(new URL('./MemberPortalPage.tsx', import.meta.url), 'utf8')
const payments = readFileSync(new URL('./MemberPayments.tsx', import.meta.url), 'utf8')

const charge = (month: number, payments: MemberTreasuryCharge['payments']): MemberTreasuryCharge => ({ chargeId: `c${month}`, organizationId: 'o', organization: 'Taller', periodYear: 2026, periodMonth: month, chargedAmount: 21000, paidAmount: 21000, balance: 0, periodStatus: 'paid', status: 'paid', payments })

/* Pedido del PO 08-10-2026: el hermano entiende sus pagos; Tesorería y Hospitalaria se mantienen separadas. */
describe('PMGM-UX Mis pagos claros, Tesorería y Hospitalaria separadas', () => {
  it('la vista muestra dos bloques separados y ninguna tabla en la vista principal', () => {
    expect(page).toContain('<TreasuryPayments account={treasuryAccount} fallback={treasury} api={membershipApi} />')
    expect(page).toContain('<HospitalariaPayments api={membershipApi} fallback={hospitalaria} />')
    expect(payments).toContain('<h2 id="pay-treasury-title">Tesorería</h2>')
    expect(payments).toContain('<h2 id="pay-hospitalaria-title">Hospitalaria</h2>')
    expect(page).not.toContain('<table')
  })

  it('cada bloque dice el estado en una frase y separa «Lo que debo» de «Mis pagos realizados»', () => {
    expect(payments).toContain('text="Estás al día"')
    expect(payments.match(/<h3>Lo que debo<\/h3>/g)).toHaveLength(2)
    expect(payments.match(/<h3>Mis pagos realizados<\/h3>/g)).toHaveLength(2)
  })

  it('la cartola completa y los decretos se abren en panel; sin el botón «Ver detalle» que no hacía nada', () => {
    expect(payments).toContain('<ActionDrawer label="Ver mi cartola completa"')
    expect(payments).toContain('<ActionDrawer label="Ver detalle y decretos"')
    expect(page).not.toContain('>Ver detalle</button>')
    for (const jargon of ['Cargado histórico', 'Morosidad anterior', 'Libro {book.currency}']) expect(payments + page).not.toContain(jargon)
  })

  it('agrupa por recibo: un pago que cubre dos meses aparece una vez con su total y los meses', () => {
    const pay = (amount: number) => ({ id: 'p1', receiptNumber: 'R-1', amount, paymentMethod: 'transfer', paymentDate: '2026-03-05', reference: null })
    const receipts = treasuryReceipts([charge(3, [pay(21000)]), charge(4, [pay(21000)]), charge(2, [{ ...pay(21000), id: 'p0', receiptNumber: 'R-0', paymentDate: '2026-02-03' }])])
    expect(receipts.map(r => r.receiptNumber)).toEqual(['R-1', 'R-0'])
    expect(receipts[0]).toMatchObject({ amount: 42000, periods: ['marzo 2026', 'abril 2026'] })
  })

  it('no mezcla dos pagos con un folio reutilizado: la descarga usa el ID inmutable del recibo', () => {
    const payment = (id: string, month: number) => ({
      id, receiptNumber: 'R-FOLIO-REPETIDO', amount: 21000,
      paymentMethod: 'cash', paymentDate: `2026-${String(month).padStart(2, '0')}-05`,
      reference: null,
    })
    const receipts = treasuryReceipts([charge(3, [payment('receipt-A', 3)]), charge(4, [payment('receipt-B', 4)])])
    expect(receipts).toHaveLength(2)
    expect(receipts.map(r => r.id)).toEqual(['receipt-B', 'receipt-A'])
    expect(receipts.map(r => r.amount)).toEqual([21000, 21000])
    expect(payments).toContain('receiptAction.download(r.id)')
  })
})
