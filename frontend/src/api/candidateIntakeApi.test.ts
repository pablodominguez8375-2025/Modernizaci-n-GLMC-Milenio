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
    expect(profile.employerName).toContain('Demostrativa')
    expect(profile.firstDegreePresentationDate).toBe('2026-08-28')
    expect(profile.responsibleSecretaryName).toContain('Demostrativo')
    expect(profile.photoAvailable).toBe(true)
  })

  it('loads the transversal publication list with safe photo routes', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const portal = await api.getPublishedCandidates()

    expect(portal.total).toBeGreaterThan(0)
    expect(portal.items[0].displayName).toContain('Demostrativa')
    expect(portal.items[0].photoUrl).toMatch(/^\/api\/candidate-publications\/[0-9a-f-]+\/photo$/i)
    expect(portal.items.some(item => item.photoUrl === null)).toBe(true)
  })

  it('lets Taller Secretaría complete a pending private ficha without publishing it', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const before = await api.getWorkshopQueue()
    const pending = before.items.find(item => !item.profileAvailable)
    expect(pending).toBeDefined()

    const saved = await api.saveProfile(pending!.ceremonyRequestId, {
      firstNames: 'Persona',
      paternalSurname: 'QA',
      maternalSurname: 'Taller',
      rutOrInstitutionalId: 'DEMO-01',
      birthDate: '1992-05-10',
      nationality: 'Chilena · demo',
      civilStatus: 'Demo',
      occupation: 'Profesión ficticia',
      employerName: 'Empresa ficticia',
      workAddress: 'Dirección laboral ficticia',
      workPosition: 'Cargo ficticio',
      workPhone: '+56 2 2000 0000',
      phone: '+56 9 0000 0000',
      email: 'taller.qa@ejemplo.cl',
      address: 'Dirección ficticia',
      city: 'Santiago · demo',
      orient: 'Santiago',
      presenters: ['H∴ Presentante QA'],
      insinuationDate: '2026-09-01',
      firstDegreePresentationDate: '2026-09-05',
      responsibleSecretaryName: 'H∴ Secretario QA',
      interviewSummary: 'Entrevista ficticia.',
      internalObservations: 'Sólo QA.',
    })

    const after = await api.getWorkshopQueue()
    const row = after.items.find(item => item.ceremonyRequestId === pending!.ceremonyRequestId)
    expect(saved.reviewStatus).toBe('pending_grand_secretariat')
    expect(row?.profileAvailable).toBe(true)
    expect(row?.reviewStatus).toBe('pending_grand_secretariat')
    expect(saved.workPosition).toBe('Cargo ficticio')
    expect(saved.responsibleSecretaryName).toBe('H∴ Secretario QA')

    const published = await api.getPublishedCandidates()
    expect(published.items.some(item => item.displayName === saved.firstNames)).toBe(false)
  })

  it('links a processed passport-photo version and keeps the ficha pending for Gran Secretaría', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const queue = await api.getWorkshopQueue()
    const pending = queue.items.find(item => !item.profileAvailable)!

    await api.saveProfile(pending.ceremonyRequestId, {
      firstNames: 'Persona Foto',
      paternalSurname: 'QA',
      presenters: ['H∴ Presentante QA'],
      insinuationDate: '2026-09-01',
    })
    await api.attachPhotoVersion(pending.ceremonyRequestId, '11111111-2222-4333-8444-555555555555')

    const profile = await api.getProfile(pending.ceremonyRequestId)
    const refreshed = await api.getWorkshopQueue()
    const row = refreshed.items.find(item => item.ceremonyRequestId === pending.ceremonyRequestId)
    expect(profile.photoAvailable).toBe(true)
    expect(profile.reviewStatus).toBe('pending_grand_secretariat')
    expect(row?.photoAvailable).toBe(true)
  })

  it('uploads a selected photo directly in the demo contract', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const queue = await api.getWorkshopQueue()
    const requestId = queue.items[0].ceremonyRequestId
    const photo = new File([new Uint8Array([137, 80, 78, 71])], 'foto-demo.png', { type: 'image/png' })

    await api.uploadPhoto(requestId, photo)

    expect(await api.getPrivatePhoto(requestId)).toBe(photo)
    expect((await api.getProfile(requestId)).photoAvailable).toBe(true)
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
