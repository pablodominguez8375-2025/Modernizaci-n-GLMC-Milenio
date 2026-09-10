import { describe, expect, it } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'

describe('CandidateIntakeApiClient demo workflow', () => {
  it('loads a fictitious Gran Secretaría queue and private profile', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const queue = await api.getGrandSecretariatQueue()

    expect(queue.total).toBeGreaterThan(0)
    expect(queue.items[0].workshopName).toContain('Demostrativo')

    const profile = await api.getProfile(queue.items[0].ceremonyRequestId)
    expect(profile.rutOrInstitutionalId).toContain('DEMO')
    expect(profile.email).toContain('ejemplo.cl')
    expect(profile.photoAvailable).toBe(true)
  })

  it('records an observation and refreshes its state without publishing', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const queue = await api.getGrandSecretariatQueue('pending_grand_secretariat')
    const requestId = queue.items[0].ceremonyRequestId

    await api.review(requestId, 'observed', 'Antecedente ficticio pendiente de aclaración.')

    const observed = await api.getGrandSecretariatQueue('observed')
    const profile = await api.getProfile(requestId)
    expect(observed.items.some(item => item.ceremonyRequestId === requestId)).toBe(true)
    expect(profile.reviewStatus).toBe('observed')
    expect(profile.internalObservations).toContain('ficticio')
  })

  it('approves the reviewed file and marks it as published in the demo state', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const queue = await api.getGrandSecretariatQueue()
    const requestId = queue.items[0].ceremonyRequestId

    await api.approveAndPublish(requestId)

    const approved = await api.getGrandSecretariatQueue('approved')
    const profile = await api.getProfile(requestId)
    expect(approved.items.some(item => item.ceremonyRequestId === requestId)).toBe(true)
    expect(profile.reviewStatus).toBe('approved')
  })
})
