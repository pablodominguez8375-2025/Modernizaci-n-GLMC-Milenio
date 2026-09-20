import { describe, expect, it } from 'vitest'
import { LodgeApiClient } from './api/lodgeApi'

describe('cartas de retiro con cuatro firmas', () => {
  it('materializa el retiro solamente después de las cuatro firmas institucionales', async () => {
    const api = new LodgeApiClient({ useMocks: true })
    const organizationId = '23232323-2323-2323-2323-232323232323'
    const initial = await api.getWithdrawals(organizationId)
    const request = initial.items[0]

    expect(request.executedAtUtc).toBeNull()
    await api.signWithdrawal(request.id, 'venerable')
    await api.signWithdrawal(request.id, 'treasurer')
    await api.signWithdrawal(request.id, 'orator')
    const beforeLastSignature = await api.getWithdrawals(organizationId)
    expect(beforeLastSignature.items[0].executedAtUtc).toBeNull()

    const completed = await api.signWithdrawal(request.id, 'secretary')
    expect(completed.executedAtUtc).not.toBeNull()
    expect(Object.values(completed.signatures).every(signature => signature.signed)).toBe(true)
  })
})
