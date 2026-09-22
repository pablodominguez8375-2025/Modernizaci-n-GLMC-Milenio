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
export interface OrganizationOption { id: string; name: string; number: string | null; type: string }
export interface OrganizationOptionsResponse { total: number; items: OrganizationOption[] }
export interface SystemSetting { code: string; category: string; label: string; valueType: 'integer' | 'text' | 'list'; value: string; effectiveFrom: string; sourceReference: string; status: string }
export interface SystemSettingsResponse { total: number; items: SystemSetting[] }
export interface CreateSystemSettingVersionRequest { value: string; effectiveFrom: string; sourceReference: string }
export interface SystemSettingVersion { id: string; value: string; effectiveFrom: string; effectiveTo: string | null; sourceReference: string; status: string; createdAtUtc: string }
export interface SystemSettingVersionsResponse { total: number; items: SystemSettingVersion[] }
export interface AuditLogEvent { id:string; occurredAtUtc:string; user:string; ipAddress:string; menu:string|null; submenu:string|null; summary:string; action:string; result:'success'|'rejected'|'observed'; correlationId:string }
export interface AuditLogResponse { total:number; page:number; pageSize:number; immutable:boolean; items:AuditLogEvent[] }
export type LodgeFeeType = 'normal' | 'student' | 'senior' | 'spouse' | 'past_active'
export interface LodgeFeePlan { id: string; organizationId: string; feeType: LodgeFeeType; memberAmount: number; grandTreasuryAmount: number; workshopAmount: number; effectiveFrom: string; effectiveUntil: string | null; isActive: boolean }
export interface LodgeTreasurySummary { organizationId: string; periodYear: number; periodMonth: number; members: number; memberExpected: number; collected: number; receivable: number; grandTreasuryExpected: number; workshopMarginProjected: number; paid: number; partial: number; overdue: number; trafficLight: 'green' | 'amber' | 'red' | 'no_data' }
export interface LodgeTreasuryPayment { id:string; receiptNumber:string; amount:number; paymentMethod:'cash'|'transfer'|'deposit'|'other'; paymentDate:string; reference:string|null }
export interface LodgeTreasuryCharge { id:string; memberId:string; memberDisplayName:string; memberAmount:number; paidAmount:number; balance:number; status:'pending'|'partial'|'paid'; payments:LodgeTreasuryPayment[] }
export interface LodgeTreasuryExpense { id:string; organizationId:string; category:string; amount:number; expenseDate:string; description:string; evidenceReference:string|null; approvalStatus:'pending_approval'|'approved'; recordedBySubject:string; approvedBySubject:string|null; approvedAtUtc:string|null; recordedAtUtc:string }
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

export interface TreasuryStatementLine {
  id: string; memberId: string | null; membershipId: string | null; degreeCodeAtCutoff: string; officeCodeAtCutoff: string | null
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
  { displayName: 'Persona Demostrativa Uno', workshopName: 'Taller Demostrativo NÂº 1', workshopNumber: '1', publishedFromUtc: '2026-08-25T15:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 13, complianceDateUtc: '2026-09-14T15:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
  { displayName: 'Persona Demostrativa Dos', workshopName: 'Taller Demostrativo NÂº 23', workshopNumber: '23', publishedFromUtc: '2026-08-15T18:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 23, complianceDateUtc: '2026-09-04T18:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
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
  { code:'system.workflow.initiation.approval_steps',category:'Flujos',label:'Aprobaciones de iniciaciÃ³n',valueType:'list',value:'RÃ©gimen Interior|Gran TesorerÃ­a|Gran Hospitalaria|Gran SecretarÃ­a|Gran MaestrÃ­a',effectiveFrom:'2026-01-01',sourceReference:'Protocolo 2026',status:'default' },
  { code:'system.publication.candidate.minimum_days',category:'Publicaciones',label:'DÃ­as mÃ­nimos de publicaciÃ³n',valueType:'integer',value:'20',effectiveFrom:'2026-01-01',sourceReference:'Protocolo 2026',status:'default' },
  { code:'system.interviews.minimum_count',category:'Procesos-uç®m¢G§²ÚîÆ­yÙˆ
Z][K˜Xİ[ÛœË˜Ø[]]Üš^™JH›İÈ™]È\œ›ÜŠ	ÔİHİY[H›ÈYYH]]Üš^˜\ˆ\İHÙ\™[[ÛšXK‰ÊBˆYˆ
Z][K™[YÚXš[]K˜Ø[]]Üš^™JH›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXHpî›ˆY[™H™\]Z\Ú]ÜÈØ›YØ]Üš[ÜÈ[™Y[\Ë‰ÊBˆ][Kœİ]\ÈH	Ø]]Üš^™Y	ÎÈ][K˜Xİ[ÛœÈHÈØ[•˜[Y]R[\›˜[Y™˜Z\œÎˆ˜[ÙKØ[”X›\ÚØ[™Y]Nˆ˜[ÙKØ[]]Üš^™Nˆ˜[ÙHBˆ™]\›ˆÈYˆ][KšYİ]\Îˆ][Kœİ]\ÈBˆBˆ™]\›ˆ\Ëœ™\]Y\İÈYÎˆİš[™ÎÈİ]\Îˆİš[™ÈOŠØ\KØÙ\™[[ÛšX\ËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KØ]]Üš^˜\˜ÈY]Ùˆ	ÔÔÕ	ÈJBˆBˆ\Ş[˜È™YÚ\İ\’[š]X][ÛŠÙ\™[[ÛT™\]Y\İYˆİš[™ËÙ\™[[ÛQ]Nˆİš[™ËZ[]T™Y™\™[˜ÙNˆİš[™ÊNˆ›ÛZ\ÙO[š]X][ÛÛÛ\][Û”™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İYİ]\Îˆ	ØÛÛ\]Y	ËY[X™\’Yˆ	ÛY[X™\‹Y[[ËLŒ‹LIËY[X™\œÚ\İ]\Îˆ	ØXİ]™IËYÜ™YNˆ	Ø\™[XÙIËY™™Xİ]™Q]NˆÙ\™[[ÛQ]KØİ[Y[ÛÙNˆ	ĞUUPÑT‹QSSËLŒ‹LIÈBˆ™]\›ˆ\ËœÜİœÛÛ[š]X][ÛÛÛ\][Û”™\ÜÛœÙOŠØ\KØÙ\™[[ÛšX\ËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KÜ™YÚ\İ˜\‹Z[šXÚXXÚ[Û˜ÈÙ\™[[ÛQ]KZ[]T™Y™\™[˜ÙHJBˆBˆ\Ş[˜È™XÛÜ™[š]X[[X™\˜][ÛŠÙ\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆ[š]X[[X™\˜][Û”™\]Y\İ
Nˆ›ÛZ\ÙO[š]X[[X™\˜][Û”™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆ[ØÚÒ[š]X[[X™\˜][ÛŠÙ\™[[ÛT™\]Y\İY^[ØY
Bˆ™]\›ˆ\ËœÜİœÛÛ[š]X[[X™\˜][Û”™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KÙ[X™\˜XÚ[Û‹Z[šXÚX[^[ØY
BˆBˆ\Ş[˜È\ØY[\šY]ÑØİ[Y[
Ù\™[[ÛT™\]Y\İYˆİš[™Ë[\šY]ÒYˆİš[™Ëš[Nˆš[KY]Y]NˆÛZ]Ø[™Y]R[\šY]Ñ]šY[˜ÙK	ÙØİ[Y[™\œÚ[Û’Y	ÏŠNˆ›ÛZ\ÙO[\šY]ÑØİ[Y[™\ÜÛœÙOˆÂˆÛÛœİ^[œÚ[ÛˆHš[K›˜[YKÓİÙ\Ø\ÙJ
KœÜ]
	Ë‰ÊKœÜ

BˆÛÛœİÛÛ[\HHš[K\H
^[œÚ[ÛˆOOH	Ü‰ÈÈ	Ø\XØ][Û‹Ü‰Èˆ^[œÚ[ÛˆOOH	ÙØŞ	ÈÈ	Ø\XØ][Û‹İ›™›Ü[[›Ü›X]Ë[Ù™šXÙYØİ[Y[ÛÜ™›ØÙ\ÜÚ[™Û[™Øİ[Y[	Èˆ	ÉÊBˆYˆ
VÉØ\XØ][Û‹Ü‰Ë	Ø\XØ][Û‹İ›™›Ü[[›Ü›X]Ë[Ù™šXÙYØİ[Y[ÛÜ™›ØÙ\ÜÚ[™Û[™Øİ[Y[	×Kš[˜ÛY\ÊÛÛ[\JJH›İÈ™]È\œ›ÜŠ	ÓH[™]š\İHX™HY[\œÙH[ˆÛÜ™
™ØŞ
HÈ‹‰ÊBˆYˆ
š[KœÚ^™HHš[KœÚ^™HˆL—ÍÎ
H›İÈ™]È\œ›ÜŠ	Ñ[\˜Ú]›ÈX™HÛÛ[™\ˆ[™›Ü›XXÚpìÛˆH\Ø\ˆÛÛ[Èpè^[[ÈLP‹‰ÊBˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİØİ[Y[™\œÚ[Û’YHÜ\Ëœ˜[™ÛUURQ

Bˆ\Ë›[ØÚÒ[\šY]ÑØİ[Y[ËœÙ]
Øİ[Y[™\œÚ[Û’YÈš[Kš[S˜[YNˆš[K›˜[YKY]Y]NˆÈ‹‹›Y]Y]KØİ[Y[™\œÚ[Û’YHJBˆ™]\›ˆÈ[\šY]ÒYØİ[Y[™\œÚ[Û’Yš[S˜[YNˆš[K›˜[YK™\İ[ˆY]Y]Kœ™\İ[İ[[X\NˆY]Y]Kœİ[[X\KÚ^™P]\Îˆš[KœÚ^™HBˆBˆ™]\›ˆ\Ëœ™\]Y\İ[\šY]ÑØİ[Y[™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KÙ[™]š\İ\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
[\šY]ÒY
_KØÛÛ[šYØÈY]Ùˆ	ÔU	ËXY\œÎˆÈ	ĞÛÛ[U\IÎˆÛÛ[\K	ÖQš[KS˜[YIÎˆ[˜ÛÙUT’PÛÛ\Û™[
š[K›˜[YJK	ÖR[\šY]Ù\‰Îˆ[˜ÛÙUT’PÛÛ\Û™[
Y]Y]Kš[\šY]Ù\‘\Ü^S˜[YJK	ÖR[\šY]ËTİ[[X\IÎˆ[˜ÛÙUT’PÛÛ\Û™[
Y]Y]Kœİ[[X\JK	ÖR[\šY]ËT™\İ[	ÎˆY]Y]Kœ™\İ[	ÖR[\šY]ËQ]IÎˆY]Y]Kš[\šY]Ñ]HK›ÙNˆš[HJBˆBˆ\Ş[˜È™XÛÜ™[\šY]ÔXÚØYÙJÙ\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆ[\šY]ÔXÚØYÙT™\]Y\İ
Nˆ›ÛZ\ÙO[\šY]ÔXÚØYÙT™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆYˆ
^[ØYš[\šY]ÜË›[™İÊH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	İ\™ÙYÜ™YWÜ™]šY]Ëš[\šY]ÜÉË™X\ÛÛˆ[^YY[H™\]ZY\™H[Y[›ÜÈ™\È[™]š\İ\ÈÛÛ\]\ÎÈXİX[Y[H™YÚ\İ˜H	Ü^[ØYš[\šY]ÜË›[™İK˜ÛÛ\]Y[\šY]ÜÎˆ^[ØYš[\šY]ÜË›[™İÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆYˆ
^[ØYš[\šY]ÜËœÛÛYJ][HOˆZ][Kœİ[[X\Kš[J
HZ][K™Øİ[Y[™\œÚ[Û’YVÉÙ˜]›Ü˜X›IË	Ù\Ù˜]›Ü˜X›I×Kš[˜ÛY\Ê][Kœ™\İ[
JJH›İÈ™]È\œ›ÜŠ	ĞØYH[™]š\İH™\]ZY\™H™\İ[Y[‹™\İ[YÈH\˜Ú]›ÈÛÜ™È‹‰ÊBˆYˆ
\^[ØY˜ÛÛ™šY[X[]Y\İ[Û›˜Z\™P]˜Z[X›JH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	İ\™ÙYÜ™YWÜ™]šY]Ë˜ÛÛ™šY[X[Ü]Y\İ[Û›˜Z\™IË™X\ÛÛˆ	Ñ˜[H[İY\İ[Û˜\š[ÈÛÛ™šY[˜ÚX[™\]Y\šYÈ\˜HH™]š\ÚpìÛˆH\˜Ù\ˆÜ˜YË‰ËÛÛ\]Y[\šY]ÜÎˆ^[ØYš[\šY]ÜË›[™İÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆYˆ
\^[ØY˜]]Øš[ÙÜ˜\P]˜Z[X›JH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	İ\™ÙYÜ™YWÜ™]šY]Ë˜]]Øš[ÙÜ˜\IË™X\ÛÛˆ	Ñ˜[HH]]Øš[ÙÜ˜Y°ëXH™\]Y\šYH\˜HH™]š\ÚpìÛˆH\˜Ù\ˆÜ˜YË‰ËÛÛ\]Y[\šY]ÜÎˆ^[ØYš[\šY]ÜË›[™İÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆ™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	Ø\›İ™Y	ËÛÙNˆ	İ\™ÙYÜ™YWÜ™]šY]ËœXÚØYÙWØÛÛ\]IË™X\ÛÛˆ[^YY[HÛÛY[™H	Ü^[ØYš[\šY]ÜË›[™İH[™]š\İ\ÈHÜÈ[XÙY[\È™\]Y\šYÜË˜ÛÛ\]Y[\šY]ÜÎˆ^[ØYš[\šY]ÜË›[™İÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆBˆ™]\›ˆ\ËœÜİœÛÛ[\šY]ÔXÚØYÙT™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KØ[XÙY[\Ø^[ØY
BˆBˆ\Ş[˜È™XÛÜ™\™YÜ™YT™]šY]ÊÙ\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆ\™YÜ™YT™]šY]Ô™\]Y\İ
Nˆ›ÛZ\ÙO\™YÜ™YT™]šY]Ô™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆYˆ
\^[ØYœÛİ\˜ÙT™Y™\™[˜ÙKš[J
JH›İÈ™]È\œ›ÜŠ	ÑX™H[™XØ\ˆH™Y™\™[˜ÚXH[^˜XİÈHXİK‰ÊBˆYˆ
^[ØYœ™\Ù[›İ\œÈH^[ØY›İ\Ò[‘˜]›Üˆ^[ØY›İ\ĞYØZ[œİ^[ØY˜Xœİ[[ÛœÈ^[ØY›İ\Ò[‘˜]›Üˆ
È^[ØY›İ\ĞYØZ[œİ
È^[ØY˜Xœİ[[ÛœÈOOH^[ØYœ™\Ù[›İ\œÊH›İÈ™]È\œ›ÜŠ	ÓHİ[XHH›İÜÈX™HÛÚ[˜ÚY\ˆÛÛˆH\Ú\İ[˜ÚXH™YÚ\İ˜YK‰ÊBˆÛÛœİİ]\ÈH^[ØY›Ü[•›İP\›İ™YÈ	Ø\›İ™Y	Èˆ	Ü™Z™XİY	Âˆ™]\›ˆÈ\™YÜ™YNˆÈYˆÙ\™[[ÛT™\]Y\İYİ]\ËÛÙNˆ\™ÙYÜ™YWÜ™]šY]Ë‰Üİ]\ßX™X\ÛÛˆ^[ØY›Ü[•›İP\›İ™YÈ	ÓH›İXÚpìÛˆXšY\HH\˜Ù\ˆÜ˜YÈYH˜]›Ü˜X›K‰Èˆ	ÓH›İXÚpìÛˆXšY\HH\˜Ù\ˆÜ˜YÈ›ÈYH˜]›Ü˜X›K‰ÈKİ]\Îˆ^[ØY›Ü[•›İP\›İ™YÈ	İ[™\—Ü™]šY]ÉÈˆ	Ü™Z™XİY	ÈBˆBˆ™]\›ˆ\ËœÜİœÛÛ\™YÜ™YT™]šY]Ô™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KÜ™]š\Ú[Û‹]\˜Ù\‹YÜ˜YØ^[ØY
BˆBˆ\Ş[˜È™XÛÜ™š[˜[˜[İ
Ù\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆš[˜[˜[İ™\]Y\İ
Nˆ›ÛZ\ÙOš[˜[˜[İ™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆYˆ
\^[ØYœÛİ\˜ÙT™Y™\™[˜ÙKš[J
JH›İÈ™]È\œ›ÜŠ	ÑX™H[™XØ\ˆH™Y™\™[˜ÚXH[^˜XİÈHXİK‰ÊBˆYˆ
^[ØY˜˜[İË›[™İH^[ØY˜˜[İË›[™İˆÈ™]ÈÙ]
^[ØY˜˜[İË›X\
][HOˆ][Kœ›ØÙY\™S[X™\ŠJKœÚ^™HOOH^[ØY˜˜[İË›[™İ
H›İÈ™]È\œ›ÜŠ	ÑX™H™YÚ\İ˜\ˆ[™H[›ÈH™\È°è[Z]\È\İ[ÜË‰ÊBˆYˆ
^[ØY˜˜[İËœÛÛYJ][HOˆ][K™[YÚX›U›İ\œÈH][KÚ]P˜[İÈ][K˜›XÚĞ˜[İÈ][KÚ]P˜[İÈ
È][K˜›XÚĞ˜[İÈOOH][K™[YÚX›U›İ\œÊJH›İÈ™]È\œ›ÜŠ	Ó\È˜[İ\È›[˜Ø\ÈH™YÜ˜\ÈX™[ˆÛÚ[˜ÚY\ˆÛÛˆ\È\œÛÛ˜\ÈXš[]Y\Ë‰ÊBˆ™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ^[ØY˜˜[İ\›İ™YÈ	Ø\›İ™Y	Èˆ	Ü™Z™XİY	Ë\ÓÙ‘]Nˆ^[ØY˜˜[İ]KÛÙNˆ^[ØY˜˜[İ\›İ™YÈ	Ùš\œİÙYÜ™YWØ˜[İ˜\›İ™Y	Èˆ	Ùš\œİÙYÜ™YWØ˜[İœ™Z™XİY	Ë™X\ÛÛˆ^[ØY˜˜[İ\›İ™YÈ	Ñ[˜[İZ™HYš[š]]›ÈYH˜]›Ü˜X›K‰Èˆ	Ñ[˜[İZ™HYš[š]]›ÈYH\Ù˜]›Ü˜X›K‰ËÙ\™[[ÛTİ]\Îˆ^[ØY˜˜[İ\›İ™YÈ	İ[™\—Ü™]šY]ÉÈˆ	Ü™Z™XİY	ÈBˆBˆ™]\›ˆ\ËœÜİœÛÛš[˜[˜[İ™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KØ˜[İZ™X^[ØY
BˆBˆ\Ş[˜ÈİX›Z][š]X][Û”™\]Y\İ
Ù\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆ[š]X][Û”™\]Y\İİX›Z\ÜÚ[ÛŠNˆ›ÛZ\ÙO[š]X][Û”™\]Y\İİX›Z\ÜÚ[Û”™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆYˆ
^[ØYœ›ÜÜÙYÙ\™[[ÛQ]H^[ØYœİX›Z\ÜÚ[Û‘]JH›İÈ™]È\œ›ÜŠ	ÓH™XÚH›ÜY\İH›ÈYYHÙ\ˆ[\š[ÜˆHHÛÛXÚ]Y‰ÊBˆYˆ
\^[ØY™[™\˜X›P\›İ˜[
H›İÈ™]È\œ›ÜŠ	ÓHÛÛXÚ]Y™\]ZY\™HÛÛ™š\›XXÚpìÛˆ[™[™\˜X›HXY\İ›Ë‰ÊBˆYˆ
\^[ØYœÙXÜ™]\Q\Ü^S˜[YKš[J
H\^[ØYœÛİ\˜ÙT™Y™\™[˜ÙKš[J
JH›İÈ™]È\œ›ÜŠ	ÑX™H[™XØ\ˆÙXÜ™]\°ëXH™\ÜÛœØX›HH™Y™\™[˜ÚXHØİ[Y[[‰ÊBˆ™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	Ø\›İ™Y	Ë›ÜÜÙY]Nˆ^[ØYœ›ÜÜÙYÙ\™[[ÛQ]KÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉË[™XYTİX›Z]Yˆ˜[ÙHBˆBˆ™]\›ˆ\ËœÜİœÛÛ[š]X][Û”™\]Y\İİX›Z\ÜÚ[Û”™\ÜÛœÙOŠØ\KÚ[œÚ[XYÜËÜÛÛXÚ]Y\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KÜÛÛXÚ]YZ[šXÚXXÚ[Û˜^[ØY
BˆB‚ˆ\Ş[˜ÈÙ]™X\İ\UÛÜšÜÚÜ™Yİ[\š]JÜ™Ø[š^˜][Û’Yˆİš[™Ë\ÓÙÎˆİš[™ÊNˆ›ÛZ\ÙOÛÜšÜÚÜ™Yİ[\š]TÛ˜\Úİ[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆ[ØÚÔÛ˜\Úİ\ÓÙŠ\Ë›[ØÚÕ™X\İ\K™Ù]
Ü™Ø[š^˜][Û’Y
K\ÓÙŠBˆÛÛœİ]Y\HH™]ÈT“ÙX\˜Ú\˜[\Ê
NÈYˆ
\ÓÙŠH]Y\KœÙ]
	Ø\ÓÙ‰Ë\ÓÙŠBˆ™]\›ˆ\Ë›Ü[Û˜[Ù]ÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİŠØ\Kİ\ÛÜ™\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™Yİ[\šYY	Ü]Y\KœÚ^™HÈÉÜ]Y\_Xˆ	ÉßX
BˆBˆ\Ş[˜ÈÙ]™X\İ\UÛÜšÜÚÜ™Yİ[\š]JÜ™Ø[š^˜][Û’Yˆİš[™Ë^[ØYˆÛÜšÜÚÜ™Yİ[\š]T™\]Y\İ
Nˆ›ÛZ\ÙOÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİÛ˜\ÚİH[ØÚÔ™Yİ[\š]TÛ˜\Úİ
Ü™Ø[š^˜][Û’Y^[ØY	İ™X\İ\IÊNÈ\Ë›[ØÚÕ™X\İ\KœÙ]
Ü™Ø[š^˜][Û’YÛ˜\Úİ
NÈ™]\›ˆÛ˜\ÚİBˆ™]\›ˆ\ËœÜİœÛÛÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİŠØ\Kİ\ÛÜ™\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™Yİ[\šYY^[ØY
BˆBˆ\Ş[˜È\İ™X\İ\Tİ][Y[ÊÜ™Ø[š^˜][Û’Yˆİš[™ËYX\Îˆ[X™\‹[ÛÎˆ[X™\ŠNˆ›ÛZ\ÙO™X\İ\Tİ][Y[\İ™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİ][\ÈHË‹‹\Ë›[ØÚÕ™X\İ\Tİ][Y[Ë˜[Y\Ê
WBˆ™š[\Š][HOˆ][K›Ü™Ø[š^˜][Û’YOOHÜ™Ø[š^˜][Û’Y	‰ˆ
YX\ˆOH[][Kœ\š[ÙYX\ˆOOHYX\ŠH	‰ˆ
[ÛOH[][Kœ\š[Ù[ÛOOH[Û
JBˆœÛÜ

KŠHOˆ‹œ\š[ÙYX\ˆHKœ\š[ÙYX\ˆ‹œ\š[Ù[ÛHKœ\š[Ù[Û
Bˆ›X\
][HOˆ
ÈYš][KšYÜ™Ø[š^˜][Û’Yš][K›Ü™Ø[š^˜][Û’Y\š[ÙYX\š][Kœ\š[ÙYX\‹\š[Ù[Ûš][Kœ\š[Ù[Ûİ]Ù™‘]Nš][K˜İ]Ù™‘]Kİ]\Îš][Kœİ]\ËÛİ\˜ÙT™Y™\™[˜ÙNš][KœÛİ\˜ÙT™Y™\™[˜ÙKİX›Z]Y]]Îš][KœİX›Z]Y]]Ë™XÛÛ˜Ú[Y]]Îš][Kœ™XÛÛ˜Ú[Y]]ËÛÜÙY]]Îš][K˜ÛÜÙY]]ÈJJBˆ™]\›ˆÈİ[š][\Ë›[™İ][\ÈBˆBˆÛÛœİ]Y\HH™]ÈT“ÙX\˜Ú\˜[\Ê
BˆYˆ
YX\ˆOH[
H]Y\KœÙ]
	ŞYX\‰Ëİš[™ÊYX\ŠJBˆYˆ
[ÛOH[
H]Y\KœÙ]
	Û[Û	Ëİš[™Ê[Û
JBˆ™]\›ˆ\Ëœ™\]Y\İ™X\İ\Tİ][Y[\İ™\ÜÛœÙOŠØ\Kİ\ÛÜ™\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KØİXY›ÜÉÜ]Y\KœÚ^™HÈÉÜ]Y\_Xˆ	ÉßX
BˆB‚ˆ\Ş[˜ÈÜ™X]U™X\İ\Tİ][Y[
Ü™Ø[š^˜][Û’Yˆİš[™Ë^[ØYˆÜ™X]U™X\İ\Tİ][Y[™\]Y\İ
Nˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİİ][Y[H[ØÚÕ™X\İ\Tİ][Y[
Ü™Ø[š^˜][Û’Y^[ØY
Bˆ\Ë›[ØÚÕ™X\İ\Tİ][Y[ËœÙ]
İ][Y[šYİ][Y[
Bˆ™]\›ˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[
BˆBˆ™]\›ˆ\ËœÜİœÛÛ™X\İ\Tİ][Y[ŠØ\Kİ\ÛÜ™\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KØİXY›ÜØ^[ØY
BˆBˆ\Ş[˜ÈÙ[™\˜]U™X\İ\Tİ][Y[[™\Êİ][Y[Yˆİš[™Ë^[ØYˆÙ[™\˜]U™X\İ\S[™\Ô™\]Y\İ
Nˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİİ][Y[H\Ëœ™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
İ][Y[Y
BˆÛÛœİ[[İ[ÈHÜ^[ØY›X\İ\[[İ[^[ØY›X\İ\[[İ[^[ØY›X\İ\[[İ[^[ØY™™[İØÜ˜Y[[İ[^[ØY™™[İØÜ˜Y[[İ[^[ØY˜\™[XÙP[[İ[^[ØY˜\™[XÙP[[İ[BˆÛÛœİYÜ™Y\ÈHÉÛX\İ\‰Ë	ÛX\İ\‰Ë	ÛX\İ\‰Ë	Ù™[İØÜ˜Y	Ë	Ù™[İØÜ˜Y	Ë	Ø\™[XÙIË	Ø\™[XÙI×BˆÛÛœİ˜[Y\ÈHÉÕ™[™\˜X›HXY\İ˜IË	Ôš[Y\ˆšYÚ[[IË	ÔÙYİ[™ÈšYÚ[[IË	ĞÛÛ\pìY\›È[›ÉË	ĞÛÛ\pìY\˜HÜÉË	Ğ\™[™^ˆ[›ÉË	Ğ\™[™^˜HÜÉ×BˆÛÛœİÛÛšX][Û•\\Î“ÙÙQ™YU\V×OVÉÛ›Ü›X[	Ë	Û›Ü›X[	Ë	ÜÙ[š[Ü‰Ë	ÜÜİ\ÙIË	ÜİY[	Ë	Ü\İØXİ]™IË	Û›Ü›X[	×Bˆİ][Y[›[™\ÈH[[İ[Ë›X\

[[İ[[™^
HOˆ
ÈYˆÜ\Ëœ˜[™ÛUURQ

KY[X™\’Yˆ[[Ë[Y[X™\‹IÚ[™^
È_XY[X™\œÚ\Yˆ[[Ë[Y[X™\œÚ\IÚ[™^
È_XYÜ™YPÛÙP]İ]Ù™ˆYÜ™Y\ÖÚ[™^KÙ™šXÙPÛÙP]İ]Ù™ˆ[™^ÈÈÉÕ“IË	Ô‰Ë	ÔÕ‰×VÚ[™^Hˆ[˜\ÙP[[İ[ˆ[[İ[Y\İY[[[İ[ˆ[™^OOHˆÈNˆ^XX›P[[İ[ˆ[™^OOHˆÈ[[İ[Hˆ[[İ[Y\İY[\NˆÛÛšX][Û•\\ÖÚ[™^KÛÛšX][Û•\N˜ÛÛšX][Û•\\ÖÚ[™^K]]Üš^˜][Û”™Y™\™[˜ÙNˆ[™^OOHˆÈ	Ô[˜ÚHSSËLŒËÌŒ‰Èˆ[ØœÙ\˜][Ûˆ˜[Y\ÖÚ[™^KY[]SX]Úİ]\Îˆ	ÛX]ÚY	ÈJJBˆ™XØ[İ[]S[ØÚÕ™X\İ\Jİ][Y[
NÈ™]\›ˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[
BˆBˆ™]\›ˆ\ËœÜİœÛÛ™X\İ\Tİ][Y[ŠØ\Kİ\ÛÜ™\šXKØİXY›ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İ][Y[Y
_KÙÙ[™\˜\‹[[™X\Ø^[ØY
BˆBˆ\Ş[˜ÈY™X\İ\Tİ][Y[^[Y[
İ][Y[Yˆİš[™Ë^[ØYˆY™X\İ\T^[Y[™\]Y\İ
Nˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİİ][Y[H\Ëœ™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
İ][Y[Y
BˆYˆ
İ][Y[œİ]\ÈOOH	Ù˜Y	ÊH›İÈ™]È\œ›ÜŠ	ÓÜÈYÛÜÈğìÛÈYY[ˆ™YÚ\İ˜\œÙHZY[˜\È[İXY›È\İ0èH[ˆ›Üœ˜YÜ‹‰ÊBˆYˆ
^[ØY˜[[İ[H\^[ØYœ^Y\‘\Ü^S˜[YOËš[J
H\^[ØYœ™Y™\™[˜ÙOËš[J
JH›İÈ™]È\œ›ÜŠ	ÔYØYÜˆH™Y™\™[˜ÚXKØÛÛ\›Ø˜[HÛÛˆØ›YØ]Üš[ÜÈ\˜H™YÚ\İ˜\ˆ[YÛË‰ÊBˆİ][Y[œ^[Y[Ëœ\Ú
ÈYˆÜ\Ëœ˜[™ÛUURQ

K^[Y[Y]Ùˆ^[ØYœ^[Y[Y]Ù^[Y[]Nˆ^[ØYœ^[Y[]K[[İ[ˆ^[ØY˜[[İ[^Y\‘\Ü^S˜[YNˆ^[ØYœ^Y\‘\Ü^S˜[YKš[J
K™Y™\™[˜ÙNˆ^[ØYœ™Y™\™[˜ÙKš[J
K™XÛÜ™Y]]Îˆ™]È]J
KÒTÓÔİš[™Ê
HJBˆ™XØ[İ[]S[ØÚÕ™X\İ\Jİ][Y[
NÈ™]\›ˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[
BˆBˆ]ØZ]\ËœÜİœÛÛ[šÛ›İÛŠØ\Kİ\ÛÜ™\šXKØİXY›ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İ][Y[Y
_KÜYÛÜØ^[ØY
Bˆ™]\›ˆ\Ë™Ù]™X\İ\Tİ][Y[
İ][Y[Y
BˆBˆ\Ş[˜ÈİX›Z]™X\İ\Tİ][Y[
İ][Y[Yˆİš[™ÊNˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİİ][Y[H\Ëœ™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
İ][Y[Y
BˆYˆ
İ][Y[›[™\Ë›[™İOOH
H›İÈ™]È\œ›ÜŠ	Ñ[İXY›ÈX™HÛÛ[™\ˆ0ë[™X\È[\ÈH[šX\œÙK‰ÊBˆYˆ
İ][Y[™Y™™\™[˜ÙP[[İ[OOHİ][Y[[œ™\ÛÛ™YY[]Y\ÈOOH
H›İÈ™]È\œ›ÜŠ	Ñ[İXY›È›ÈYYH[šX\œÙHZY[˜\È^\İHY™\™[˜ÚXHÈY[YY\ÈÚ[ˆÛÛ˜Ú[X\‹‰ÊBˆİ][Y[œİ]\ÈH	ÜİX›Z]Y	ÎÈİ][Y[œİX›Z]Y]]ÈH™]È]J
KÒTÓÔİš[™Ê
NÈ™]\›ˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[
BˆBˆ™]\›ˆ\Ëœ™\]Y\İ™X\İ\Tİ][Y[ŠØ\Kİ\ÛÜ™\šXKØİXY›ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İ][Y[Y
_KÙ[šX\˜ÈY]Ùˆ	ÔÔÕ	ÈJBˆBˆ\Ş[˜È™XÛÛ˜Ú[U™X\İ\Tİ][Y[
İ][Y[Yˆİš[™ÊNˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİİ][Y[H\Ëœ™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
İ][Y[Y
NÈYˆ
İ][Y[™Y™™\™[˜ÙP[[İ[OOH
H›İÈ™]È\œ›ÜŠ	Ñ[İXY›ÈX[Y[™H[˜HY™\™[˜ÚXH[™Y[K‰ÊNÈİ][Y[œİ]\ÈH	Ü™XÛÛ˜Ú[Y	ÎÈİ][Y[œ™XÛÛ˜Ú[Y]]ÈH™]È]J
KÒTÓÔİš[™Ê
NÈ™]\›ˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[
HBˆ™]\›ˆ\Ëœ™\]Y\İ™X\İ\Tİ][Y[ŠØ\Kİ\ÛÜ™\šXKØİXY›ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İ][Y[Y
_KØÛÛ˜Ú[X\˜ÈY]Ùˆ	ÔÔÕ	ÈJBˆBˆ\Ş[˜ÈÙ]™X\İ\Tİ][Y[
İ][Y[Yˆİš[™Ë[˜ÛYSY[X™\‘]Z[H˜[ÙJNˆ›ÛZ\ÙO™X\İ\Tİ][Y[ˆÈYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİ][OXÛÛ™U™X\İ\Tİ][Y[
\Ëœ™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
İ][Y[Y
JNÈYŠZ[˜ÛYSY[X™\‘]Z[
Z][K›[™\ÏZ][K›[™\Ë›X\
OŠË‹‹Y[X™\’Y›[Y[X™\œÚ\Y›[YÜ™YPÛÙP]İ]Ù™‰ÉËÙ™šXÙPÛÙP]İ]Ù™›[ØœÙ\˜][Û›[]]Üš^˜][Û”™Y™\™[˜ÙN›[Y\İY[\N›[JJNÈ™]\›ˆ][HH™]\›ˆ\Ëœ™\]Y\İ™X\İ\Tİ][Y[ŠØ\Kİ\ÛÜ™\šXKØİXY›ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İ][Y[Y
_OÚ[˜ÛYSY[X™\‘]Z[IÚ[˜ÛYSY[X™\‘]Z[X
HBˆ\Ş[˜ÈÙ]ÙÙRÜÜ][\šXTİ[[X\JÜ™Ø[š^˜][Û’Yœİš[™Ëœ›ÛOÎœİš[™ËÏÎœİš[™ÊN”›ÛZ\ÙOÙÙRÜÜ][\šXTİ[[X\OÂˆYŠ\Ë\ÙS[ØÚÜÊ^ÂˆÛÛœİ][\Ï]\Ë™[œİ\™S[ØÚÒÜÜ][\šXS[İ™[Y[ÊÜ™Ø[š^˜][Û’Y
K™š[\Š][OOŠYœ›Û_][K›[İ™[Y[]OYœ›ÛJI‰Š]ß][K›[İ™[Y[]O]ÊJBˆ™]\›ˆİ[[X\š^™S[ØÚÒÜÜ][\šXJÜ™Ø[š^˜][Û’Yœ›ÛOÏØİ\œ™[[Ûİ\

KÏÏØİ\œ™[[Û[™

K][\ÊBˆBˆÛÛœİ]Y\O[™]ÈT“ÙX\˜Ú\˜[\Ê
NÈYŠœ›ÛJ\]Y\KœÙ]
	Ùœ›ÛIËœ›ÛJNÈYŠÊ\]Y\KœÙ]
	İÉËÊBˆ™]\›ˆ\Ëœ™\]Y\İÙÙRÜÜ][\šXTİ[[X\OŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™\İ[Y[‰Ü]Y\KœÚ^™OØÉÜ]Y\_X‰ÉßX
BˆB‚ˆ\Ş[˜ÈÜ™X]SÙÙRÜÜ][\šXS[İ™[Y[
Ü™Ø[š^˜][Û’Yœİš[™Ë^[ØYÜ™X]SÙÙRÜÜ][\šXS[İ™[Y[™\]Y\İ
N”›ÛZ\ÙOÙÙRÜÜ][\šXS[İ™[Y[ÂˆYŠ\Ë\ÙS[ØÚÜÊ^ÂˆYŠ^[ØY˜[[İ[L
]›İÈ™]È\œ›ÜŠ	Ñ[[ÛÈX™HÙ\ˆX^[Üˆ]YHÙ\›Ë‰ÊBˆYŠ^[ØY›[İ™[Y[\OOOIÙ^[œÙIÉ‰ˆ\^[ØY™]šY[˜ÙT™Y™\™[˜ÙOËš[J
J]›İÈ™]È\œ›ÜŠ	ÕÙÈYÜ™\ÛÈHÜÜ][\šXHX™HÛÛœÙ\˜\ˆ[˜H™Y™\™[˜ÚXHH™\Ü[Ë‰ÊBˆÛÛœİ][N“ÙÙRÜÜ][\šXS[İ™[Y[^ÚY˜Ü\Ëœ˜[™ÛUURQ

KÜ™Ø[š^˜][Û’Y‹‹œ^[ØYY[X™\”™Y™\™[˜ÙNœ^[ØY›Y[X™\”™Y™\™[˜ÙOËš[J
_[\İ[˜][Ûœ^[ØY™\İ[˜][ÛËš[J
_[]šY[˜ÙT™Y™\™[˜ÙNœ^[ØY™]šY[˜ÙT™Y™\™[˜ÙOËš[J
_[ØœÙ\˜][Ûœ^[ØY›ØœÙ\˜][ÛËš[J
_[\›İ˜[İ]\Îœ^[ØY›[İ™[Y[\OOOIÙ^[œÙIÏÉÜ[™[™×Ø\›İ˜[	Î‰Û›İÜ™\]Z\™Y	Ë\›İ˜[Ûİ\˜ÙN›[Ûİ[˜Ú[XÚ\Ú[Û’Y›[\›İ™YTİXš™Xİ›[\›İ™Y]]Î›[™XÛÜ™Y]]Î›™]È]J
KÒTÓÔİš[™Ê
_Bˆ\Ë™[œİ\™S[ØÚÒÜÜ][\šXS[İ™[Y[ÊÜ™Ø[š^˜][Û’Y
Kœ\Ú
][JNÈ™]\›ˆË‹‹š][_BˆBˆ™]\›ˆ\ËœÜİœÛÛÙÙRÜÜ][\šXS[İ™[Y[ŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÛ[İš[ZY[ÜØ^[ØY
BˆB‚ˆ\Ş[˜È\›İ™SÙÙRÜÜ][\šXQ^[œÙJ[İ™[Y[Yœİš[™ÊN”›ÛZ\ÙOÙÙRÜÜ][\šXS[İ™[Y[ÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][O]\Ëœ™\]Z\™S[ØÚÒÜÜ][\šXS[İ™[Y[
[İ™[Y[Y
NÚYŠ][K˜\›İ˜[İ]\ÈOOIÜ[™[™×Ø\›İ˜[	Ê]›İÈ™]È\œ›ÜŠ	Ñ[YÜ™\ÛÈ›È\İ0èH[™Y[K‰ÊNÚ][K˜\›İ˜[İ]\ÏIØ\›İ™Y	ÎÚ][K˜\›İ˜[Ûİ\˜ÙOIİ™[™\˜X›WÛX\İ\‰ÎÚ][K˜\›İ™YTİXš™XİIİ™[™\˜X›KY[[ÉÎÚ][K˜\›İ™Y]]Ï[™]È]J
KÒTÓÔİš[™Ê
NÜ™]\›ˆË‹‹š][__Bˆ™]\›ˆ\Ëœ™\]Y\İÙÙRÜÜ][\šXS[İ™[Y[ŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKÛ[İš[ZY[ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
[İ™[Y[Y
_KØ\›Ø˜\˜ÛY]Ù‰ÔÔÕ	ßJBˆB‚ˆ\Ş[˜È\›İ™SÙÙRÜÜ][\šXQ^[œÙPPÛİ[˜Ú[
[İ™[Y[Yœİš[™ËÛİ[˜Ú[XÚ\Ú[Û’Yœİš[™ÊN”›ÛZ\ÙOÙÙRÜÜ][\šXS[İ™[Y[ÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][O]\Ëœ™\]Z\™S[ØÚÒÜÜ][\šXS[İ™[Y[
[İ™[Y[Y
NØÛÛœİXÚ\Ú[Û[[ØÚÒÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[ÛœÊ][K›Ü™Ø[š^˜][Û’Y
K™š[™
OšYOOXÛİ[˜Ú[XÚ\Ú[Û’Y
NÚYŠYXÚ\Ú[ÛŸXÚ\Ú[Û‹˜[[İ[OOZ][K˜[[İ[][K˜Ø]YÛÜHOOIØÚ\š]WØZY	Ê]›İÈ™]È\œ›ÜŠ	Ñ[XİY\™È[ÛÛœÙZ›È›ÈÛÜœ™\ÜÛ™H[ÛØÛÜœ›Ë‰ÊNÚ][K˜\›İ˜[İ]\ÏIØ\›İ™Y	ÎÚ][K˜\›İ˜[Ûİ\˜ÙOIÛÙÙWØÛİ[˜Ú[	ÎÚ][K˜Ûİ[˜Ú[XÚ\Ú[Û’YYXÚ\Ú[Û‹šYÚ][K˜\›İ™YTİXš™XİXÛİ[˜Ú[‰ÙXÚ\Ú[Û‹šYXÚ][K˜\›İ™Y]]Ï[™]È]J
KÒTÓÔİš[™Ê
NÜ™]\›ˆË‹‹š][__Bˆ™]\›ˆ\ËœÜİœÛÛÙÙRÜÜ][\šXS[İ™[Y[ŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKÛ[İš[ZY[ÜËÉÙ[˜ÛÙUT’PÛÛ\Û™[
[İ™[Y[Y
_KØ\›Ø˜\‹XÛÛœÙZ›ØØÛİ[˜Ú[XÚ\Ú[Û’YJBˆB‚ˆ\Ş[˜ÈÙ]ÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[ÛœÊÜ™Ø[š^˜][Û’Yœİš[™ÊN”›ÛZ\ÙOİİ[›[X™\Ú][\Î’ÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[Û–×_OÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][\Ï[[ØÚÒÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[ÛœÊÜ™Ø[š^˜][Û’Y
NÜ™]\›İİ[š][\Ë›[™İ][\ß_Bˆ™]\›ˆ\Ëœ™\]Y\İİİ[›[X™\Ú][\Î’ÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[Û–×_OŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KØXİY\™ÜË\ÛØÛÜœ›Ø
BˆB‚ˆ\Ş[˜ÈÙ]ÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]ÜÊÜ™Ø[š^˜][Û’Yœİš[™ÊN”›ÛZ\ÙOİİ[›[X™\Ú][\Î’ÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]Ö×_OÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][\Ï[[ØÚÒÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]ÜÊÜ™Ø[š^˜][Û’Y
NÜ™]\›İİ[š][\Ë›[™İ][\ß_Bˆ™]\›ˆ\Ëœ™\]Y\İİİ[›[X™\Ú][\Î’ÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]Ö×_OŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™]š\Ú[Û™\ËXÛÛœÙZ›Ø
BˆB‚ˆ\Ş[˜È\Ù\ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛŠÜ™Ø[š^˜][Û’Yœİš[™ËYX\›[X™\‹[Û›[X™\‹^[ØY•\Ù\ÜÜ][\šXS[ÛTİX›Z\ÜÚ[Û”™\]Y\İ
N”›ÛZ\ÙOÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛÂˆYŠ\Ë\ÙS[ØÚÜÊ^ÂˆÛÛœİÙ^OX	ÛÜ™Ø[š^˜][Û’YN‰ŞYX\ŸKIÔİš[™Ê[Û
KœYİ\
‹	Ì	Ê_XØÛÛœİ^\İ[™Ï]\Ë›[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛœË™Ù]
Ù^JBˆYŠ^\İ[™É‰–ÉÜİX›Z]Y	Ë	Ü™XÛÛ˜Ú[Y	×Kš[˜ÛY\Ê^\İ[™Ëœİ]\ÊJ]›İÈ™]È\œ›ÜŠ	ÓH™[™XÚpìÛˆ[šXYHÈÛÛ˜Ú[XYH›ÈYYH[ÙYšXØ\œÙK‰ÊBˆÛÛœİİ\X	ŞYX\ŸKIÔİš[™Ê[Û
KœYİ\
‹	Ì	Ê_KLXØÛÛœİ[™[[Û[™
YX\‹[Û
NØÛÛœİİ[[X\OX]ØZ]\Ë™Ù]ÙÙRÜÜ][\šXTİ[[X\JÜ™Ø[š^˜][Û’Yİ\[™
BˆYŠ^[ØYœ™\[š\ÚY[ZY[[İ[Œ	‰ˆ\^[ØYœ^[Y[™Y™\™[˜ÙOËš[J
J]›İÈ™]È\œ›ÜŠ	ÑX™H™YÚ\İ˜\ˆH™Y™\™[˜ÚXKØÛÛ\›Ø˜[HHH™\ÜÚXÚpìÛˆYØYK‰ÊBˆÛÛœİ][N’ÜÜ][\šXS[ÛTİX›Z\ÜÚ[Û^ÚY™^\İ[™ÏËšYÏØÜ\Ëœ˜[™ÛUURQ

KÜ™Ø[š^˜][Û’Y\š[ÙYX\YX\‹\š[Ù[Û›[Ûİ]Ù™‘]N™[™[˜ÛÛYP[[İ[œİ[[X\Kš[˜ÛÛYK\›İ™Y^[œÙP[[İ[œİ[[X\K˜\›İ™Y^[œÙ\Ë\š[Ù™][[İ[œİ[[X\Kœ\š[Ù™][İ™[Y[Ûİ[œİ[[X\K›[İ™[Y[Ë[™[™Ñ^[œÙPÛİ[œİ[[X\Kœ[™[™Ñ^[œÙ\Ë™\[š\ÚY[YP[[İ[œ^[ØYœ™\[š\ÚY[YP[[İ[™\[š\ÚY[ZY[[İ[œ^[ØYœ™\[š\ÚY[ZY[[İ[Y™™\™[˜ÙP[[İ[œ^[ØYœ™\[š\ÚY[YP[[İ[\^[ØYœ™\[š\ÚY[ZY[[İ[^[Y[™Y™\™[˜ÙNœ^[ØYœ^[Y[™Y™\™[˜ÙOËš[J
_[Ûİ[˜Ú[š[˜[˜ÚX[™]šY]ÒYœ^[ØY˜Ûİ[˜Ú[š[˜[˜ÚX[™]šY]ÒYÏÛ[İ]\Î‰Ù˜Y	ËÛİ\˜ÙT™Y™\™[˜ÙNœ^[ØYœÛİ\˜ÙT™Y™\™[˜ÙOËš[J
_[Ü™X]Y]]Î™^\İ[™ÏË˜Ü™X]Y]]ÏÏÛ™]È]J
KÒTÓÔİš[™Ê
KİX›Z]Y]]Î›[™]šY]ÙY]]Î›[™]šY]Ó›İ\Î›[Bˆ\Ë›[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛœËœÙ]
Ù^K][JNÜ™]\›ˆÛÛ™RÜÜ][\šXTİX›Z\ÜÚ[ÛŠ][JBˆBˆ™]\›ˆ\Ëœ™\]Y\İÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™[™XÚ[Û™\ËÉŞYX\ŸKÉÛ[ÛXÛY]Ù‰ÔU	ËXY\œÎÉĞÛÛ[U\IÎ‰Ø\XØ][Û‹ÚœÛÛ‰ßK›ÙN’”ÓÓ‹œİš[™ÚYJ^[ØY
_JBˆB‚ˆ\Ş[˜ÈÙ]ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛœÊÜ™Ø[š^˜][Û’Yœİš[™ËYX\Î›[X™\‹[ÛÎ›[X™\ŠN”›ÛZ\ÙOÜÜ][\šXTİX›Z\ÜÚ[Û“\İ™\ÜÛœÙOÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][\ÏVË‹‹\Ë›[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛœË˜[Y\Ê
WK™š[\ŠO›Ü™Ø[š^˜][Û’YOO[Ü™Ø[š^˜][Û’Y	‰ŠYX\O[[œ\š[ÙYX\OO^YX\ŠI‰Š[ÛO[[œ\š[Ù[ÛOO[[Û
JK›X\
ÛÛ™RÜÜ][\šXTİX›Z\ÜÚ[ÛŠNÜ™]\›İİ[š][\Ë›[™İ][\ß_BˆÛÛœİ]Y\O[™]ÈT“ÙX\˜Ú\˜[\Ê
NÚYŠYX\ˆO[[
\]Y\KœÙ]
	ŞYX\‰Ëİš[™ÊYX\ŠJNÚYŠ[ÛO[[
\]Y\KœÙ]
	Û[Û	Ëİš[™Ê[Û
JBˆ™]\›ˆ\Ëœ™\]Y\İÜÜ][\šXTİX›Z\ÜÚ[Û“\İ™\ÜÛœÙOŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™[™XÚ[Û™\ÉÜ]Y\KœÚ^™OØÉÜ]Y\_X‰ÉßX
BˆB‚ˆ\Ş[˜ÈİX›Z]ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛŠİX›Z\ÜÚ[Û’Yœİš[™ÊN”›ÛZ\ÙOÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][O]\Ëœ™\]Z\™S[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛŠİX›Z\ÜÚ[Û’Y
NÚYŠ][Kœ[™[™Ñ^[œÙPÛİ[OOL
]›İÈ™]È\œ›ÜŠ	Ó›ÈYYH[šX\œÙHH™[™XÚpìÛˆZY[˜\È^\İ[ˆYÜ™\ÛÜÈ[™Y[\ÈH]]Üš^˜XÚpìÛ‹‰ÊNÚYŠZ][K˜Ûİ[˜Ú[š[˜[˜ÚX[™]šY]ÒY
]›İÈ™]È\œ›ÜŠ	ÑX™Hš[˜İ[\ˆH™]š\ÚpìÛˆY[œİX[HÜÜ][\šXH[ÛÛœÙZ›ÈHYZ[š\İ˜XÚpìÛ‹‰ÊNÚ][Kœİ]\ÏIÜİX›Z]Y	ÎÚ][KœİX›Z]Y]]Ï[™]È]J
KÒTÓÔİš[™Ê
NÜ™]\›ˆÛÛ™RÜÜ][\šXTİX›Z\ÜÚ[ÛŠ][J_Bˆ™]\›ˆ\Ëœ™\]Y\İÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛŠØ\KÙÙ\İ[Û‹[ÙÚX[ÚÜÜ][\šXKÜ™[™XÚ[Û™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İX›Z\ÜÚ[Û’Y
_KÙ[šX\˜ÛY]Ù‰ÔÔÕ	ßJBˆB‚ˆ\Ş[˜ÈÙ]Ü˜[™ÜÜ][\šXTİX›Z\ÜÚ[ÛœÊš[\œÎÛÜ™Ø[š^˜][Û’YÎœİš[™ÎŞYX\Î›[X™\Û[ÛÎ›[X™\Üİ]\ÏÎœİš[™ßO^ßJN”›ÛZ\ÙOÜÜ][\šXTİX›Z\ÜÚ[Û“\İ™\ÜÛœÙOÜ˜[™ÜÜ][\šXTİX›Z\ÜÚ[ÛÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][\ÏVË‹‹\Ë›[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛœË˜[Y\Ê
WK™š[\ŠOœİ]\ÈOOIÙ˜Y	É‰ŠYš[\œË›Ü™Ø[š^˜][Û’Y›Ü™Ø[š^˜][Û’YOOYš[\œË›Ü™Ø[š^˜][Û’Y
I‰Šš[\œËYX\O[[œ\š[ÙYX\OOYš[\œËYX\ŠI‰Šš[\œË›[ÛO[[œ\š[Ù[ÛOOYš[\œË›[Û
I‰ŠYš[\œËœİ]\ßœİ]\ÏOOYš[\œËœİ]\ÊJK›X\
OØÛÛœİÜ™Ï]\Ë›[ØÚÓÜ™Ø[š^˜][ÛœË™š[™
ÏO›ËšYOO^›Ü™Ø[š^˜][Û’Y
NÜ™]\›Ë‹‹˜ÛÛ™RÜÜ][\šXTİX›Z\ÜÚ[ÛŠ
KÜ™Ø[š^˜][Û“˜[YN›Ü™ÏË›˜[YOÏÉÕ[\‰ËÜ™Ø[š^˜][Û“[X™\›Ü™ÏË›[X™\ÏÛ[_JNÜ™]\›İİ[š][\Ë›[™İ][\ß_BˆÛÛœİ]Y\O[™]ÈT“ÙX\˜Ú\˜[\Ê
NÚYŠš[\œË›Ü™Ø[š^˜][Û’Y
\]Y\KœÙ]
	ÛÜ™Ø[š^˜][Û’Y	Ëš[\œË›Ü™Ø[š^˜][Û’Y
NÚYŠš[\œËYX\ˆO[[
\]Y\KœÙ]
	ŞYX\‰Ëİš[™Êš[\œËYX\ŠJNÚYŠš[\œË›[ÛO[[
\]Y\KœÙ]
	Û[Û	Ëİš[™Êš[\œË›[Û
JNÚYŠš[\œËœİ]\Ê\]Y\KœÙ]
	Üİ]\ÉËš[\œËœİ]\ÊBˆ™]\›ˆ\Ëœ™\]Y\İÜÜ][\šXTİX›Z\ÜÚ[Û“\İ™\ÜÛœÙOÜ˜[™ÜÜ][\šXTİX›Z\ÜÚ[ÛŠØ\KÚÜÜ][\šXKÜ™[™XÚ[Û™\ÉÜ]Y\KœÚ^™OØÉÜ]Y\_X‰ÉßX
BˆB‚ˆ\Ş[˜È™]šY]ÑÜ˜[™ÜÜ][\šXTİX›Z\ÜÚ[ÛŠİX›Z\ÜÚ[Û’Yœİš[™ËXÚ\Ú[Û‰ÛØœÙ\™Y	ß	Ü™XÛÛ˜Ú[Y	Ë›İ\ÏÎœİš[™ß[
N”›ÛZ\ÙOÚYœİš[™ÎÛÜ™Ø[š^˜][Û’Yœİš[™ÎÜİ]\Îœİš[™ÎÜ™]šY]ÙY]]Îœİš[™ÎÜ™]šY]Ó›İ\Îœİš[™ß[OÂˆYŠ\Ë\ÙS[ØÚÜÊ^ØÛÛœİ][O]\Ëœ™\]Z\™S[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛŠİX›Z\ÜÚ[Û’Y
NÚYŠ][Kœİ]\ÈOOIÜİX›Z]Y	Ê]›İÈ™]È\œ›ÜŠ	ÔğìÛÈ[˜H™[™XÚpìÛˆ[šXYHYYHÙ\ˆ™]š\ØYK‰ÊNÚYŠXÚ\Ú[ÛOOIÜ™XÛÛ˜Ú[Y	É‰š][K™Y™™\™[˜ÙP[[İ[Œ
]›İÈ™]È\œ›ÜŠ	Ó›ÈYYHÛÛ˜Ú[X\œÙH[˜H™[™XÚpìÛˆÛÛˆ™\ÜÚXÚ[Û™\È[™Y[\Ë‰ÊNÚYŠXÚ\Ú[ÛOOIÛØœÙ\™Y	É‰ˆ[›İ\ÏËš[J
J]›İÈ™]È\œ›ÜŠ	Õ[˜HØœÙ\˜XÚpìÛˆX™H[™XØ\ˆ[İ]›Ë‰ÊNÚ][Kœİ]\ÏYXÚ\Ú[ÛÚ][Kœ™]šY]ÙY]]Ï[™]È]J
KÒTÓÔİš[™Ê
NÚ][Kœ™]šY]Ó›İ\Ï[›İ\ÏËš[J
_[ÚYŠXÚ\Ú[ÛOOIÜ™XÛÛ˜Ú[Y	Ê]\Ë›[ØÚÒÜÜ][\šXKœÙ]
][K›Ü™Ø[š^˜][Û’YÚY˜ÜÜ][\šXKIØÜ\Ëœ˜[™ÛUURQ

_XÜ™Ø[š^˜][Û’Yš][K›Ü™Ø[š^˜][Û’Yİ]\Î‰İ\İ×Ù]IË\ÓÙ‘]Nš][K˜İ]Ù™‘]KÛİ\˜ÙT™Y™\™[˜ÙN˜ÜÜ][\šXK\™[™XÚ[Û‰Ú][KšYX›İ\Î‰Ô™Yİ[\šYY\š]˜YHH™[™XÚpìÛˆÛÛ˜Ú[XYK‰Ë™XÛÜ™Y]]Î›™]È]J
KÒTÓÔİš[™Ê
_JNÜ™]\›ÚYš][KšYÜ™Ø[š^˜][Û’Yš][K›Ü™Ø[š^˜][Û’Yİ]\Îš][Kœİ]\Ë™]šY]ÙY]]Îš][Kœ™]šY]ÙY]]Ë™]šY]Ó›İ\Îš][Kœ™]šY]Ó›İ\ß_Bˆ™]\›ˆ\ËœÜİœÛÛŠØ\KÚÜÜ][\šXKÜ™[™XÚ[Û™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
İX›Z\ÜÚ[Û’Y
_KÜ™]š\Ú[Û˜ÙXÚ\Ú[Û‹›İ\Î››İ\ÏÏÛ[JBˆB‚ˆ\Ş[˜ÈÙ]ÜÜ][\šXUÛÜšÜÚÜ™Yİ[\š]JÜ™Ø[š^˜][Û’Yˆİš[™Ë\ÓÙÎˆİš[™ÊNˆ›ÛZ\ÙOÛÜšÜÚÜ™Yİ[\š]TÛ˜\Úİ[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆ[ØÚÔÛ˜\Úİ\ÓÙŠ\Ë›[ØÚÒÜÜ][\šXK™Ù]
Ü™Ø[š^˜][Û’Y
K\ÓÙŠBˆÛÛœİ]Y\HH™]ÈT“ÙX\˜Ú\˜[\Ê
NÈYˆ
\ÓÙŠH]Y\KœÙ]
	Ø\ÓÙ‰Ë\ÓÙŠBˆ™]\›ˆ\Ë›Ü[Û˜[Ù]ÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİŠØ\KÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™Yİ[\šYY	Ü]Y\KœÚ^™HÈÉÜ]Y\_Xˆ	ÉßX
BˆBˆ\Ş[˜ÈÙ]ÜÜ][\šXUÛÜšÜÚÜ™Yİ[\š]JÜ™Ø[š^˜][Û’Yˆİš[™Ë^[ØYˆÛÜšÜÚÜ™Yİ[\š]T™\]Y\İ
Nˆ›ÛZ\ÙOÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİÛ˜\ÚİH[ØÚÔ™Yİ[\š]TÛ˜\Úİ
Ü™Ø[š^˜][Û’Y^[ØY	ÚÜÜ][\šXIÊNÈ\Ë›[ØÚÒÜÜ][\šXKœÙ]
Ü™Ø[š^˜][Û’YÛ˜\Úİ
NÈ™]\›ˆÛ˜\ÚİBˆ™]\›ˆ\ËœÜİœÛÛÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİŠØ\KÚÜÜ][\šXKİ[\™\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ü™Ø[š^˜][Û’Y
_KÜ™Yİ[\šYY^[ØY
BˆB‚ˆ\Ş[˜ÈÙ]ÙXÜ™]\šX]]˜Z[Xš[]Jœ›ÛU]Îˆİš[™ËÕ]Îˆİš[™ÊNˆ›ÛZ\ÙOÜXÙP]˜Z[Xš[]T™\ÜÛœÙOˆÈYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİ][\ÈH\Ë›[ØÚÔÜXÙ\Ë›X\
ÜXÙHOˆ
È‹‹œÜXÙK\Ğ]˜Z[X›Nˆ]\Ë›[ØÚĞ\ŞTÜXÙ\Ëš\ÊÜXÙKšY
HJJNÈ™]\›ˆÈœ›ÛU]ËÕ]Ëİ[ˆ][\Ë›[™İ]˜Z[X›Nˆ][\Ë™š[\ŠOˆš\Ğ]˜Z[X›JK›[™İ][\ÈHHÛÛœİ]Y\HH™]ÈT“ÙX\˜Ú\˜[\ÊÈœ›ÛU]ËÕ]ÈJNÈ™]\›ˆ\Ëœ™\]Y\İÜXÙP]˜Z[Xš[]T™\ÜÛœÙOŠØ\KÙÜ˜[‹\ÙXÜ™]\šXKÙ\ÜXÚ[ÜËÙ\ÜÛšXš[YYÉÜ]Y\_X
HBˆ\Ş[˜ÈÙ]ÙXÜ™]\šX]Øİ[Y[Ê
Nˆ›ÛZ\ÙOÙXÜ™]\šX]Øİ[Y[Ô™\ÜÛœÙOˆÈYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆÈİ[ˆ\Ë›[ØÚÑØİ[Y[Ë›[™İ][\ÎˆË‹‹\Ë›[ØÚÑØİ[Y[×HNÈ™]\›ˆ\Ëœ™\]Y\İÙXÜ™]\šX]Øİ[Y[Ô™\ÜÛœÙOŠ	ËØ\KÙÜ˜[‹\ÙXÜ™]\šXKÙØİ[Y[ÜÉÊHBˆ\Ş[˜ÈÙ]İX›Z]Y[šY\Ê
Nˆ›ÛZ\ÙOÜ˜[™ÙXÜ™]\šX][šY\Ô™\ÜÛœÙOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆÈİ[ˆ\Ë›[ØÚÔİX›Z]Y[šY\Ë›[™İ][\Îˆ\Ë›[ØÚÔİX›Z]Y[šY\Ë›X\
][HOˆ
È‹‹š][KÙÙNˆÈ‹‹š][K›ÙÙHHJJHBˆ™]\›ˆ\Ëœ™\]Y\İÜ˜[™ÙXÜ™]\šX][šY\Ô™\ÜÛœÙOŠ	ËØ\KÙÜ˜[‹\ÙXÜ™]\šXKİ[šY\ÉÊBˆBˆ\Ş[˜È™]šY]ÔİX›Z]Y[šYJ™XÛÜ™Yˆİš[™ËXÚ\Ú[Ûˆ	Ü™XÙZ]™Y	È	ÛØœÙ\™Y	Ë›İ\ÏÎˆİš[™È[
Nˆ›ÛZ\ÙOÈYœİš[™ÎÈİ]\Îœİš[™ÈOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİ][O]\Ë›[ØÚÔİX›Z]Y[šY\Ë™š[™
˜[YOO˜[YKœ™XÛÜ™YOO\™XÛÜ™Y
NÈYŠZ][JH›İÈ™]È\œ›ÜŠ	ÓH[šYH™[Z]YH›È^\İK‰ÊBˆ][KœİX›Z\ÜÚ[Û”İ]\ÏYXÚ\Ú[ÛÈ][Kœ™]šY]ÙY]]Ï[™]È]J
KÒTÓÔİš[™Ê
NÈ][Kœ™]šY]Ó›İ\Ï[›İ\ÏËš[J
_[ˆ™]\›ˆÈYˆ™XÛÜ™Yİ]\Îˆ][KœİX›Z\ÜÚ[Û”İ]\ÈBˆBˆ™]\›ˆ\ËœÜİœÛÛÈYœİš[™ÎÈİ]\Îœİš[™ÈOŠØ\KÙÜ˜[‹\ÙXÜ™]\šXKİ[šY\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
™XÛÜ™Y
_KÜ™]š\Ú[Û˜ÈXÚ\Ú[Û‹›İ\Îˆ›İ\ÈÏÈ[JBˆBˆ\Ş[˜ÈİÛ›ØYİX›Z]Y[šYQ^˜Xİ
™XÛÜ™Yˆİš[™ÊNˆ›ÛZ\ÙO›ØˆÂˆYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆ™]È›ØŠÉÑ^˜XİÈˆ[[Üİ˜]]›É×KÈ\Nˆ	Ø\XØ][Û‹Ü‰ÈJBˆÛÛœİXY\œÏ[™]ÈXY\œÊÈXØÙ\‰Ø\XØ][Û‹Ü‰ÈJNÈÛÛœİÚÙ[X]ØZ]\Ë™Ù]XØÙ\ÜÕÚÙ[ËŠ
NÈYŠ]ÚÙ[ŠH›İÈ™]È\œ›ÜŠ	ÑX™H[™Ü™\Ø\ˆ\˜H\ØØ\™Ø\ˆ[^˜XİË‰ÊNÈXY\œËœÙ]
	Ğ]]Üš^˜][Û‰Ë™X\™\ˆ	İÚÙ[ŸX
BˆÛÛœİ™\ÜÛœÙOX]ØZ]™]Ú
	İ\Ë˜˜\ÙU\›KØ\KÙÜ˜[‹\ÙXÜ™]\šXKİ[šY\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
™XÛÜ™Y
_KÙ^˜XİØÈÜ™Y[X[Î‰ÛÛZ]	Ë™Y\™Xİ‰Ù\œ›Ü‰ËØXÚN‰Û›Ë\İÜ™IËXY\œÈJBˆYŠ\™\ÜÛœÙK›ÚÊ^ÈYŠ™\ÜÛœÙKœİ]\ÏOOMJH]ØZ]\Ë›Û•[˜]]Üš^™YËŠ
NÈ›İÈ™]ÈYÛP\R\œ›ÜŠ™\ÜÛœÙKœİ]\Ë™\ÜÛœÙKœİ]\ÏOOMÏÉÔİHİY[H›ÈY[™H\›Z\ÛÈ\˜H\ØØ\™Ø\ˆ\İH^˜XİË‰Î˜HTH™\ÜÛ™pìÈ	Ü™\ÜÛœÙKœİ]\ßH	Ü™\ÜÛœÙKœİ]\Õ^K˜
HBˆ™]\›ˆ™\ÜÛœÙK˜›ØŠ
BˆBˆ\Ş[˜ÈÙ]ÙXÜ™]\šX]Ù\™[[ÛT]Y]YJ
Nˆ›ÛZ\ÙOÜ˜[™ÙXÜ™]\šX]Ù\™[[ÛT]Y]YT™\ÜÛœÙOˆÈYˆ
\Ë\ÙS[ØÚÜÊH™]\›ˆÈİ[ˆ\Ë›[ØÚĞÙ\™[[ÛšY\Ë›[™İ][\Îˆ\Ë›[ØÚĞÙ\™[[ÛšY\Ë›X\
][HOˆ
È‹‹š][HJJHNÈ™]\›ˆ\Ëœ™\]Y\İÜ˜[™ÙXÜ™]\šX]Ù\™[[ÛT]Y]YT™\ÜÛœÙOŠ	ËØ\KÚ[œİ]][Û˜[ÙÜ˜[‹\ÙXÜ™]\šXKØÙ\™[[ÛšX\ËX]]Üš^˜Y\ÉÊHBˆ\Ş[˜ÈÜ™X]TÙXÜ™]\šX]ÜXÙJ^[ØYˆÜ™X]TÜXÙT™\]Y\İ
Nˆ›ÛZ\ÙO[œİ]][Û˜[ÜXÙOˆÈYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİÜXÙNˆ[œİ]][Û˜[ÜXÙHHÈYˆÜ\Ëœ˜[™ÛUURQ

K‹‹œ^[ØYØØ][Ûˆ^[ØY›ØØ][ÛˆÏÈ[Ø\XÚ]Nˆ^[ØY˜Ø\XÚ]HÏÈ[İ]\Îˆ	ØXİ]™IÈNÈ\Ë›[ØÚÔÜXÙ\Ëœ\Ú
ÜXÙJNÈ™]\›ˆÜXÙHH™]\›ˆ\ËœÜİœÛÛ[œİ]][Û˜[ÜXÙOŠ	ËØ\KÙÜ˜[‹\ÙXÜ™]\šXKÙ\ÜXÚ[ÜÉË^[ØY
HBˆ\Ş[˜ÈÜ™X]TÙXÜ™]\šX]™\Ù\˜][ÛŠ^[ØYˆÜ™X]T™\Ù\˜][Û”™\]Y\İ
Nˆ›ÛZ\ÙOÈYˆİš[™ÎÈİ]\Îˆİš[™ÈOˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆYˆ
\Ë›[ØÚĞ\ŞTÜXÙ\Ëš\Ê^[ØYœÜXÙRY
JH›İÈ™]È\œ›ÜŠ	Ñ[[\ÈÈØ[HXH\İ0èH™\Ù\˜YÈ[ˆ\ÙHÜ˜\š[Ë‰ÊBˆÛÛœİÙ\™[[ÛHH^[ØY˜Ù\™[[ÛT™\]Y\İYÈ\Ë›[ØÚĞÙ\™[[ÛšY\Ë™š[™
][HOˆ][KšYOOH^[ØY˜Ù\™[[ÛT™\]Y\İY
Hˆ[™Yš[™YˆYˆ
^[ØY˜Ù\™[[ÛT™\]Y\İY	‰ˆXÙ\™[[ÛJH›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXH[™XØYH›È^\İH[ˆH˜[™Z˜H]]Üš^˜YK‰ÊBˆYˆ
Ù\™[[ÛH	‰ˆÙ\™[[ÛK›Ü™Ø[š^˜][Û’YOOH^[ØY›Ü™Ø[š^˜][Û’Y
H›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXH›ÈÛÜœ™\ÜÛ™H[[\ˆ[™XØYË‰ÊBˆÛÛœİYHÜ\Ëœ˜[™ÛUURQ

NÈ\Ë›[ØÚĞ\ŞTÜXÙ\Ë˜Y
^[ØYœÜXÙRY
BˆYˆ
Ù\™[[ÛJHÈÛÛœİÜXÙHH\Ë›[ØÚÔÜXÙ\Ë™š[™
][HOˆ][KšYOOH^[ØYœÜXÙRY
NÈÙ\™[[ÛKœÜXÙT™\Ù\˜][Û’YHYÈÙ\™[[ÛKœÜXÙS˜[YHHÜXÙOË›˜[YHÏÈ	Ñ\ÜXÚ[È[œİ]XÚ[Û˜[	ÎÈÙ\™[[ÛKœ™\Ù\˜][Û”İ\Ğ]]ÈH^[ØYœİ\Ğ]]ÎÈÙ\™[[ÛKœ™\Ù\˜][Û‘[™Ğ]]ÈH^[ØY™[™Ğ]]ÈBˆ™]\›ˆÈYİ]\Îˆ	Ü™\Ù\™Y	ÈBˆBˆ™]\›ˆ\ËœÜİœÛÛÈYˆİš[™ÎÈİ]\Îˆİš[™ÈOŠ	ËØ\KÙÜ˜[‹\ÙXÜ™]\šXKÜ™\Ù\˜\ÉË^[ØY
BˆBˆ\Ş[˜È\ÜİYTÙXÜ™]\šX]Øİ[Y[
^[ØYˆ\ÜİYQØİ[Y[™\]Y\İ
Nˆ›ÛZ\ÙOÙXÜ™]\šX]Øİ[Y[ˆÈYˆ
\Ë\ÙS[ØÚÜÊHÈÛÛœİÚ[™\^[ØY™Øİ[Y[\OOOIÜ[˜ÚIÏÊ^[ØYœ[˜ÚRÚ[™ÏÉÙ›Ü›X[ØÛÛ[][šXØ][Û‰ÊN›[ÈÛÛœİØİ[Y[ˆÙXÜ™]\šX]Øİ[Y[HÈYˆÜ\Ëœ˜[™ÛUURQ

KØİ[Y[\Nˆ^[ØY™Øİ[Y[\K[˜ÚRÚ[™ˆÚ[™Øİ[Y[ÛÙNˆ	Ü^[ØY™Øİ[Y[\HOOH	ÙXÜ™YIÈÈ	ÑPÉÈˆ	ÔKPÓÓIßKQSSËIÔİš[™Ê\Ë›[ØÚÑØİ[Y[Ë›[™İ
ÈJKœYİ\
Ë	Ì	Ê_X]Nˆ^[ØY]KÛÛ[ˆ^[ØY˜ÛÛ[Ü™Ø[š^˜][Û’Yˆ^[ØY›Ü™Ø[š^˜][Û’YÏÈ[™[]YÙ\™[[ÛT™\]Y\İYˆ[ÜXÙT™\Ù\˜][Û’Yˆ[İ]\Îˆ	Ú\ÜİYY	Ë\ÜİYY]]Îˆ™]È]J
KÒTÓÔİš[™Ê
K\ÜİYYTİXš™Xİˆ	Ù[[ÉÈNÈ\Ë›[ØÚÑØİ[Y[Ë[œÚY
Øİ[Y[
NÈ™]\›ˆØİ[Y[H™]\›ˆ\ËœÜİœÛÛÙXÜ™]\šX]Øİ[Y[Š	ËØ\KÙÜ˜[‹\ÙXÜ™]\šXKÙØİ[Y[ÜÉË^[ØY
HBˆ\Ş[˜È\ÜİYTÙXÜ™]\šX]Ù\™[[ÛP]]Üš^˜][ÛŠÙ\™[[ÛT™\]Y\İYˆİš[™ËÜXÙT™\Ù\˜][Û’Yˆİš[™È[H[
Nˆ›ÛZ\ÙOÙXÜ™]\šX]Øİ[Y[ˆÂˆYˆ
\Ë\ÙS[ØÚÜÊHÂˆÛÛœİÙ\™[[ÛHH\Ë›[ØÚĞÙ\™[[ÛšY\Ë™š[™
][HOˆ][KšYOOHÙ\™[[ÛT™\]Y\İY
NÈYˆ
XÙ\™[[ÛJH›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXH[™XØYH›È^\İK‰ÊNÈYˆ
Ù\™[[ÛK™›Ü›X[]]Üš^˜][Û’\ÜİYY
H›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXHXHİY[HÛÛˆ]]Üš^˜XÚpìÛˆ›Ü›X[šYÙ[K‰ÊNÈYˆ
ÜXÙT™\Ù\˜][Û’Y	‰ˆÙ\™[[ÛKœÜXÙT™\Ù\˜][Û’YOOHÜXÙT™\Ù\˜][Û’Y
H›İÈ™]È\œ›ÜŠ	ÓH™\Ù\˜H[™XØYH›ÈÛÜœ™\ÜÛ™HH\İHÙ\™[[ÛšXK‰ÊNÈÙ\™[[ÛK™›Ü›X[]]Üš^˜][Û’\ÜİYYHYBˆÛÛœİØİ[Y[ˆÙXÜ™]\šX]Øİ[Y[HÈYˆÜ\Ëœ˜[™ÛUURQ

KØİ[Y[\Nˆ	Ü[˜ÚIË[˜ÚRÚ[™ˆ	ØÙ\™[[ÛWØ]]Üš^˜][Û‰ËØİ[Y[ÛÙNˆKPUUPÑT‹QSSËIÔİš[™Ê\Ë›[ØÚÑØİ[Y[Ë›[™İ
ÈJKœYİ\
Ë	Ì	Ê_X]Nˆ[˜ÚHH]]Üš^˜XÚpìÛˆHÙ\™[[ÛšXH8 %	ØÙ\™[[ÛU\SX™[
Ù\™[[ÛK˜Ù\™[[ÛU\J_XÛÛ[ˆ[˜ÚH›Ü›X[H]]Üš^˜XÚpìÛˆ[[Üİ˜]]˜H\˜H	ØÙ\™[[ÛK›Ü™Ø[š^˜][Û“˜[Y_Kˆ›ÈÛÛœİ]^YHXÜ™]Ë˜Ü™Ø[š^˜][Û’YˆÙ\™[[ÛK›Ü™Ø[š^˜][Û’Y™[]YÙ\™[[ÛT™\]Y\İYˆÙ\™[[ÛKšYÜXÙT™\Ù\˜][Û’Yİ]\Îˆ	Ú\ÜİYY	Ë\ÜİYY]]Îˆ™]È]J
KÒTÓÔİš[™Ê
K\ÜİYYTİXš™Xİˆ	Ù[[ÉÈNÈ\Ë›[ØÚÑØİ[Y[Ë[œÚY
Øİ[Y[
NÈ™]\›ˆØİ[Y[ˆBˆ™]\›ˆ\ËœÜİœÛÛÙXÜ™]\šX]Øİ[Y[ŠØ\KÙÜ˜[‹\ÙXÜ™]\šXKØÙ\™[[ÛšX\ËÉÙ[˜ÛÙUT’PÛÛ\Û™[
Ù\™[[ÛT™\]Y\İY
_KØ]]Üš^˜XÚ[Û˜ÈÜXÙT™\Ù\˜][Û’YJBˆB‚ˆš]˜]H™\]Z\™S[ØÚÔ™]šY]ĞÙ\™[[ÛJYˆİš[™ÊNˆÙ\™[[ÛT™]šY]Ô]Y]YR][HÈÛÛœİ][HH\Ë›[ØÚÔ™]šY]ĞÙ\™[[ÛšY\Ë™š[™
˜[YHOˆ˜[YKšYOOHY
NÈYˆ
Z][JH›İÈ™]È\œ›ÜŠ	ÓHÙ\™[[ÛšXH[™XØYH›È^\İH[ˆH˜[™Z˜K‰ÊNÈ™]\›ˆ][HBˆš]˜]H™\]Z\™S[ØÚÕ™X\İ\Tİ][Y[
Yˆİš[™ÊNˆ™X\İ\Tİ][Y[ÈÛÛœİ][HH\Ë›[ØÚÕ™X\İ\Tİ][Y[Ë™Ù]
Y
NÈYˆ
Z][JH›İÈ™]È\œ›ÜŠ	Ñ[İXY›ÈY[œİX[[™XØYÈ›È^\İK‰ÊNÈ™]\›ˆ][HBˆš]˜]H[œİ\™S[ØÚÒÜÜ][\šXS[İ™[Y[ÊÜ™Ø[š^˜][Û’Yœİš[™ÊN“ÙÙRÜÜ][\šXS[İ™[Y[×^Âˆ]][\Ï]\Ë›[ØÚÓÙÙRÜÜ][\šXS[İ™[Y[Ë™Ù]
Ü™Ø[š^˜][Û’Y
BˆYŠZ][\Ê^Ú][\ÏYY˜][ÜÜ][\šXS[İ™[Y[ÊÜ™Ø[š^˜][Û’Y
Nİ\Ë›[ØÚÓÙÙRÜÜ][\šXS[İ™[Y[ËœÙ]
Ü™Ø[š^˜][Û’Y][\Ê_Bˆ™]\›ˆ][\ÂˆBˆš]˜]H™\]Z\™S[ØÚÒÜÜ][\šXS[İ™[Y[
Yœİš[™ÊN“ÙÙRÜÜ][\šXS[İ™[Y[Ù›ÜŠÛÛœİ›İÜÈÙˆ\Ë›[ØÚÓÙÙRÜÜ][\šXS[İ™[Y[Ë˜[Y\Ê
J^ØÛÛœİ][O\›İÜË™š[™
OšYOOZY
NÚYŠ][J\™]\›ˆ][_]›İÈ™]È\œ›ÜŠ	Ñ[[İš[ZY[ÈHÜÜ][\šXH›È^\İK‰Ê_Bˆš]˜]H™\]Z\™S[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛŠYœİš[™ÊN’ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛØÛÛœİ][OVË‹‹\Ë›[ØÚÒÜÜ][\šXTİX›Z\ÜÚ[ÛœË˜[Y\Ê
WK™š[™
OšYOOZY
NÚYŠZ][J]›İÈ™]È\œ›ÜŠ	ÓH™[™XÚpìÛˆHÜÜ][\šXH›È^\İK‰ÊNÜ™]\›ˆ][_Bˆš]˜]HÜİœÛÛŠ]ˆİš[™Ë^[ØYˆ[šÛ›İÛŠNˆ›ÛZ\ÙOˆÈ™]\›ˆ\Ëœ™\]Y\İŠ]ÈY]Ùˆ	ÔÔÕ	ËXY\œÎˆÈ	ĞÛÛ[U\IÎˆ	Ø\XØ][Û‹ÚœÛÛ‰ÈK›ÙNˆ”ÓÓ‹œİš[™ÚYJ^[ØY
HJHBˆš]˜]H\Ş[˜ÈÜ[Û˜[Ù]Š]ˆİš[™ÊNˆ›ÛZ\ÙO[ˆÈHÈ™]\›ˆ]ØZ]\Ëœ™\]Y\İŠ]
HHØ]Ú
\œ›ÜŠHÈYˆ
\œ›Üˆ[œİ[˜Ù[ÙˆYÛP\R\œ›Üˆ	‰ˆ\œ›Ü‹œİ]\ÈOOH
H™]\›ˆ[È›İÈ\œ›ÜˆHBˆš]˜]H\Ş[˜È™\]Y\İŠ]ˆİš[™Ë[š]ˆ™\]Y\İ[š]HßJNˆ›ÛZ\ÙOˆÂˆÛÛœİXY\œÈH™]ÈXY\œÊ[š]šXY\œÊNÈXY\œËœÙ]
	ĞXØÙ\	Ë	Ø\XØ][Û‹ÚœÛÛ‰ÊBˆÛÛœİÚÙ[ˆH]ØZ]\Ë™Ù]XØÙ\ÜÕÚÙ[ËŠ
NÈYˆ
]ÚÙ[ŠH›İÈ™]È\œ›ÜŠ	ÑX™H[™Ü™\Ø\ˆ\˜HÛÛœİ[\ˆH[™›Ü›XXÚpìÛˆ[œİ]XÚ[Û˜[‰ÊNÈXY\œËœÙ]
	Ğ]]Üš^˜][Û‰Ë™X\™\ˆ	İÚÙ[ŸX
BˆÛÛœİ™\ÜÛœÙHH]ØZ]™]Ú
	İ\Ë˜˜\ÙU\›IÜ]XÈ‹‹š[š]Ü™Y[X[Îˆ	ÛÛZ]	Ë™Y\™Xİˆ	Ù\œ›Ü‰ËØXÚNˆ	Û›Ë\İÜ™IËXY\œÈJBˆYˆ
\™\ÜÛœÙK›ÚÊHÂˆYˆ
™\ÜÛœÙKœİ]\ÈOOHJH]ØZ]\Ë›Û•[˜]]Üš^™YËŠ
Bˆ]Y\ÜØYÙHH	ÉÂˆHÈÛÛœİ›ÙHH]ØZ]™\ÜÛœÙK˜ÛÛ™J
KšœÛÛŠ
H\ÈÈY\ÜØYÙOÎˆİš[™ÈNÈY\ÜØYÙHH\[Ùˆ›ÙK›Y\ÜØYÙHOOH	Üİš[™ÉÈÈ›ÙK›Y\ÜØYÙHˆ	ÉÈHØ]ÚÈÊˆ™\ÜY\İHÚ[ˆ”ÓÓˆ
‹ÈBˆYˆ
™\ÜÛœÙKœİ]\ÈOOHÊHY\ÜØYÙHH	ÔİHİY[H›ÈY[™H\›Z\ÛÈ\˜H™X[^˜\ˆ\İHÜ\˜XÚpìÛ‹‰Âˆ›İÈ™]ÈYÛP\R\œ›ÜŠ™\ÜÛœÙKœİ]\ËY\ÜØYÙHHTH™\ÜÛ™pìÈ	Ü™\ÜÛœÙKœİ]\ßH	Ü™\ÜÛœÙKœİ]\Õ^K˜
BˆBˆ™]\›ˆ™\ÜÛœÙKšœÛÛŠ
H\È›ÛZ\ÙO‚ˆBŸB‚™^Ü[˜İ[ÛˆÜ™X]QY˜][YÛP\PÛY[
Ù]XØÙ\ÜÕÚÙ[ÎˆXØÙ\ÜÕÚÙ[”›İšY\‹Û•[˜]]Üš^™YÎˆ

HOˆ›ÛZ\ÙO›ÚYŠNˆYÛP\PÛY[ÈÛÛœİ˜\ÙU\›H[\Ü›Y]K™[‹•’UWĞTWĞTÑWÕT“ÏÈ	ÉÎÈÛÛœİ\ÙS[ØÚÜÈH[\Ü›Y]K™[‹•’UWÕTÑWÓSĞÒÔÈOOH	İYIÎÈÛÛœİ\›H™]ÈT“
˜\ÙU\›	ËÉËÚ[™İË›ØØ][Û‹›ÜšYÚ[ŠNÈYˆ
\››ÜšYÚ[ˆOOHÚ[™İË›ØØ][Û‹›ÜšYÚ[ˆ\›\Ù\›˜[YH\›œ\ÜİÛÜ™\›œÙX\˜Ú\›š\Ú
H›İÈ™]È\œ›ÜŠ	ÓHTHX™H\Ø\ˆ[Z\Û[ÈÜšYÙ[ˆYYX[H[›ŞH[œİ]XÚ[Û˜[‰ÊNÈ™]\›ˆ™]ÈYÛP\PÛY[
È˜\ÙU\›\ÙS[ØÚÜËÙ]XØÙ\ÜÕÚÙ[‹Û•[˜]]Üš^™YJHB™[˜İ[ÛˆÛY\
Z[\ÙXÛÛ™Îˆ[X™\ŠNˆ›ÛZ\ÙO›ÚYˆÈ™]\›ˆ™]È›ÛZ\ÙJ™\ÛÛ™HOˆÚ[™İËœÙ][Y[İ]
™\ÛÛ™KZ[\ÙXÛÛ™ÊJHB™[˜İ[Ûˆ[ØÚÒ[š]X[[X™\˜][ÛŠÙ\™[[ÛT™\]Y\İYˆİš[™Ë^[ØYˆ[š]X[[X™\˜][Û”™\]Y\İ
Nˆ[š]X[[X™\˜][Û”™\ÜÛœÙHÂˆYˆ
\^[ØYœÛİ\˜ÙT™Y™\™[˜ÙKš[J
JH›İÈ™]È\œ›ÜŠ	ÑX™H[™XØ\ˆH™Y™\™[˜ÚXH[XİHÈ^˜XİÈ]YH™\Ü[HH[X™\˜XÚpìÛ‹‰ÊBˆYˆ
^[ØYœ™\Ù[›İ\œÈH
H™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	Ú[š]X[Ù[X™\˜][Û‹œ][Ü[IË™X\ÛÛˆ	ÑX™H^\İ\ˆ[Y[›ÜÈ[˜H\œÛÛ˜HXš[]YH™\Ù[H\˜H™YÚ\İ˜\ˆH›İXÚpìÛ‹‰ËÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆYˆ
^[ØY›İ\Ò[‘˜]›Üˆ^[ØY›İ\Ò[‘˜]›Üˆˆ^[ØYœ™\Ù[›İ\œÊH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	Ú[š]X[Ù[X™\˜][Û‹›İ\ÉË™X\ÛÛˆ	ÓHØ[YYH›İÜÈ˜]›Ü˜X›\È›È\È°è[YK‰ËÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆÛÛœİ[\ÙY^\ÈH]SÛ›Q^S[X™\Š^[ØY™[X™\˜][Û‘]JHH]SÛ›Q^S[X™\Š	ÌŒ‹LKLL‰ÊBˆÛÛœİZ[š[][UØZ][™Ñ^\ÈH^[ØY›Z[š[][UØZ][™Ñ^\ÈÏÈÂˆYˆ
[\ÙY^\ÈZ[š[][UØZ][™Ñ^\ÊH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	ÛØœÙ\™Y	ËÛÙNˆ	Ú[š]X[Ù[X™\˜][Û‹ØZ][™×Ü\š[Ù	Ë™X\ÛÛˆX™[ˆ˜[œØİ\œš\ˆ[Y[›ÜÈ	ÛZ[š[][UØZ][™Ñ^\ßH0ëX\È\ÙHH™\Ù[XÚpìÛÈ[ˆ˜[œØİ\œšYÈ	Ù[\ÙY^\ßK˜Ù\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBˆYˆ
^[ØY›İ\Ò[‘˜]›ÜˆOOH^[ØYœ™\Ù[›İ\œÊH™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	Ü™Z™XİY	ËÛÙNˆ	Ú[š]X[Ù[X™\˜][Û‹[˜[š[Z]IË™X\ÛÛˆ	ÓH\›Ø˜XÚpìÛˆ[šXÚX[™\]ZY\™H[˜[š[ZYYH\È\œÛÛ˜\È™\Ù[\Ë‰ËÙ\™[[ÛTİ]\Îˆ	Ü™Z™XİY	ÈBˆ™]\›ˆÈYˆÙ\™[[ÛT™\]Y\İY˜[Y][Û”İ]\Îˆ	Ø\›İ™Y	ËÛÙNˆ	Ú[š]X[Ù[X™\˜][Û‹˜\›İ™Y	Ë™X\ÛÛˆ	ÔÙHİ[\H[^›Èpë[š[[ÈHH›İXÚpìÛˆ[šXÚX[YH[°è[š[YK‰ËÙ\™[[ÛTİ]\Îˆ	İ[™\—Ü™]šY]ÉÈBŸB™[˜İ[Ûˆ]SÛ›Q^S[X™\Š˜[YNˆİš[™ÊHÈÛÛœİŞYX\‹[Û^WHH˜[YKœÜ]
	ËIÊK›X\
[X™\ŠNÈYˆ
^YX\ˆ[[ÛY^JH›İÈ™]È\œ›ÜŠ	ÓH™XÚHH[X™\˜XÚpìÛˆ›È\È°è[YK‰ÊNÈ™]\›ˆX]™›ÛÜŠ]K•UÊYX\‹[ÛHK^JHÈ—ÍÌ
HB™[˜İ[ÛˆÙ\™[[ÛU\SX™[
\NˆÙ\™[[ÛU\JHÈ™]\›ˆ\HOOH	Ú[š]X][Û‰ÈÈ	Ò[šXÚXXÚpìÛ‰Èˆ\HOOH	İØYÙWÚ[˜Ü™X\ÙIÈÈ	Ğ][Y[ÈHØ[\š[ÉÈˆ	Ñ^[XÚpìÛ‰ÈB™[˜İ[ÛˆY˜][ÜÜ][\šXS[İ™[Y[ÊÜ™Ø[š^˜][Û’Yœİš[™ÊN“ÙÙRÜÜ][\šXS[İ™[Y[×^Âˆ™]\›ˆÂˆÚY˜ÜÜZ[˜ÛÛYKIÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’Y[İ™[Y[\N‰Ú[˜ÛÛYIËØ]YÛÜN‰ØÚ\š]WØ˜YÉË[[İ[ŒNL[İ™[Y[]N‰ÌŒ‹LKLIËY[X™\”™Y™\™[˜ÙN›[\İ[˜][Û›[]šY[˜ÙT™Y™\™[˜ÙN‰ÕS’QKQSSËLŒ‹LKLIËØœÙ\˜][Û‰Õ›Û˜ÛÈH™[™YšXÙ[˜ÚXH0­È]ÈšXİXÚ[ÉË\›İ˜[İ]\Î‰Û›İÜ™\]Z\™Y	Ë\›İ˜[Ûİ\˜ÙN›[Ûİ[˜Ú[XÚ\Ú[Û’Y›[\›İ™YTİXš™Xİ›[\›İ™Y]]Î›[™XÛÜ™Y]]Î‰ÌŒ‹LKLUŒÎŒŒ‰ßKˆÚY˜ÜÜXZYIÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’Y[İ™[Y[\N‰Ù^[œÙIËØ]YÛÜN‰ØÚ\š]WØZY	Ë[[İ[L[İ™[Y[]N‰ÌŒ‹LKLL	ËY[X™\”™Y™\™[˜ÙN‰Ô™Y™\™[˜ÚXH™\Ù\˜YHSSËLIË\İ[˜][Û‰ÔÛØÛÜœ›È™\Ù\˜YÈ0­È[[ÉË]šY[˜ÙT™Y™\™[˜ÙN‰ĞVUQKQSSËLIËØœÙ\˜][Û‰Ğ[XÙY[HÙ[œÚX›HšXİXÚ[ÎÈğìÛÈ[\‹‰Ë\›İ˜[İ]\Î‰Ü[™[™×Ø\›İ˜[	Ë\›İ˜[Ûİ\˜ÙN›[Ûİ[˜Ú[XÚ\Ú[Û’Y›[\›İ™YTİXš™Xİ›[\›İ™Y]]Î›[™XÛÜ™Y]]Î‰ÌŒ‹LKLLNŒŒ‰ßBˆBŸB™[˜İ[Ûˆ[ØÚÒÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[ÛœÊÜ™Ø[š^˜][Û’Yœİš[™ÊN’ÜÜ][\šXPÛİ[˜Ú[ZYXÚ\Ú[Û–×^Ü™]\›–ŞÚY˜Ûİ[˜Ú[XZYIÛÜ™Ø[š^˜][Û’YXÙ\ÜÚ[Û’Y˜Ûİ[˜Ú[\Ù\ÜÚ[Û‹IÛÜ™Ø[š^˜][Û’YXÙ\ÜÚ[Û‘]N‰ÌŒ‹LKLLIËİXš™Xİ‰ÔÛØÛÜœ›È™\Ù\˜YÈ0­È™Y™\™[˜ÚXH[[ÉË[[İ[LW_B™[˜İ[Ûˆ[ØÚÒÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]ÜÊÜ™Ø[š^˜][Û’Yœİš[™ÊN’ÜÜ][\šXPÛİ[˜Ú[š[˜[˜ÚX[™]šY]Ö×^Ü™]\›–ŞÚY˜Ûİ[˜Ú[\™]šY]ËIÛÜ™Ø[š^˜][Û’YXÙ\ÜÚ[Û’Y˜Ûİ[˜Ú[\Ù\ÜÚ[Û‹\™]šY]ËIÛÜ™Ø[š^˜][Û’YXÙ\ÜÚ[Û‘]N‰ÌŒ‹LKLMIË\š[ÙX™[‰ÌŒ‹LIËÛÛ˜Û\Ú[Û‰Ñ\İYÈY[œİX[HÜÜ][\šXH™]š\ØYÈÜˆÛÛœÙZ›È0­È[[ÉßW_B™[˜İ[Ûˆİ[[X\š^™S[ØÚÒÜÜ][\šXJÜ™Ø[š^˜][Û’Yœİš[™Ëœ›ÛNœİš[™ËÎœİš[™Ë][\Î“ÙÙRÜÜ][\šXS[İ™[Y[×JN“ÙÙRÜÜ][\šXTİ[[X\^ØÛÛœİ[˜ÛÛYOZ][\Ë™š[\ŠO›[İ™[Y[\OOOIÚ[˜ÛÛYIÊKœ™YXÙJ
K
OO˜JŞ˜[[İ[
NØÛÛœİ\›İ™Y^[œÙ\ÏZ][\Ë™š[\ŠO›[İ™[Y[\OOOIÙ^[œÙIÉ‰˜\›İ˜[İ]\ÏOOIØ\›İ™Y	ÊKœ™YXÙJ
K
OO˜JŞ˜[[İ[
NØÛÛœİ[™[™Ñ^[œÙ\ÏZ][\Ë™š[\ŠO›[İ™[Y[\OOOIÙ^[œÙIÉ‰˜\›İ˜[İ]\ÏOOIÜ[™[™×Ø\›İ˜[	ÊK›[™İØÛÛœİØ]YÛÜšY\ÏVË‹‹›™]ÈÙ]
][\Ë›X\
O˜Ø]YÛÜJJWK›X\
Ø]YÛÜOOŠØØ]YÛÜKİ[š][\Ë™š[\ŠO˜Ø]YÛÜOOOXØ]YÛÜJKœ™YXÙJ
K
OO˜JŞ˜[[İ[
KÛİ[š][\Ë™š[\ŠO˜Ø]YÛÜOOOXØ]YÛÜJK›[™İJJNÜ™]\›ÛÜ™Ø[š^˜][Û’Yœ›ÛKË[˜ÛÛYK\›İ™Y^[œÙ\Ë\š[Ù™]š[˜ÛÛYKX\›İ™Y^[œÙ\Ë[™[™Ñ^[œÙ\Ë[İ™[Y[Îš][\Ë›[™İØ]YÛÜšY\Ë][\Îš][\Ë›X\
OŠË‹‹JJ__B™[˜İ[Ûˆİ\œ™[[Ûİ\

^ØÛÛœİ[™]È]J
NÜ™]\›ˆ™]È[‘]U[YQ›Ü›X]
	Ù[‹PĞIËİ[YV›Û™N‰Ğ[Y\šXØKÔØ[XYÛÉËYX\‰Û[Y\šXÉË[Û‰Ì‹YYÚ]	ßJK™›Ü›X]

JÉËLIßB™[˜İ[Ûˆİ\œ™[[Û[™

^ØÛÛœİXİ\œ™[[Ûİ\

KœÛXÙJÊKœÜ]
	ËIÊK›X\
[X™\ŠNÜ™]\›ˆ[Û[™
ÌKÌWJ_B™[˜İ[Ûˆ[Û[™
YX\›[X™\‹[Û›[X™\Š^Ü™]\›ˆ	ŞYX\ŸKIÔİš[™Ê[Û
KœYİ\
‹	Ì	Ê_KIÔİš[™Ê™]È]J]K•UÊYX\‹[Û
JK™Ù]UÑ]J
JKœYİ\
‹	Ì	Ê_XB™[˜İ[ÛˆÛÛ™RÜÜ][\šXTİX›Z\ÜÚ[ÛŠ][N’ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛŠN’ÜÜ][\šXS[ÛTİX›Z\ÜÚ[ÛÜ™]\›Ë‹‹š][__B™[˜İ[Ûˆ[ØÚÔÛ˜\Úİ\ÓÙŠÛ˜\ÚİˆÛÜšÜÚÜ™Yİ[\š]TÛ˜\Úİ[™Yš[™Y\ÓÙÎˆİš[™ÊNˆÛÜšÜÚÜ™Yİ[\š]TÛ˜\Úİ[ÈYˆ
\Û˜\Úİ
H™]\›ˆ[ÈYˆ
\ÓÙˆ	‰ˆÛ˜\Úİ˜\ÓÙ‘]Hˆ\ÓÙŠH™]\›ˆ[È™]\›ˆÈ‹‹œÛ˜\ÚİHB™[˜İ[ÛˆY˜][ÙÙQ™YT[œÊÜ™Ø[š^˜][Û’Yˆİš[™ÊNˆÙÙQ™YT[–×HÈ™]\›ˆÂˆÈYˆ™YK[›Ü›X[IÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’Y™YU\Nˆ	Û›Ü›X[	ËY[X™\[[İ[ˆŒÜ˜[™™X\İ\P[[İ[ˆŒLÛÜšÜÚÜ[[İ[ˆLY™™Xİ]™Qœ›ÛNˆ	ÌŒ‹LKLIËY™™Xİ]™U[[ˆ[\ĞXİ]™NˆYHKˆÈYˆ™YK\İY[IÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’Y™YU\Nˆ	ÜİY[	ËY[X™\[[İ[ˆLÌÜ˜[™™X\İ\P[[İ[ˆLLÛÜšÜÚÜ[[İ[ˆŒY™™Xİ]™Qœ›ÛNˆ	ÌŒ‹LKLIËY™™Xİ]™U[[ˆ[\ĞXİ]™NˆYHKˆÈYˆ™YK\Ù[š[Ü‹IÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’Y™YU\Nˆ	ÜÙ[š[Ü‰ËY[X™\[[İ[ˆMŒÜ˜[™™X\İ\P[[İ[ˆLÌÛÜšÜÚÜ[[İ[ˆÌY™™Xİ]™Qœ›ÛNˆ	ÌŒ‹LKLIËY™™Xİ]™U[[ˆ[\ĞXİ]™NˆYHK—HB™[˜İ[Ûˆ[ØÚÕ™X\İ\PÚ\™Ù\ÊÜ™Ø[š^˜][Û’Yœİš[™ÊN“ÙÙU™X\İ\PÚ\™ÙV×^Ü™]\›–ÂˆÚY˜Ú\™ÙKLKIÛÜ™Ø[š^˜][Û’YXY[X™\’Y‰ÛY[X™\‹Y[[ËLIËY[X™\‘\Ü^S˜[YN‰Ğ[™™XH[[Üİ˜]]˜IËY[X™\[[İ[ŒŒZY[[İ[ŒŒ˜[[˜ÙNŒİ]\Î‰ÜZY	Ë^[Y[Î–ŞÚY‰Ü^KY[[ËLIË™XÙZ\[X™\‰Ô‘PËQSSËLIË[[İ[ŒŒ^[Y[Y]Ù‰İ˜[œÙ™\‰Ë^[Y[]N‰ÌŒ‹LKLIË™Y™\™[˜ÙN‰Õ–QSSËLIßW_KˆÚY˜Ú\™ÙKL‹IÛÜ™Ø[š^˜][Û’YXY[X™\’Y‰ÛY[X™\‹Y[[ËL‰ËY[X™\‘\Ü^S˜[YN‰Ğ™X]š^ˆ[[Üİ˜]]˜IËY[X™\[[İ[ŒŒZY[[İ[ŒLÌ˜[[˜ÙNŒLÌİ]\Î‰Ü\X[	Ë^[Y[Î–ŞÚY‰Ü^KY[[ËL‰Ë™XÙZ\[X™\‰Ô‘PËQSSËL‰Ë[[İ[ŒLÌ^[Y[Y]Ù‰ØØ\Ú	Ë^[Y[]N‰ÌŒ‹LKLIË™Y™\™[˜ÙN›[W_KˆÚY˜Ú\™ÙKLËIÛÜ™Ø[š^˜][Û’YXY[X™\’Y‰ÛY[X™\‹Y[[ËLÉËY[X™\‘\Ü^S˜[YN‰ĞØ\›Û[˜H[[Üİ˜]]˜IËY[X™\[[İ[ŒMŒZY[[İ[Œ˜[[˜ÙNŒMŒİ]\Î‰Ü[™[™ÉË^[Y[Î–×_B—_B™[˜İ[Ûˆ[ØÚÕ™X\İ\Q^[œÙ\ÊÜ™Ø[š^˜][Û’Yœİš[™ÊN“ÙÙU™X\İ\Q^[œÙV×^Ü™]\›–ÂˆÚY˜^[œÙKLKIÛÜ™Ø[š^˜][Û’YXÜ™Ø[š^˜][Û’YØ]YÛÜN‰ÔÙ\šXÚ[ÜÉË[[İ[Œ^[œÙQ]N‰ÌŒ‹LKLL‰Ë\ØÜš\[Û‰ÔÙ\šXÚ[ÈÜ\˜]]›È[[\ˆ0­È]ÈšXİXÚ[ÉË]šY[˜ÙT™Y™\™[˜ÙN‰Ô‹T‘TÔSËQSSËLIË\›İ˜[İ]\Î‰Ü[™[™×Ø\›İ˜[	Ë™XÛÜ™YTİXš™Xİ‰İ\ÛÜ™\šXKY[[ÉË\›İ™YTİXš™Xİ›[\›İ™Y]]Î›[™XÛÜ™Y]]Î‰ÌŒ‹LKLL•NŒŒ‰ßB—_B™[˜İ[ÛˆÛÛ™U™X\İ\PÚ\™ÙJ][N“ÙÙU™X\İ\PÚ\™ÙJN“ÙÙU™X\İ\PÚ\™Ù^Ü™]\›Ë‹‹š][K^[Y[Îš][Kœ^[Y[Ë›X\
OŠË‹‹JJ__B™[˜İ[Ûˆ[ØÚÔ™Yİ[\š]TÛ˜\Úİ
Ü™Ø[š^˜][Û’Yˆİš[™Ë^[ØYˆÛÜšÜÚÜ™Yİ[\š]T™\]Y\İ™Yš^ˆİš[™ÊNˆÛÜšÜÚÜ™Yİ[\š]TÛ˜\ÚİÈ™]\›ˆÈYˆ	Ü™Yš^KIØÜ\Ëœ˜[™ÛUURQ

_XÜ™Ø[š^˜][Û’YØÛÜNˆ	ÛÜ™Ø[š^˜][Û‰Ëİ]\Îˆ^[ØYœİ]\Ë\ÓÙ‘]Nˆ^[ØY˜\ÓÙ‘]KÛİ\˜ÙT™Y™\™[˜ÙNˆ^[ØYœÛİ\˜ÙT™Y™\™[˜ÙHÏÈ[›İ\Îˆ^[ØY››İ\ÈÏÈ[™XÛÜ™Y]]Îˆ™]È]J
KÒTÓÔİš[™Ê
HHB™[˜İ[Ûˆ[ØÚÕ™X\İ\Tİ][Y[
Ü™Ø[š^˜][Û’Yˆİš[™Ë^[ØYˆÜ™X]U™X\İ\Tİ][Y[™\]Y\İ
Nˆ™X\İ\Tİ][Y[È™]\›ˆÈYˆÜ\Ëœ˜[™ÛUURQ

KÜ™Ø[š^˜][Û’Y\š[ÙYX\ˆ^[ØYœ\š[ÙYX\‹\š[Ù[Ûˆ^[ØYœ\š[Ù[Ûİ]Ù™‘]Nˆ^[ØY˜İ]Ù™‘]Kİ]\Îˆ	Ù˜Y	ËÛİ\˜ÙT™Y™\™[˜ÙNˆ^[ØYœÛİ\˜ÙT™Y™\™[˜ÙHÏÈ[^XİY[[İ[ˆ˜[œÙ™\[[İ[ˆ\ÜÚ][[İ[ˆZY[[İ[ˆY™™\™[˜ÙP[[İ[ˆ[œ™\ÛÛ™YY[]Y\Îˆ™YPœ™XZÙİÛ–×K[™\Îˆ×K^[Y[Îˆ×KİX›Z]Y]]Îˆ[™XÛÛ˜Ú[Y]]Îˆ[ÛÜÙY]]Îˆ[HB™[˜İ[Ûˆ™XØ[İ[]S[ØÚÕ™X\İ\Jİ][Y[ˆ™X\İ\Tİ][Y[
HÈİ][Y[™^XİY[[İ[Hİ][Y[›[™\Ëœ™YXÙJ
İ[[™JHOˆİ[
È[™Kœ^XX›P[[İ[
NÈİ][Y[˜[œÙ™\[[İ[Hİ][Y[œ^[Y[Ë™š[\Š^[Y[Oˆ^[Y[œ^[Y[Y]ÙOOH	İ˜[œÙ™\‰ÊKœ™YXÙJ
İ[^[Y[
HOˆİ[
È^[Y[˜[[İ[
NÈİ][Y[™\ÜÚ][[İ[Hİ][Y[œ^[Y[Ë™š[\Š^[Y[Oˆ^[Y[œ^[Y[Y]ÙOOH	Ù\ÜÚ]	ÊKœ™YXÙJ
İ[^[Y[
HOˆİ[
È^[Y[˜[[İ[
NÈİ][Y[œZY[[İ[Hİ][Y[˜[œÙ™\[[İ[
Èİ][Y[™\ÜÚ][[İ[Èİ][Y[™Y™™\™[˜ÙP[[İ[Hİ][Y[™^XİY[[İ[Hİ][Y[œZY[[İ[Èİ][Y[™™YPœ™XZÙİÛP\œ˜^K™œ›ÛJİ][Y[›[™\Ëœ™YXÙJ
X\[™JOOØÛÛœİİ\œ™[[X\™Ù]
[™K˜ÛÛšX][Û•\JOÏŞÙ™YU\N›[™K˜ÛÛšX][Û•\KY[X™\œÎŒ[[İ[ŒNØİ\œ™[›Y[X™\œÊÊÎØİ\œ™[˜[[İ[
Ï[[™Kœ^XX›P[[İ[ÛX\œÙ]
[™K˜ÛÛšX][Û•\Kİ\œ™[
NÜ™]\›ˆX\K™]ÈX\ÙÙQ™YU\K™X\İ\Q™YPœ™XZÙİÛŠ
JK˜[Y\Ê
JHB™[˜İ[ÛˆÛÛ™U™X\İ\Tİ][Y[
İ][Y[ˆ™X\İ\Tİ][Y[
Nˆ™X\İ\Tİ][Y[È™]\›ˆÈ‹‹œİ][Y[™YPœ™XZÙİÛœİ][Y[™™YPœ™XZÙİÛ‹›X\
][OOŠË‹‹š][_JJK[™\Îˆİ][Y[›[™\Ë›X\
[™HOˆ
È‹‹›[™HJJK^[Y[Îˆİ][Y[œ^[Y[Ë›X\
^[Y[Oˆ
È‹‹œ^[Y[JJHHB™[˜İ[ÛˆÛÛ™PÙ\™[[ÛT]Y]YR][J][NˆÙ\™[[ÛT™]šY]Ô]Y]YR][JNˆÙ\™[[ÛT™]šY]Ô]Y]YR][HÈ™]\›ˆÈ‹‹š][K[YÚXš[]NˆÈ‹‹š][K™[YÚXš[]KX›XØ][Ûˆ][K™[YÚXš[]KœX›XØ][ÛˆÈÈ‹‹š][K™[YÚXš[]KœX›XØ][ÛˆHˆ[™\]Z\™[Y[Îˆ][K™[YÚXš[]Kœ™\]Z\™[Y[Ë›X\
˜[YHOˆ
È‹‹˜[YHJJHKXİ[ÛœÎˆÈ‹‹š][K˜Xİ[ÛœÈHHB™[˜İ[Ûˆ™XÛÛ\]S[ØÚÑ[YÚXš[]J][NˆÙ\™[[ÛT™]šY]Ô]Y]YR][JHÈÛÛœİ›ØÚÙYH][K™[YÚXš[]Kœ™\]Z\™[Y[ËœÛÛYJ˜[YHOˆ˜[YKœİ]\ÈOOH	Ü™Z™XİY	ÊNÈÛÛœİØœÙ\™YH][K™[YÚXš[]Kœ™\]Z\™[Y[ËœÛÛYJ˜[YHOˆ˜[YKœİ]\ÈOOH	ÛØœÙ\™Y	ÊNÈ][K™[YÚXš[]K˜Ø[]]Üš^™HHX›ØÚÙY	‰ˆ[ØœÙ\™YÈ][K™[YÚXš[]Kœİ]\ÈH][K™[YÚXš[]K˜Ø[]]Üš^™HÈ	ØÛÛ\Y\ÉÈˆØœÙ\™YÈ	ÛØœÙ\™Y	Èˆ	ÙÙ\×Û›İØÛÛ\IÈB™[˜İ[Ûˆ[ØÚÔ™YÚ[Y[”İ[[X\Jš[\œÎˆÈÜ™Ø[š^˜][Û’YÎˆİš[™ÎÈ\ÓÙÎˆİš[™ÎÈœ›ÛOÎˆİš[™ÈJNˆ™YÚ[Y[’[\š[Ü”İ[[X\HÂˆÛÛœİ\ÓÙˆHš[\œË˜\ÓÙˆÏÈ	ÌŒ‹LKL	ÂˆÛÛœİœ›ÛHHš[\œË™œ›ÛHÏÈ	ÌŒ‹LKLIÂˆÛÛœİØÛÜYHHYš[\œË›Ü™Ø[š^˜][Û’YˆÛÛœİ][\Y\ˆHØÛÜYÈHˆŒˆÛÛœİY™š[X]YH
ˆ][\Y\‚ˆ™]\›ˆÂˆØÛÜNˆØÛÜYÈ	ÛÜ™Ø[š^˜][Û‰Èˆ	ÛÜ™\‰ËˆÜ™Ø[š^˜][Û’Yˆš[\œË›Ü™Ø[š^˜][Û’YÏÈ[ˆ\ÓÙ‹ˆ\š[ÙˆÈœ›ÛKÎˆ\ÓÙˆKˆY[X™\œÎˆÈİ[™[]YˆY™š[X]Yİ\œ™[PY™š[X]YˆY™š[X]YXİ]™NˆY™š[X]Y[˜Xİ]™Nˆİ\œ™[Ú]›ØÚÚ[™Ôİ]\ÎˆKˆ]™[ÎˆÈ›Û[\UÚ]˜]Ø[Îˆ›Ü˜ÙYÚ]˜]Ø[Îˆ™Z[œİ][Y[ÎˆX]Îˆ˜[œÙ™\œÎˆKˆš[˜[˜ÚX[™Yİ[\š]NˆÈÛİ\˜ÙNˆ	ÑÜ˜[ˆ\ÛÜ™\°ëXH0­È\ØÙ[˜\š[ÈPIËİ\œ™[Y™š[X][ÛœÎˆY™š[X]Y\Ñ]NˆY™š[X]Y[[œ]Y[ˆ[™[™Îˆ^[\ˆÚ]İ]İ]\Îˆ[[œ]Y[Y[X™\œÑ\İ[˜İˆKˆYÜ™YQ\İšX][ÛˆÈX\İ\ˆLˆ
ˆ][\Y\‹™[İØÜ˜YˆH
ˆ][\Y\‹\™[XÙNˆH
ˆ][\Y\‹\İØXİ]™Nˆˆ
ˆ][\Y\ˆKˆ[™[™Õ˜[œÙ™\œÎˆˆBŸB