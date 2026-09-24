import { describe, expect, it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'

describe('Derechos de ceremonia en Tesorería', () => {
  it('reconcilia pagos parciales, conserva el comprobante al reintentar y actualiza elegibilidad', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const item = (await api.getCeremonyReviewQueue()).items[0]
    expect(item.eligibility.ceremonyRight).toMatchObject({ amount: 31000, paid: 10000, balance: 21000, currency: 'CLP' })

    const payload = { amount: 11000, paymentMethod: 'transfer' as const, paymentDate: '2026-09-24', reference: 'TRX-DEMO-001', idempotencyKey: 'demo-right-payment-01' }
    const first = await api.recordCeremonyRightPayment(item.id, payload)
    const retry = await api.recordCeremonyRightPayment(item.id, payload)
    expect(retry.receiptNumber).toBe(first.receiptNumber)
    expect(retry).toMatchObject({ paidTotal: 21000, balance: 10000 })

    const current = (await api.getCeremonyReviewQueue()).items.find(value => value.id === item.id)!
    expect(current.eligibility.ceremonyRight).toMatchObject({ paid: 21000, balance: 10000 })
    expect(current.eligibility.requirements.find(value => value.code === 'ceremony_right_payment')?.status).toBe('rejected')
    await expect(api.recordCeremonyRightPayment(item.id, { ...payload, amount: 12000 })).rejects.toThrow('identificador de reintento')
  })

  it('permite autorizar el avance financiero al completar el derecho', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const item = (await api.getCeremonyReviewQueue()).items[0]
    await api.recordCeremonyRightPayment(item.id, { amount: 21000, paymentMethod: 'transfer', paymentDate: '2026-09-24', reference: 'TRX-DEMO-002', idempotencyKey: 'demo-right-payment-full' })
    const current = (await api.getCeremonyReviewQueue()).items.find(value => value.id === item.id)!
    expect(current.eligibility.ceremonyRight).toMatchObject({ paid: 31000, balance: 0 })
    expect(current.eligibility.requirements.find(value => value.code === 'ceremony_right_payment')?.status).toBe('approved')
    expect(current.eligibility.canAuthorize).toBe(true)
    expect(current.actions.canAuthorize).toBe(true)
  })

  it('expone a Gran Tesorería sólo expedientes con saldo y su referencia financiera mínima', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const initial = await api.getTreasuryCeremonyRights()
    expect(initial.total).toBe(2)
    expect(initial.items[0]).toMatchObject({ organizationName: expect.any(String), ceremonyType: 'wage_increase', amount: 31000, balance: 21000 })
    const item = initial.items[0]
    await api.recordCeremonyRightPayment(item.id, { amount: item.balance, paymentMethod: 'transfer', paymentDate: '2026-09-24', reference: 'TRX-GT-DEMO', idempotencyKey: 'gt-demo-right-001' })
    const refreshed = await api.getTreasuryCeremonyRights()
    expect(refreshed.total).toBe(1)
    expect(refreshed.items.some(value => value.id === item.id)).toBe(false)
  })
})
