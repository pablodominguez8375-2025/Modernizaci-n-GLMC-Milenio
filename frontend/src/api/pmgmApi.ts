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
  canReadLodgeSecretariat?: boolean
  canManageLodgeSecretariat?: boolean
  canManageLodgeTreasury?: boolean
  canReadLodgeHospitalaria?: boolean
  canManageLodgeHospitalaria?: boolean
  canApproveLodgeExpenses?: boolean
  canConfigureSystem?: boolean
}
export interface SessionProfile { displayName: string; accessScope: 'order' | 'organization' | 'authenticated'; capabilities: SessionCapabilities }
export type TreasuryTerritory = 'santiago' | 'other_oriente' | 'peru'
export interface OrganizationOption { id: string; name: string; number: string | null; type: string }
export interface TreasuryTerritoryOption extends OrganizationOption { treasuryTerritory: TreasuryTerritory | null }
export interface OrganizationOptionsResponse { total: number; items: OrganizationOption[] }
export interface SystemSetting { code: string; category: string; label: string; valueType: 'integer' | 'text' | 'list'; value: string; effectiveFrom: string; sourceReference: string; status: string }
export interface SystemSettingsResponse { total: number; items: SystemSetting[] }
export interface CreateSystemSettingVersionRequest { value: string; effectiveFrom: string; sourceReference: string }
export interface SystemSettingVersion { id: string; value: string; effectiveFrom: string; effectiveTo: string | null; sourceReference: string; status: string; createdAtUtc: string }
export interface SystemSettingVersionsResponse { total: number; items: SystemSettingVersion[] }
export interface AuditLogEvent { id:string; occurredAtUtc:string; user:string; ipAddress:string; menu:string|null; submenu:string|null; summary:string; action:string; result:'success'|'rejected'|'observed'; correlationId:string }
export interface AuditLogResponse { total:number; page:number; pageSize:number; immutable:boolean; items:AuditLogEvent[] }
export type LodgeFeeType = 'normal' | 'student' | 'senior' | 'spouse' | 'past_active'
export interface LodgeFeePlan { id: string; organizationId: string; feeType: LodgeFeeType; memberAmount: number; grandTreasuryAmount: number | null; workshopAmount: number | null; rateAvailable?: boolean; effectiveFrom: string; effectiveUntil: string | null; isActive: boolean }
export interface LodgeTreasurySummary { organizationId: string; periodYear: number; periodMonth: number; members: number; memberExpected: number; collected: number; receivable: number; grandTreasuryExpected: number; workshopMarginProjected: number; paid: number; partial: number; overdue: number; trafficLight: 'green' | 'amber' | 'red' | 'no_data' }
export interface LodgeTreasuryPayment { id:string; receiptNumber:string; amount:number; paymentMethod:'cash'|'transfer'|'deposit'|'other'; paymentDate:string; reference:string|null; idempotencyKey?:string }
export interface LodgeTreasuryCharge { id:string; memberId:string; memberDisplayName:string; memberAmount:number; feeType?:LodgeFeeType; monthlyFeeAmount?:number; maxPaymentAmount?:number; paidAmount:number; balance:number; status:'pending'|'partial'|'paid'; payments:LodgeTreasuryPayment[] }
export interface LodgeTreasuryExpense { id:string; organizationId:string; category:string; amount:number; expenseDate:string; description:string; evidenceReference:string|null; approvalStatus:'pending_approval'|'approved'; recordedBySubject:string; approvedBySubject:string|null; approvedAtUtc:string|null; recordedAtUtc:string }
export interface LodgeTreasuryIncome { id:string; organizationId:string; category:string; amount:number; incomeDate:string; description:string; evidenceReference:string|null; recordedBySubject:string; recordedAtUtc:string }
export interface LodgeCashSummary { organizationId:string; asOf:string; openingBalance:number; cumulativeIncome:number; cumulativeExpense:number; cumulativeBalance:number; monthIncome:number; monthExpense:number; monthBalance:number; pendingExpenses:number }
export interface LodgeTreasuryReconciliation { id:string; organizationId:string; from:string; to:string; openingBalance:number; income:number; authorizedExpenses:number; pendingExpenses:number; closingBalance:number; observedBalance:number; difference:number; movementCount:number; evidenceReference:string|null; notes:string|null; recordedBySubject:string; recordedAtUtc:string }
export interface LodgeTreasuryReport { organizationId:string; from:string; to:string; openingBalance:number; income:number; authorizedExpenses:number; pendingExpenses:number; closingBalance:number; observedBalance:number|null; difference:number|null; reconciliationHistory?:LodgeTreasuryReconciliation[]; monthlyTotals?:{period:string;type:'ingreso'|'egreso';category:string;debit:number;credit:number;net:number;pendingAmount:number;count:number}[]; movements:{transactionId:string;date:string;type:'ingreso'|'egreso';category:string;description:string;amount:number;status:string;reference:string|null;paymentMethod?:string|null;recordedBySubject:string;recordedAtUtc:string;approvedBySubject:string|null;approvedAtUtc:string|null}[] }
export interface LodgeTreasuryYearClosure { id:string; organizationId:string; accountingYear:number; openingBalance:number; income:number; authorizedExpenses:number; closingBalance:number; movementCount:number; closedBySubject:string; closedAtUtc:string }
export interface LodgeTreasuryConfiguration { organizationId:string; openingBalance:number; openingBalanceDate:string; incomeCategories:string; expenseCategories:string }
export interface InstitutionalSpace { id: string; code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location: string | null; capacity: number | null; status?: string; isAvailable?: boolean }
export interface SpaceAvailabilityResponse { fromUtc: string; toUtc: string; total: number; available: number; items: InstitutionalSpace[] }
export type SecretariatDocumentType = 'decree' | 'plancha' | 'communication' | 'ceremony_authorization' | 'ceremony_authorization_plancha'
export type SecretariatPlanchaKind = 'formal_communication' | 'ceremony_authorization'
export interface SecretariatDocument { id: string; documentType: SecretariatDocumentType; planchaKind: SecretariatPlanchaKind | null; documentCode: string; title: string; content: string; organizationId: string | null; relatedCeremonyRequestId: string | null; spaceReservationId: string | null; status: string; issuedAtUtc: string; issuedBySubject: string }
export interface SecretariatDocumentsResponse { total: number; items: SecretariatDocument[] }
export interface GrandSecretariatTenidaItem {
  recordId: string
  lodge: { id: string; name: string; number: string | null }
  meetingId: string
  meetingDate: string
  meetingType: string
  grade: string
  ceremonyType: CeremonyType | null
  modality: 'in_person' | 'virtual'
  title: string | null
  meetingStatus: string
  extractDocumentVersionId: string
  submissionStatus: 'submitted' | 'received' | 'observed'
  submittedAtUtc: string
  reviewedAtUtc: string | null
  reviewNotes: string | null
}
export interface GrandSecretariatTenidasResponse { total: number; items: GrandSecretariatTenidaItem[] }
export interface CreateSpaceRequest { code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location?: string | null; capacity?: number | null }
export interface CreateReservationRequest { spaceId: string; organizationId: string; ceremonyRequestId?: string | null; purpose: string; startsAtUtc: string; endsAtUtc: string; notes?: string | null }
export interface IssueDocumentRequest { documentType: 'decree' | 'plancha'; planchaKind?: 'formal_communication' | null; title: string; content: string; organizationId?: string | null }
export type CeremonyType = 'initiation' | 'affiliation' | 'wage_increase' | 'exaltation' | 'incorporation'
export type CeremonyRequestStatus = 'draft' | 'under_review' | 'eligible' | 'observed' | 'rejected' | 'authorized'
export type CeremonyValidationStatus = 'pending' | 'approved' | 'observed' | 'rejected' | 'not_applicable' | 'exception_approved'
export interface GrandSecretariatCeremonyQueueItem {
  id: string; organizationId: string; organizationName: string; organizationNumber: string | null; ceremonyType: CeremonyType; proposedDate: string | null; status: string; formalAuthorizationIssued: boolean
  spaceReservationId: string | null; spaceName: string | null; reservationStartsAtUtc: string | null; reservationEndsAtUtc: string | null; createdAtUtc: string
}
export interface GrandSecretariatCeremonyQueueResponse { total: number; items: GrandSecretariatCeremonyQueueItem[] }
export interface CeremonyQueueRequirement { code: string; name: string; status: string; reason: string }
export interface CeremonyQueuePublication { status: string; requiredDays: number; completedDays: number; publishedFromUtc: string; publishedUntilUtc: string | null }
export interface CeremonyRightSummary { amount: number; currency: string; paid: number; balance: number; source: string }
export interface TreasuryCeremonyRightItem extends CeremonyRightSummary { id: string; organizationId: string; organizationName: string; organizationNumber: string | null; ceremonyType: CeremonyType; proposedDate: string | null; subjectDisplayName: string }
export interface TreasuryCeremonyRightsResponse { total: number; items: TreasuryCeremonyRightItem[] }
export interface CeremonyQueueEligibility { status: string; canAuthorize: boolean; requirements: CeremonyQueueRequirement[]; publication: CeremonyQueuePublication | null; ceremonyRight?: CeremonyRightSummary | null }
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

export type HospitalariaMovementType = 'income' | 'expense'
export type HospitalariaMovementCategory = 'death_replenishment' | 'annual_replenishment_fund' | 'charity_bag' | 'voluntary_contribution' | 'initiation_fee' | 'charity_aid' | 'supplies' | 'ceremony'
export interface LodgeHospitalariaMovement {
  id:string; organizationId:string; movementType:HospitalariaMovementType; category:HospitalariaMovementCategory; amount:number; movementDate:string
  memberReference:string|null; destination:string|null; evidenceReference:string|null; observation:string|null
  approvalStatus:'pending_approval'|'approved'|'not_required'; approvalSource:'venerable_master'|'lodge_council'|null; councilDecisionId:string|null
  approvedBySubject:string|null; approvedAtUtc:string|null; recordedAtUtc:string
}
export interface CreateLodgeHospitalariaMovementRequest {
  movementType:HospitalariaMovementType; category:HospitalariaMovementCategory; amount:number; movementDate:string
  memberReference?:string|null; destination?:string|null; evidenceReference?:string|null; observation?:string|null
}
export interface LodgeHospitalariaSummary {
  organizationId:string; from:string; to:string; income:number; approvedExpenses:number; periodNet:number; pendingExpenses:number; movements:number
  categories:Array<{category:string;total:number;count:number}>; items:LodgeHospitalariaMovement[]
}
export interface HospitalariaCouncilAidDecision { id:string; sessionId:string; sessionDate:string; subject:string; amount:number|null }
export interface HospitalariaCouncilFinancialReview { id:string; sessionId:string; sessionDate:string; periodLabel:string; conclusion:string }
export interface HospitalariaMonthlySubmission {
  id:string; organizationId:string; periodYear:number; periodMonth:number; cutoffDate:string
  incomeAmount:number; approvedExpenseAmount:number; periodNetAmount:number; movementCount:number; pendingExpenseCount:number
  replenishmentDueAmount:number; replenishmentPaidAmount:number; differenceAmount:number; paymentReference:string|null
  councilFinancialReviewId:string|null; status:'draft'|'submitted'|'observed'|'reconciled'; sourceReference:string|null
  createdAtUtc:string; submittedAtUtc:string|null; reviewedAtUtc:string|null; reviewNotes:string|null
}
export interface UpsertHospitalariaMonthlySubmissionRequest {
  replenishmentDueAmount:number; replenishmentPaidAmount:number; paymentReference?:string|null; councilFinancialReviewId?:string|null; sourceReference?:string|null
}
export interface GrandHospitalariaSubmission extends HospitalariaMonthlySubmission {
  organizationName:string; organizationNumber:string|null
}
export interface HospitalariaSubmissionListResponse<T = HospitalariaMonthlySubmission> { total:number; items:T[] }


export interface HospitalariaReplenishmentRate { id:string; amountPerActiveMember:number; effectiveFrom:string; effectiveUntil:string|null; sourceReference:string; createdBySubject?:string }
export interface DeathReplenishmentPayment { id:string; amount:number; paymentMethod:string; paymentDate:string; receiptNumber:string; reference:string }
export interface DeathReplenishmentObligation { id:string; caseId:string; deathDate:string; deceasedDisplayName:string; memberId:string; memberDisplayName:string; amountDue:number; paidAmount:number; balance:number; status:'pending'|'partial'|'paid'; payments:DeathReplenishmentPayment[] }
export interface DeathReplenishmentTransfer { id:string; organizationId:string; organizationName?:string; submissionNumber?:number; amount:number; expectedAmount:number; transferDate:string; reference:string; status:'submitted'|'observed'|'reconciled'; reviewNotes?:string|null }
export interface DeathReplenishmentCase { id:string; deathDate:string; deceasedDisplayName:string; amountPerActiveMember:number; obligatedMembers:number; dueAmount:number; paidAmount:number; pendingMembers:number; transfers:DeathReplenishmentTransfer[] }
export interface WorkshopDeathReplenishments { organizationId:string; items:DeathReplenishmentObligation[]; cases:Array<{caseId:string;deathDate:string;deceasedDisplayName:string;dueAmount:number;paidAmount:number;allPaid:boolean;transfer:Pick<DeathReplenishmentTransfer,'id'|'amount'|'reference'|'status'|'transferDate'>|null}> }

export interface TreasuryStatementLine {
  id: string; memberId: string | null; membershipId: string | null; degreeCodeAtCutoff: string; officeCodeAtCutoff: string | null
  rut?: string | null; firstNames?: string | null; lastNames?: string | null
  baseAmount: number; adjustmentAmount: number; payableAmount: number; adjustmentType: string | null; authorizationReference: string | null
  observation: string | null; identityMatchStatus: string; contributionType: LodgeFeeType
}
export interface TreasuryFeeBreakdown { feeType: LodgeFeeType; members: number; amount: number }
export interface TreasuryStatementPayment { id: string; paymentMethod: string; paymentDate: string; amount: number; payerDisplayName: string | null; reference: string | null; recordedAtUtc?: string }
export interface TreasuryStatement {
  id: string; organizationId: string; periodYear: number; periodMonth: number; cutoffDate: string; status: string; sourceReference: string | null
  expectedAmount: number; transferAmount: number; depositAmount: number; paidAmount: number; differenceAmount: number; unresolvedIdentities: number
  feeBreakdown: TreasuryFeeBreakdown[]; lines: TreasuryStatementLine[]; payments: TreasuryStatementPayment[]; submittedAtUtc: string | null; reconciledAtUtc: string | null; closedAtUtc: string | null
}
export interface CreateTreasuryStatementRequest { periodYear: number; periodMonth: number; cutoffDate: string; sourceReference?: string | null }
export interface TreasuryStatementSummary { id: string; organizationId: string; periodYear: number; periodMonth: number; cutoffDate: string; status: string; sourceReference: string | null; submittedAtUtc: string | null; reconciledAtUtc: string | null; closedAtUtc: string | null }
export interface TreasuryStatementListResponse { total: number; items: TreasuryStatementSummary[] }
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
    canManageLodgeTreasury: true,
    canConfigureSystem: true,
  },
}
const defaultMockSystemSettings: SystemSetting[] = [
  { code:'system.workflow.initiation.approval_steps',category:'Flujos',label:'Aprobaciones de iniciación',valueType:'list',value:'Régimen Interior|Gran Tesorería|Gran Hospitalaria|Gran Secretaría|Gran Maestría',effectiveFrom:'2026-01-01',sourceReference:'Protocolo 2026',status:'default' },
  { code:'system.publication.candidate.minimum_days',category:'Publicaciones',label:'Días mínimos de publicación',valueType:'integer',value:'20',effectiveFrom:'2026-01-01',sourceReference:'Protocolo 2026',status:'default' },
  { code:'system.interviews.minimum_count',category:'Procesos',label:'Entrevistas mínimas',valueType:'integer',value:'3',effectiveFrom:'2026-01-01',sourceReference:'Protocolo 2026',status:'default' },
  { code:'system.rejection.block_months',category:'Procesos',label:'Meses de bloqueo tras rechazo',valueType:'integer',value:'12',effectiveFrom:'2026-01-01',sourceReference:'Reglamento General art. 1.5',status:'default' },
  { code:'system.library.document_types',category:'Biblioteca',label:'Tipos de publicación',valueType:'list',value:'Plancha|Libro|Revista|Ritual|Historia|Circular',effectiveFrom:'2026-01-01',sourceReference:'Catálogo institucional QA',status:'default' },
  { code:'system.library.catalog_fields',category:'Biblioteca',label:'Campos de catalogación',valueType:'list',value:'Autor|Título|Grado|Tema|Fecha|Palabras clave',effectiveFrom:'2026-01-01',sourceReference:'Catálogo institucional QA',status:'default' },
  { code:'system.archive.document_series',category:'Gran Archivo',label:'Series documentales',valueType:'list',value:'Decretos|Actas Gran Asamblea|Correspondencia|Patrimonio histórico',effectiveFrom:'2026-01-01',sourceReference:'Clasificación institucional QA',status:'default' },
  { code:'system.archive.retention_policy',category:'Gran Archivo',label:'Política de conservación',valueType:'text',value:'Según tabla de retención institucional vigente',effectiveFrom:'2026-01-01',sourceReference:'Política institucional',status:'default' },
  { code:'system.documents.allowed_extensions',category:'Gestor documental',label:'Extensiones permitidas',valueType:'list',value:'pdf|docx|xlsx|jpg|png',effectiveFrom:'2026-01-01',sourceReference:'Seguridad documental',status:'default' },
  { code:'system.documents.maximum_size_mb',category:'Gestor documental',label:'Tamaño máximo por archivo (MB)',valueType:'integer',value:'50',effectiveFrom:'2026-01-01',sourceReference:'Seguridad documental',status:'default' },
  { code:'system.treasury.cutoff_day',category:'Tesorería',label:'Día de corte mensual',valueType:'integer',value:'5',effectiveFrom:'2026-01-01',sourceReference:'Configuración financiera QA',status:'default' },
  { code:'system.hospitalaria.replacement_days',category:'Hospitalaria',label:'Plazo de reposición (días)',valueType:'integer',value:'30',effectiveFrom:'2026-01-01',sourceReference:'Configuración hospitalaria QA',status:'default' },
  { code:'system.notifications.reminder_days',category:'Notificaciones',label:'Anticipación de recordatorios (días)',valueType:'integer',value:'3',effectiveFrom:'2026-01-01',sourceReference:'Configuración operativa QA',status:'default' },
  { code:'system.security.session_minutes',category:'Seguridad',label:'Duración de sesión (minutos)',valueType:'integer',value:'30',effectiveFrom:'2026-01-01',sourceReference:'Política de seguridad',status:'default' },
  { code:'system.permissions.system_administrators',category:'Perfiles y permisos',label:'Administradores habilitados',valueType:'list',value:'Superadministrador|Administrador Gran Logia',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_venerable',category:'Perfiles y permisos',label:'Venerable Maestro',valueType:'list',value:'Gestión del Taller|Insinuados|Circuito de iniciación|Aprobar egresos|Firmar documentos|Ceremonias',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_secretariat',category:'Perfiles y permisos',label:'Secretaría del Taller',valueType:'list',value:'Datos administrativos|Tenidas y asistencia|Actas|Insinuados|Documentos',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_treasury',category:'Perfiles y permisos',label:'Tesorería del Taller',valueType:'list',value:'Planes de cuota|Ingresos|Egresos|Estado de pagos|Reportes de Tesorería',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_hospitalaria',category:'Perfiles y permisos',label:'Hospitalaria del Taller',valueType:'list',value:'Aportes|Egresos|Reposiciones|Estado de obligaciones|Reportes de Hospitalaria',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_orator',category:'Perfiles y permisos',label:'Orador del Taller',valueType:'list',value:'Revisión normativa|Firmar retiros|Firmar documentos definidos|Consulta de actas',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_first_warden',category:'Perfiles y permisos',label:'Primer Vigilante',valueType:'list',value:'Docencia de Compañeros|Seguimiento formativo|Evaluaciones docentes',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_second_warden',category:'Perfiles y permisos',label:'Segundo Vigilante',valueType:'list',value:'Docencia de Aprendices|Seguimiento formativo|Evaluaciones docentes',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.lodge_past_master',category:'Perfiles y permisos',label:'Ex Venerable Maestro',valueType:'list',value:'Apoyo docente|Consulta histórica|Acompañamiento al Taller',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.permissions.grand_dignitaries',category:'Perfiles y permisos',label:'Grandes dignatarios',valueType:'list',value:'Régimen Interior|Gran Tesorería|Gran Hospitalaria|Gran Secretaría|Gran Archivo|Gran Maestría',effectiveFrom:'2026-01-01',sourceReference:'Matriz de perfiles Proyecto Centenario',status:'default' },
  { code:'system.backup.schedule',category:'Respaldo y restauración',label:'Programación de respaldo',valueType:'text',value:'Diario 02:00 America/Santiago',effectiveFrom:'2026-01-01',sourceReference:'Política de continuidad QA',status:'default' },
  { code:'system.backup.retention_days',category:'Respaldo y restauración',label:'Retención de respaldos (días)',valueType:'integer',value:'30',effectiveFrom:'2026-01-01',sourceReference:'Política de continuidad QA',status:'default' },
  { code:'system.backup.offsite_required',category:'Respaldo y restauración',label:'Copia externa obligatoria',valueType:'text',value:'Sí',effectiveFrom:'2026-01-01',sourceReference:'Política de continuidad QA',status:'default' },
  { code:'system.mail.smtp_host',category:'Correo electrónico',label:'Servidor SMTP',valueType:'text',value:'smtp.ejemplo.cl',effectiveFrom:'2026-01-01',sourceReference:'Configuración demostrativa',status:'default' },
  { code:'system.mail.smtp_port',category:'Correo electrónico',label:'Puerto SMTP',valueType:'integer',value:'587',effectiveFrom:'2026-01-01',sourceReference:'Configuración demostrativa',status:'default' },
  { code:'system.mail.sender_address',category:'Correo electrónico',label:'Cuenta remitente',valueType:'text',value:'notificaciones@ejemplo.cl',effectiveFrom:'2026-01-01',sourceReference:'Configuración demostrativa',status:'default' },
  { code:'system.mail.sender_name',category:'Correo electrónico',label:'Nombre del remitente',valueType:'text',value:'Gran Logia Mixta de Chile',effectiveFrom:'2026-01-01',sourceReference:'Configuración demostrativa',status:'default' },
  { code:'system.mail.security',category:'Correo electrónico',label:'Seguridad de transporte',valueType:'list',value:'STARTTLS',effectiveFrom:'2026-01-01',sourceReference:'Configuración demostrativa',status:'default' },
  { code:'system.brand.organization_name',category:'Identidad visual',label:'Nombre institucional',valueType:'text',value:'Gran Logia Mixta de Chile',effectiveFrom:'2026-01-01',sourceReference:'Identidad institucional QA',status:'default' },
  { code:'system.brand.primary_color',category:'Identidad visual',label:'Color institucional principal',valueType:'text',value:'#06148E',effectiveFrom:'2026-01-01',sourceReference:'Identidad institucional QA',status:'default' },
  { code:'system.brand.secondary_color',category:'Identidad visual',label:'Azul institucional complementario',valueType:'text',value:'#004AD4',effectiveFrom:'2026-01-01',sourceReference:'Línea Base Maestra LB-PC-2026-09-17',status:'default' },
  { code:'system.brand.gold_color',category:'Identidad visual',label:'Dorado institucional',valueType:'text',value:'#F3C609',effectiveFrom:'2026-01-01',sourceReference:'Línea Base Maestra LB-PC-2026-09-17',status:'default' },
  { code:'system.brand.accent_color',category:'Identidad visual',label:'Dorado institucional fuerte',valueType:'text',value:'#FBAE17',effectiveFrom:'2026-01-01',sourceReference:'Línea Base Maestra LB-PC-2026-09-17',status:'default' },
  { code:'system.brand.logo_reference',category:'Identidad visual',label:'Referencia del logotipo',valueType:'text',value:'logo-institucional.svg',effectiveFrom:'2026-01-01',sourceReference:'Identidad institucional QA',status:'default' },
  { code:'system.identity.provider',category:'Usuarios',label:'Proveedor de identidad',valueType:'text',value:'Keycloak',effectiveFrom:'2026-01-01',sourceReference:'Arquitectura de identidad',status:'default' },
  { code:'system.identity.require_mfa_admins',category:'Usuarios',label:'MFA obligatorio para administradores',valueType:'text',value:'Sí',effectiveFrom:'2026-01-01',sourceReference:'Política de seguridad',status:'default' },
  { code:'system.access.profile_definitions',category:'Usuarios',label:'Definiciones independientes de perfiles',valueType:'text',value:'[]',effectiveFrom:'2026-01-01',sourceReference:'Modelo de acceso Proyecto Centenario',status:'default' },
  { code:'system.access.user_assignments',category:'Usuarios',label:'Asignaciones de perfiles a usuarios',valueType:'text',value:'[]',effectiveFrom:'2026-01-01',sourceReference:'Modelo de acceso Proyecto Centenario',status:'default' },
  { code:'system.access.review_frequency_days',category:'Auditoría de accesos',label:'Frecuencia de certificación (días)',valueType:'integer',value:'90',effectiveFrom:'2026-01-01',sourceReference:'Política de revisión de accesos',status:'default' },
]
type MockOrganizationOption = OrganizationOption & { treasuryTerritory: TreasuryTerritory | null }
const defaultMockOrganizations: MockOrganizationOption[] = [1, ...Array.from({ length: 18 }, (_, index) => index + 2), 23].map(number => ({
  id: number === 1 ? '11111111-1111-1111-1111-111111111111' : number === 23 ? '23232323-2323-2323-2323-232323232323' : `00000000-0000-0000-0000-${String(number).padStart(12, '0')}`,
  name: `Taller Demostrativo Nº ${number}`,
  number: String(number),
  type: 'workshop',
  treasuryTerritory: 'santiago',
}))
const defaultMockSpaces: InstitutionalSpace[] = [
  { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', code: 'TEMP-01', name: 'Templo Principal', spaceType: 'temple', location: 'Sede institucional', capacity: 80, status: 'active' },
  { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', code: 'SEC-01', name: 'Sala de Secretaría', spaceType: 'secretariat_room', location: 'Sede institucional', capacity: 16, status: 'active' },
]
const defaultMockCeremonies: GrandSecretariatCeremonyQueueItem[] = [
  { id: 'cccccccc-1111-1111-1111-111111111111', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'wage_increase', proposedDate: '2026-09-18', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: null, spaceName: null, reservationStartsAtUtc: null, reservationEndsAtUtc: null, createdAtUtc: '2026-09-07T16:00:00Z' },
  { id: 'cccccccc-2222-2222-2222-222222222222', organizationId: defaultMockOrganizations[0].id, organizationName: defaultMockOrganizations[0].name, organizationNumber: '1', ceremonyType: 'exaltation', proposedDate: '2026-09-20', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', spaceName: defaultMockSpaces[0].name, reservationStartsAtUtc: '2026-09-20T22:00:00Z', reservationEndsAtUtc: '2026-09-21T01:00:00Z', createdAtUtc: '2026-09-07T17:00:00Z' },
  { id: 'cccccccc-3333-3333-3333-333333333333', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'affiliation', proposedDate: '2026-10-10', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: null, spaceName: null, reservationStartsAtUtc: null, reservationEndsAtUtc: null, createdAtUtc: '2026-09-08T12:00:00Z' },
]
const defaultMockReviewCeremonies: CeremonyReviewQueueItem[] = [
  {
    id: 'eeeeeeee-1111-1111-1111-111111111111', organizationId: defaultMockOrganizations[0].id, organizationName: defaultMockOrganizations[0].name, organizationNumber: '1', ceremonyType: 'wage_increase', subjectDisplayName: 'Hermano Demostrativo', proposedDate: '2026-09-25', status: 'under_review', createdAtUtc: '2026-09-08T13:00:00Z',
    eligibility: { status: 'does_not_comply', canAuthorize: false, publication: null, ceremonyRight: { amount: 31000, currency: 'CLP', paid: 10000, balance: 21000, source: 'Decreto N.º 1.759 — cuotas 2026' }, requirements: [
      { code: 'regimen_interior', name: 'Régimen Interior', status: 'approved', reason: 'Aprobación vigente registrada.' },
      { code: 'gran_tesoreria', name: 'Gran Tesorería', status: 'approved', reason: 'El Taller se encuentra al día para la fecha evaluada.' },
      { code: 'gran_hospitalaria', name: 'Gran Hospitalaria', status: 'approved', reason: 'El Taller se encuentra al día en reposiciones u obligaciones hospitalarias.' },
      { code: 'ceremony_right_payment', name: 'Derecho de ceremonia', status: 'rejected', reason: 'El derecho de ceremonia registra saldo pendiente en Tesorería.' },
    ] },
    actions: { canValidateInternalAffairs: true, canPublishCandidate: false, canAuthorize: false },
  },
  {
    id: 'eeeeeeee-2222-2222-2222-222222222222', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'initiation', subjectDisplayName: 'Persona Demostrativa Uno', proposedDate: '2026-10-03', status: 'under_review', createdAtUtc: '2026-09-08T13:15:00Z',
    eligibility: { status: 'does_not_comply', canAuthorize: false, publication: { status: 'published', requiredDays: 20, completedDays: 13, publishedFromUtc: '2026-08-26T15:00:00Z', publishedUntilUtc: null }, ceremonyRight: { amount: 41000, currency: 'CLP', paid: 0, balance: 41000, source: 'Decreto N.º 1.759 — cuotas 2026' }, requirements: [
      { code: 'regimen_interior', name: 'Régimen Interior', status: 'rejected', reason: 'No existe una aprobación habilitante de Régimen Interior.' },
      { code: 'gran_tesoreria', name: 'Gran Tesorería', status: 'approved', reason: 'El Taller se encuentra al día para la fecha evaluada.' },
      { code: 'gran_hospitalaria', name: 'Gran Hospitalaria', status: 'approved', reason: 'El Taller se encuentra al día en reposiciones u obligaciones hospitalarias.' },
      { code: 'ceremony_right_payment', name: 'Derecho de ceremonia', status: 'rejected', reason: 'El derecho de ceremonia registra saldo pendiente en Tesorería.' },
      { code: 'publicacion_insinuado', name: 'Publicación del insinuado', status: 'rejected', reason: 'Se requieren 20 días de publicación y se han cumplido 13 días válidos.' },
    ] },
    actions: { canValidateInternalAffairs: true, canPublishCandidate: false, canAuthorize: false },
  },
]

export class PmgmApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockOrganizations: MockOrganizationOption[] = [...defaultMockOrganizations]
  private readonly mockSpaces = [...defaultMockSpaces]
  private readonly mockBusySpaces = new Set<string>([defaultMockSpaces[0].id])
  private readonly mockDocuments: SecretariatDocument[] = []
  private readonly mockSubmittedTenidas: GrandSecretariatTenidaItem[] = [
    {
      recordId: 'gs-tenida-demo-001', lodge: { id: defaultMockOrganizations[1].id, name: defaultMockOrganizations[1].name, number: '23' },
      meetingId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', meetingDate: '2026-09-12', meetingType: 'regular', grade: 'all', ceremonyType: null,
      modality: 'in_person', title: 'Tenida Regular — Primera implementación', meetingStatus: 'held',
      extractDocumentVersionId: 'extract-demo-001', submissionStatus: 'submitted', submittedAtUtc: '2026-09-13T13:00:00Z', reviewedAtUtc: null, reviewNotes: null,
    },
  ]
  // QA only: uploaded interview files live in browser memory for this session.
  private readonly mockInterviewDocuments = new Map<string, { file: Blob; fileName: string; metadata: CandidateInterviewEvidence }>()
  private readonly mockCeremonies = defaultMockCeremonies.map(item => ({ ...item }))
  private readonly mockReviewCeremonies = defaultMockReviewCeremonies.map(cloneCeremonyQueueItem)
  private readonly mockCeremonyRightPayments = new Map<string, { payload: string; receiptNumber: string; paidTotal: number; balance: number }>()
  private readonly mockTreasury = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'treasury-demo-1', organizationId: defaultMockOrganizations[0].id, scope: 'organization', status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'TES-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:00:00Z' }]])
  private readonly mockTreasuryStatements = new Map<string, TreasuryStatement>()
  private readonly mockHospitalaria = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'hospitalaria-demo-1', organizationId: defaultMockOrganizations[0].id, status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:05:00Z' }]])
  private readonly mockLodgeHospitalariaMovements = new Map<string, LodgeHospitalariaMovement[]>()
  private readonly mockHospitalariaSubmissions = new Map<string, HospitalariaMonthlySubmission>()
  private readonly mockDeathReplenishmentCases: DeathReplenishmentCase[] = []
  private readonly mockDeathReplenishmentObligations = new Map<string, DeathReplenishmentObligation[]>()
  private mockReplenishmentRate: HospitalariaReplenishmentRate = { id: 'rate-demo', amountPerActiveMember: 1500, effectiveFrom: '2026-01-01', effectiveUntil: null, sourceReference: 'Configuración institucional demo' }
  private readonly mockLodgeFeePlans = new Map<string, LodgeFeePlan[]>()
  private readonly mockLodgeTreasuryCharges = new Map<string, LodgeTreasuryCharge[]>()
  private readonly mockLodgeTreasuryExpenses = new Map<string, LodgeTreasuryExpense[]>()
  private readonly mockLodgeTreasuryIncomes = new Map<string, LodgeTreasuryIncome[]>()
  private readonly mockLodgeTreasuryConfigurations = new Map<string, LodgeTreasuryConfiguration>()
  private readonly mockLodgeTreasuryYearClosures = new Map<string, LodgeTreasuryYearClosure[]>()
  private readonly mockLodgeTreasuryReconciliations = new Map<string, LodgeTreasuryReconciliation[]>()
  private readonly mockSystemSettings = defaultMockSystemSettings.map(item => ({ ...item }))
  private readonly mockSystemSettingVersions = new Map<string, SystemSettingVersion[]>()

  constructor(options: PmgmApiClientOptions = {}) { this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, ''); this.getAccessToken = options.getAccessToken; this.useMocks = options.useMocks ?? false; this.onUnauthorized = options.onUnauthorized; for (const item of this.mockSystemSettings) this.mockSystemSettingVersions.set(item.code,[{id:`base-${item.code}`,value:item.value,effectiveFrom:item.effectiveFrom,effectiveTo:null,sourceReference:item.sourceReference,status:item.status,createdAtUtc:'2026-01-01T00:00:00Z'}]) }

  async getCandidatePortal(): Promise<CandidatePortalResponse> { if (this.useMocks) { await sleep(120); return { culture: 'es-CL', portal: 'Insinuados en período de publicación', total: mockCandidates.length, items: mockCandidates } } return this.request<CandidatePortalResponse>('/api/ceremonias/portal-insinuados') }
  async getSystemInfo(): Promise<SystemInfo> { if (this.useMocks) return { project: 'Proyecto Milenio — Modernización Gran Logia Mixta de Chile', api: 'PMGM.Api', version: '0.12.1', runtime: '.NET 10', culture: 'es-CL', institutionalTimeZone: 'America/Santiago', defaultCurrency: 'CLP' }; return this.request<SystemInfo>('/api/system/info') }
  async getSessionProfile(): Promise<SessionProfile> { if (this.useMocks) return mockSession; return this.request<SessionProfile>('/api/session/me') }
  async getOrganizationOptions(): Promise<OrganizationOptionsResponse> { if (this.useMocks) return { total: this.mockOrganizations.length, items: this.mockOrganizations.map(({id,name,number,type})=>({id,name,number,type})) }; return this.request<OrganizationOptionsResponse>('/api/institutional/organizations/options') }
  async getTreasuryTerritories(): Promise<{total:number;items:TreasuryTerritoryOption[]}>{if(this.useMocks)return{total:this.mockOrganizations.length,items:this.mockOrganizations.map(item=>({...item,treasuryTerritory:item.treasuryTerritory??null}))};return this.request('/api/tesoreria/talleres/orientes')}
  async getTreasuryTerritory(organizationId:string):Promise<{organizationId:string;territory:TreasuryTerritory|null}>{if(this.useMocks){const item=this.mockOrganizations.find(value=>value.id===organizationId);if(!item)throw new Error('El Taller no existe.');return{organizationId,territory:item.treasuryTerritory??null}}return this.request(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/oriente`)}
  async setTreasuryTerritory(organizationId:string,territory:TreasuryTerritory):Promise<{organizationId:string;territory:TreasuryTerritory}>{if(this.useMocks){const organization=this.mockOrganizations.find(item=>item.id===organizationId);if(!organization)throw new Error('El Taller no existe.');organization.treasuryTerritory=territory;return{organizationId,territory}}return this.postJson(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/oriente`,{territory})}
  async getSystemSettings(): Promise<SystemSettingsResponse> { if (this.useMocks) return { total: this.mockSystemSettings.length, items: this.mockSystemSettings.map(item => ({ ...item })) }; return this.request<SystemSettingsResponse>('/api/system/settings/') }
  async createSystemSettingVersion(code: string, payload: CreateSystemSettingVersionRequest): Promise<SystemSetting> { if (this.useMocks) { const item=this.mockSystemSettings.find(value=>value.code===code); if(!item) throw new Error('El parámetro no pertenece al catálogo administrable.'); if(!payload.value.trim()||!payload.sourceReference.trim()) throw new Error('Valor y fundamento son obligatorios.'); const status=payload.effectiveFrom>new Date().toISOString().slice(0,10)?'scheduled':'active'; Object.assign(item,{value:payload.value.trim(),effectiveFrom:payload.effectiveFrom,sourceReference:payload.sourceReference.trim(),status}); const versions=this.mockSystemSettingVersions.get(code)??[]; versions.unshift({id:crypto.randomUUID(),value:item.value,effectiveFrom:item.effectiveFrom,effectiveTo:null,sourceReference:item.sourceReference,status,createdAtUtc:new Date().toISOString()}); this.mockSystemSettingVersions.set(code,versions); return {...item}; } return this.postJson<SystemSetting>(`/api/system/settings/${encodeURIComponent(code)}`,payload) }
  async getSystemSettingVersions(code: string): Promise<SystemSettingVersionsResponse> { if(this.useMocks){const items=this.mockSystemSettingVersions.get(code)??[];return{total:items.length,items:items.map(item=>({...item}))}} return this.request<SystemSettingVersionsResponse>(`/api/system/settings/${encodeURIComponent(code)}/versions`) }
  async getAuditLog():Promise<AuditLogResponse>{if(this.useMocks){const items:AuditLogEvent[]=[{id:'audit-1',occurredAtUtc:'2026-09-14T12:42:18Z',user:'Administrador QA',ipAddress:'192.0.2.14',menu:'Sistema',submenu:'Usuarios',summary:'Asignó perfil temporal a usuario demostrativo',action:'system.access.assignment.created',result:'success',correlationId:'qa-audit-001'},{id:'audit-2',occurredAtUtc:'2026-09-14T12:35:04Z',user:'Administrador QA',ipAddress:'192.0.2.14',menu:'Sistema',submenu:'Correo',summary:'Probó configuración SMTP',action:'system.mail.connection.tested',result:'success',correlationId:'qa-audit-002'},{id:'audit-3',occurredAtUtc:'2026-09-14T12:20:51Z',user:'Usuario QA',ipAddress:'198.51.100.22',menu:'Acceso',submenu:'Inicio de sesión',summary:'Intento de autenticación rechazado',action:'identity.login.rejected',result:'rejected',correlationId:'qa-audit-003'}];return{total:items.length,page:1,pageSize:50,immutable:true,items}}return this.request<AuditLogResponse>('/api/system/audit-events')}

  async createLodgeFeePlan(organizationId: string, payload: { feeType: LodgeFeeType; memberAmount: number; grandTreasuryAmount: number; effectiveFrom: string; effectiveUntil?: string | null }): Promise<LodgeFeePlan> {
    if (this.useMocks) {
      const territory=this.mockOrganizations.find(item=>item.id===organizationId)?.treasuryTerritory
      const official=mockGrandTreasuryRate(payload.feeType,territory,payload.effectiveFrom)
      if(official===null)throw new Error('No existe una tarifa institucional CLP aplicable para esta categoría, Oriente y vigencia.')
      if(payload.memberAmount<official)throw new Error('La cuota local no puede ser inferior al aporte decretado a Gran Tesorería.')
      const plans = this.mockLodgeFeePlans.get(organizationId) ?? []
      if (plans.some(item => item.feeType === payload.feeType && item.isActive)) throw new Error('Ya existe una cuota activa del mismo tipo para esa vigencia.')
      const plan = { id: crypto.randomUUID(), organizationId, ...payload, grandTreasuryAmount:official, effectiveUntil: payload.effectiveUntil ?? null, workshopAmount: payload.memberAmount - official, isActive: true }
      plans.push(plan); this.mockLodgeFeePlans.set(organizationId, plans); return { ...plan }
    }
    return this.postJson<LodgeFeePlan>(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/planes-cuota`, payload)
  }
  async getLodgeFeePlans(organizationId: string): Promise<{ total: number; items: LodgeFeePlan[] }> {
    if (this.useMocks) {
      let plans = this.mockLodgeFeePlans.get(organizationId)
      if (!plans) { plans = defaultLodgeFeePlans(organizationId); this.mockLodgeFeePlans.set(organizationId, plans) }
      const territory=this.mockOrganizations.find(item=>item.id===organizationId)?.treasuryTerritory
      return { total: plans.length, items: plans.map(item => {const official=mockGrandTreasuryRate(item.feeType,territory,item.effectiveFrom);return{...item,grandTreasuryAmount:official,workshopAmount:official===null?null:item.memberAmount-official,rateAvailable:official!==null}}) }
    }
    return this.request<{ total: number; items: LodgeFeePlan[] }>(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/planes-cuota`)
  }
  async generateLodgeCharges(organizationId: string, periodYear: number, periodMonth: number): Promise<LodgeTreasurySummary> {
    if (this.useMocks) {
      const key = `${organizationId}:${periodYear}-${periodMonth}`
      const current = await this.getLodgeTreasuryCharges(organizationId, periodYear, periodMonth)
      const plans = (await this.getLodgeFeePlans(organizationId)).items
      const charges = current.items.map(charge => {
        if (!charge.feeType) throw new Error(`El cargo ficticio de ${charge.memberDisplayName} no tiene categoría de cuota.`)
        const plan = mockEffectiveLodgeFeePlan(plans, charge.feeType, periodYear, periodMonth)
        if (charge.feeType !== 'past_active' && !plan) throw new Error(`No hay una tarifa vigente para ${charge.feeType} en ${periodYear}-${String(periodMonth).padStart(2, '0')}.`)
        const memberAmount = charge.feeType === 'past_active' ? charge.memberAmount : plan!.memberAmount
        if (charge.paidAmount > memberAmount) throw new Error(`Los pagos registrados de ${charge.memberDisplayName} superan la tarifa vigente; no se regeneró el cargo.`)
        const balance = memberAmount - charge.paidAmount
        return { ...charge, memberAmount, monthlyFeeAmount: memberAmount, balance, status: balance === 0 ? 'paid' as const : charge.paidAmount > 0 ? 'partial' as const : 'pending' as const }
      })
      this.mockLodgeTreasuryCharges.set(key, charges)
      return summarizeMockLodgeTreasury(organizationId, periodYear, periodMonth, charges, plans)
    }
    return this.postJson<LodgeTreasurySummary>(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/cargos/generar`, { periodYear, periodMonth, assignments: [] })
  }
  async getLodgeTreasurySummary(organizationId: string, periodYear: number, periodMonth: number): Promise<LodgeTreasurySummary> {
    if (this.useMocks) {
      const charges = await this.getLodgeTreasuryCharges(organizationId, periodYear, periodMonth)
      const plans = (await this.getLodgeFeePlans(organizationId)).items
      return summarizeMockLodgeTreasury(organizationId, periodYear, periodMonth, charges.items, plans)
    }
    return this.request<LodgeTreasurySummary>(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/resumen?year=${periodYear}&month=${periodMonth}`)
  }
  async getLodgeTreasuryCharges(organizationId:string,periodYear:number,periodMonth:number):Promise<{total:number;items:LodgeTreasuryCharge[]}>{
    if(this.useMocks){const key=`${organizationId}:${periodYear}-${periodMonth}`;let items=this.mockLodgeTreasuryCharges.get(key);if(!items){items=mockTreasuryCharges(organizationId);this.mockLodgeTreasuryCharges.set(key,items)}return{total:items.length,items:items.map(cloneTreasuryCharge)}}
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/cargos?year=${periodYear}&month=${periodMonth}`)
  }
  async addLodgeTreasuryPayment(chargeId:string,payload:{amount:number;paymentMethod:LodgeTreasuryPayment['paymentMethod'];paymentDate:string;reference?:string|null;idempotencyKey:string}):Promise<LodgeTreasuryPayment&{paidAmount:number;balance:number;status:LodgeTreasuryCharge['status']}>{
    if(this.useMocks){const owner=[...this.mockLodgeTreasuryCharges.entries()].flatMap(([organizationId,items])=>items.filter(x=>x.id===chargeId).map(charge=>({organizationId,charge}))).at(0);if(!owner)throw new Error('El cargo indicado no existe.');const charge=owner.charge;const replay=charge.payments.find(x=>x.idempotencyKey===payload.idempotencyKey);if(replay){if(replay.amount!==payload.amount||replay.paymentDate!==payload.paymentDate||replay.paymentMethod!==payload.paymentMethod||replay.reference!==(payload.reference?.trim()||null))throw new Error('La clave ya fue utilizada con datos de pago distintos.');return{...replay,paidAmount:charge.paidAmount,balance:charge.balance,status:charge.status}}if(this.mockYearIsClosed(owner.organizationId,Number(payload.paymentDate.slice(0,4))))throw new Error('La fecha de pago pertenece a un ejercicio cerrado.');if(payload.amount<=0||payload.amount>charge.balance)throw new Error('El abono debe ser positivo y no superar el saldo.');const reference=payload.reference?.trim()||null;if(reference&&charge.payments.some(x=>x.amount===payload.amount&&x.paymentDate===payload.paymentDate&&x.paymentMethod===payload.paymentMethod&&x.reference?.toLocaleLowerCase('es-CL')===reference.toLocaleLowerCase('es-CL')))throw new Error('Este pago ya fue registrado para el mismo cargo, fecha, monto, medio y referencia.');const payment:LodgeTreasuryPayment={id:crypto.randomUUID(),receiptNumber:`REC-DEMO-${String(charge.payments.length+1).padStart(3,'0')}`,amount:payload.amount,paymentMethod:payload.paymentMethod,paymentDate:payload.paymentDate,reference,idempotencyKey:payload.idempotencyKey};charge.payments.unshift(payment);charge.paidAmount+=payment.amount;charge.balance-=payment.amount;charge.status=charge.balance===0?'paid':'partial';return{...payment,paidAmount:charge.paidAmount,balance:charge.balance,status:charge.status}}
    return this.postJson<LodgeTreasuryPayment&{paidAmount:number;balance:number;status:LodgeTreasuryCharge['status']}>(`/api/gestion-logial/tesoreria/cargos/${encodeURIComponent(chargeId)}/pagos`,payload)
  }
  async getLodgeTreasuryExpenses(organizationId:string,from:string,to:string):Promise<{total:number;items:LodgeTreasuryExpense[]}>{
    if(this.useMocks){let items=this.mockLodgeTreasuryExpenses.get(organizationId);if(!items){items=mockTreasuryExpenses(organizationId);this.mockLodgeTreasuryExpenses.set(organizationId,items)}const selected=items.filter(x=>x.expenseDate>=from&&x.expenseDate<=to);return{total:selected.length,items:selected.map(x=>({...x}))}}
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/egresos?from=${from}&to=${to}`)
  }
  async createLodgeTreasuryExpense(organizationId:string,payload:{category:string;amount:number;expenseDate:string;description:string;evidenceReference?:string|null}):Promise<LodgeTreasuryExpense>{
    if(this.useMocks){if(this.mockYearIsClosed(organizationId,Number(payload.expenseDate.slice(0,4))))throw new Error('La fecha de egreso pertenece a un ejercicio cerrado.');const item:LodgeTreasuryExpense={id:crypto.randomUUID(),organizationId,...payload,evidenceReference:payload.evidenceReference?.trim()||null,approvalStatus:'pending_approval',recordedBySubject:'tesoreria-demo',approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:new Date().toISOString()};const items=this.mockLodgeTreasuryExpenses.get(organizationId)??[];items.unshift(item);this.mockLodgeTreasuryExpenses.set(organizationId,items);return{...item}}
    return this.postJson(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/egresos`,payload)
  }
  async approveLodgeTreasuryExpense(expenseId:string):Promise<LodgeTreasuryExpense>{
    if(this.useMocks){const item=[...this.mockLodgeTreasuryExpenses.values()].flat().find(x=>x.id===expenseId);if(!item)throw new Error('El egreso no existe.');if(this.mockYearIsClosed(item.organizationId,Number(item.expenseDate.slice(0,4))))throw new Error('El ejercicio del egreso está cerrado.');if(item.approvalStatus!=='pending_approval')throw new Error('El egreso ya fue resuelto.');item.approvalStatus='approved';item.approvedBySubject='venerable-demo';item.approvedAtUtc=new Date().toISOString();return{...item}}
    return this.postJson(`/api/gestion-logial/tesoreria/egresos/${encodeURIComponent(expenseId)}/aprobar`,{})
  }

  async createLodgeTreasuryIncome(organizationId:string,payload:{category:string;amount:number;incomeDate:string;description:string;evidenceReference?:string|null}):Promise<LodgeTreasuryIncome>{
    if(this.useMocks){if(this.mockYearIsClosed(organizationId,Number(payload.incomeDate.slice(0,4))))throw new Error('La fecha de ingreso pertenece a un ejercicio cerrado.');const item:LodgeTreasuryIncome={id:crypto.randomUUID(),organizationId,...payload,evidenceReference:payload.evidenceReference?.trim()||null,recordedBySubject:'tesoreria-demo',recordedAtUtc:new Date().toISOString()};const rows=this.mockLodgeTreasuryIncomes.get(organizationId)??[];rows.unshift(item);this.mockLodgeTreasuryIncomes.set(organizationId,rows);return item}
    return this.postJson(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/ingresos`,payload)
  }
  async getLodgeCashSummary(organizationId:string,asOf:string):Promise<LodgeCashSummary>{
    if(this.useMocks){const month=asOf.slice(0,7);const charges=await this.getLodgeTreasuryCharges(organizationId,Number(month.slice(0,4)),Number(month.slice(5,7)));const expenses=await this.getLodgeTreasuryExpenses(organizationId,'0001-01-01',asOf);const incomes=this.mockLodgeTreasuryIncomes.get(organizationId)??[];const payments=charges.items.flatMap(c=>c.payments);const allIncomes=[...payments.map(x=>({date:x.paymentDate,amount:x.amount})),...incomes.map(x=>({date:x.incomeDate,amount:x.amount}))];const approved=expenses.items.filter(x=>x.approvalStatus==='approved');const cumulativeIncome=allIncomes.filter(x=>x.date<=asOf).reduce((s,x)=>s+x.amount,0);const cumulativeExpense=approved.filter(x=>x.expenseDate<=asOf).reduce((s,x)=>s+x.amount,0);const monthIncome=allIncomes.filter(x=>x.date.startsWith(month)).reduce((s,x)=>s+x.amount,0);const monthExpense=approved.filter(x=>x.expenseDate.startsWith(month)).reduce((s,x)=>s+x.amount,0);return{organizationId,asOf,openingBalance:0,cumulativeIncome,cumulativeExpense,cumulativeBalance:cumulativeIncome-cumulativeExpense,monthIncome,monthExpense,monthBalance:monthIncome-monthExpense,pendingExpenses:expenses.items.filter(x=>x.approvalStatus==='pending_approval').length}}
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/caja?asOf=${asOf}`)
  }
  async getLodgeTreasuryReport(organizationId:string,from:string,to:string,observedBalance?:number|null):Promise<LodgeTreasuryReport>{
    if(this.useMocks){const charges=await this.getLodgeTreasuryCharges(organizationId,Number(to.slice(0,4)),Number(to.slice(5,7)));const expenses=await this.getLodgeTreasuryExpenses(organizationId,from,to);const incomeRows=this.mockLodgeTreasuryIncomes.get(organizationId)??[];const movements=[...charges.items.flatMap(c=>c.payments.filter(p=>p.paymentDate>=from&&p.paymentDate<=to).map(p=>({transactionId:p.id,date:p.paymentDate,type:'ingreso' as const,category:'Cuotas',description:`Cuota · ${c.memberDisplayName}`,amount:p.amount,status:'registrado',reference:p.receiptNumber,recordedBySubject:'tesorero-demo',recordedAtUtc:new Date().toISOString(),approvedBySubject:null,approvedAtUtc:null}))),...incomeRows.filter(x=>x.incomeDate>=from&&x.incomeDate<=to).map(x=>({transactionId:x.id,date:x.incomeDate,type:'ingreso' as const,category:x.category,description:x.description,amount:x.amount,status:'registrado',reference:x.evidenceReference,recordedBySubject:x.recordedBySubject,recordedAtUtc:x.recordedAtUtc,approvedBySubject:null,approvedAtUtc:null})),...expenses.items.map(x=>({transactionId:x.id,date:x.expenseDate,type:'egreso' as const,category:x.category,description:x.description,amount:x.amount,status:x.approvalStatus==='approved'?'autorizado':'pendiente de autorización',reference:x.evidenceReference,recordedBySubject:x.recordedBySubject,recordedAtUtc:x.recordedAtUtc,approvedBySubject:x.approvedBySubject,approvedAtUtc:x.approvedAtUtc}))].sort((a,b)=>a.date.localeCompare(b.date));const income=movements.filter(x=>x.type==='ingreso').reduce((s,x)=>s+x.amount,0);const authorizedExpenses=movements.filter(x=>x.type==='egreso'&&x.status==='autorizado').reduce((s,x)=>s+x.amount,0);return{organizationId,from,to,openingBalance:0,income,authorizedExpenses,pendingExpenses:movements.filter(x=>x.type==='egreso'&&x.status!=='autorizado').reduce((s,x)=>s+x.amount,0),closingBalance:income-authorizedExpenses,observedBalance:observedBalance??null,difference:observedBalance==null?null:observedBalance-(income-authorizedExpenses),reconciliationHistory:(this.mockLodgeTreasuryReconciliations.get(organizationId)??[]).filter(x=>x.from===from&&x.to===to).slice(0,50),movements}}
    const query=new URLSearchParams({from,to});if(observedBalance!=null)query.set('observedBalance',String(observedBalance))
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/reportes?${query}`)
  }
  async saveLodgeTreasuryReconciliation(organizationId:string,payload:{from:string;to:string;observedBalance:number;evidenceReference?:string|null;notes?:string|null}):Promise<LodgeTreasuryReconciliation>{
    if(this.useMocks){if(payload.to<payload.from||payload.observedBalance<0)throw new Error('El rango y saldo observado deben ser válidos.');const report=await this.getLodgeTreasuryReport(organizationId,payload.from,payload.to);const item:LodgeTreasuryReconciliation={id:crypto.randomUUID(),organizationId,from:payload.from,to:payload.to,openingBalance:report.openingBalance,income:report.income,authorizedExpenses:report.authorizedExpenses,pendingExpenses:report.pendingExpenses,closingBalance:report.closingBalance,observedBalance:payload.observedBalance,difference:payload.observedBalance-report.closingBalance,movementCount:report.movements.length,evidenceReference:payload.evidenceReference?.trim()||null,notes:payload.notes?.trim()||null,recordedBySubject:'tesoreria-demo',recordedAtUtc:new Date().toISOString()};const items=this.mockLodgeTreasuryReconciliations.get(organizationId)??[];items.unshift(item);this.mockLodgeTreasuryReconciliations.set(organizationId,items);return{...item}}
    return this.postJson(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/conciliaciones`,payload)
  }
  async getLodgeTreasuryConfiguration(organizationId:string):Promise<LodgeTreasuryConfiguration>{
    if(this.useMocks)return this.mockLodgeTreasuryConfigurations.get(organizationId)??{organizationId,openingBalance:0,openingBalanceDate:'2026-01-01',incomeCategories:'Otros ingresos',expenseCategories:'Servicios;Materiales;Arriendo;Traslado'}
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/configuracion`)
  }
  async getLodgeTreasuryYearClosures(organizationId:string):Promise<{total:number;items:LodgeTreasuryYearClosure[]}>{
    if(this.useMocks){const items=this.mockLodgeTreasuryYearClosures.get(organizationId)??[];return{total:items.length,items:items.map(x=>({...x}))}}
    return this.request('/api/gestion-logial/tesoreria/talleres/'+encodeURIComponent(organizationId)+'/cierres-anuales')
  }
  async closeLodgeTreasuryYear(organizationId:string,year:number):Promise<LodgeTreasuryYearClosure>{
    if(this.useMocks){
      if(year>=new Date().getFullYear())throw new Error('Sólo se puede cerrar un ejercicio anual ya finalizado.')
      const items=this.mockLodgeTreasuryYearClosures.get(organizationId)??[]
      if(items.some(x=>x.accountingYear===year))throw new Error('El ejercicio ya está cerrado.')
      const report=await this.getLodgeTreasuryReport(organizationId,String(year)+'-01-01',String(year)+'-12-31')
      const expenseRows=(this.mockLodgeTreasuryExpenses.get(organizationId)??[]).filter(x=>x.expenseDate.startsWith(String(year)))
      if(expenseRows.some(x=>x.approvalStatus==='pending_approval'))throw new Error('No se puede cerrar: hay egresos pendientes de autorización.')
      const item:LodgeTreasuryYearClosure={id:crypto.randomUUID(),organizationId,accountingYear:year,openingBalance:report.openingBalance,income:report.income,authorizedExpenses:report.authorizedExpenses,closingBalance:report.closingBalance,movementCount:report.movements.length,closedBySubject:'tesorero-demo',closedAtUtc:new Date().toISOString()}
      items.unshift(item);this.mockLodgeTreasuryYearClosures.set(organizationId,items);return{...item}
    }
    return this.postJson('/api/gestion-logial/tesoreria/talleres/'+encodeURIComponent(organizationId)+'/cierres-anuales/'+year+'/cerrar',{})
  }
  private mockYearIsClosed(organizationId:string,year:number){return(this.mockLodgeTreasuryYearClosures.get(organizationId)??[]).some(x=>x.accountingYear>=year)}
  async saveLodgeTreasuryConfiguration(organizationId:string,payload:Omit<LodgeTreasuryConfiguration,'organizationId'>):Promise<LodgeTreasuryConfiguration>{
    if(this.useMocks){const configuration={organizationId,...payload};this.mockLodgeTreasuryConfigurations.set(organizationId,configuration);return configuration}
    return this.request(`/api/gestion-logial/tesoreria/talleres/${encodeURIComponent(organizationId)}/configuracion`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
  }

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
  async getTreasuryCeremonyRights(): Promise<TreasuryCeremonyRightsResponse> {
    if (this.useMocks) {
      const items = this.mockReviewCeremonies.filter(item => (item.eligibility.ceremonyRight?.balance ?? 0) > 0).map(item => ({
        id: item.id, organizationId: item.organizationId, organizationName: item.organizationName, organizationNumber: item.organizationNumber,
        ceremonyType: item.ceremonyType, proposedDate: item.proposedDate, subjectDisplayName: item.subjectDisplayName,
        ...item.eligibility.ceremonyRight!
      }))
      return { total: items.length, items }
    }
    return this.request<TreasuryCeremonyRightsResponse>('/api/tesoreria/derechos-ceremoniales')
  }
  async recordCeremonyRightPayment(ceremonyRequestId: string, payload: { amount: number; paymentMethod: 'cash'|'transfer'|'deposit'; paymentDate: string; reference: string|null; idempotencyKey: string }): Promise<{ receiptNumber: string; paidTotal: number; balance: number }> {
    if (this.useMocks) {
      const item = this.requireMockReviewCeremony(ceremonyRequestId)
      const paymentKey = `${ceremonyRequestId}:${payload.idempotencyKey}`
      const payloadFingerprint = JSON.stringify({ amount: payload.amount, paymentMethod: payload.paymentMethod, paymentDate: payload.paymentDate, reference: payload.reference })
      const existing = this.mockCeremonyRightPayments.get(paymentKey)
      if (existing) {
        if (existing.payload !== payloadFingerprint) throw new Error('El identificador de reintento ya se usó con datos distintos.')
        return { receiptNumber: existing.receiptNumber, paidTotal: existing.paidTotal, balance: existing.balance }
      }
      const right = item.eligibility.ceremonyRight
      if (!right || payload.amount <= 0 || payload.amount > right.balance) throw new Error('El monto supera el saldo del derecho de ceremonia.')
      right.paid += payload.amount; right.balance = Math.max(0, right.amount - right.paid)
      const requirement = item.eligibility.requirements.find(value => value.code === 'ceremony_right_payment')
      if (requirement) { requirement.status = right.balance === 0 ? 'approved' : 'rejected'; requirement.reason = right.balance === 0 ? 'El derecho de ceremonia está pagado según el libro de Tesorería.' : 'El derecho de ceremonia registra saldo pendiente en Tesorería.' }
      recomputeMockEligibility(item)
      const receiptNumber = `CER-DEMO-${payload.paymentDate.replaceAll('-', '')}-${String(this.mockCeremonyRightPayments.size + 1).padStart(4, '0')}`
      this.mockCeremonyRightPayments.set(paymentKey, { payload: payloadFingerprint, receiptNumber, paidTotal: right.paid, balance: right.balance })
      return { receiptNumber, paidTotal: right.paid, balance: right.balance }
    }
    return this.postJson(`/api/ceremonias/solicitudes/${encodeURIComponent(ceremonyRequestId)}/derecho/pagos`, payload)
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
  async listTreasuryStatements(organizationId: string, year?: number, month?: number): Promise<TreasuryStatementListResponse> {
    if (this.useMocks) {
      const items = [...this.mockTreasuryStatements.values()]
        .filter(item => item.organizationId === organizationId && (year == null || item.periodYear === year) && (month == null || item.periodMonth === month))
        .sort((a,b) => b.periodYear - a.periodYear || b.periodMonth - a.periodMonth)
        .map(item => ({ id:item.id, organizationId:item.organizationId, periodYear:item.periodYear, periodMonth:item.periodMonth, cutoffDate:item.cutoffDate, status:item.status, sourceReference:item.sourceReference, submittedAtUtc:item.submittedAtUtc, reconciledAtUtc:item.reconciledAtUtc, closedAtUtc:item.closedAtUtc }))
      return { total:items.length, items }
    }
    const query = new URLSearchParams()
    if (year != null) query.set('year', String(year))
    if (month != null) query.set('month', String(month))
    return this.request<TreasuryStatementListResponse>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/cuadros${query.size ? `?${query}` : ''}`)
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
      const names = ['María Elena Rojas Demo', 'Juan Carlos Silva Demo', 'Patricia Muñoz Soto Demo', 'Roberto Paredes Demo', 'Camila Torres Demo', 'Tomás Díaz Demo', 'Elisa Fuentes Demo']
      const contributionTypes:LodgeFeeType[]=['normal','normal','senior','spouse','student','past_active','normal']
      const demoPayableAmounts = contributionTypes.map((feeType,index)=>feeType==='past_active'?0:feeType==='senior'?10000:feeType==='student'?8000:feeType==='spouse'?13000:amounts[index])
      statement.lines = amounts.map((_, index) => ({ id: crypto.randomUUID(), memberId: `demo-member-${index + 1}`, membershipId: `demo-membership-${index + 1}`, rut: `RUT-DEMO-${String(index+1).padStart(3,'0')}`, firstNames: names[index].split(' ').slice(0,-2).join(' '), lastNames: names[index].split(' ').slice(-2).join(' '), degreeCodeAtCutoff: degrees[index], officeCodeAtCutoff: index < 3 ? ['venerable_master', 'first_vigilante', 'second_vigilante'][index] : null, baseAmount: demoPayableAmounts[index], adjustmentAmount: 0, payableAmount: demoPayableAmounts[index], adjustmentType: contributionTypes[index], contributionType:contributionTypes[index], authorizationReference: ['normal','past_active'].includes(contributionTypes[index]) ? null : `Plancha DEMO-02${index+1}/2026`, observation: names[index], identityMatchStatus: 'matched' }))
      recalculateMockTreasury(statement); return cloneTreasuryStatement(statement)
    }
    return this.postJson<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/generar-lineas`, payload)
  }
  async addTreasuryStatementPayment(statementId: string, payload: AddTreasuryPaymentRequest): Promise<TreasuryStatement> {
    if (this.useMocks) {
      const statement = this.requireMockTreasuryStatement(statementId)
      if (statement.status !== 'draft') throw new Error('Los pagos sólo pueden registrarse mientras el Cuadro está en borrador.')
      if (payload.amount <= 0 || !payload.payerDisplayName?.trim() || !payload.reference?.trim()) throw new Error('Pagador y referencia/comprobante son obligatorios para registrar el pago.')
      statement.payments.push({ id: crypto.randomUUID(), paymentMethod: payload.paymentMethod, paymentDate: payload.paymentDate, amount: payload.amount, payerDisplayName: payload.payerDisplayName.trim(), reference: payload.reference.trim(), recordedAtUtc: new Date().toISOString() })
      recalculateMockTreasury(statement); return cloneTreasuryStatement(statement)
    }
    await this.postJson<unknown>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/pagos`, payload)
    return this.getTreasuryStatement(statementId)
  }
  async submitTreasuryStatement(statementId: string): Promise<TreasuryStatement> {
    if (this.useMocks) {
      const statement = this.requireMockTreasuryStatement(statementId)
      if (statement.lines.length === 0) throw new Error('El cuadro debe contener líneas antes de enviarse.')
      if (statement.differenceAmount !== 0 || statement.unresolvedIdentities !== 0) throw new Error('El cuadro no puede enviarse mientras exista diferencia o identidades sin conciliar.')
      statement.status = 'submitted'; statement.submittedAtUtc = new Date().toISOString(); return cloneTreasuryStatement(statement)
    }
    return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/enviar`, { method: 'POST' })
  }
  async reconcileTreasuryStatement(statementId: string): Promise<TreasuryStatement> {
    if (this.useMocks) { const statement = this.requireMockTreasuryStatement(statementId); if (statement.differenceAmount !== 0) throw new Error('El cuadro mantiene una diferencia pendiente.'); statement.status = 'reconciled'; statement.reconciledAtUtc = new Date().toISOString(); return cloneTreasuryStatement(statement) }
    return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}/conciliar`, { method: 'POST' })
  }
  async getTreasuryStatement(statementId: string, includeMemberDetail = false): Promise<TreasuryStatement> { if (this.useMocks) { const item=cloneTreasuryStatement(this.requireMockTreasuryStatement(statementId)); if(!includeMemberDetail)item.lines=item.lines.map(x=>({...x,memberId:null,membershipId:null,degreeCodeAtCutoff:'',officeCodeAtCutoff:null,observation:null,authorizationReference:null,adjustmentType:null})); return item } return this.request<TreasuryStatement>(`/api/tesoreria/cuadros/${encodeURIComponent(statementId)}?includeMemberDetail=${includeMemberDetail}`) }
  async getLodgeHospitalariaSummary(organizationId:string, from?:string, to?:string):Promise<LodgeHospitalariaSummary>{
    if(this.useMocks){
      const items=this.ensureMockHospitalariaMovements(organizationId).filter(item=>(!from||item.movementDate>=from)&&(!to||item.movementDate<=to))
      return summarizeMockHospitalaria(organizationId,from??currentMonthStart(),to??currentMonthEnd(),items)
    }
    const query=new URLSearchParams(); if(from)query.set('from',from); if(to)query.set('to',to)
    return this.request<LodgeHospitalariaSummary>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/resumen${query.size?`?${query}`:''}`)
  }

  async createLodgeHospitalariaMovement(organizationId:string,payload:CreateLodgeHospitalariaMovementRequest):Promise<LodgeHospitalariaMovement>{
    if(this.useMocks){
      if(payload.amount<=0)throw new Error('El monto debe ser mayor que cero.')
      if(payload.movementType==='expense'&&!payload.evidenceReference?.trim())throw new Error('Todo egreso de Hospitalaria debe conservar una referencia de respaldo.')
      const item:LodgeHospitalariaMovement={id:crypto.randomUUID(),organizationId,...payload,memberReference:payload.memberReference?.trim()||null,destination:payload.destination?.trim()||null,evidenceReference:payload.evidenceReference?.trim()||null,observation:payload.observation?.trim()||null,approvalStatus:payload.movementType==='expense'?'pending_approval':'not_required',approvalSource:null,councilDecisionId:null,approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:new Date().toISOString()}
      this.ensureMockHospitalariaMovements(organizationId).push(item); return {...item}
    }
    return this.postJson<LodgeHospitalariaMovement>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/movimientos`,payload)
  }

  async approveLodgeHospitalariaExpense(movementId:string):Promise<LodgeHospitalariaMovement>{
    if(this.useMocks){const item=this.requireMockHospitalariaMovement(movementId);if(item.approvalStatus!=='pending_approval')throw new Error('El egreso no está pendiente.');item.approvalStatus='approved';item.approvalSource='venerable_master';item.approvedBySubject='venerable-demo';item.approvedAtUtc=new Date().toISOString();return {...item}}
    return this.request<LodgeHospitalariaMovement>(`/api/gestion-logial/hospitalaria/movimientos/${encodeURIComponent(movementId)}/aprobar`,{method:'POST'})
  }

  async approveLodgeHospitalariaExpenseByCouncil(movementId:string,councilDecisionId:string):Promise<LodgeHospitalariaMovement>{
    if(this.useMocks){const item=this.requireMockHospitalariaMovement(movementId);const decision=mockHospitalariaCouncilAidDecisions(item.organizationId).find(x=>x.id===councilDecisionId);if(!decision||decision.amount!==item.amount||item.category!=='charity_aid')throw new Error('El acuerdo del Consejo no corresponde al socorro.');item.approvalStatus='approved';item.approvalSource='lodge_council';item.councilDecisionId=decision.id;item.approvedBySubject=`council:${decision.id}`;item.approvedAtUtc=new Date().toISOString();return {...item}}
    return this.postJson<LodgeHospitalariaMovement>(`/api/gestion-logial/hospitalaria/movimientos/${encodeURIComponent(movementId)}/aprobar-consejo`,{councilDecisionId})
  }

  async getHospitalariaCouncilAidDecisions(organizationId:string):Promise<{total:number;items:HospitalariaCouncilAidDecision[]}>{
    if(this.useMocks){const items=mockHospitalariaCouncilAidDecisions(organizationId);return{total:items.length,items}}
    return this.request<{total:number;items:HospitalariaCouncilAidDecision[]}>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/acuerdos-socorro`)
  }

  async getHospitalariaCouncilFinancialReviews(organizationId:string):Promise<{total:number;items:HospitalariaCouncilFinancialReview[]}>{
    if(this.useMocks){const items=mockHospitalariaCouncilFinancialReviews(organizationId);return{total:items.length,items}}
    return this.request<{total:number;items:HospitalariaCouncilFinancialReview[]}>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/revisiones-consejo`)
  }

  async upsertHospitalariaMonthlySubmission(organizationId:string,year:number,month:number,payload:UpsertHospitalariaMonthlySubmissionRequest):Promise<HospitalariaMonthlySubmission>{
    if(this.useMocks){
      const key=`${organizationId}:${year}-${String(month).padStart(2,'0')}`;const existing=this.mockHospitalariaSubmissions.get(key)
      if(existing&&['submitted','reconciled'].includes(existing.status))throw new Error('La rendición enviada o conciliada no puede modificarse.')
      const start=`${year}-${String(month).padStart(2,'0')}-01`;const end=monthEnd(year,month);const summary=await this.getLodgeHospitalariaSummary(organizationId,start,end)
      if(payload.replenishmentPaidAmount>0&&!payload.paymentReference?.trim())throw new Error('Debe registrar la referencia/comprobante de la reposición pagada.')
      const item:HospitalariaMonthlySubmission={id:existing?.id??crypto.randomUUID(),organizationId,periodYear:year,periodMonth:month,cutoffDate:end,incomeAmount:summary.income,approvedExpenseAmount:summary.approvedExpenses,periodNetAmount:summary.periodNet,movementCount:summary.movements,pendingExpenseCount:summary.pendingExpenses,replenishmentDueAmount:payload.replenishmentDueAmount,replenishmentPaidAmount:payload.replenishmentPaidAmount,differenceAmount:payload.replenishmentDueAmount-payload.replenishmentPaidAmount,paymentReference:payload.paymentReference?.trim()||null,councilFinancialReviewId:payload.councilFinancialReviewId??null,status:'draft',sourceReference:payload.sourceReference?.trim()||null,createdAtUtc:existing?.createdAtUtc??new Date().toISOString(),submittedAtUtc:null,reviewedAtUtc:null,reviewNotes:null}
      this.mockHospitalariaSubmissions.set(key,item);return cloneHospitalariaSubmission(item)
    }
    return this.request<HospitalariaMonthlySubmission>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/rendiciones/${year}/${month}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)})
  }

  async getHospitalariaMonthlySubmissions(organizationId:string,year?:number,month?:number):Promise<HospitalariaSubmissionListResponse>{
    if(this.useMocks){const items=[...this.mockHospitalariaSubmissions.values()].filter(x=>x.organizationId===organizationId&&(year==null||x.periodYear===year)&&(month==null||x.periodMonth===month)).map(cloneHospitalariaSubmission);return{total:items.length,items}}
    const query=new URLSearchParams();if(year!=null)query.set('year',String(year));if(month!=null)query.set('month',String(month))
    return this.request<HospitalariaSubmissionListResponse>(`/api/gestion-logial/hospitalaria/talleres/${encodeURIComponent(organizationId)}/rendiciones${query.size?`?${query}`:''}`)
  }

  async submitHospitalariaMonthlySubmission(submissionId:string):Promise<HospitalariaMonthlySubmission>{
    if(this.useMocks){const item=this.requireMockHospitalariaSubmission(submissionId);if(item.pendingExpenseCount!==0)throw new Error('No puede enviarse la rendición mientras existan egresos pendientes de autorización.');if(!item.councilFinancialReviewId)throw new Error('Debe vincular la revisión mensual de Hospitalaria del Consejo de Administración.');item.status='submitted';item.submittedAtUtc=new Date().toISOString();return cloneHospitalariaSubmission(item)}
    return this.request<HospitalariaMonthlySubmission>(`/api/gestion-logial/hospitalaria/rendiciones/${encodeURIComponent(submissionId)}/enviar`,{method:'POST'})
  }

  async getGrandHospitalariaSubmissions(filters:{organizationId?:string;year?:number;month?:number;status?:string}={}):Promise<HospitalariaSubmissionListResponse<GrandHospitalariaSubmission>>{
    if(this.useMocks){const items=[...this.mockHospitalariaSubmissions.values()].filter(x=>x.status!=='draft'&&(!filters.organizationId||x.organizationId===filters.organizationId)&&(filters.year==null||x.periodYear===filters.year)&&(filters.month==null||x.periodMonth===filters.month)&&(!filters.status||x.status===filters.status)).map(x=>{const org=this.mockOrganizations.find(o=>o.id===x.organizationId);return{...cloneHospitalariaSubmission(x),organizationName:org?.name??'Taller',organizationNumber:org?.number??null}});return{total:items.length,items}}
    const query=new URLSearchParams();if(filters.organizationId)query.set('organizationId',filters.organizationId);if(filters.year!=null)query.set('year',String(filters.year));if(filters.month!=null)query.set('month',String(filters.month));if(filters.status)query.set('status',filters.status)
    return this.request<HospitalariaSubmissionListResponse<GrandHospitalariaSubmission>>(`/api/hospitalaria/rendiciones${query.size?`?${query}`:''}`)
  }

  async reviewGrandHospitalariaSubmission(submissionId:string,decision:'observed'|'reconciled',notes?:string|null):Promise<{id:string;organizationId:string;status:string;reviewedAtUtc:string;reviewNotes:string|null}>{
    if(this.useMocks){const item=this.requireMockHospitalariaSubmission(submissionId);if(item.status!=='submitted')throw new Error('Sólo una rendición enviada puede ser revisada.');if(decision==='reconciled'&&item.differenceAmount>0)throw new Error('No puede conciliarse una rendición con reposiciones pendientes.');if(decision==='observed'&&!notes?.trim())throw new Error('Una observación debe indicar motivo.');item.status=decision;item.reviewedAtUtc=new Date().toISOString();item.reviewNotes=notes?.trim()||null;if(decision==='reconciled')this.mockHospitalaria.set(item.organizationId,{id:`hospitalaria-${crypto.randomUUID()}`,organizationId:item.organizationId,status:'up_to_date',asOfDate:item.cutoffDate,sourceReference:`hospitalaria-rendicion:${item.id}`,notes:'Regularidad derivada de rendición conciliada.',recordedAtUtc:new Date().toISOString()});return{id:item.id,organizationId:item.organizationId,status:item.status,reviewedAtUtc:item.reviewedAtUtc,reviewNotes:item.reviewNotes}}
    return this.postJson(`/api/hospitalaria/rendiciones/${encodeURIComponent(submissionId)}/revision`,{decision,notes:notes??null})
  }


  async getHospitalariaReplenishmentRate(asOf?:string):Promise<HospitalariaReplenishmentRate>{if(this.useMocks)return {...this.mockReplenishmentRate};return this.request(`/api/hospitalaria/reposiciones/tarifa${asOf?`?asOf=${encodeURIComponent(asOf)}`:''}`)}
  async setHospitalariaReplenishmentRate(payload:{amountPerActiveMember:number;effectiveFrom:string;sourceReference:string}):Promise<HospitalariaReplenishmentRate>{if(this.useMocks){if(payload.amountPerActiveMember<=0||!payload.sourceReference.trim())throw new Error('La tarifa debe ser positiva e indicar referencia.');this.mockReplenishmentRate={id:crypto.randomUUID(),...payload,effectiveUntil:null};return {...this.mockReplenishmentRate}}return this.postJson('/api/hospitalaria/reposiciones/tarifa',payload)}
  async syncDeathReplenishmentCases():Promise<{created:number}>{if(this.useMocks){const created=this.mockDeathReplenishmentCases.length===0;this.ensureMockDeathReplenishments();return {created:created?1:0}}return this.request('/api/hospitalaria/reposiciones/sincronizar-defunciones',{method:'POST'})}
  async getDeathReplenishmentCases():Promise<{total:number;items:DeathReplenishmentCase[]}>{if(this.useMocks){this.ensureMockDeathReplenishments();const items=this.mockDeathReplenishmentCases.map(c=>({...c,transfers:c.transfers.map(t=>({...t,organizationName:this.mockOrganizations.find(o=>o.id===t.organizationId)?.name??'Taller'}))}));return {total:items.length,items}}return this.request('/api/hospitalaria/reposiciones')}
  async getWorkshopDeathReplenishments(organizationId:string):Promise<WorkshopDeathReplenishments>{if(this.useMocks){this.ensureMockDeathReplenishments();const items=(this.mockDeathReplenishmentObligations.get(organizationId)??[]).map(x=>({...x,payments:x.payments.map(p=>({...p}))}));return {organizationId,items,cases:groupWorkshopDeathCases(organizationId,items,this.mockDeathReplenishmentCases)}}return this.request(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/reposiciones`)}
  async addDeathReplenishmentPayment(obligationId:string,organizationId:string,payload:{amount:number;paymentMethod:string;paymentDate:string;reference:string}):Promise<DeathReplenishmentPayment>{if(this.useMocks){this.ensureMockDeathReplenishments();const item=this.mockDeathReplenishmentObligations.get(organizationId)?.find(x=>x.id===obligationId);if(!item)throw new Error('No existe la obligación.');if(payload.amount<=0||payload.amount>item.balance)throw new Error('El pago debe ser positivo y no superar el monto pendiente.');if(!payload.reference.trim()||!payload.paymentMethod.trim())throw new Error('Debe registrar medio y referencia del comprobante.');const payment={id:crypto.randomUUID(),...payload,receiptNumber:`HOSP-DEMO-${String(item.payments.length+1).padStart(3,'0')}`};item.payments.unshift(payment);item.paidAmount+=payload.amount;item.balance-=payload.amount;item.status=item.balance===0?'paid':'partial';const c=this.mockDeathReplenishmentCases.find(x=>x.id===item.caseId);if(c){c.paidAmount+=payload.amount;c.pendingMembers=this.countPending(item.caseId)}return payment}return this.postJson(`/api/hospitalaria/reposiciones/obligaciones/${encodeURIComponent(obligationId)}/pagos`,payload)}
  async submitDeathReplenishmentTransfer(caseId:string,organizationId:string,payload:{amount:number;transferDate:string;reference:string}):Promise<DeathReplenishmentTransfer>{if(this.useMocks){this.ensureMockDeathReplenishments();const items=(this.mockDeathReplenishmentObligations.get(organizationId)??[]).filter(x=>x.caseId===caseId);if(!items.length)throw new Error('No existen obligaciones para el Taller.');if(items.some(x=>x.status!=='paid'))throw new Error('La transferencia se habilita cuando se completa la cobranza.');const c=this.mockDeathReplenishmentCases.find(x=>x.id===caseId)!;const last=c.transfers.filter(x=>x.organizationId===organizationId).sort((a,b)=>(b.submissionNumber??1)-(a.submissionNumber??1))[0];if(last&&last.status!=='observed')throw new Error('La transferencia más reciente sigue en revisión o conciliada.');const expected=items.reduce((n,x)=>n+x.amountDue,0);if(payload.amount!==expected||!payload.reference.trim())throw new Error('El monto transferido debe coincidir con lo recaudado y tener referencia.');const transfer:DeathReplenishmentTransfer={id:crypto.randomUUID(),organizationId,submissionNumber:(last?.submissionNumber??0)+1,amount:payload.amount,expectedAmount:expected,transferDate:payload.transferDate,reference:payload.reference,status:'submitted'};c.transfers.push(transfer);return {...transfer}}return this.postJson(`/api/hospitalaria/reposiciones/casos/${encodeURIComponent(caseId)}/talleres/${encodeURIComponent(organizationId)}/transferencias`,payload)}
  async reviewDeathReplenishmentTransfer(transferId:string,decision:'observed'|'reconciled',notes?:string|null):Promise<{id:string;status:string;organizationId:string}>{if(this.useMocks){const t=this.mockDeathReplenishmentCases.flatMap(c=>c.transfers).find(x=>x.id===transferId);if(!t)throw new Error('No existe la transferencia.');if(decision==='observed'&&!notes?.trim())throw new Error('Indique el motivo de observación.');if(decision==='reconciled'&&t.amount!==t.expectedAmount)throw new Error('El pago transferido no coincide con el total esperado.');t.status=decision;t.reviewNotes=notes??null;const cases=[...new Set((this.mockDeathReplenishmentObligations.get(t.organizationId)??[]).map(x=>x.caseId))];const outstanding=cases.some(id=>(this.mockDeathReplenishmentObligations.get(t.organizationId)??[]).some(x=>x.caseId===id&&x.status!=='paid')||!this.mockDeathReplenishmentCases.find(c=>c.id===id)?.transfers.some(x=>x.organizationId===t.organizationId&&x.status==='reconciled'));this.mockHospitalaria.set(t.organizationId,{id:`hospitalaria-${crypto.randomUUID()}`,organizationId:t.organizationId,status:decision==='reconciled'&&!outstanding?'up_to_date':'overdue',asOfDate:new Date().toISOString().slice(0,10),sourceReference:`hospitalaria-reposicion:${t.id}`,notes:decision==='reconciled'&&!outstanding?'Reposición transferida y conciliada por Gran Hospitalaria.':'Existen reposiciones pendientes u observadas.',recordedAtUtc:new Date().toISOString()});return{id:t.id,status:t.status,organizationId:t.organizationId}}return this.postJson(`/api/hospitalaria/reposiciones/transferencias/${encodeURIComponent(transferId)}/revision`,{decision,notes:notes??null})}
  private countPending(caseId:string){return [...this.mockDeathReplenishmentObligations.values()].flat().filter(x=>x.caseId===caseId&&x.status!=='paid').length}
  private ensureMockDeathReplenishments(){if(this.mockDeathReplenishmentCases.length)return;const caseId='death-case-demo-1';this.mockDeathReplenishmentCases.push({id:caseId,deathDate:'2026-09-10',deceasedDisplayName:'Hermano fallecido demostrativo',amountPerActiveMember:1500,obligatedMembers:440,dueAmount:660000,paidAmount:1500,pendingMembers:439,transfers:[]});for(const org of this.mockOrganizations){const items=Array.from({length:22},(_,i)=>{const paid=org.number==='1'&&i===0?1500:0;return{id:`obligation-demo-${org.number}-${i+1}`,caseId,deathDate:'2026-09-10',deceasedDisplayName:'Hermano fallecido demostrativo',memberId:`member-demo-${org.number}-${i+1}`,memberDisplayName:`Hermano ${String(i+1).padStart(2,'0')} · Taller ${org.number}`,amountDue:1500,paidAmount:paid,balance:1500-paid,status:(paid===1500?'paid':'pending') as 'paid'|'pending',payments:paid?[{id:'hosp-pay-demo-1',amount:paid,paymentMethod:'transfer',paymentDate:'2026-09-12',receiptNumber:'HOSP-DEMO-001',reference:'TRX-HOSP-DEMO-001'}]:[]}});this.mockDeathReplenishmentObligations.set(org.id,items)}}

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
  async getSubmittedTenidas(): Promise<GrandSecretariatTenidasResponse> {
    if (this.useMocks) return { total: this.mockSubmittedTenidas.length, items: this.mockSubmittedTenidas.map(item => ({ ...item, lodge: { ...item.lodge } })) }
    return this.request<GrandSecretariatTenidasResponse>('/api/gran-secretaria/tenidas')
  }
  async reviewSubmittedTenida(recordId: string, decision: 'received' | 'observed', notes?: string | null): Promise<{ id:string; status:string }> {
    if (this.useMocks) {
      const item=this.mockSubmittedTenidas.find(value=>value.recordId===recordId); if(!item) throw new Error('La Tenida remitida no existe.')
      item.submissionStatus=decision; item.reviewedAtUtc=new Date().toISOString(); item.reviewNotes=notes?.trim()||null
      return { id: recordId, status: item.submissionStatus }
    }
    return this.postJson<{ id:string; status:string }>(`/api/gran-secretaria/tenidas/${encodeURIComponent(recordId)}/revision`, { decision, notes: notes ?? null })
  }
  async downloadSubmittedTenidaExtract(recordId: string): Promise<Blob> {
    if (this.useMocks) return new Blob(['Extracto PDF demostrativo'], { type: 'application/pdf' })
    const headers=new Headers({ Accept:'application/pdf' }); const token=await this.getAccessToken?.(); if(!token) throw new Error('Debe ingresar para descargar el extracto.'); headers.set('Authorization', `Bearer ${token}`)
    const response=await fetch(`${this.baseUrl}/api/gran-secretaria/tenidas/${encodeURIComponent(recordId)}/extracto`, { credentials:'omit', redirect:'error', cache:'no-store', headers })
    if(!response.ok){ if(response.status===401) await this.onUnauthorized?.(); throw new PmgmApiHttpError(response.status, response.status===403?'Su cuenta no tiene permiso para descargar este extracto.':`La API respondió ${response.status} ${response.statusText}.`) }
    return response.blob()
  }
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
  async issueSecretariatDocument(payload: IssueDocumentRequest): Promise<SecretariatDocument> { if (this.useMocks) { const kind=payload.documentType==='plancha'?(payload.planchaKind??'formal_communication'):null; const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: payload.documentType, planchaKind: kind, documentCode: `${payload.documentType === 'decree' ? 'DEC' : 'PLA-COM'}-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: payload.title, content: payload.content, organizationId: payload.organizationId ?? null, relatedCeremonyRequestId: null, spaceReservationId: null, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document } return this.postJson<SecretariatDocument>('/api/gran-secretaria/documentos', payload) }
  async issueSecretariatCeremonyAuthorization(ceremonyRequestId: string, spaceReservationId: string | null = null): Promise<SecretariatDocument> {
    if (this.useMocks) {
      const ceremony = this.mockCeremonies.find(item => item.id === ceremonyRequestId); if (!ceremony) throw new Error('La ceremonia indicada no existe.'); if (ceremony.formalAuthorizationIssued) throw new Error('La ceremonia ya cuenta con autorización formal vigente.'); if (spaceReservationId && ceremony.spaceReservationId !== spaceReservationId) throw new Error('La reserva indicada no corresponde a esta ceremonia.'); ceremony.formalAuthorizationIssued = true
      const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: 'plancha', planchaKind: 'ceremony_authorization', documentCode: `PLA-AUT-CER-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: `Plancha de Autorización de Ceremonia — ${ceremonyTypeLabel(ceremony.ceremonyType)}`, content: `Plancha formal de autorización demostrativa para ${ceremony.organizationName}. No constituye Decreto.`, organizationId: ceremony.organizationId, relatedCeremonyRequestId: ceremony.id, spaceReservationId, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document
    }
    return this.postJson<SecretariatDocument>(`/api/gran-secretaria/ceremonias/${encodeURIComponent(ceremonyRequestId)}/autorizacion`, { spaceReservationId })
  }

  private requireMockReviewCeremony(id: string): CeremonyReviewQueueItem { const item = this.mockReviewCeremonies.find(value => value.id === id); if (!item) throw new Error('La ceremonia indicada no existe en la bandeja.'); return item }
  private requireMockTreasuryStatement(id: string): TreasuryStatement { const item = this.mockTreasuryStatements.get(id); if (!item) throw new Error('El cuadro mensual indicado no existe.'); return item }
  private ensureMockHospitalariaMovements(organizationId:string):LodgeHospitalariaMovement[]{
    let items=this.mockLodgeHospitalariaMovements.get(organizationId)
    if(!items){items=defaultHospitalariaMovements(organizationId);this.mockLodgeHospitalariaMovements.set(organizationId,items)}
    return items
  }
  private requireMockHospitalariaMovement(id:string):LodgeHospitalariaMovement{for(const rows of this.mockLodgeHospitalariaMovements.values()){const item=rows.find(x=>x.id===id);if(item)return item}throw new Error('El movimiento de Hospitalaria no existe.')}
  private requireMockHospitalariaSubmission(id:string):HospitalariaMonthlySubmission{const item=[...this.mockHospitalariaSubmissions.values()].find(x=>x.id===id);if(!item)throw new Error('La rendición de Hospitalaria no existe.');return item}
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
function defaultHospitalariaMovements(organizationId:string):LodgeHospitalariaMovement[]{
  return [
    {id:`hosp-income-${organizationId}`,organizationId,movementType:'income',category:'charity_bag',amount:185000,movementDate:'2026-09-05',memberReference:null,destination:null,evidenceReference:'TENIDA-DEMO-2026-09-05',observation:'Tronco de Beneficencia · dato ficticio',approvalStatus:'not_required',approvalSource:null,councilDecisionId:null,approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-05T23:00:00Z'},
    {id:`hosp-aid-${organizationId}`,organizationId,movementType:'expense',category:'charity_aid',amount:45000,movementDate:'2026-09-10',memberReference:'Referencia reservada DEMO-001',destination:'Socorro reservado · demo',evidenceReference:'AYUDA-DEMO-001',observation:'Antecedente sensible ficticio; sólo Taller.',approvalStatus:'pending_approval',approvalSource:null,councilDecisionId:null,approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-10T18:00:00Z'}
  ]
}
function mockHospitalariaCouncilAidDecisions(organizationId:string):HospitalariaCouncilAidDecision[]{return[{id:`council-aid-${organizationId}`,sessionId:`council-session-${organizationId}`,sessionDate:'2026-09-11',subject:'Socorro reservado · referencia demo',amount:45000}]}
function mockHospitalariaCouncilFinancialReviews(organizationId:string):HospitalariaCouncilFinancialReview[]{return[{id:`council-review-${organizationId}`,sessionId:`council-session-review-${organizationId}`,sessionDate:'2026-09-15',periodLabel:'2026-09',conclusion:'Estado mensual de Hospitalaria revisado por Consejo · demo'}]}
function summarizeMockHospitalaria(organizationId:string,from:string,to:string,items:LodgeHospitalariaMovement[]):LodgeHospitalariaSummary{const income=items.filter(x=>x.movementType==='income').reduce((a,x)=>a+x.amount,0);const approvedExpenses=items.filter(x=>x.movementType==='expense'&&x.approvalStatus==='approved').reduce((a,x)=>a+x.amount,0);const pendingExpenses=items.filter(x=>x.movementType==='expense'&&x.approvalStatus==='pending_approval').length;const categories=[...new Set(items.map(x=>x.category))].map(category=>({category,total:items.filter(x=>x.category===category).reduce((a,x)=>a+x.amount,0),count:items.filter(x=>x.category===category).length}));return{organizationId,from,to,income,approvedExpenses,periodNet:income-approvedExpenses,pendingExpenses,movements:items.length,categories,items:items.map(x=>({...x}))}}
function currentMonthStart(){const d=new Date();return new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit'}).format(d)+'-01'}
function currentMonthEnd(){const p=currentMonthStart().slice(0,7).split('-').map(Number);return monthEnd(p[0],p[1])}
function monthEnd(year:number,month:number){return `${year}-${String(month).padStart(2,'0')}-${String(new Date(Date.UTC(year,month,0)).getUTCDate()).padStart(2,'0')}`}
function cloneHospitalariaSubmission(item:HospitalariaMonthlySubmission):HospitalariaMonthlySubmission{return{...item}}
function mockSnapshotAsOf(snapshot: WorkshopRegularitySnapshot | undefined, asOf?: string): WorkshopRegularitySnapshot | null { if (!snapshot) return null; if (asOf && snapshot.asOfDate > asOf) return null; return { ...snapshot } }
function mockGrandTreasuryRate(feeType:LodgeFeeType,territory:TreasuryTerritory|null|undefined,asOf:string):number|null{if(asOf<'2026-01-01')return null;if(feeType==='past_active')return 0;if(territory==='santiago')return feeType==='normal'?21000:feeType==='spouse'?13000:feeType==='senior'?10000:feeType==='student'?8000:null;if(territory==='other_oriente')return feeType==='normal'?15000:feeType==='spouse'?10000:feeType==='senior'?8000:feeType==='student'?8000:null;return null}
function defaultLodgeFeePlans(organizationId: string): LodgeFeePlan[] { return [
  { id: `fee-normal-${organizationId}`, organizationId, feeType: 'normal', memberAmount: 26000, grandTreasuryAmount: 21000, workshopAmount: 5000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-student-${organizationId}`, organizationId, feeType: 'student', memberAmount: 10000, grandTreasuryAmount: 8000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-senior-${organizationId}`, organizationId, feeType: 'senior', memberAmount: 12000, grandTreasuryAmount: 10000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
  { id: `fee-spouse-${organizationId}`, organizationId, feeType: 'spouse', memberAmount: 15000, grandTreasuryAmount: 13000, workshopAmount: 2000, effectiveFrom: '2026-01-01', effectiveUntil: null, isActive: true },
] }
function mockEffectiveLodgeFeePlan(plans:LodgeFeePlan[],feeType:LodgeFeeType,periodYear:number,periodMonth:number):LodgeFeePlan|undefined{
  const cutoff=new Date(Date.UTC(periodYear,periodMonth,0)).toISOString().slice(0,10)
  return plans.filter(plan=>plan.feeType===feeType&&plan.effectiveFrom<=cutoff&&(plan.effectiveUntil===null||plan.effectiveUntil>=cutoff)).sort((a,b)=>b.effectiveFrom.localeCompare(a.effectiveFrom))[0]
}
function summarizeMockLodgeTreasury(organizationId:string,periodYear:number,periodMonth:number,charges:LodgeTreasuryCharge[],plans:LodgeFeePlan[]):LodgeTreasurySummary{
  const memberExpected=charges.reduce((sum,charge)=>sum+charge.memberAmount,0)
  const collected=charges.reduce((sum,charge)=>sum+charge.paidAmount,0)
  const receivable=charges.reduce((sum,charge)=>sum+charge.balance,0)
  const grandTreasuryExpected=charges.reduce((sum,charge)=>{
    if(charge.feeType==='past_active')return sum
    if(!charge.feeType)throw new Error(`El cargo ficticio de ${charge.memberDisplayName} no tiene categoría de cuota.`)
    const plan=mockEffectiveLodgeFeePlan(plans,charge.feeType,periodYear,periodMonth)
    if(!plan||plan.grandTreasuryAmount===null)throw new Error(`No hay aporte institucional vigente para ${charge.feeType} en ${periodYear}-${String(periodMonth).padStart(2, '0')}.`)
    return sum+plan.grandTreasuryAmount
  },0)
  const paid=charges.filter(charge=>charge.status==='paid').length
  const partial=charges.filter(charge=>charge.status==='partial').length
  const pending=charges.filter(charge=>charge.status==='pending').length
  return{organizationId,periodYear,periodMonth,members:charges.length,memberExpected,collected,receivable,grandTreasuryExpected,workshopMarginProjected:memberExpected-grandTreasuryExpected,paid,partial,overdue:pending,trafficLight:charges.length===0?'no_data':receivable===0?'green':collected>0?'amber':'red'}
}
function mockTreasuryCharges(organizationId:string):LodgeTreasuryCharge[]{return[
  {id:`charge-1-${organizationId}`,memberId:'member-demo-001',memberDisplayName:'Andrea Demostrativa',feeType:'normal',memberAmount:26000,monthlyFeeAmount:26000,paidAmount:26000,balance:0,status:'paid',payments:[{id:'pay-demo-001',receiptNumber:'REC-DEMO-001',amount:26000,paymentMethod:'transfer',paymentDate:'2026-09-05',reference:'TRX-DEMO-001'}]},
  {id:`charge-2-${organizationId}`,memberId:'member-demo-002',memberDisplayName:'Beatriz Demostrativa',feeType:'normal',memberAmount:26000,monthlyFeeAmount:26000,paidAmount:13000,balance:13000,status:'partial',payments:[{id:'pay-demo-002',receiptNumber:'REC-DEMO-002',amount:13000,paymentMethod:'cash',paymentDate:'2026-09-09',reference:null}]},
  {id:`charge-3-${organizationId}`,memberId:'member-demo-003',memberDisplayName:'Carolina Demostrativa',feeType:'spouse',memberAmount:15000,monthlyFeeAmount:15000,paidAmount:0,balance:15000,status:'pending',payments:[]}
]}
function mockTreasuryExpenses(organizationId:string):LodgeTreasuryExpense[]{return[
  {id:`expense-1-${organizationId}`,organizationId,category:'Servicios',amount:42000,expenseDate:'2026-09-12',description:'Servicio operativo del Taller · dato ficticio',evidenceReference:'PDF-RESPALDO-DEMO-001',approvalStatus:'pending_approval',recordedBySubject:'tesoreria-demo',approvedBySubject:null,approvedAtUtc:null,recordedAtUtc:'2026-09-12T18:00:00Z'}
]}
function cloneTreasuryCharge(item:LodgeTreasuryCharge):LodgeTreasuryCharge{return{...item,payments:item.payments.map(x=>({...x}))}}
function mockRegularitySnapshot(organizationId: string, payload: WorkshopRegularityRequest, prefix: string): WorkshopRegularitySnapshot { return { id: `${prefix}-${crypto.randomUUID()}`, organizationId, scope: 'organization', status: payload.status, asOfDate: payload.asOfDate, sourceReference: payload.sourceReference ?? null, notes: payload.notes ?? null, recordedAtUtc: new Date().toISOString() } }
function mockTreasuryStatement(organizationId: string, payload: CreateTreasuryStatementRequest): TreasuryStatement { return { id: crypto.randomUUID(), organizationId, periodYear: payload.periodYear, periodMonth: payload.periodMonth, cutoffDate: payload.cutoffDate, status: 'draft', sourceReference: payload.sourceReference ?? null, expectedAmount: 0, transferAmount: 0, depositAmount: 0, paidAmount: 0, differenceAmount: 0, unresolvedIdentities: 0, feeBreakdown:[], lines: [], payments: [], submittedAtUtc: null, reconciledAtUtc: null, closedAtUtc: null } }
function recalculateMockTreasury(statement: TreasuryStatement) { statement.expectedAmount = statement.lines.reduce((total, line) => total + line.payableAmount, 0); statement.transferAmount = statement.payments.filter(payment => payment.paymentMethod === 'transfer').reduce((total, payment) => total + payment.amount, 0); statement.depositAmount = statement.payments.filter(payment => payment.paymentMethod === 'deposit').reduce((total, payment) => total + payment.amount, 0); statement.paidAmount = statement.transferAmount + statement.depositAmount; statement.differenceAmount = statement.expectedAmount - statement.paidAmount; statement.feeBreakdown=Array.from(statement.lines.reduce((map,line)=>{const current=map.get(line.contributionType)??{feeType:line.contributionType,members:0,amount:0};current.members++;current.amount+=line.payableAmount;map.set(line.contributionType,current);return map},new Map<LodgeFeeType,TreasuryFeeBreakdown>()).values()) }
function cloneTreasuryStatement(statement: TreasuryStatement): TreasuryStatement { return { ...statement, feeBreakdown:statement.feeBreakdown.map(item=>({...item})), lines: statement.lines.map(line => ({ ...line })), payments: statement.payments.map(payment => ({ ...payment })) } }
function cloneCeremonyQueueItem(item: CeremonyReviewQueueItem): CeremonyReviewQueueItem { return { ...item, eligibility: { ...item.eligibility, publication: item.eligibility.publication ? { ...item.eligibility.publication } : null, ceremonyRight: item.eligibility.ceremonyRight ? { ...item.eligibility.ceremonyRight } : null, requirements: item.eligibility.requirements.map(value => ({ ...value })) }, actions: { ...item.actions } } }
function recomputeMockEligibility(item: CeremonyReviewQueueItem) { const blocked = item.eligibility.requirements.some(value => value.status === 'rejected'); const observed = item.eligibility.requirements.some(value => value.status === 'observed'); item.eligibility.canAuthorize = !blocked && !observed; item.eligibility.status = item.eligibility.canAuthorize ? 'complies' : observed ? 'observed' : 'does_not_comply'; item.actions.canAuthorize = item.eligibility.canAuthorize }
function mockRegimenSummary(filters: { organizationId?: string; asOf?: string; from?: string }): RegimenInteriorSummary {
  const asOf = filters.asOf ?? '2026-09-08'
  const from = filters.from ?? '2026-01-01'
  const scoped = !!filters.organizationId
  const multiplier = scoped ? 1 : 20
  const affiliated = 24 * multiplier
  return {
    scope: scoped ? 'organization' : 'order',
    organizationId: filters.organizationId ?? null,
    asOf,
    period: { from, to: asOf },
    members: { totalRelated: affiliated, currentlyAffiliated: affiliated, active: affiliated, inactive: 0, currentWithBlockingStatus: 0 },
    events: { voluntaryWithdrawals: 0, forcedWithdrawals: 0, reinstatements: 0, deaths: 0, transfers: 0 },
    financialRegularity: { source: 'Gran Tesorería · escenario QA', currentAffiliations: affiliated, upToDate: affiliated, delinquent: 0, pending: 0, exempt: 0, withoutStatus: 0, delinquentMembersDistinct: 0 },
    degreeDistribution: { master: 12 * multiplier, fellowcraft: 5 * multiplier, apprentice: 5 * multiplier, past_active: 2 * multiplier },
    pendingTransfers: 0,
  }
}

function groupWorkshopDeathCases(organizationId:string,items:DeathReplenishmentObligation[],cases:DeathReplenishmentCase[]){return [...new Set(items.map(x=>x.caseId))].map(caseId=>{const rows=items.filter(x=>x.caseId===caseId);const transfer=cases.find(x=>x.id===caseId)?.transfers.filter(x=>x.organizationId===organizationId).sort((a,b)=>(b.submissionNumber??1)-(a.submissionNumber??1))[0];return{caseId,deathDate:rows[0].deathDate,deceasedDisplayName:rows[0].deceasedDisplayName,dueAmount:rows.reduce((n,x)=>n+x.amountDue,0),paidAmount:rows.reduce((n,x)=>n+x.paidAmount,0),allPaid:rows.every(x=>x.status==='paid'),transfer:transfer?{id:transfer.id,amount:transfer.amount,reference:transfer.reference,status:transfer.status,transferDate:transfer.transferDate}:null}})}
