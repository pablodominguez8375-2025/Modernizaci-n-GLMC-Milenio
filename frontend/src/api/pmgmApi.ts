export interface CandidatePublication { displayName: string; workshopName: string; workshopNumber: string | null; publishedFromUtc: string; publishedUntilUtc: string | null; requiredDays: number; elapsedDays: number; complianceDateUtc: string; ruleCode: string; status: string }
export interface CandidatePortalResponse { culture: string; portal: string; total: number; items: CandidatePublication[] }
export interface SystemInfo { project: string; api: string; version: string; runtime: string; culture: string; institutionalTimeZone: string; defaultCurrency: string }
export interface SessionCapabilities {
  canApproveTransfers: boolean
  canRunRegimenInteriorReports: boolean
  canManageGrandSecretariat: boolean
  canManageTreasuryRegularity: boolean
  canManageHospitalariaRegularity: boolean
  canEvaluateCeremonies: boolean
  canReviewCeremonies: boolean
  canValidateCeremonyInternalAffairs: boolean
  canAuthorizeCeremonies: boolean
  canManagePrivacy: boolean
}
export interface SessionProfile { displayName: string; accessScope: 'order' | 'organization' | 'authenticated'; capabilities: SessionCapabilities }
export interface OrganizationOption { id: string; name: string; number: string | null; type: string }
export interface OrganizationOptionsResponse { total: number; items: OrganizationOption[] }
export interface InstitutionalSpace { id: string; code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location: string | null; capacity: number | null; status?: string; isAvailable?: boolean }
export interface SpaceAvailabilityResponse { fromUtc: string; toUtc: string; total: number; available: number; items: InstitutionalSpace[] }
export interface SecretariatDocument { id: string; documentType: 'decree' | 'communication' | 'ceremony_authorization'; documentCode: string; title: string; content: string; organizationId: string | null; relatedCeremonyRequestId: string | null; spaceReservationId: string | null; status: string; issuedAtUtc: string; issuedBySubject: string }
export interface SecretariatDocumentsResponse { total: number; items: SecretariatDocument[] }
export interface CreateSpaceRequest { code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location?: string | null; capacity?: number | null }
export interface CreateReservationRequest { spaceId: string; organizationId: string; ceremonyRequestId?: string | null; purpose: string; startsAtUtc: string; endsAtUtc: string; notes?: string | null }
export interface IssueDocumentRequest { documentType: 'decree' | 'communication'; title: string; content: string; organizationId?: string | null }
export type CeremonyType = 'initiation' | 'wage_increase' | 'exaltation'
export type CeremonyRequestStatus = 'draft' | 'under_review' | 'eligible' | 'observed' | 'rejected' | 'authorized'
export type CeremonyValidationStatus = 'pending' | 'approved' | 'observed' | 'rejected' | 'not_applicable' | 'exception_approved'
export interface GrandSecretariatCeremonyQueueItem {
  id: string; organizationId: string; organizationName: string; organizationNumber: string | null; ceremonyType: CeremonyType; proposedDate: string | null; status: string; formalAuthorizationIssued: boolean
  spaceReservationId: string | null; spaceName: string | null; reservationStartsAtUtc: string | null; reservationEndsAtUtc: string | null; createdAtUtc: string
}
export interface GrandSecretariatCeremonyQueueResponse { total: number; items: GrandSecretariatCeremonyQueueItem[] }
export interface CeremonyQueueRequirement { code: string; name: string; status: string; reason: string }
export interface CeremonyQueuePublication { status: string; requiredDays: number; completedDays: number; publishedFromUtc: string; publishedUntilUtc: string | null }
export interface CeremonyQueueEligibility { status: string; canAuthorize: boolean; requirements: CeremonyQueueRequirement[]; publication: CeremonyQueuePublication | null }
export interface CeremonyQueueActions { canValidateInternalAffairs: boolean; canPublishCandidate: boolean; canAuthorize: boolean }
export interface CeremonyReviewQueueItem {
  id: string
  organizationId: string
  organizationName: string
  organizationNumber: string | null
  ceremonyType: CeremonyType
  subjectDisplayName: string
  proposedDate: string | null
  status: CeremonyRequestStatus | string
  eligibility: CeremonyQueueEligibility
  actions: CeremonyQueueActions
  createdAtUtc: string
}
export interface CeremonyReviewQueueResponse { total: number; items: CeremonyReviewQueueItem[] }
export interface CeremonyInternalAffairsValidationRequest { status: Exclude<CeremonyValidationStatus, 'pending' | 'not_applicable'>; sourceReference?: string | null; notes?: string | null }
export interface InitiationCompletionResponse { id: string; status: string; memberId: string; membershipStatus: string; degree: string; effectiveDate: string; documentCode: string }
export interface InitialDeliberationRequest { deliberationDate: string; presentVoters: number; votesInFavor: number; minimumWaitingDays?: number; sourceReference: string }
export interface InitialDeliberationResponse { id: string; validationStatus: 'approved' | 'observed' | 'rejected'; code: string; reason: string; ceremonyStatus: string }
export interface CandidatePublicationWorkflowResponse { id: string; ceremonyRequestId: string; publishedFromUtc: string; requiredDays: number; ruleCode: string; status: string; alreadyPublished: boolean; notificationRecipients: number; notificationsCreated: number }
export type CandidateInterviewResult = 'favorable' | 'desfavorable'
export interface CandidateInterviewEvidence { interviewDate: string; interviewerDisplayName: string; summary: string; result: CandidateInterviewResult; documentVersionId: string }
export interface InterviewPackageRequest { asOfDate: string; interviews: CandidateInterviewEvidence[]; confidentialQuestionnaireAvailable: boolean; confidentialQuestionnaireReference: string | null; autobiographyAvailable: boolean; autobiographyReference: string | null }
export interface InterviewPackageResponse { id: string; validationStatus: 'approved' | 'observed' | 'rejected'; code: string; reason: string; completedInterviews: number; ceremonyStatus: string }
export interface InterviewDocumentResponse { interviewId: string; documentVersionId: string; fileName: string; result: CandidateInterviewResult; summary: string; sizeBytes: number }
export interface ThirdDegreeReviewRequest { reviewDate: string; presentVoters: number; votesInFavor: number; votesAgainst: number; abstentions: number; openVoteApproved: boolean; sourceReference: string }
export interface ThirdDegreeReviewResponse { thirdDegree: { id: string; status: 'approved' | 'observed' | 'rejected'; code: string; reason: string }; status: string }
export interface FinalBallotRound { procedureNumber: 1 | 2 | 3; eligibleVoters: number; whiteBallots: number; blackBallots: number }
export interface FinalBallotRequest { ballotDate: string; ballots: FinalBallotRound[]; ballotApproved: boolean; sourceReference: string }
export interface FinalBallotResponse { id: string; validationStatus: 'approved' | 'observed' | 'rejected'; asOfDate: string; code: string; reason: string; ceremonyStatus: string }
export interface InitiationRequestSubmission { submissionDate: string; proposedCeremonyDate: string; venerableApproval: boolean; secretaryDisplayName: string; sourceReference: string }
export interface InitiationRequestSubmissionResponse { id: string; validationStatus: 'approved'; proposedDate: string; ceremonyStatus: string; alreadySubmitted: boolean }
export interface OrderRejectionAlert { personId: string; firstNames: string; lastNames: string; workshopName: string; workshopNumber: string | null; rejectionDate: string; reason: string; sourceReference: string | null; notes: string | null }
export interface OrderRejectionAlertResponse { total: number; items: OrderRejectionAlert[] }
export interface RegimenInteriorSummary {
  scope: 'order' | 'organization'; organizationId: string | null; asOf: string; period: { from: string; to: string }
  members: { totalRelated: number; currentlyAffiliated: number; active: number; inactive: number; currentWithBlockingStatus: number }
  events: { voluntaryWithdrawals: number; forcedWithdrawals: number; reinstatements: number; deaths: number; transfers: number }
  financialRegularity: { source: string; currentAffiliations: number; upToDate: number; delinquent: number; pending: number; exempt: number; withoutStatus: number; delinquentMembersDistinct: number }
  degreeDistribution: Record<string, number>; pendingTransfers: number
}
export type TreasuryRegularityStatus = 'up_to_date' | 'delinquent' | 'pending' | 'exempt'
export type HospitalariaRegularityStatus = 'up_to_date' | 'overdue' | 'pending' | 'exempt'
export interface WorkshopRegularitySnapshot {
  id?: string; organizationId?: string; memberId?: string | null; scope?: string; status: string; asOfDate: string; sourceReference?: string | null; notes?: string | null; recordedAtUtc?: string
}
export interface WorkshopRegularityRequest { status: string; asOfDate: string; sourceReference?: string | null; notes?: string | null }
export interface TreasuryStatementLine {
  id: string; memberId: string | null; membershipId: string | null; degreeCodeAtCutoff: string; officeCodeAtCutoff: string | null
  baseAmount: number; adjustmentAmount: number; payableAmount: number; adjustmentType: string | null; authorizationReference: string | null
  observation: string | null; identityMatchStatus: string
}
export interface TreasuryStatementPayment { id: string; paymentMethod: string; paymentDate: string; amount: number; payerDisplayName: string | null; reference: string | null; recordedAtUtc?: string }
export interface TreasuryStatement {
  id: string; organizationId: string; periodYear: number; periodMonth: number; cutoffDate: string; status: string; sourceReference: string | null
  expectedAmount: number; transferAmount: number; depositAmount: number; paidAmount: number; differenceAmount: number; unresolvedIdentities: number
  lines: TreasuryStatementLine[]; payments: TreasuryStatementPayment[]; submittedAtUtc: string | null; reconciledAtUtc: string | null; closedAtUtc: string | null
}
export interface CreateTreasuryStatementRequest { periodYear: number; periodMonth: number; cutoffDate: string; sourceReference?: string | null }
export interface GenerateTreasuryLinesRequest { apprenticeAmount: number; fellowcraftAmount: number; masterAmount: number }
export interface AddTreasuryPaymentRequest { paymentMethod: 'transfer' | 'deposit'; paymentDate: string; amount: number; payerDisplayName?: string | null; payerRut?: string | null; reference?: string | null }
export type AccessTokenProvider = () => Promise<string | null>
export interface PmgmApiClientOptions { baseUrl?: string; getAccessToken?: AccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

class PmgmApiHttpError extends Error { constructor(readonly status: number, message: string) { super(message); this.name = 'PmgmApiHttpError' } }

const mockCandidates: CandidatePublication[] = [
  { displayName: 'Persona Demostrativa Uno', workshopName: 'Taller Demostrativo Nº 1', workshopNumber: '1', publishedFromUtc: '2026-08-25T15:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 13, complianceDateUtc: '2026-09-14T15:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
  { displayName: 'Persona Demostrativa Dos', workshopName: 'Taller Demostrativo Nº 23', workshopNumber: '23', publishedFromUtc: '2026-08-15T18:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 23, complianceDateUtc: '2026-09-04T18:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
]
const mockSession: SessionProfile = {
  displayName: 'Usuario demostrativo',
  accessScope: 'order',
  capabilities: {
    canApproveTransfers: true,
    canRunRegimenInteriorReports: true,
    canManageGrandSecretariat: true,
    canManageTreasuryRegularity: true,
    canManageHospitalariaRegularity: true,
    canEvaluateCeremonies: true,
    canReviewCeremonies: true,
    canValidateCeremonyInternalAffairs: true,
    canAuthorizeCeremonies: true,
    canManagePrivacy: true,
  },
}
const defaultMockOrganizations: OrganizationOption[] = [1, ...Array.from({ length: 18 }, (_, index) => index + 2), 23].map(number => ({
  id: number === 1 ? '11111111-1111-1111-1111-111111111111' : number === 23 ? '23232323-2323-2323-2323-232323232323' : `00000000-0000-0000-0000-${String(number).padStart(12, '0')}`,
  name: `Taller Demostrativo Nº ${number}`,
  number: String(number),
  type: 'workshop',
}))
const defaultMockSpaces: InstitutionalSpace[] = [
  { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', code: 'TEMP-01', name: 'Templo Principal', spaceType: 'temple', location: 'Sede institucional', capacity: 80, status: 'active' },
  { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', code: 'SEC-01', name: 'Sala de Secretaría', spaceType: 'secretariat_room', location: 'Sede institucional', capacity: 16, status: 'active' },
]
const defaultMockCeremonies: GrandSecretariatCeremonyQueueItem[] = [
  { id: 'cccccccc-1111-1111-1111-111111111111', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'wage_increase', proposedDate: '2026-09-18', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: null, spaceName: null, reservationStartsAtUtc: null, reservationEndsAtUtc: null, createdAtUtc: '2026-09-07T16:00:00Z' },
  { id: 'cccccccc-2222-2222-2222-222222222222', organizationId: defaultMockOrganizations[0].id, organizationName: defaultMockOrganizations[0].name, organizationNumber: '1', ceremonyType: 'exaltation', proposedDate: '2026-09-20', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', spaceName: defaultMockSpaces[0].name, reservationStartsAtUtc: '2026-09-20T22:00:00Z', reservationEndsAtUtc: '2026-09-21T01:00:00Z', createdAtUtc: '2026-09-07T17:00:00Z' },
]
const defaultMockReviewCeremonies: CeremonyReviewQueueItem[] = [
  {
    id: 'eeeeeeee-1111-1111-1111-111111111111', organizationId: defaultMockOrganizations[0].id, organizationName: defaultMockOrganizations[0].name, organizationNumber: '1', ceremonyType: 'wage_increase', subjectDisplayName: 'Hermano Demostrativo', proposedDate: '2026-09-25', status: 'under_review', createdAtUtc: '2026-09-08T13:00:00Z',
    eligibility: { status: 'complies', canAuthorize: true, publication: null, requirements: [
      { code: 'regimen_interior', name: 'Régimen Interior', status: 'approved', reason: 'Aprobación vigente registrada.' },
      { code: 'gran_tesoreria', name: 'Gran Tesorería', status: 'approved', reason: 'El Taller se encuentra al día para la fecha evaluada.' },
      { code: 'gran_hospitalaria', name: 'Gran Hospitalaria', status: 'approved', reason: 'El Taller se encuentra al día en reposiciones u obligaciones hospitalarias.' },
    ] },
    actions: { canValidateInternalAffairs: true, canPublishCandidate: false, canAuthorize: true },
  },
  {
    id: 'eeeeeeee-2222-2222-2222-222222222222', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'initiation', subjectDisplayName: 'Persona Demostrativa Uno', proposedDate: '2026-10-03', status: 'under_review', createdAtUtc: '2026-09-08T13:15:00Z',
    eligibility: { status: 'does_not_comply', canAuthorize: false, publication: { status: 'published', requiredDays: 20, completedDays: 13, publishedFromUtc: '2026-08-26T15:00:00Z', publishedUntilUtc: null }, requirements: [
      { code: 'regimen_interior', name: 'Régimen Interior', status: 'rejected', reason: 'No existe una aprobación habilitante de Régimen Interior.' },
      { code: 'gran_tesoreria', name: 'Gran Tesorería', status: 'approved', reason: 'El Taller se encuentra al día para la fecha evaluada.' },
      { code: 'gran_hospitalaria', name: 'Gran Hospitalaria', status: 'approved', reason: 'El Taller se encuentra al día en reposiciones u obligaciones hospitalarias.' },
      { code: 'publicacion_insinuado', name: 'Publicación del insinuado', status: 'rejected', reason: 'Se requieren 20 días de publicación y se han cumplido 13 días válidos.' },
    ] },
    actions: { canValidateInternalAffairs: true, canPublishCandidate: false, canAuthorize: true },
  },
]

export class PmgmApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockOrganizations = [...defaultMockOrganizations]
  private readonly mockSpaces = [...defaultMockSpaces]
  private readonly mockBusySpaces = new Set<string>([defaultMockSpaces[0].id])
  private readonly mockDocuments: SecretariatDocument[] = []
  // QA only: uploaded interview files live in browser memory for this session.
  private readonly mockInterviewDocuments = new Map<string, { file: Blob; fileName: string; metadata: CandidateInterviewEvidence }>()
  private readonly mockCeremonies = defaultMockCeremonies.map(item => ({ ...item }))
  private readonly mockReviewCeremonies = defaultMockReviewCeremonies.map(cloneCeremonyQueueItem)
  private readonly mockTreasury = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'treasury-demo-1', organizationId: defaultMockOrganizations[0].id, scope: 'organization', status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'TES-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:00:00Z' }]])
  private readonly mockTreasuryStatements = new Map<string, TreasuryStatement>()
  private readonly mockHospitalaria = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'hospitalaria-demo-1', organizationId: defaultMockOrganizations[0].id, status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:05:00Z' }]])

  constructor(options: PmgmApiClientOptions = {}) { this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, ''); this.getAccessToken = options.getAccessToken; this.useMocks = options.useMocks ?? false; this.onUnauthorized = options.onUnauthorized }

  async getCandidatePortal(): Promise<CandidatePortalResponse> { if (this.useMocks) { await sleep(120); return { culture: 'es-CL', portal: 'Insinuados en período de publicación', total: mockCandidates.length, items: mockCandidates } } return this.request<CandidatePortalResponse>('/api/ceremonias/portal-insinuados') }
  async getSystemInfo(): Promise<SystemInfo> { if (this.useMocks) return { project: 'Proyecto Milenio — Modernización Gran Logia Mixta de Chile', api: 'PMGM.Api', version: '0.12.1', runtime: '.NET 10', culture: 'es-CL', institutionalTimeZone: 'America/Santiago', defaultCurrency: 'CLP' }; return this.request<SystemInfo>('/api/system/info') }
  async getSessionProfile(): Promise<SessionProfile> { if (this.useMocks) return mockSession; return this.request<SessionProfile>('/api/session/me') }
  async getOrganizationOptions(): Promise<OrganizationOptionsResponse> { if (this.useMocks) return { total: this.mockOrganizations.length, items: [...this.mockOrganizations] }; return this.request<OrganizationOptionsResponse>('/api/institutional/organizations/options') }

  async getRegimenInteriorSummary(filters: { organizationId?: string; asOf?: string; from?: string } = {}): Promise<RegimenInteriorSummary> {
    if (this.useMocks) return mockRegimenSummary(filters)
    const query = new URLSearchParams(); if (filters.organizationId) query.set('organizationId', filters.organizationId); if (filters.asOf) query.set('asOf', filters.asOf); if (filters.from) query.set('from', filters.from)
    return this.request<RegimenInteriorSummary>(`/api/regimen-interior/summary${query.size ? `?${query}` : ''}`)
  }
  async getOrderRejectionAlerts(): Promise<OrderRejectionAlertResponse> {
    if (this.useMocks) return { total: 1, items: [{ personId: 'person-demo-blocked', firstNames: 'Persona Rechazada', lastNames: 'Demostrativa', workshopName: 'Taller Demostrativo Nº 7', workshopNumber: '7', rejectionDate: '2026-09-30', reason: 'Rechazo en Cámara del Medio / tercer grado', sourceReference: 'ACTA-RECHAZO-DEMO-2026-007', notes: 'Antecedente reservado para consulta de Régimen Interior.' }] }
    return this.request<OrderRejectionAlertResponse>('/api/insinuados/regimen-interior/alertas-rechazo')
  }

  async getCeremonyReviewQueue(): Promise<CeremonyReviewQueueResponse> {
    if (this.useMocks) return { total: this.mockReviewCeremonies.length, items: this.mockReviewCeremonies.map(cloneCeremonyQueueItem) }
    return this.request<CeremonyReviewQueueResponse>('/api/institutional/ceremonias/bandeja')
  }
  async setCeremonyInternalAffairsValidation(ceremonyRequestId: string, payload: CeremonyInternalAffairsValidationRequest): Promise<unknown> {
    if (this.useMocks) {
      if (ceremonyRequestId === 'eeeeeeee-2222-2222-2222-222222222222') return { status: payload.status }
      const item = this.requireMockReviewCeremony(ceremonyRequestId)
      if (!item.actions.canValidateInternalAffairs) throw new Error('La solicitud ya no admite validación de Régimen Interior.')
      const requirement = item.eligibility.requirements.find(value => value.code === 'regimen_interior')
      if (requirement) {
        const approved = payload.status === 'approved' || payload.status === 'exception_approved'
        requirement.status = approved ? 'approved' : payload.status
        requirement.reason = approved ? 'Aprobación vigente registrada.' : payload.status === 'observed' ? 'La solicitud tiene observaciones pendientes de Régimen Interior.' : 'No existe una aprobación habilitante de Régimen Interior.'
      }
      recomputeMockEligibility(item)
      return { status: payload.status }
    }
    return this.postJson<unknown>(`/api/ceremonias/solicitudes/${encodeURIComponent(ceremonyRequestId)}/validaciones/regimen-interior`, payload)
  }
  async publishCeremonyCandidate(ceremonyRequestId: string): Promise<CandidatePublicationWorkflowResponse> {
    if (this.useMocks) {
      if (ceremonyRequestId === 'eeeeeeee-2222-2222-2222-222222222222') return { id: 'publication-demo-2026-001', ceremonyRequestId, publishedFromUtc: '2026-09-21T15:00:00Z', requiredDays: 20, ruleCode: 'initiation.publication.minimum_days', status: 'published', alreadyPublished: false, notificationRecipients: 34, notificationsCreated: 34 }
      const item = this.requireMockReviewCeremony(ceremonyRequestId)
      if (!item.actions.canPublishCandidate || item.ceremonyType !== 'initiation') throw new Error('La solicitud no admite iniciar una nueva publicación del insinuado.')
      item.eligibility.publication = { status: 'published', requiredDays: 20, completedDays: 0, publishedFromUtc: new Date().toISOString(), publishedUntilUtc: null }
      const existing = item.eligibility.requirements.find(value => value.code === 'publicacion_insinuado')
      const requirement = { code: 'publicacion_insinuado', name: 'Publicación del insinuado', status: 'rejected', reason: 'Se requieren 20 días de publicación y se han cumplido 0 días válidos.' }
      if (existing) Object.assign(existing, requirement); else item.eligibility.requirements.push(requirement)
      item.actions.canPublishCandidate = false; recomputeMockEligibility(item)
      return { id: `publication-${item.id}`, ceremonyRequestId, publishedFromUtc: item.eligibility.publication.publishedFromUtc, requiredDays: 20, ruleCode: 'initiation.publication.minimum_days', status: 'published', alreadyPublished: false, notificationRecipients: 34, notificationsCreated: 34 }
    }
    return this.request<CandidatePublicationWorkflowResponse>(`/api/ceremonias/solicitudes/${encodeURIComponent(ceremonyRequestId)}/publicacion-insinuado`, { method: 'POST' })
  }
  async authorizeCeremony(ceremonyRequestId: string): Promise<{ id?: string; status: string }> {
    if (this.useMocks) {
      const item = this.requireMockReviewCeremony(ceremonyRequestId)
      if (!item.actions.canAuthorize) throw new Error('Su cuenta no puede autorizar esta ceremonia.')
      if (!item.eligibility.canAuthorize) throw new Error('La ceremonia aún tiene requisitos obligatorios pendientes.')
      item.status = 'authorized'; item.actions = { canValidateInternalAffairs: false, canPublishCandidate: false, canAuthorize: false }
      return { id: item.id, status: item.status }
    }
    return this.request<{ id?: string; status: string }>(`/api/ceremonias/solicitudes/${encodeURIComponent(ceremonyRequestId)}/autorizar`, { method: 'POST' })
  }
  async registerInitiation(ceremonyRequestId: string, ceremonyDate: string, minuteReference: string): Promise<InitiationCompletionResponse> {
    if (this.useMocks) return { id: ceremonyRequestId, status: 'completed', memberId: 'member-demo-2026-001', membershipStatus: 'active', degree: 'apprentice', effectiveDate: ceremonyDate, documentCode: 'AUT-CER-DEMO-2026-001' }
    return this.postJson<InitiationCompletionResponse>(`/api/ceremonias/solicitudes/${encodeURIComponent(ceremonyRequestId)}/registrar-iniciacion`, { ceremonyDate, minuteReference })
  }
  async recordInitialDeliberation(ceremonyRequestId: string, payload: InitialDeliberationRequest): Promise<InitialDeliberationResponse> {
    if (this.useMocks) return mockInitialDeliberation(ceremonyRequestId, payload)
    return this.postJson<InitialDeliberationResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/deliberacion-inicial`, payload)
  }
  async uploadInterviewDocument(ceremonyRequestId: string, interviewId: string, file: File, metadata: Omit<CandidateInterviewEvidence, 'documentVersionId'>): Promise<InterviewDocumentResponse> {
    const extension = file.name.toLowerCase().split('.').pop()
    const contentType = file.type || (extension === 'pdf' ? 'application/pdf' : extension === 'docx' ? 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' : '')
    if (!['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'].includes(contentType)) throw new Error('La entrevista debe adjuntarse en Word (.docx) o PDF.')
    if (file.size <= 0 || file.size > 52_428_800) throw new Error('El archivo debe contener información y pesar como máximo 50 MB.')
    if (this.useMocks) {
      const documentVersionId = crypto.randomUUID()
      this.mockInterviewDocuments.set(documentVersionId, { file, fileName: file.name, metadata: { ...metadata, documentVersionId } })
      return { interviewId, documentVersionId, fileName: file.name, result: metadata.result, summary: metadata.summary, sizeBytes: file.size }
    }
    return this.request<InterviewDocumentResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/entrevistas/${encodeURIComponent(interviewId)}/contenido`, { method: 'PUT', headers: { 'Content-Type': contentType, 'X-File-Name': encodeURIComponent(file.name), 'X-Interviewer': encodeURIComponent(metadata.interviewerDisplayName), 'X-Interview-Summary': encodeURIComponent(metadata.summary), 'X-Interview-Result': metadata.result, 'X-Interview-Date': metadata.interviewDate }, body: file })
  }
  async recordInterviewPackage(ceremonyRequestId: string, payload: InterviewPackageRequest): Promise<InterviewPackageResponse> {
    if (this.useMocks) {
      if (payload.interviews.length < 3) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'third_degree_review.interviews', reason: `El expediente requiere al menos tres entrevistas completas; actualmente registra ${payload.interviews.length}.`, completedInterviews: payload.interviews.length, ceremonyStatus: 'under_review' }
      if (payload.interviews.some(item => !item.summary.trim() || !item.documentVersionId || !['favorable', 'desfavorable'].includes(item.result))) throw new Error('Cada entrevista requiere resumen, resultado y archivo Word o PDF.')
      if (!payload.confidentialQuestionnaireAvailable) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'third_degree_review.confidential_questionnaire', reason: 'Falta el Cuestionario Confidencial requerido para la revisión de tercer grado.', completedInterviews: payload.interviews.length, ceremonyStatus: 'under_review' }
      if (!payload.autobiographyAvailable) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'third_degree_review.autobiography', reason: 'Falta la autobiografía requerida para la revisión de tercer grado.', completedInterviews: payload.interviews.length, ceremonyStatus: 'under_review' }
      return { id: ceremonyRequestId, validationStatus: 'approved', code: 'third_degree_review.package_complete', reason: `El expediente contiene ${payload.interviews.length} entrevistas y los antecedentes requeridos.`, completedInterviews: payload.interviews.length, ceremonyStatus: 'under_review' }
    }
    return this.postJson<InterviewPackageResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/antecedentes`, payload)
  }
  async recordThirdDegreeReview(ceremonyRequestId: string, payload: ThirdDegreeReviewRequest): Promise<ThirdDegreeReviewResponse> {
    if (this.useMocks) {
      if (!payload.sourceReference.trim()) throw new Error('Debe indicar la referencia del extracto de acta.')
      if (payload.presentVoters <= 0 || payload.votesInFavor < 0 || payload.votesAgainst < 0 || payload.abstentions < 0 || payload.votesInFavor + payload.votesAgainst + payload.abstentions !== payload.presentVoters) throw new Error('La suma de votos debe coincidir con la asistencia registrada.')
      const status = payload.openVoteApproved ? 'approved' : 'rejected'
      return { thirdDegree: { id: ceremonyRequestId, status, code: `third_degree_review.${status}`, reason: payload.openVoteApproved ? 'La votación abierta de tercer grado fue favorable.' : 'La votación abierta de tercer grado no fue favorable.' }, status: payload.openVoteApproved ? 'under_review' : 'rejected' }
    }
    return this.postJson<ThirdDegreeReviewResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/revision-tercer-grado`, payload)
  }
  async recordFinalBallot(ceremonyRequestId: string, payload: FinalBallotRequest): Promise<FinalBallotResponse> {
    if (this.useMocks) {
      if (!payload.sourceReference.trim()) throw new Error('Debe indicar la referencia del extracto de acta.')
      if (payload.ballots.length < 1 || payload.ballots.length > 3 || new Set(payload.ballots.map(item => item.procedureNumber)).size !== payload.ballots.length) throw new Error('Debe registrar entre uno y tres trámites distintos.')
      if (payload.ballots.some(item => item.eligibleVoters <= 0 || item.whiteBallots < 0 || item.blackBallots < 0 || item.whiteBallots + item.blackBallots !== item.eligibleVoters)) throw new Error('Las balotas blancas y negras deben coincidir con las personas habilitadas.')
      return { id: ceremonyRequestId, validationStatus: payload.ballotApproved ? 'approved' : 'rejected', asOfDate: payload.ballotDate, code: payload.ballotApproved ? 'first_degree_ballot.approved' : 'first_degree_ballot.rejected', reason: payload.ballotApproved ? 'El balotaje definitivo fue favorable.' : 'El balotaje definitivo fue desfavorable.', ceremonyStatus: payload.ballotApproved ? 'under_review' : 'rejected' }
    }
    return this.postJson<FinalBallotResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/balotaje`, payload)
  }
  async submitInitiationRequest(ceremonyRequestId: string, payload: InitiationRequestSubmission): Promise<InitiationRequestSubmissionResponse> {
    if (this.useMocks) {
      if (payload.proposedCeremonyDate < payload.submissionDate) throw new Error('La fecha propuesta no puede ser anterior a la solicitud.')
      if (!payload.venerableApproval) throw new Error('La solicitud requiere confirmación del Venerable Maestro.')
      if (!payload.secretaryDisplayName.trim() || !payload.sourceReference.trim()) throw new Error('Debe indicar Secretaría responsable y referencia documental.')
      return { id: ceremonyRequestId, validationStatus: 'approved', proposedDate: payload.proposedCeremonyDate, ceremonyStatus: 'under_review', alreadySubmitted: false }
    }
    return this.postJson<InitiationRequestSubmissionResponse>(`/api/insinuados/solicitudes/${encodeURIComponent(ceremonyRequestId)}/solicitud-iniciacion`, payload)
  }

  async getTreasuryWorkshopRegularity(organizationId: string, asOf?: string): Promise<WorkshopRegularitySnapshot | null> {
    if (this.useMocks) return mockSnapshotAsOf(this.mockTreasury.get(organizationId), asOf)
    const query = new URLSearchParams(); if (asOf) query.set('asOf', asOf)
    return this.optionalGet<WorkshopRegularitySnapshot>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/regularidad${query.size ? `?${query}` : ''}`)
  }
  async setTreasuryWorkshopRegularity(organizationId: string, payload: WorkshopRegularityRequest): Promise<WorkshopRegularitySnapshot> {
    if (this.useMocks) { const snapshot = mockRegularitySnapshot(organizationId, payload, 'treasury'); this.mockTreasury.set(organizationId, snapshot); return snapshot }
    return this.postJson<WorkshopRegularitySnapshot>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/regularidad`, payload)
  }
  async createTreasuryStatement(organizationId: string, payload: CreateTreasuryStatementRequest): Promise<TreasuryStatement> {
    if (this.useMocks) {
      const statement = mockTreasuryStatement(organizationId, payload)
      this.mockTreasuryStatements.set(statement.id, statement)
      return cloneTreasuryStatement(statement)
    }
    return this.postJson<TreasuryStatement>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/cuadros`, payload)
  }
  async generateTreasuryStatementLines(statementId: string, payload: GenerateTreasuryLinesRequest): Promise<TreasuryStatement> {
    if (this.useMocks) {
      const statement = this.requireMockTreasuryStatement(statementId)
      const amounts = [payload.masterAmount, payload.masterAmount, payload.masterAmount, payload.fellowcraftAmount, payload.fellowcraftAmount, payload.apprenticeAmount, payload.apprenticeAmount]
      const degrees = ['master', 'master', 'master', 'fellowcraft', 'fellowcraft', 'apprentice', 'apprentice']
      const names = ['Venerable Maestra', 'Primer Vigilante', 'Segundo Vigilante', 'Compañero Uno', 'Compañera Dos', 'Aprendiz Uno', 'Aprendiza Dos']
      statement.lines = amounts.map((amount, index) => ({ id: crypto.randomUUID(), memberId: `demo-member-${index + 1}`, membershipId: `demo-membership-${index + 1}`, degreeCodeAtCutoff: degrees[index], officeCodeAtCutoff: index < 3 ? ['VM', 'PV', 'SV'][index] : null, baseAmount: amount, adjustmentAmount: index === 2 ? -8000 : 0, payableAmount: index === 2 ? amount - 8000 : amount, adjustmentType: index === 2 ? 'senior_discount' : null, authorizationReference: index === 2 ? 'Plancha DEMO-023/2026' : null, observation: names[index], identityMatchStatus: 'matched' }))
      recalculateMockTreasury(statement); return cloneTreasuryStatement(statement)
    }
    return this.postJson<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/generar-lineas`, payload)
  }
  async addTreasuryStatementPayment(statementId: string, payload: AddTreasuryPaymentRequest): Promise<TreasuryStatement> {
    if (this.useMocks) {
      const statement = this.requireMockTreasuryStatement(statementId)
      statement.payments.push({ id: crypto.randomUUID(), paymentMethod: payload.paymentMethod, paymentDate: payload.paymentDate, amount: payload.amount, payerDisplayName: payload.payerDisplayName ?? null, reference: payload.reference ?? null, recordedAtUtc: new Date().toISOString() })
      recalculateMockTreasury(statement); return cloneTreasuryStatement(statement)
    }
    await this.postJson<unknown>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/pagos`, payload)
    return this.getTreasuryStatement(statementId)
  }
  async submitTreasuryStatement(statementId: string): Promise<TreasuryStatement> {
    if (this.useMocks) { const statement = this.requireMockTreasuryStatement(statementId); statement.status = 'submitted'; statement.submittedAtUtc = new Date().toISOString(); return cloneTreasuryStatement(statement) }
    return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/enviar`, { method: 'POST' })
  }
  async reconcileTreasuryStatement(statementId: string): Promise<TreasuryStatement> {
    if (this.useMocks) { const statement = this.requireMockTreasuryStatement(statementId); if (statement.differenceAmount !== 0) throw new Error('El cuadro mantiene una diferencia pendiente.'); statement.status = 'reconciled'; statement.reconciledAtUtc = new Date().toISOString(); return cloneTreasuryStatement(statement) }
    return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/conciliar`, { method: 'POST' })
  }
  async getTreasuryStatement(statementId: string): Promise<TreasuryStatement> { if (this.useMocks) return cloneTreasuryStatement(this.requireMockTreasuryStatement(statementId)); return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}`) }
  async getHospitalariaWorkshopRegularity(organizationId: string, asOf?: string): Promise<WorkshopRegularitySnapshot | null> {
    if (this.useMocks) return mockSnapshotAsOf(this.mockHospitalaria.get(organizationId), asOf)
    const query = new URLSearchParams(); if (asOf) query.set('asOf', asOf)
    return this.optionalGet<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad${query.size ? `?${query}` : ''}`)
  }
  async setHospitalariaWorkshopRegularity(organizationId: string, payload: WorkshopRegularityRequest): Promise<WorkshopRegularitySnapshot> {
    if (this.useMocks) { const snapshot = mockRegularitySnapshot(organizationId, payload, 'hospitalaria'); this.mockHospitalaria.set(organizationId, snapshot); return snapshot }
    return this.postJson<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad`, payload)
  }

  async getSecretariatAvailability(fromUtc: string, toUtc: string): Promise<SpaceAvailabilityResponse> { if (this.useMocks) { const items = this.mockSpaces.map(space => ({ ...space, isAvailable: !this.mockBusySpaces.has(space.id) })); return { fromUtc, toUtc, total: items.length, available: items.filter(x => x.isAvailable).length, items } } const query = new URLSearchParams({ fromUtc, toUtc }); return this.request<SpaceAvailabilityResponse>(`/api/gran-secretaria/espacios/disponibilidad?${query}`) }
  async getSecretariatDocuments(): Promise<SecretariatDocumentsResponse> { if (this.useMocks) return { total: this.mockDocuments.length, items: [...this.mockDocuments] }; return this.request<SecretariatDocumentsResponse>('/api/gran-secretaria/documentos') }
  async getSecretariatCeremonyQueue(): Promise<GrandSecretariatCeremonyQueueResponse> { if (this.useMocks) return { total: this.mockCeremonies.length, items: this.mockCeremonies.map(item => ({ ...item })) }; return this.request<GrandSecretariatCeremonyQueueResponse>('/api/institutional/gran-secretaria/ceremonias-autorizadas') }
  async createSecretariatSpace(payload: CreateSpaceRequest): Promise<InstitutionalSpace> { if (this.useMocks) { const space: InstitutionalSpace = { id: crypto.randomUUID(), ...payload, location: payload.location ?? null, capacity: payload.capacity ?? null, status: 'active' }; this.mockSpaces.push(space); return space } return this.postJson<InstitutionalSpace>('/api/gran-secretaria/espacios', payload) }
  async createSecretariatReservation(payload: CreateReservationRequest): Promise<{ id: string; status: string }> {
    if (this.useMocks) {
      if (this.mockBusySpaces.has(payload.spaceId)) throw new Error('El templo o sala ya está reservado en ese horario.')
      const ceremony = payload.ceremonyRequestId ? this.mockCeremonies.find(item => item.id === payload.ceremonyRequestId) : undefined
      if (payload.ceremonyRequestId && !ceremony) throw new Error('La ceremonia indicada no existe en la bandeja autorizada.')
      if (ceremony && ceremony.organizationId !== payload.organizationId) throw new Error('La ceremonia no corresponde al Taller indicado.')
      const id = crypto.randomUUID(); this.mockBusySpaces.add(payload.spaceId)
      if (ceremony) { const space = this.mockSpaces.find(item => item.id === payload.spaceId); ceremony.spaceReservationId = id; ceremony.spaceName = space?.name ?? 'Espacio institucional'; ceremony.reservationStartsAtUtc = payload.startsAtUtc; ceremony.reservationEndsAtUtc = payload.endsAtUtc }
      return { id, status: 'reserved' }
    }
    return this.postJson<{ id: string; status: string }>('/api/gran-secretaria/reservas', payload)
  }
  async issueSecretariatDocument(payload: IssueDocumentRequest): Promise<SecretariatDocument> { if (this.useMocks) { const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: payload.documentType, documentCode: `${payload.documentType === 'decree' ? 'DEC' : 'COM'}-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: payload.title, content: payload.content, organizationId: payload.organizationId ?? null, relatedCeremonyRequestId: null, spaceReservationId: null, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document } return this.postJson<SecretariatDocument>('/api/gran-secretaria/documentos', payload) }
  async issueSecretariatCeremonyAuthorization(ceremonyRequestId: string, spaceReservationId: string | null = null): Promise<SecretariatDocument> {
    if (this.useMocks) {
      const ceremony = this.mockCeremonies.find(item => item.id === ceremonyRequestId); if (!ceremony) throw new Error('La ceremonia indicada no existe.'); if (ceremony.formalAuthorizationIssued) throw new Error('La ceremonia ya cuenta con autorización formal vigente.'); if (spaceReservationId && ceremony.spaceReservationId !== spaceReservationId) throw new Error('La reserva indicada no corresponde a esta ceremonia.'); ceremony.formalAuthorizationIssued = true
      const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: 'ceremony_authorization', documentCode: `AUT-CER-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: `Autorización de ceremonia — ${ceremonyTypeLabel(ceremony.ceremonyType)}`, content: `Autorización institucional demostrativa para ${ceremony.organizationName}.`, organizationId: ceremony.organizationId, relatedCeremonyRequestId: ceremony.id, spaceReservationId, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document
    }
    return this.postJson<SecretariatDocument>(`/api/gran-secretaria/ceremonias/${encodeURIComponent(ceremonyRequestId)}/autorizacion`, { spaceReservationId })
  }

  private requireMockReviewCeremony(id: string): CeremonyReviewQueueItem { const item = this.mockReviewCeremonies.find(value => value.id === id); if (!item) throw new Error('La ceremonia indicada no existe en la bandeja.'); return item }
  private requireMockTreasuryStatement(id: string): TreasuryStatement { const item = this.mockTreasuryStatements.get(id); if (!item) throw new Error('El cuadro mensual indicado no existe.'); return item }
  private postJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private async optionalGet<T>(path: string): Promise<T | null> { try { return await this.request<T>(path) } catch (error) { if (error instanceof PmgmApiHttpError && error.status === 404) return null; throw error } }
  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para consultar la información institucional.'); headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* respuesta sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para realizar esta operación.'
      throw new PmgmApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultPmgmApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): PmgmApiClient { const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''; const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'; const url = new URL(baseUrl || '/', window.location.origin); if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La API debe usar el mismo origen mediante el proxy institucional.'); return new PmgmApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized }) }
function sleep(milliseconds: number): Promise<void> { return new Promise(resolve => window.setTimeout(resolve, milliseconds)) }
function mockInitialDeliberation(ceremonyRequestId: string, payload: InitialDeliberationRequest): InitialDeliberationResponse {
  if (!payload.sourceReference.trim()) throw new Error('Debe indicar la referencia del acta o extracto que respalda la deliberación.')
  if (payload.presentVoters <= 0) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.quorum', reason: 'Debe existir al menos una persona habilitada presente para registrar la votación.', ceremonyStatus: 'under_review' }
  if (payload.votesInFavor < 0 || payload.votesInFavor > payload.presentVoters) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.votes', reason: 'La cantidad de votos favorables no es válida.', ceremonyStatus: 'under_review' }
  const elapsedDays = dateOnlyDayNumber(payload.deliberationDate) - dateOnlyDayNumber('2026-09-12')
  const minimumWaitingDays = payload.minimumWaitingDays ?? 7
  if (elapsedDays < minimumWaitingDays) return { id: ceremonyRequestId, validationStatus: 'observed', code: 'initial_deliberation.waiting_period', reason: `Deben transcurrir al menos ${minimumWaitingDays} días desde la presentación; han transcurrido ${elapsedDays}.`, ceremonyStatus: 'under_review' }
  if (payload.votesInFavor !== payload.presentVoters) return { id: ceremonyRequestId, validationStatus: 'rejected', code: 'initial_deliberation.unanimity', reason: 'La aprobación inicial requiere unanimidad de las personas presentes.', ceremonyStatus: 'rejected' }
  return { id: ceremonyRequestId, validationStatus: 'approved', code: 'initial_deliberation.approved', reason: 'Se cumple el plazo mínimo y la votación inicial fue unánime.', ceremonyStatus: 'under_review' }
}
function dateOnlyDayNumber(value: string) { const [year, month, day] = value.split('-').map(Number); if (!year || !month || !day) throw new Error('La fecha de deliberación no es válida.'); return Math.floor(Date.UTC(year, month - 1, day) / 86_400_000) }
function ceremonyTypeLabel(type: CeremonyType) { return type === 'initiation' ? 'Iniciación' : type === 'wage_increase' ? 'Aumento de salario' : 'Exaltación' }
function mockSnapshotAsOf(snapshot: WorkshopRegularitySnapshot | undefined, asOf?: string): WorkshopRegularitySnapshot | null { if (!snapshot) return null; if (asOf && snapshot.asOfDate > asOf) return null; return { ...snapshot } }
function mockRegularitySnapshot(organizationId: string, payload: WorkshopRegularityRequest, prefix: string): WorkshopRegularitySnapshot { return { id: `${prefix}-${crypto.randomUUID()}`, organizationId, scope: 'organization', status: payload.status, asOfDate: payload.asOfDate, sourceReference: payload.sourceReference ?? null, notes: payload.notes ?? null, recordedAtUtc: new Date().toISOString() } }
function mockTreasuryStatement(organizationId: string, payload: CreateTreasuryStatementRequest): TreasuryStatement { return { id: crypto.randomUUID(), organizationId, periodYear: payload.periodYear, periodMonth: payload.periodMonth, cutoffDate: payload.cutoffDate, status: 'draft', sourceReference: payload.sourceReference ?? null, expectedAmount: 0, transferAmount: 0, depositAmount: 0, paidAmount: 0, differenceAmount: 0, unresolvedIdentities: 0, lines: [], payments: [], submittedAtUtc: null, reconciledAtUtc: null, closedAtUtc: null } }
function recalculateMockTreasury(statement: TreasuryStatement) { statement.expectedAmount = statement.lines.reduce((total, line) => total + line.payableAmount, 0); statement.transferAmount = statement.payments.filter(payment => payment.paymentMethod === 'transfer').reduce((total, payment) => total + payment.amount, 0); statement.depositAmount = statement.payments.filter(payment => payment.paymentMethod === 'deposit').reduce((total, payment) => total + payment.amount, 0); statement.paidAmount = statement.transferAmount + statement.depositAmount; statement.differenceAmount = statement.expectedAmount - statement.paidAmount }
function cloneTreasuryStatement(statement: TreasuryStatement): TreasuryStatement { return { ...statement, lines: statement.lines.map(line => ({ ...line })), payments: statement.payments.map(payment => ({ ...payment })) } }
function cloneCeremonyQueueItem(item: CeremonyReviewQueueItem): CeremonyReviewQueueItem { return { ...item, eligibility: { ...item.eligibility, publication: item.eligibility.publication ? { ...item.eligibility.publication } : null, requirements: item.eligibility.requirements.map(value => ({ ...value })) }, actions: { ...item.actions } } }
function recomputeMockEligibility(item: CeremonyReviewQueueItem) { const blocked = item.eligibility.requirements.some(value => value.status === 'rejected'); const observed = item.eligibility.requirements.some(value => value.status === 'observed'); item.eligibility.canAuthorize = !blocked && !observed; item.eligibility.status = item.eligibility.canAuthorize ? 'complies' : observed ? 'observed' : 'does_not_comply' }
function mockRegimenSummary(filters: { organizationId?: string; asOf?: string; from?: string }): RegimenInteriorSummary { const asOf = filters.asOf ?? '2026-09-08'; const from = filters.from ?? '2026-01-01'; const scoped = !!filters.organizationId; return { scope: scoped ? 'organization' : 'order', organizationId: filters.organizationId ?? null, asOf, period: { from, to: asOf }, members: { totalRelated: scoped ? 41 : 315, currentlyAffiliated: scoped ? 34 : 268, active: scoped ? 31 : 241, inactive: scoped ? 3 : 27, currentWithBlockingStatus: scoped ? 2 : 18 }, events: { voluntaryWithdrawals: scoped ? 1 : 9, forcedWithdrawals: scoped ? 0 : 3, reinstatements: scoped ? 1 : 7, deaths: scoped ? 0 : 4, transfers: scoped ? 2 : 13 }, financialRegularity: { source: 'Gran Tesorería', currentAffiliations: scoped ? 34 : 268, upToDate: scoped ? 28 : 221, delinquent: scoped ? 4 : 29, pending: scoped ? 1 : 10, exempt: scoped ? 1 : 5, withoutStatus: scoped ? 0 : 3, delinquentMembersDistinct: scoped ? 4 : 28 }, degreeDistribution: scoped ? { apprentice: 9, fellowcraft: 8, master: 17 } : { apprentice: 71, fellowcraft: 63, master: 134 }, pendingTransfers: scoped ? 1 : 6 } }
