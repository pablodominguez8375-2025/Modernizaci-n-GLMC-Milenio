export type LodgeMeetingType = 'regular' | 'solemn' | 'instruction' | 'anniversary' | 'funeral' | 'special'
export type LodgeGrade = 'apprentice' | 'fellowcraft' | 'master' | 'all'
export type LodgeMeetingStatus = 'scheduled' | 'open' | 'closed' | 'cancelled'
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
  title: string | null
  status: LodgeMeetingStatus
  createdAtUtc: string
  closedAtUtc: string | null
}
export interface LodgeMeetingsResponse { total: number; items: LodgeMeeting[] }
export interface CreateLodgeMeetingRequest { meetingDate: string; meetingType: LodgeMeetingType; grade: LodgeGrade; title?: string | null }
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
export interface LodgeAnonymousBallot { id: string; meetingId: string; version: number; ballotType: LodgeBallotType; subject: string; attendeeCount: number; eligibleCount: number; positiveCount: number; negativeCount: number; recountObservation: string | null; status: 'closed' | 'superseded'; recordedAtUtc: string }
export interface LodgeAnonymousBallotRequest { ballotType: LodgeBallotType; subject: string; eligibleCount: number; positiveCount: number; negativeCount: number; recountObservation?: string | null }
export interface LodgeAnonymousBallotsResponse { total: number; items: LodgeAnonymousBallot[] }
export interface LodgeMinuteExtract { meetingId: string; attendeeCount: number; absentCount: number; excusedCount: number; ballotCount: number; content: string }
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
      grade: 'all' as const, title: 'Tenida Ordinaria · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-01T15:00:00Z', closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0026-0000-000000000002', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-26', meetingType: 'instruction' as const,
      grade: 'all' as const, title: 'Tenida de Instrucción · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-02T15:00:00Z', closedAtUtc: null,
    },
    {
      id: 'bbbbbbbb-2309-0005-0000-000000000003', organizationId: DEMO_LODGE_23_ID, meetingDate: '2026-09-05', meetingType: 'regular' as const,
      grade: 'all' as const, title: 'Tenida Ordinaria anterior · demo', status: 'closed' as const, createdAtUtc: '2026-08-25T15:00:00Z', closedAtUtc: '2026-09-06T01:20:00Z',
    },
    {
      id: 'bbbbbbbb-0109-0019-0000-000000000004', organizationId: DEMO_LODGE_1_ID, meetingDate: '2026-09-19', meetingType: 'solemn' as const,
      grade: 'all' as const, title: 'Tenida Solemne · demo', status: 'scheduled' as const, createdAtUtc: '2026-09-03T15:00:00Z', closedAtUtc: null,
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
  ballots: [{ id: 'ffffffff-0001-0001-0001-000000000001', meetingId: 'bbbbbbbb-2309-0005-0000-000000000003', version: 1, ballotType: 'white_black' as const, subject: 'Admisión de Persona Demostrativa', attendeeCount: 2, eligibleCount: 2, positiveCount: 2, negativeCount: 0, recountObservation: null, status: 'closed' as const, recordedAtUtc: '2026-09-06T01:15:00Z' }] satisfies LodgeAnonymousBallot[],
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
      const meeting: LodgeMeeting = {
        id: crypto.randomUUID(),
        organizationId,
        meetingDate: payload.meetingDate,
        meetingType: payload.meetingType,
        grade: payload.grade,
        title: payload.title?.trim() || null,
        status: 'scheduled',
        createdAtUtc: new Date().toISOString(),
        closedAtUtc: null,
      }
      this.mockMeetings.unshift(meeting)
      return { ...meeting }
    }
    return this.postJson<LodgeMeeting>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/tenidas`, payload)
  }

  async closeMeeting(meetingId: string): Promise<LodgeMeeting> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId)
      if (meeting.status === 'closed') throw new Error('La tenida ya se encuentra cerrada.')
      meeting.status = 'closed'; meeting.closedAtUtc = new Date().toISOString()
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
      if (meeting.status !== 'closed') throw new Error('La asistencia sólo puede registrarse después de cerrar la tenida realizada.')
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
      const meeting = this.requireMeeting(meetingId); if (meeting.status !== 'closed') throw new Error('El escrutinio sólo puede registrarse después de cerrar la Tenida realizada.')
      const attendeeCount = (await this.getAttendance(meetingId)).items.filter(item => item.status === 'present').length
      if (payload.eligibleCount > attendeeCount) throw new Error('Las personas habilitadas no pueden superar a las asistentes presentes.')
      if (payload.positiveCount + payload.negativeCount !== payload.eligibleCount && !payload.recountObservation?.trim()) throw new Error('La diferencia del recuento debe explicarse en el acta.')
      const rows = this.mockBallots.get(meetingId) ?? []; const same = rows.filter(item => item.subject === payload.subject.trim()); same.filter(item => item.status === 'closed').forEach(item => { item.status = 'superseded' })
      const item: LodgeAnonymousBallot = { id: crypto.randomUUID(), meetingId, version: Math.max(0, ...same.map(value => value.version)) + 1, ...payload, subject: payload.subject.trim(), attendeeCount, recountObservation: payload.recountObservation?.trim() || null, status: 'closed', recordedAtUtc: new Date().toISOString() }
      rows.push(item); this.mockBallots.set(meetingId, rows); return { ...item }
    }
    return this.postJson<LodgeAnonymousBallot>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/votaciones`, payload)
  }

  async generateMinuteExtract(meetingId: string): Promise<LodgeMinuteExtract> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId); const attendance = (await this.getAttendance(meetingId)).items; const ballots = (await this.getAnonymousBallots(meetingId)).items.filter(item => item.status === 'closed')
      const present = attendance.filter(item => item.status === 'present'), absent = attendance.filter(item => item.status === 'absent'), excused = attendance.filter(item => item.status === 'excused')
      const ballotLines = ballots.length ? ballots.map((item, index) => `${index + 1}. ${item.subject}: ${item.ballotType === 'white_black' ? 'blancas' : 'positivos'} ${item.positiveCount}; ${item.ballotType === 'white_black' ? 'negras' : 'negativos'} ${item.negativeCount}; habilitados ${item.eligibleCount}; contabilizados ${item.positiveCount + item.negativeCount}.`).join('\n') : 'Sin balotajes o votaciones registrados.'
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
