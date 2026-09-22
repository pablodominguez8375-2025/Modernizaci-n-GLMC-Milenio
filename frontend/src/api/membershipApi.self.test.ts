import { describe, expect, it } from 'vitest'
import { MembershipApiClient } from './membershipApi'

describe('member self-service API client', () => {
  it('keeps mock contact updates in the QA session without changing institutional data', async () => {
    const api = new MembershipApiClient({ useMocks: true })
    const before = await api.getSelfProfile()

    const result = await api.updateSelfContact({
      email: 'actualizado@example.test',
      phone: '+56 9 3333 3333',
      address: 'Domicilio QA actualizado',
    })
    const after = await api.getSelfProfile()

    expect(result.status).toBe('updated')
    expect(result.changedFields).toEqual(['email', 'phone', 'address'])
    expect(after.contact.email).toBe('actualizado@example.test')
    expect(after.contact.phone).toBe('+56 9 3333 3333')
    expect(after.contact.address).toBe('Domicilio QA actualizado')
    expect(after.member.institutionalNumber).toBe(before.member.institutionalNumber)
    expect(after.current.effectiveDegree).toBe(before.current.effectiveDegree)
    expect(after.current.membership?.organizationId).toBe(before.current.membership?.organizationId)
    expect(after.treasuryAccount.totalCharged).toBe(75000)
    expect(after.treasuryAccount.totalPaid).toBe(75000)
    expect(after.treasuryAccount.balance).toBe(0)
    expect(after.treasuryAccount.items[0].payments[0].receiptNumber).toBe('REC-DEMO-003')
  })

  it('reports unchanged when no personal field changes', async () => {
    const api = new MembershipApiClient({ useMocks: true })
    const before = await api.getSelfProfile()
    const result = await api.updateSelfContact(before.contact)

    expect(result.status).toBe('unchanged')
    expect(result.changedFields).toEqual([])
  })
})
