import { describe, expect, it } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'

describe('CandidateIntakeApiClient demo workflow', () => {
  it('starts a new insinuado without a ceremony date', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const created = await api.createDraftRequest({
      firstNames: 'Insinuado',
      paternalSurname: 'SinFecha',
      maternalSurname: null,
      insinuationDate: '2026-09-20',
    })

    expect(created.proposedDate).toBeNull()
  })

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
    expect(profile.completenessPercent).toBe(100)
    expect(profile.missingRequirements).toEqual([])
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

  it('blocks publication when the official profile is incomplete', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const pending = (await api.getWorkshopQueue()).items.find(item => !item.profileAvailable)!
    const profile = await api.saveProfile(pending.ceremonyRequestId, {
      firstNames: 'Persona Incompleta',
      paternalSurname: 'QA',
      presenters: ['H∴ Presentante QA'],
      insinuationDate: '2026-09-01',
    })

    expect(profile.completenessPercent).toBeLessThan(100)
    expect(profile.missingRequirements).toContain('Fotografía tipo pasaporte')
    await expect(api.approveAndPublish(pending.ceremonyRequestId)).rejects.toThrow('La ficha no está completa')
  })
  it('reproduces the complete regulatory insinuation flow with the same API contract in demo mode', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const requestId = (await api.getWorkshopQueue()).items[0].ceremonyRequestId

    const initial = await api.getWorkflow(requestId)
    expect(initial.initialDeliberation?.status).toBe('approved')

    await api.approveAndPublish(requestId)
    expect((await api.getWorkflow(requestId)).publication?.requiredDays).toBe(20)

    const interviews = []
    for (let index = 0; index < 3; index += 1) {
      const file = new File([new Uint8Array([37, 80, 68, 70])], `entrevista-${index + 1}.pdf`, { type: 'application/pdf' })
      const metadata = {
        interviewDate: `2026-08-${String(22 + index).padStart(2, '0')}`,
        interviewerDisplayName: `Maestro Entrevistador ${index + 1}`,
        summary: `Resumen ficticio de entrevista ${index + 1}`,
        result: 'favorable' as const,
      }
      const uploaded = await api.uploadInterviewDocument(requestId, crypto.randomUUID(), file, metadata)
      interviews.push({ ...metadata, documentVersionId: uploaded.documentVersionId })
    }

    await api.recordInterviewPackage(requestId, {
      asOfDate: '2026-08-24',
      interviews,
      confidentialQuestionnaireAvailable: true,
      confidentialQuestionnaireReference: 'DOC-DEMO-CUESTIONARIO',
      autobiographyAvailable: true,
      autobiographyReference: 'DOC-DEMO-AUTOBIOGRAFIA',
    })
    expect((await api.getWorkflow(requestId)).interviewPackage?.status).toBe('approved')

    await api.recordThirdDegreeReview(requestId, {
      reviewDate: '2026-09-05',
      presentVoters: 12,
      votesInFavor: 12,
      votesAgainst: 0,
      abstentions: 0,
      openVoteApproved: true,
      sourceReference: 'ACTA-DEMO-3G',
    })
    expect((await api.getWorkflow(requestId)).thirdDegreeReview?.status).toBe('approved')

    await api.recordFinalBallot(requestId, {
      ballotDate: '2026-09-15',
      ballots: [{ procedureNumber: 1, eligibleVoters: 12, whiteBallots: 12, blackBallots: 0 }],
      ballotApproved: true,
      sourceReference: 'ACTA-DEMO-BALOTAJE',
    })
    expect((await api.getWorkflow(requestId)).finalBallot?.status).toBe('approved')

    await api.submitInitiationRequest(requestId, {
      submissionDate: '2026-09-16',
      proposedCeremonyDate: '2026-10-10',
      venerableApproval: true,
      secretaryDisplayName: 'H∴ Secretario Demostrativo',
      sourceReference: 'SOL-INIT-DEMO-001',
    })
    expect((await api.getWorkflow(requestId)).initiationRequest?.status).toBe('approved')
  })

})
