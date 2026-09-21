export type LodgeMeetingType = 'regular' | 'solemn' | 'instruction' | 'anniversary' | 'funeral' | 'special'
export type LodgeGrade = 'apprentice' | 'fellowcraft' | 'master' | 'all'
export type LodgeMeetingStatus = 'scheduled' | 'open' | 'held' | 'closed' | 'cancelled'
export type LodgeMeetingModality = 'in_person' | 'virtual'
export type LodgeCeremonyType = 'initiation' | 'affiliation' | 'wage_increase' | 'exaltation' | 'incorporation'
export type LodgeAttendanceStatus = 'present' | 'excused' | 'absent'
export type LodgeMinuteStatus = 'draft' | 'approved' | 'superseded'

export interface LodgeMemberOption { id: string; displayName: string }
export interface LodgeMemberOptionsResponse { total: number; items: LodgeMemberOption[] }
export interface LodgeMeeting {
  id: string
  organizationId: string
  meetingDate: string
  meetingType: LodgeMeetingType
  grade: LodgeGrade
  ceremonyType: LodgeCeremonyType | null
  modality: LodgeMeetingModality
  locationReference: string | null
  virtualAccessReference: string | null
  title: string | null
  status: LodgeMeetingStatus
  createdAtUtc: string
  heldAtUtc: string | null
  closedAtUtc: string | null
}
export interface LodgeMeetingsResponse { total: number; items: LodgeMeeting[] }
export interface CreateLodgeMeetingRequest {
  meetingDate: string
  meetingType: LodgeMeetingType
  grade: LodgeGrade
  ceremonyType?: LodgeCeremonyType | null
  modality: LodgeMeetingModality
  locationReference?: string | null
  virtualAccessReference?: string | null
  title?: string | null
  ceremonyAuthorizationDocumentId?: string | null
}
export interface LodgeAttendanceCurrent {
  recordId: string
  memberId: string
  displayName: string
  status: LodgeAttendanceStatus
  excuseReason: string | null
  recordedAtUtc: string
}
export interface LodgeAttendanceResponse { total: number; items: LodgeAttendanceCurrent[] }
export interface LodgeAttendanceRequest { memberId: string; status: LodgeAttendanceStatus; excuseReason?: string | null }
export interface LodgeMinute {
  id: string
  meetingId: string
  version: number
  content: string
  status: LodgeMinuteStatus
  createdAtUtc: string
  approvedAtUtc: string | null
}
export interface LodgeInstruction {
  id: string
  organizationId: string
  instructionDate: string
  grade: Exclude<LodgeGrade, 'all'>
  topic: string
  responsibleOffice: 'second_warden' | 'first_warden' | 'immediate_past_master'
  instructorMemberId: string | null
  status: 'scheduled' | 'held' | 'cancelled'
}
export interface LodgeInstructionsResponse { total: number; items: LodgeInstruction[] }
export interface CreateLodgeInstructionRequest {
  instructionDate: string
  grade: Exclude<LodgeGrade, 'all'>
  topic: string
  instructorMemberId?: string | null
}
export interface LodgeInstructionAttendanceItem { memberId: string; status: 'present' | 'absent' }
export interface LodgeMinutesResponse { total: number; items: LodgeMinute[] }
export type LodgeBallotType = 'white_black' | 'positive_negative' | 'candidate'
export interface LodgeAnonymousBallot { id: string; meetingId: string; version: number; ballotType: LodgeBallotType; procedureNumber: 1 | 2 | 3 | null; subject: string; attendeeCount: number; eligibleCount: number; positiveCount: number; negativeCount: number; recountObservation: string | null; status: 'closed' | 'superseded'; recordedAtUtc: string }
export interface LodgeAnonymousBallotRequest { ballotType: LodgeBallotType; procedureNumber?: 1 | 2 | 3 | null; subject: string; eligibleCount: number; positiveCount: number; negativeCount: number; recountObservation?: string | null }
export interface LodgeAnonymousBallotsResponse { total: number; items: LodgeAnonymousBallot[] }
export interface LodgeMinuteExtract { meetingId: string; attendeeCount: number; absentCount: number; excusedCount: number; ballotCount: number; content: string }
export type LodgeWithdrawalType = 'voluntary' | 'forced'
export type LodgeWithdrawalSignatureRole = 'venerable' | 'treasurer' | 'orator' | 'secretary'
export interface LodgeWithdrawalSignature { signed: boolean; signedAtUtc: string | null }
export interface LodgeWithdrawal {
  id: string; memberId: string; originOrganizationId: string; withdrawalType: LodgeWithdrawalType; requestedEffectiveDate: string
  reason: string; evidenceReference: string; status: 'pending' | 'approved' | 'rejected'; resolution: string | null
  createdAtUtc: string; decidedAtUtc: string | null; executedAtUtc: string | null
  signatures: Record<LodgeWithdrawalSignatureRole, LodgeWithdrawalSignature>
}
export interface LodgeWithdrawalsResponse { total: number; items: LodgeWithdrawal[] }
export interface CreateLodgeWithdrawalRequest { memberId: string; organizationId: string; withdrawalType: LodgeWithdrawalType; requestedEffectiveDate: string; reason: string; evidenceReference: string }

export type HistoricalIntakeStatus = 'draft' | 'submitted' | 'observed' | 'approved' | 'rejected'
export interface HistoricalOfficeInput { officeType: string; period: string; startDate?: string | null; endDate?: string | null; isCurrent: boolean }
export interface HistoricalMemberIntake {
  id: string; organizationId: string; targetMemberId: string | null; cutoffDate: string; firstNames: string; lastNames: string
  rut: string | null; institutionalNumber: string | null; email: string | null; phone: string | null; currentDegree: 'apprentice' | 'fellowcraft' | 'master'
  membershipStartDate: string | null; initiationDate: string | null; wageIncreaseDate: string | null; exaltationDate: string | null
  evidenceReference: string; status: HistoricalIntakeStatus; revision: number; createdAtUtc: string; submittedAtUtc: string | null
  reviewedAtUtc: string | null; reviewNotes: string | null; approvedMemberId: string | null
  offices: Array<{ id: string; officeType: string; period: string; startDate: string | null; endDate: string | null; isCurrent: boolean }>
}
export interface HistoricalMemberIntakesResponse { total: number; items: HistoricalMemberIntake[] }
export interface CreateHistoricalMemberIntakeRequest {
  firstNames: string; lastNames: string; rut?: string | null; institutionalNumber?: string | null; email?: string | null; phone?: string | null
  currentDegree: 'apprentice' | 'fellowcraft' | 'master'; membershipStartDate?: string | null; initiationDate?: string | null
  wageIncreaseDate?: string | null; exaltationDate?: string | null; cutoffDate: string; evidenceReference: string; offices: HistoricalOfficeInput[]
}
export interface LodgeAdministrativeMeeting {
  id: string; organizationId: string; meetingDate: string; title: string; purpose: string | null
  status: 'scheduled' | 'held' | 'cancelled'; createdAtUtc: string; heldAtUtc: string | null
}
export interface LodgeAdministrativeMeetingsResponse { total: number; items: LodgeAdministrativeMeeting[] }
export type LodgeSecretariatRecordType = 'tenida' | 'reunion' | 'consejo'
export interface LodgeSecretariatRecord {
  id: string; organizationId: string; recordType: LodgeSecretariatRecordType; sourceRecordId: string; eventDate: string; title: string
  workPaperDocumentVersionId: string | null; workPaperAuthorMemberId: string | null; extractDocumentVersionId: string | null
  fullMinuteDocumentVersionId: string | null; ceremonyAuthorizationDocumentId: string | null
  status: 'draft' | 'submitted' | 'received' | 'observed'
  createdAtUtc: string; submittedAtUtc: string | null; reviewedAtUtc: string | null; reviewNotes: string | null
}
export interface LodgeSecretariatRecordsResponse { total: number; items: LodgeSecretariatRecord[] }
export interface LodgeWorkPaper {
  id:string; organizationId:string; authorMemberId:string; authorName:string; documentId:string; documentVersionId:string
  meetingId:string|null; title:string; topic:string|null; degree:'apprentice'|'fellowcraft'|'master'; presentedOn:string
  shortDescription:string|null; status:'private'|'library_requested'|'published'; createdAtUtc:string
  libraryRequestedAtUtc:string|null; publishedAtUtc:string|null
}
export interface LodgeWorkPapersResponse { total:number; items:LodgeWorkPaper[] }
export interface CreateLodgeWorkPaperRequest { authorMemberId:string; documentId:string; documentVersionId:string; meetingId:string|null; title:string; topic:string|null; degree:LodgeWorkPaper['degree']; presentedOn:string; shortDescription:string|null }
export interface UpsertLodgeSecretariatRecordRequest {
  workPaperDocumentVersionId?: string | null; workPaperAuthorMemberId?: string | null
  extractDocumentVersionId?: string | null; fullMinuteDocumentVersionId?: string | null
  ceremonyAuthorizationDocumentId?: string | null
}
export interface LodgeCeremonyAuthorizationOption {
  id: string; documentCode: string; title: string; ceremonyRequestId: string; ceremonyType: LodgeCeremonyType
  proposedDate: string | null; issuedAtUtc: string
}
export interface LodgeCeremonyAuthorizationOptionsResponse { total: number; items: LodgeCeremonyAuthorizationOption[] }
export interface LodgeAdvancementRequest {
  id:string; organizationId:string; ceremonyType:'wage_increase'|'exaltation'; memberId:string; memberName:string
  tentativeDate:string|null; status:string; notes:string|null; createdAtUtc:string
  dispensationRequested:boolean; councilApprovedDispensation:boolean|null; councilRecordReference:string|null; dispensationRequirement:string|null
}
export interface LodgeAdvancementRequestsResponse { total:number; items:LodgeAdvancementRequest[] }
export interface LodgeAdvancementEligibility { requestId:string; status:string; canAuthorize:boolean; advancement:null|{mode:string;sourceDegree:number;ruleVersion:string;requirements:Array<{code:string;name:string;minimum:number;achieved:number;complies:boolean}>;dispensation:null|{status:string;reason:string}} }
export interface LodgeCorrespondence { id:string; organizationId:string; direction:'received'|'sent'; folio:string; correspondenceDate:string; subject:string; counterparty:string; channel:'email'|'letter'|'hand_delivery'|'other'; reference:string|null; status:'registered'|'closed'; createdAtUtc:string; closedAtUtc:string|null }
export interface LodgeSecretariatTask { id:string; organizationId:string; title:string; detail:string|null; dueDate:string|null; priority:'low'|'normal'|'high'|'urgent'; responsible:string|null; status:'pending'|'in_progress'|'completed'|'cancelled'; createdAtUtc:string; completedAtUtc:string|null }
export interface LodgeAgendaItem { id:string; organizationId:string; meetingId:string|null; order:number; title:string; detail:string|null; status:'pending'|'covered'|'deferred'; createdAtUtc:string; updatedAtUtc:string|null }

export type LodgeAccessTokenProvider = () => Promise<string | null>

interface LodgeApiClientOptions {
  baseUrl?: string
  getAccessToken?: LodgeAccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

class LodgeApiHttpError extends Error {
  constructor(readonly status: number, message: string) {
    super(message)
    this.name = 'LodgeApiHttpError'
  }
}

const DEMO_LODGE_1_ID = '11111111-1111-1111-1111-111111111111'
const DEMO_LODGE_23_ID = '23232323-2323-2323-2323-232323232323'

const demoMembers: LodgeMemberOption[] = [
  { id: 'aaaaaaaa-1111-1111-1111-111111111111', displayName: 'Hermana Demostrativa Uno' },
  { id: 'aaaaaaaa-2222-2222-2222-222222222222', displayName: 'Hermano Demostrativo Dos' },
  { id: 'aaaaaaaa-3333-3333-3333-333333333333', displayName: 'Hermana Demostrativa Tres' },
]

export const demoLodgeSeed = {
  meetings: [
    {
      id: 'bbbbbbbb-2309-0012-0000-000000000001', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-12', meetingType: 'regular' as const,
      grade: 'all' as const, ceremonyType: null, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null, title: 'Tenida Ordinaria · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-01T15:00:00Z', heldAtUtc: null, closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0026-0000-000000000002', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-26', meetingType: 'instruction' as const,
      grade: 'all' as const, ceremonyType: null, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null, title: 'Tenida de Instrucción · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-02T15:00:00Z', heldAtUtc: null, closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0005-0000-000000000003', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-05', meetingType: 'regular' as const,
      grade: 'all' as const, ceremonyType: null, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null, title: 'Tenida Ordinaria anterior · demo', status: 'held' as const, createdAtUtc: '2026-08-25T15:00:00Z', heldAtUtc: '2026-09-06T01:20:00Z', closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0018-0000-000000000005', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-18', meetingType: 'solemn' as const,
      grade: 'apprentice' as const, ceremonyType: 'initiation' as const, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null, title: 'Tenida de Iniciación · demo', status: 'held' as const, createdAtUtc: '2026-09-10T15:00:00Z', heldAtUtc: '2026-09-19T00:30:00Z', closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0018-0000-000000000006', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-18', meetingType: 'solemn' as const,
      grade: 'master' as const, ceremonyType: 'affiliation' as const, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null,
      title: 'Tenida de Afiliación · demo', status: 'closed' as const, createdAtUtc: '2026-09-12T15:00:00Z', heldAtUtc: '2026-09-19T00:10:00Z', closedAtUtc: '2026-09-19T02:20:00Z',
    },
    {
      id: 'bbbbbbbb-0109-0019-0000-000000000004', organizationId: DEMO_LODGE_1_ID, meetingDate: '2026-09-19', meetingType: 'solemn' as const,
      grade: 'all' as const, ceremonyType: null, modality: 'in_person' as const, locationReference: 'Templo Demostrativo', virtualAccessReference: null, title: 'Tenida Solemne · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-03T15:00:00Z', heldAtUtc: null, closedAtUtc: null,
    },
  ] satisfies LodgeMeeting[],
  attendance: [
    { recordId: 'cccccccc-0001-0001-0001-000000000001', memberId: demoMembers[0].id, displayName: demoMembers[0].displayName, status: 'present' as const, excuseReason: null, recordedAtUtc: '2026-09-05T23:05:00Z' },
    { recordId: 'cccccccc-0002-0002-0002-000000000002', memberId: demoMembers[1].id, displayName: demoMembers[1].displayName, status: 'present' as const, excuseReason: null, recordedAtUtc: '2026-09-05T23:06:00Z' },
    { recordId: 'cccccccc-0003-0003-0003-000000000003', memberId: demoMembers[2].id, displayName: demoMembers[2].displayName, status: 'excused' as const, excuseReason: 'Justificación demostrativa', recordedAtUtc: '2026-09-05T23:07:00Z' },
  ] satisfies LodgeAttendanceCurrent[],
  minute: {
    id: 'dddddddd-0001-0001-0001-000000000001', meetingId: 'bbbbbbbb-2309-0005-0000-000000000003', version: 1,
    content: 'Acta demostrativa: contenido ficticio para validar versionado, aprobación y navegación del módulo de Gestión Logial.',
    status: 'approved' as const, createdAtUtc: '2026-09-06T01:25:00Z', approvedAtUtc: '2026-09-06T01:40:00Z',
  } satisfies LodgeMinute,
  ballots: [{ id: 'ffffffff-0001-0001-0001-000000000001', meetingId: 'bbbbbbbb-2309-0005-0000-000000000003', version: 1, ballotType: 'white_black' as const, procedureNumber: 1 as const, subject: 'Admisión de Persona Demostrativa', attendeeCount: 2, eligibleCount: 2, positiveCount: 2, negativeCount: 0, recountObservation: null, status: 'closed' as const, recordedAtUtc: '2026-09-06T01:15:00Z' }] satisfies LodgeAnonymousBallot[],
  ceremonyAuthorizations: [
    {
      id: 'abababab-5555-2222-3333-444444444444',
      documentCode: 'PLA-AUT-CER-2026-AFI0001',
      title: 'Plancha de Autorización de Ceremonia — affiliation',
      ceremonyRequestId: 'ac000000-0000-0000-0000-000000000003',
      ceremonyType: 'affiliation' as const,
      proposedDate: '2026-09-18',
      issuedAtUtc: '2026-09-17T18:00:00Z',
    },
    {
      id: 'abababab-1111-2222-3333-444444444444',
      documentCode: 'PLA-AUT-CER-2026-DEMO0001',
      title: 'Plancha de Autorización de Ceremonia — initiation',
      ceremonyRequestId: 'cdcdcdcd-1111-2222-3333-444444444444',
      ceremonyType: 'initiation' as const,
      proposedDate: '2026-09-18',
      issuedAtUtc: '2026-09-16T18:00:00Z',
    },
  ] satisfies LodgeCeremonyAuthorizationOption[],
  secretariatRecords: [
    {
      id: 'edededed-2309-0018-0000-000000000006', organizationId: DEMO_LODGE_23_ID, recordType: 'tenida' as const,
      sourceRecordId: 'bbbbbbbb-2309-0018-0000-000000000006', eventDate: '2026-09-18', title: 'Tenida de Afiliación · demo',
      workPaperDocumentVersionId: null, workPaperAuthorMemberId: null, extractDocumentVersionId: 'edededed-extract-0018-000000000006',
      fullMinuteDocumentVersionId: null, ceremonyAuthorizationDocumentId: 'abababab-5555-2222-3333-444444444444',
      status: 'submitted' as const, createdAtUtc: '2026-09-19T01:40:00Z', submittedAtUtc: '2026-09-19T02:25:00Z', reviewedAtUtc: null, reviewNotes: null,
    },
  ] satisfies LodgeSecretariatRecord[],
  instructions: [
    { id: 'eeeeeeee-0001-0001-0001-000000000001', organizationId: DEMO_LODGE_23_ID, instructionDate: '2026-09-05', grade: 'apprentice' as const, topic: 'Simbología del grado', responsibleOffice: 'second_warden' as const, instructorMemberId: null, status: 'held' as const },
    { id: 'eeeeeeee-0002-0002-0002-000000000002', organizationId: DEMO_LODGE_23_ID, instructionDate: '2026-09-26', grade: 'fellowcraft' as const, topic: 'Las artes liberales', responsibleOffice: 'first_warden' as const, instructorMemberId: null, status: 'scheduled' as const },
  ] satisfies LodgeInstruction[],
} as const

export class LodgeApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: LodgeAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockMeetings: LodgeMeeting[] = demoLodgeSeed.meetings.map(item => ({ ...item }))
  private readonly mockAttendance = new Map<string, LodgeAttendanceCurrent[]>([[demoLodgeSeed.meetings[2].id, demoLodgeSeed.attendance.map(item => ({ ...item }))]])
  private readonly mockMinutes = new Map<string, LodgeMinute[]>([[demoLodgeSeed.meetings[2].id, [{ ...demoLodgeSeed.minute }]]])
  private readonly mockBallots = new Map<string, LodgeAnonymousBallot[]>([[demoLodgeSeed.meetings[2].id, demoLodgeSeed.ballots.map(item => ({ ...item }))]])
  private readonly mockInstructions: LodgeInstruction[] = demoLodgeSeed.instructions.map(item => ({ ...item }))
  private readonly mockInstructionAttendance = new Map<string, LodgeInstructionAttendanceItem[]>()
  private readonly mockWithdrawals: LodgeWithdrawal[] = [{
    id: '67676767-2323-2323-2323-232323232323', memberId: demoMembers[0].id, originOrganizationId: DEMO_LODGE_23_ID,
    withdrawalType: 'voluntary', requestedEffectiveDate: '2026-10-01', reason: 'Solicitud demostrativa aprobada por Cámara del Medio.',
    evidenceReference: 'CRV-DEMO-2026-001', status: 'approved', resolution: 'Aprobada para completar las cuatro firmas institucionales.',
    createdAtUtc: '2026-09-18T15:00:00Z', decidedAtUtc: '2026-09-19T15:00:00Z', executedAtUtc: null,
    signatures: {
      venerable: { signed: false, signedAtUtc: null }, treasurer: { signed: false, signedAtUtc: null },
      orator: { signed: false, signedAtUtc: null }, secretary: { signed: false, signedAtUtc: null },
    },
  }]
  private readonly mockHistoricalIntakes: HistoricalMemberIntake[] = []
  private readonly mockAdministrativeMeetings: LodgeAdministrativeMeeting[] = []
  private readonly mockSecretariatRecords: LodgeSecretariatRecord[] = demoLodgeSeed.secretariatRecords.map(item => ({ ...item }))
  private readonly mockWorkPapers: LodgeWorkPaper[] = []
  private readonly mockAdvancementRequests: LodgeAdvancementRequest[] = []
  private readonly mockCeremonyAuthorizations: LodgeCeremonyAuthorizationOption[] = demoLodgeSeed.ceremonyAuthorizations.map(item => ({ ...item }))
  private readonly mockCorrespondence: LodgeCorrespondence[] = [{id:'corr-demo-001',organizationId:DEMO_LODGE_23_ID,direction:'received',folio:'REC-2026-001',correspondenceDate:'2026-09-17',subject:'Circular institucional demostrativa',counterparty:'Gran Secretaría',channel:'email',reference:'Correo institucional ficticio',status:'registered',createdAtUtc:'2026-09-17T15:00:00Z',closedAtUtc:null}]
  private readonly mockSecretariatTasks: LodgeSecretariatTask[] = [{id:'task-demo-001',organizationId:DEMO_LODGE_23_ID,title:'Preparar extracto de próxima Tenida',detail:'Pendiente demostrativo sin datos reales.',dueDate:'2026-09-25',priority:'high',responsible:'Secretaría del Taller',status:'pending',createdAtUtc:'2026-09-18T15:00:00Z',completedAtUtc:null}]
  private readonly mockAgenda: LodgeAgendaItem[] = [{id:'agenda-demo-001',organizationId:DEMO_LODGE_23_ID,meetingId:'bbbbbbbb-2309-0026-0000-000000000002',order:1,title:'Apertura y lectura del acta anterior',detail:null,status:'pending',createdAtUtc:'2026-09-18T16:00:00Z',updatedAtUtc:null}]

  constructor(options: LodgeApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getMemberOptions(organizationId: string): Promise<LodgeMemberOptionsResponse> {
    if (this.useMocks) return { total: demoMembers.length, items: demoMembers.map(item => ({ ...item })) }
    return this.request<LodgeMemberOptionsResponse>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/miembros/opciones`)
  }


  async getHistoricalMemberIntakes(organizationId: string): Promise<HistoricalMemberIntakesResponse> {
    if (this.useMocks) {
      const items=this.mockHistoricalIntakes.filter(x=>x.organizationId===organizationId).map(x=>({...x,offices:x.offices.map(o=>({...o}))}))
      return { total:items.length, items }
    }
    return this.request<HistoricalMemberIntakesResponse>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/cuadro/carga-historica`)
  }

  async getCorrespondence(organizationId:string):Promise<{total:number;items:LodgeCorrespondence[]}>{ if(this.useMocks){const items=this.mockCorrespondence.filter(x=>x.organizationId===organizationId);return{total:items.length,items:items.map(x=>({...x}))}} return this.request(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/correspondencia`) }
  async createCorrespondence(organizationId:string,payload:Omit<LodgeCorrespondence,'id'|'organizationId'|'status'|'createdAtUtc'|'closedAtUtc'>):Promise<LodgeCorrespondence>{ if(this.useMocks){const item:LodgeCorrespondence={id:crypto.randomUUID(),organizationId,...payload,status:'registered',createdAtUtc:new Date().toISOString(),closedAtUtc:null};this.mockCorrespondence.unshift(item);return{...item}} return this.postJson(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/correspondencia`,payload) }
  async updateCorrespondenceStatus(id:string,status:'registered'|'closed'):Promise<LodgeCorrespondence>{ if(this.useMocks){const x=this.mockCorrespondence.find(i=>i.id===id);if(!x)throw new Error('Correspondencia no encontrada.');x.status=status;x.closedAtUtc=status==='closed'?new Date().toISOString():null;return{...x}} return this.postJson(`/api/secretaria/correspondencia/${encodeURIComponent(id)}/estado`,{status}) }
  async getSecretariatTasks(organizationId:string):Promise<{total:number;items:LodgeSecretariatTask[]}>{if(this.useMocks){const items=this.mockSecretariatTasks.filter(x=>x.organizationId===organizationId);return{total:items.length,items:items.map(x=>({...x}))}}return this.request(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/pendientes`)}
  async createSecretariatTask(organizationId:string,payload:{title:string;detail?:string|null;dueDate?:string|null;priority:LodgeSecretariatTask['priority'];responsible?:string|null}):Promise<LodgeSecretariatTask>{if(this.useMocks){const item:LodgeSecretariatTask={id:crypto.randomUUID(),organizationId,title:payload.title,detail:payload.detail??null,dueDate:payload.dueDate??null,priority:payload.priority,responsible:payload.responsible??null,status:'pending',createdAtUtc:new Date().toISOString(),completedAtUtc:null};this.mockSecretariatTasks.unshift(item);return{...item}}return this.postJson(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/pendientes`,payload)}
  async updateSecretariatTaskStatus(id:string,status:LodgeSecretariatTask['status']):Promise<LodgeSecretariatTask>{if(this.useMocks){const x=this.mockSecretariatTasks.find(i=>i.id===id);if(!x)throw new Error('Pendiente no encontrado.');x.status=status;x.completedAtUtc=status==='completed'?new Date().toISOString():null;return{...x}}return this.postJson(`/api/secretaria/pendientes/${encodeURIComponent(id)}/estado`,{status})}
  async getAgendaItems(organizationId:string):Promise<{total:number;items:LodgeAgendaItem[]}>{if(this.useMocks){const items=this.mockAgenda.filter(x=>x.organizationId===organizationId);return{total:items.length,items:items.map(x=>({...x}))}}return this.request(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/agenda`)}
  async createAgendaItem(organizationId:string,payload:{meetingId?:string|null;order:number;title:string;detail?:string|null}):Promise<LodgeAgendaItem>{if(this.useMocks){const item:LodgeAgendaItem={id:crypto.randomUUID(),organizationId,meetingId:payload.meetingId??null,order:payload.order,title:payload.title,detail:payload.detail??null,status:'pending',createdAtUtc:new Date().toISOString(),updatedAtUtc:null};this.mockAgenda.push(item);return{...item}}return this.postJson(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/agenda`,payload)}
  async updateAgendaStatus(id:string,status:LodgeAgendaItem['status']):Promise<LodgeAgendaItem>{if(this.useMocks){const x=this.mockAgenda.find(i=>i.id===id);if(!x)throw new Error('Punto de agenda no encontrado.');x.status=status;x.updatedAtUtc=new Date().toISOString();return{...x}}return this.postJson(`/api/secretaria/agenda/${encodeURIComponent(id)}/estado`,{status})}

  async createHistoricalMemberIntake(organizationId: string, payload: CreateHistoricalMemberIntakeRequest): Promise<HistoricalMemberIntake> {
    if (this.useMocks) {
      const item:HistoricalMemberIntake={ id:crypto.randomUUID(), organizationId, targetMemberId:null, ...payload,
        rut:payload.rut??null, institutionalNumber:payload.institutionalNumber??null, email:payload.email??null, phone:payload.phone??null,
        membershipStartDate:payload.membershipStartDate??null, initiationDate:payload.initiationDate??null, wageIncreaseDate:payload.wageIncreaseDate??null,
        exaltationDate:payload.exaltationDate??null, status:'draft', revision:1, createdAtUtc:new Date().toISOString(), submittedAtUtc:null,
        reviewedAtUtc:null, reviewNotes:null, approvedMemberId:null,
        offices:payload.offices.map(o=>({id:crypto.randomUUID(),officeType:o.officeType,period:o.period,startDate:o.startDate??null,endDate:o.endDate??null,isCurrent:o.isCurrent})) }
      this.mockHistoricalIntakes.unshift(item); return {...item,offices:item.offices.map(o=>({...o}))}
    }
    return this.postJson<HistoricalMemberIntake>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/cuadro/carga-historica`, payload)
  }

  async submitHistoricalMemberIntake(intakeId: string): Promise<HistoricalMemberIntake> {
    if(this.useMocks){ const item=this.mockHistoricalIntakes.find(x=>x.id===intakeId); if(!item) throw new Error('La carga histórica no existe.'); item.status='submitted'; item.submittedAtUtc=new Date().toISOString(); item.revision+=1; return {...item,offices:item.offices.map(o=>({...o}))} }
    return this.request<HistoricalMemberIntake>(`/api/secretaria/carga-historica/${encodeURIComponent(intakeId)}/enviar-ri`,{method:'POST'})
  }

  async downloadHistoricalTemplate(organizationId: string): Promise<Blob> {
    if(this.useMocks) return new Blob(['Plantilla XLSX demostrativa'],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'})
    const response=await this.authorizedFetch(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/cuadro/plantilla.xlsx`,{},'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
    return response.blob()
  }

  async importHistoricalMemberIntakes(organizationId: string,file: File): Promise<{imported:number;status:string}> {
    if(this.useMocks) return { imported:1,status:'draft' }
    const body=new FormData(); body.append('file',file)
    const response=await this.authorizedFetch(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/cuadro/importar.xlsx`,{method:'POST',body},'application/json')
    return response.json() as Promise<{imported:number;status:string}>
  }

  async getAdministrativeMeetings(organizationId: string): Promise<LodgeAdministrativeMeetingsResponse> {
    if(this.useMocks){ const items=this.mockAdministrativeMeetings.filter(x=>x.organizationId===organizationId).map(x=>({...x})); return {total:items.length,items} }
    return this.request<LodgeAdministrativeMeetingsResponse>(`/api/secretaria/reuniones/talleres/${encodeURIComponent(organizationId)}`)
  }

  async createAdministrativeMeeting(organizationId:string,payload:{meetingDate:string;title:string;purpose?:string|null}):Promise<LodgeAdministrativeMeeting>{
    if(this.useMocks){ const item:LodgeAdministrativeMeeting={id:crypto.randomUUID(),organizationId,meetingDate:payload.meetingDate,title:payload.title.trim(),purpose:payload.purpose?.trim()||null,status:'scheduled',createdAtUtc:new Date().toISOString(),heldAtUtc:null}; this.mockAdministrativeMeetings.unshift(item); return {...item} }
    return this.postJson<LodgeAdministrativeMeeting>(`/api/secretaria/reuniones/talleres/${encodeURIComponent(organizationId)}`,payload)
  }

  async markAdministrativeMeetingHeld(meetingId:string):Promise<LodgeAdministrativeMeeting>{
    if(this.useMocks){ const item=this.mockAdministrativeMeetings.find(x=>x.id===meetingId); if(!item) throw new Error('La reunión no existe.'); item.status='held'; item.heldAtUtc=new Date().toISOString(); return {...item} }
    return this.request<LodgeAdministrativeMeeting>(`/api/secretaria/reuniones/${encodeURIComponent(meetingId)}/realizar`,{method:'POST'})
  }

  async getCeremonyAuthorizationOptions(organizationId:string):Promise<LodgeCeremonyAuthorizationOptionsResponse>{
    if(this.useMocks){
      const items = organizationId === DEMO_LODGE_23_ID ? this.mockCeremonyAuthorizations.map(item=>({...item})) : []
      return { total:items.length, items }
    }
    return this.request<LodgeCeremonyAuthorizationOptionsResponse>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/autorizaciones-ceremonia`)
  }

  async getSecretariatRecords(organizationId:string):Promise<LodgeSecretariatRecordsResponse>{
    if(this.useMocks){ const items=this.mockSecretariatRecords.filter(x=>x.organizationId===organizationId).map(x=>({...x})); return {total:items.length,items} }
    return this.request<LodgeSecretariatRecordsResponse>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/registros`)
  }

  async upsertSecretariatRecord(organizationId:string,recordType:LodgeSecretariatRecordType,sourceRecordId:string,payload:UpsertLodgeSecretariatRecordRequest):Promise<LodgeSecretariatRecord>{
    if(this.useMocks){ let item=this.mockSecretariatRecords.find(x=>x.recordType===recordType&&x.sourceRecordId===sourceRecordId); if(!item){ item={id:crypto.randomUUID(),organizationId,recordType,sourceRecordId,eventDate:new Date().toISOString().slice(0,10),title:`${recordType} demostrativo`,workPaperDocumentVersionId:null,workPaperAuthorMemberId:null,extractDocumentVersionId:null,fullMinuteDocumentVersionId:null,ceremonyAuthorizationDocumentId:null,status:'draft',createdAtUtc:new Date().toISOString(),submittedAtUtc:null,reviewedAtUtc:null,reviewNotes:null}; this.mockSecretariatRecords.unshift(item) } Object.assign(item,payload); return {...item} }
    return this.putJson<LodgeSecretariatRecord>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/registros/${encodeURIComponent(recordType)}/${encodeURIComponent(sourceRecordId)}`,payload)
  }

  async submitTenidaExtract(organizationId:string,meetingId:string):Promise<LodgeSecretariatRecord>{
    if(this.useMocks){ const item=this.mockSecretariatRecords.find(x=>x.recordType==='tenida'&&x.sourceRecordId===meetingId); if(!item?.extractDocumentVersionId) throw new Error('Debe cargar el extracto PDF antes de remitir la Tenida.'); item.status='submitted'; item.submittedAtUtc=new Date().toISOString(); return {...item} }
    return this.request<LodgeSecretariatRecord>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/tenidas/${encodeURIComponent(meetingId)}/remitir-extracto`,{method:'POST'})
  }

  async getWorkPapers(organizationId:string):Promise<LodgeWorkPapersResponse>{
    if(this.useMocks){const items=this.mockWorkPapers.filter(x=>x.organizationId===organizationId).map(x=>({...x}));return{total:items.length,items}}
    return this.request<LodgeWorkPapersResponse>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/planchas-trabajo`)
  }

  async createWorkPaper(organizationId:string,payload:CreateLodgeWorkPaperRequest):Promise<LodgeWorkPaper>{
    if(this.useMocks){const author=demoMembers.find(x=>x.id===payload.authorMemberId);const item:LodgeWorkPaper={id:crypto.randomUUID(),organizationId,...payload,authorName:author?.displayName??'Hermano demostrativo',status:'private',createdAtUtc:new Date().toISOString(),libraryRequestedAtUtc:null,publishedAtUtc:null};this.mockWorkPapers.unshift(item);return{...item}}
    return this.postJson<LodgeWorkPaper>(`/api/secretaria/talleres/${encodeURIComponent(organizationId)}/planchas-trabajo`,payload)
  }

  async requestWorkPaperLibraryPublication(id:string):Promise<void>{
    if(this.useMocks){const item=this.mockWorkPapers.find(x=>x.id===id);if(!item)throw new Error('La plancha no existe.');item.status='library_requested';item.libraryRequestedAtUtc=new Date().toISOString();return}
    await this.request(`/api/secretaria/planchas-trabajo/${encodeURIComponent(id)}/solicitar-biblioteca`,{method:'POST'})
  }

  async getAdvancementRequests(organizationId:string):Promise<LodgeAdvancementRequestsResponse>{
    if(this.useMocks){const items=this.mockAdvancementRequests.filter(x=>x.organizationId===organizationId).map(x=>({...x}));return{total:items.length,items}}
    return this.request<LodgeAdvancementRequestsResponse>(`/api/ceremonias/talleres/${encodeURIComponent(organizationId)}/solicitudes-avance`)
  }

  async createAdvancementRequest(organizationId:string,payload:{ceremonyType:'wage_increase'|'exaltation';memberId:string;tentativeDate:string;notes?:string|null;dispensationRequested?:boolean;councilApprovedDispensation?:boolean|null;councilRecordReference?:string|null;dispensationRequirement?:string|null}):Promise<LodgeAdvancementRequest>{
    if(this.useMocks){
      if(this.mockAdvancementRequests.some(x=>x.memberId===payload.memberId&&x.ceremonyType===payload.ceremonyType&&!['rejected','completed'].includes(x.status)))throw new Error('Ya existe una solicitud de avance vigente para este hermano y ceremonia.')
      const member=demoMembers.find(x=>x.id===payload.memberId);const item:LodgeAdvancementRequest={id:crypto.randomUUID(),organizationId,ceremonyType:payload.ceremonyType,memberId:payload.memberId,memberName:member?.displayName??'Hermano demostrativo',tentativeDate:payload.tentativeDate,status:'under_review',notes:payload.notes?.trim()||null,dispensationRequested:payload.dispensationRequested??false,councilApprovedDispensation:payload.councilApprovedDispensation??null,councilRecordReference:payload.councilRecordReference??null,dispensationRequirement:payload.dispensationRequirement??null,createdAtUtc:new Date().toISOString()};this.mockAdvancementRequests.unshift(item);return{...item}
    }
    const created=await this.postJson<{id:string;organizationId:string;ceremonyType:'wage_increase'|'exaltation';memberId:string;proposedDate:string|null;status:string;dispensationRequested:boolean;councilApprovedDispensation:boolean|null;councilRecordReference:string|null;dispensationRequirement:string|null}>('/api/ceremonias/solicitudes',{organizationId,ceremonyType:payload.ceremonyType,memberId:payload.memberId,candidatePersonId:null,proposedDate:payload.tentativeDate,notes:payload.notes??null,dispensationRequested:payload.dispensationRequested??false,councilApprovedDispensation:payload.councilApprovedDispensation??null,councilRecordReference:payload.councilRecordReference??null,dispensationRequirement:payload.dispensationRequirement??null})
    return{id:created.id,organizationId:created.organizationId,ceremonyType:created.ceremonyType,memberId:created.memberId,memberName:'',tentativeDate:created.proposedDate,status:created.status,notes:payload.notes??null,dispensationRequested:created.dispensationRequested,councilApprovedDispensation:created.councilApprovedDispensation,councilRecordReference:created.councilRecordReference,dispensationRequirement:created.dispensationRequirement,createdAtUtc:new Date().toISOString()}
  }

  async getAdvancementEligibility(requestId:string):Promise<LodgeAdvancementEligibility>{
    if(this.useMocks)return{requestId,status:'observed',canAuthorize:false,advancement:{mode:'blocked',sourceDegree:1,ruleVersion:'demo-2026',requirements:[{code:'months_in_degree',name:'Antigüedad continuada en el grado (meses)',minimum:24,achieved:18,complies:false},{code:'meeting_attendance',name:'Asistencia a tenidas',minimum:30,achieved:22,complies:false},{code:'instruction_attendance',name:'Asistencia a instrucciones',minimum:10,achieved:8,complies:false},{code:'work_papers',name:'Planchas de trabajo',minimum:2,achieved:1,complies:false}],dispensation:null}}
    return this.request<LodgeAdvancementEligibility>(`/api/ceremonias/solicitudes/${encodeURIComponent(requestId)}/elegibilidad`)
  }

  async getWithdrawals(organizationId: string): Promise<LodgeWithdrawalsResponse> {
    if (this.useMocks) {
      const items = this.mockWithdrawals.filter(item => item.originOrganizationId === organizationId).map(item => structuredClone(item))
      return { total: items.length, items }
    }
    return this.request<LodgeWithdrawalsResponse>(`/api/gestion-logial/retiros?organizationId=${encodeURIComponent(organizationId)}`)
  }

  async createWithdrawal(payload: CreateLodgeWithdrawalRequest): Promise<LodgeWithdrawal> {
    if (this.useMocks) {
      if (this.mockWithdrawals.some(item => item.memberId === payload.memberId && item.status === 'pending')) throw new Error('El hermano ya tiene un retiro pendiente de resolución.')
      if (payload.reason.trim().length < 10) throw new Error('La causal o fundamento debe contener al menos 10 caracteres.')
      const item: LodgeWithdrawal = {
        id: crypto.randomUUID(), memberId: payload.memberId, originOrganizationId: payload.organizationId,
        withdrawalType: payload.withdrawalType, requestedEffectiveDate: payload.requestedEffectiveDate,
        reason: payload.reason, evidenceReference: payload.evidenceReference, status: 'pending', resolution: null,
        createdAtUtc: new Date().toISOString(), decidedAtUtc: null, executedAtUtc: null,
        signatures: {
          venerable: { signed: false, signedAtUtc: null }, treasurer: { signed: false, signedAtUtc: null },
          orator: { signed: false, signedAtUtc: null }, secretary: { signed: false, signedAtUtc: null },
        },
      }
      this.mockWithdrawals.unshift(item)
      return { ...item }
    }
    return this.postJson<LodgeWithdrawal>('/api/gestion-logial/retiros/', payload)
  }

  async signWithdrawal(requestId: string, role: LodgeWithdrawalSignatureRole): Promise<LodgeWithdrawal> {
    if (this.useMocks) {
      const item = this.mockWithdrawals.find(entry => entry.id === requestId)
      if (!item) throw new Error('La carta de retiro no existe.')
      if (item.status !== 'approved') throw new Error('El retiro debe estar aprobado antes de firmarse.')
      if (item.executedAtUtc) throw new Error('La carta ya fue completada y materializada.')
      if (item.signatures[role].signed) throw new Error('Este cargo ya firmó la carta de retiro.')
      item.signatures[role] = { signed: true, signedAtUtc: new Date().toISOString() }
      if (Object.values(item.signatures).every(signature => signature.signed)) item.executedAtUtc = new Date().toISOString()
      return structuredClone(item)
    }
    return this.postJson<LodgeWithdrawal>(`/api/gestion-logial/retiros/${encodeURIComponent(requestId)}/firmas/${encodeURIComponent(role)}`, {})
  }

  async getMeetings(organizationId: string, filters: { from?: string; to?: string } = {}): Promise<LodgeMeetingsResponse> {
    if (this.useMocks) {
      const items = this.mockMeetings
        .filter(item => item.organizationId === organizationId)
        .filter(item => !filters.from || item.meetingDate >= filters.from)
        .filter(item => !filters.to || item.meetingDate <= filters.to)
        .sort((a, b) => b.meetingDate.localeCompare(a.meetingDate))
        .map(item => ({ ...item }))
      return { total: items.length, items }
    }
    const query = new URLSearchParams()
    if (filters.from) query.set('from', filters.from)
    if (filters.to) query.set('to', filters.to)
    return this.request<LodgeMeetingsResponse>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/tenidas${query.size ? `?${query}` : ''}`)
  }

  async createMeeting(organizationId: string, payload: CreateLodgeMeetingRequest): Promise<LodgeMeeting> {
    if (this.useMocks) {
      if(payload.ceremonyType&&!payload.ceremonyAuthorizationDocumentId)throw new Error('No puede programarse una Tenida ceremonial antes de recibir la Plancha de Autorización de Gran Secretaría.')
      if(payload.ceremonyType&&!this.mockCeremonyAuthorizations.some(x=>x.id===payload.ceremonyAuthorizationDocumentId&&x.ceremonyType===payload.ceremonyType&&x.proposedDate===payload.meetingDate))throw new Error('La Plancha no corresponde al tipo o fecha de la ceremonia que intenta programar.')
      const meeting: LodgeMeeting = {
        id: crypto.randomUUID(),
        organizationId,
        meetingDate: payload.meetingDate,
        meetingType: payload.meetingType,
        grade: payload.grade,
        ceremonyType: payload.ceremonyType ?? null,
        modality: payload.modality,
        locationReference: payload.modality === 'in_person' ? payload.locationReference?.trim() || null : null,
        virtualAccessReference: payload.modality === 'virtual' ? payload.virtualAccessReference?.trim() || null : null,
        title: payload.title?.trim() || null,
        status: 'scheduled',
        createdAtUtc: new Date().toISOString(),
        heldAtUtc: null,
        closedAtUtc: null,
      }
      this.mockMeetings.unshift(meeting)
      return { ...meeting }
    }
    return this.postJson<LodgeMeeting>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/tenidas`, payload)
  }

  async markMeetingHeld(meetingId: string): Promise<LodgeMeeting> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId)
      if (meeting.status === 'held' || meeting.status === 'closed') throw new Error('La Tenida ya se encuentra realizada.')
      if (meeting.status === 'cancelled') throw new Error('Una Tenida cancelada no puede marcarse como realizada.')
      meeting.status = 'held'
      meeting.heldAtUtc = new Date().toISOString()
      meeting.closedAtUtc = null
      return { ...meeting }
    }
    return this.request<LodgeMeeting>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/realizar`, { method: 'POST' })
  }

  async closeMeeting(meetingId: string): Promise<LodgeMeeting> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId)
      if (meeting.status !== 'held') throw new Error(meeting.status === 'closed' ? 'La Tenida ya se encuentra cerrada.' : 'La Tenida debe estar Realizada antes de iniciar su cierre documental.')
      const record = this.mockSecretariatRecords.find(item => item.recordType === 'tenida' && item.sourceRecordId === meetingId)
      if (!record?.extractDocumentVersionId) throw new Error('Debe adjuntar el Extracto de Acta en PDF antes de cerrar la Tenida.')
      if (meeting.ceremonyType && !record.ceremonyAuthorizationDocumentId) throw new Error('Una Tenida ceremonial requiere la Plancha de Autorización de Ceremonia emitida por Gran Secretaría antes de cerrarse.')
      meeting.status = 'closed'
      meeting.closedAtUtc = new Date().toISOString()
      return { ...meeting }
    }
    return this.request<LodgeMeeting>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/cerrar`, { method: 'POST' })
  }

  async getAttendance(meetingId: string): Promise<LodgeAttendanceResponse> {
    if (this.useMocks) {
      const history = this.mockAttendance.get(meetingId) ?? []
      const latest = new Map<string, LodgeAttendanceCurrent>()
      for (const item of history) latest.set(item.memberId, item)
      const items = [...latest.values()].sort((a, b) => a.displayName.localeCompare(b.displayName, 'es'))
      return { total: items.length, items: items.map(item => ({ ...item })) }
    }
    return this.request<LodgeAttendanceResponse>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/asistencia`)
  }

  async recordAttendance(meetingId: string, payload: LodgeAttendanceRequest): Promise<unknown> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId)
      if (meeting.status !== 'held' && meeting.status !== 'closed') throw new Error('La asistencia sólo puede registrarse después de cerrar la tenida realizada.')
      const member = demoMembers.find(item => item.id === payload.memberId)
      if (!member) throw new Error('El hermano indicado no pertenece al Taller.')
      const row: LodgeAttendanceCurrent = {
        recordId: crypto.randomUUID(), memberId: payload.memberId, displayName: member.displayName,
        status: payload.status, excuseReason: payload.status === 'excused' ? payload.excuseReason?.trim() || null : null,
        recordedAtUtc: new Date().toISOString(),
      }
      const history = this.mockAttendance.get(meetingId) ?? []; history.push(row); this.mockAttendance.set(meetingId, history)
      return { ...row }
    }
    return this.postJson<unknown>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/asistencia`, payload)
  }

  async getMinutes(meetingId: string): Promise<LodgeMinutesResponse> {
    if (this.useMocks) {
      const items = [...(this.mockMinutes.get(meetingId) ?? [])].sort((a, b) => b.version - a.version).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<LodgeMinutesResponse>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/actas`)
  }

  async createMinute(meetingId: string, content: string): Promise<LodgeMinute> {
    if (this.useMocks) {
      const versions = this.mockMinutes.get(meetingId) ?? []
      const minute: LodgeMinute = {
        id: crypto.randomUUID(), meetingId, version: Math.max(0, ...versions.map(item => item.version)) + 1,
        content: content.trim(), status: 'draft', createdAtUtc: new Date().toISOString(), approvedAtUtc: null,
      }
      versions.push(minute); this.mockMinutes.set(meetingId, versions)
      return { ...minute }
    }
    return this.postJson<LodgeMinute>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/actas`, { content })
  }

  async approveMinute(meetingId: string, minuteId: string): Promise<LodgeMinute> {
    if (this.useMocks) {
      const versions = this.mockMinutes.get(meetingId) ?? []
      const minute = versions.find(item => item.id === minuteId)
      if (!minute) throw new Error('La versión de acta indicada no existe.')
      for (const previous of versions) if (previous.status === 'approved') previous.status = 'superseded'
      minute.status = 'approved'; minute.approvedAtUtc = new Date().toISOString()
      return { ...minute }
    }
    return this.request<LodgeMinute>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/actas/${encodeURIComponent(minuteId)}/aprobar`, { method: 'POST' })
  }

  async getAnonymousBallots(meetingId: string): Promise<LodgeAnonymousBallotsResponse> {
    if (this.useMocks) { const items = [...(this.mockBallots.get(meetingId) ?? [])].sort((a, b) => b.recordedAtUtc.localeCompare(a.recordedAtUtc)); return { total: items.length, items: items.map(item => ({ ...item })) } }
    return this.request<LodgeAnonymousBallotsResponse>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/votaciones`)
  }

  async recordAnonymousBallot(meetingId: string, payload: LodgeAnonymousBallotRequest): Promise<LodgeAnonymousBallot> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId); if (meeting.status !== 'held' && meeting.status !== 'closed') throw new Error('El escrutinio sólo puede registrarse después de cerrar la Tenida realizada.')
      const attendeeCount = (await this.getAttendance(meetingId)).items.filter(item => item.status === 'present').length
      if (payload.eligibleCount > attendeeCount) throw new Error('Las personas habilitadas no pueden superar a las asistentes presentes.')
      if (payload.positiveCount + payload.negativeCount !== payload.eligibleCount && !payload.recountObservation?.trim()) throw new Error('La diferencia del recuento debe explicarse en el acta.')
      if (payload.ballotType === 'white_black' && !payload.procedureNumber) throw new Error('El balotaje B/N debe indicar primer, segundo o tercer trámite.')
      const procedureNumber = payload.ballotType === 'white_black' ? payload.procedureNumber ?? null : null
      const rows = this.mockBallots.get(meetingId) ?? []; const same = rows.filter(item => item.subject === payload.subject.trim() && item.procedureNumber === procedureNumber); same.filter(item => item.status === 'closed').forEach(item => { item.status = 'superseded' })
      const item: LodgeAnonymousBallot = { id: crypto.randomUUID(), meetingId, version: Math.max(0, ...same.map(value => value.version)) + 1, ...payload, procedureNumber, subject: payload.subject.trim(), attendeeCount, recountObservation: payload.recountObservation?.trim() || null, status: 'closed', recordedAtUtc: new Date().toISOString() }
      rows.push(item); this.mockBallots.set(meetingId, rows); return { ...item }
    }
    return this.postJson<LodgeAnonymousBallot>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/votaciones`, payload)
  }

  async generateMinuteExtract(meetingId: string): Promise<LodgeMinuteExtract> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId); const attendance = (await this.getAttendance(meetingId)).items; const ballots = (await this.getAnonymousBallots(meetingId)).items.filter(item => item.status === 'closed')
      const present = attendance.filter(item => item.status === 'present'), absent = attendance.filter(item => item.status === 'absent'), excused = attendance.filter(item => item.status === 'excused')
      const ballotLines = ballots.length ? ballots.map((item, index) => `${index + 1}. ${procedureLabel(item.procedureNumber)}${item.subject}: ${item.ballotType === 'white_black' ? 'blancas' : 'positivos'} ${item.positiveCount}; ${item.ballotType === 'white_black' ? 'negras' : 'negativos'} ${item.negativeCount}; habilitados ${item.eligibleCount}; contabilizados ${item.positiveCount + item.negativeCount}.`).join('\n') : 'Sin balotajes o votaciones registrados.'
      const content = `EXTRACTO DE ACTA\nTaller: Taller Demostrativo Nº 23\nFecha: ${meeting.meetingDate} · Tipo: ${meeting.meetingType} · Grado: ${meeting.grade}\n\nASISTENCIA\nPresentes: ${present.length} · Inasistentes: ${absent.length} · Excusados: ${excused.length} · Total registrado: ${attendance.length}\nPresentes: ${present.map(item => item.displayName).join(', ') || 'Sin registros'}\nExcusas: ${excused.map(item => item.displayName).join(', ') || 'Sin registros'}\n\nBALOTAJE Y VOTACIONES\n${ballotLines}\n\nApertura: __________ · Acta anterior: __________ · Correspondencia: __________ · Decretos: __________\nTrabajo presentado: __________ · Aportes: __________ · Bien general: __________\nTronco de beneficencia: __________ · Clausura: __________ · Cierre de cadena: __________\nFirmas: Venerable Maestro/a · Secretario/a · Orador/a`
      return { meetingId, attendeeCount: present.length, absentCount: absent.length, excusedCount: excused.length, ballotCount: ballots.length, content }
    }
    return this.request<LodgeMinuteExtract>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/extracto-acta`)
  }

  async getInstructions(organizationId: string): Promise<LodgeInstructionsResponse> {
    if (this.useMocks) {
      const items = this.mockInstructions.filter(item => item.organizationId === organizationId).sort((a, b) => b.instructionDate.localeCompare(a.instructionDate)).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<LodgeInstructionsResponse>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/instrucciones`)
  }

  async createInstruction(organizationId: string, payload: CreateLodgeInstructionRequest): Promise<LodgeInstruction> {
    if (this.useMocks) {
      const responsibleOffice = payload.grade === 'apprentice' ? 'second_warden' : payload.grade === 'fellowcraft' ? 'first_warden' : 'immediate_past_master'
      const instruction: LodgeInstruction = { id: crypto.randomUUID(), organizationId, instructionDate: payload.instructionDate, grade: payload.grade, topic: payload.topic.trim(), responsibleOffice, instructorMemberId: payload.instructorMemberId ?? null, status: 'scheduled' }
      this.mockInstructions.unshift(instruction)
      return { ...instruction }
    }
    return this.postJson<LodgeInstruction>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/instrucciones`, payload)
  }

  async completeInstruction(instructionId: string): Promise<LodgeInstruction> {
    if (this.useMocks) {
      const instruction = this.mockInstructions.find(item => item.id === instructionId)
      if (!instruction) throw new Error('La instrucción indicada no existe.')
      if (instruction.status !== 'scheduled') throw new Error('La instrucción no se encuentra programada.')
      instruction.status = 'held'
      return { ...instruction }
    }
    return this.request<LodgeInstruction>(`/api/gestion-logial/instrucciones/${encodeURIComponent(instructionId)}/realizar`, { method: 'POST' })
  }

  async recordInstructionAttendance(instructionId: string, items: LodgeInstructionAttendanceItem[]): Promise<{ instructionId: string; recorded: number }> {
    if (this.useMocks) {
      const instruction = this.mockInstructions.find(item => item.id === instructionId)
      if (!instruction) throw new Error('La instrucción indicada no existe.')
      if (instruction.status !== 'held') throw new Error('La asistencia sólo puede registrarse después de realizar la instrucción.')
      this.mockInstructionAttendance.set(instructionId, items.map(item => ({ ...item })))
      return { instructionId, recorded: items.length }
    }
    return this.postJson<{ instructionId: string; recorded: number }>(`/api/gestion-logial/instrucciones/${encodeURIComponent(instructionId)}/asistencia`, { items })
  }

  private requireMeeting(id: string): LodgeMeeting {
    const meeting = this.mockMeetings.find(item => item.id === id)
    if (!meeting) throw new Error('La tenida indicada no existe.')
    return meeting
  }

  private postJson<T>(path: string, payload: unknown): Promise<T> {
    return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) })
  }

  private putJson<T>(path:string,payload:unknown):Promise<T>{ return this.request<T>(path,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)}) }

  private async authorizedFetch(path:string,init:RequestInit={},accept='application/json'):Promise<Response>{
    const headers=new Headers(init.headers); headers.set('Accept',accept)
    const token=await this.getAccessToken?.(); if(!token) throw new Error('Debe ingresar para operar Gestión Logial.')
    headers.set('Authorization',`Bearer ${token}`)
    const response=await fetch(`${this.baseUrl}${path}`,{...init,credentials:'omit',redirect:'error',cache:'no-store',headers})
    if(!response.ok){ if(response.status===401) await this.onUnauthorized?.(); let message=''; try{ const body=await response.clone().json() as {message?:string}; message=body.message??'' }catch{/* sin JSON */} if(response.status===403) message='Su cuenta no tiene permiso para esta operación de Secretaría.'; throw new LodgeApiHttpError(response.status,message||`La API respondió ${response.status} ${response.statusText}.`) }
    return response
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para operar Gestión Logial.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* sin cuerpo JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para operar la Gestión Logial de este Taller.'
      throw new LodgeApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultLodgeApiClient(getAccessToken?: LodgeAccessTokenProvider, onUnauthorized?: () => Promise<void>): LodgeApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Gestión Logial debe usar el mismo origen mediante el proxy institucional.')
  return new LodgeApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function procedureLabel(value: 1 | 2 | 3 | null) { return value === 1 ? 'Primer trámite · ' : value === 2 ? 'Segundo trámite · ' : value === 3 ? 'Tercer trámite · ' : '' }
