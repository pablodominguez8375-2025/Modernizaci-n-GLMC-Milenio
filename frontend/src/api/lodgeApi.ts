export type LodgeMeetingType = 'regular' | 'solemn' | 'instruction' | 'anniversary' | 'funeral' | 'special'
export type LodgeGrade = 'apprentice' | 'fellowcraft' | 'master' | 'all'
export type LodgeMeetingStatus = 'scheduled' | 'open' | 'closed' | 'cancelled'
export type LodgeAttendanceStatus = 'present' | 'excused' | 'absent'
export type LodgeMinuteStatus = 'draft' | 'approved' | 'superseded'
export type LodgeCorrespondenceDirection = 'incoming' | 'outgoing'
export type LodgeCorrespondenceChannel = 'email' | 'letter' | 'platform' | 'other'
export type LodgeCorrespondenceStatus = 'registered' | 'processed' | 'archived'
export type LodgeSecretariatPriority = 'low' | 'normal' | 'high'
export type LodgeSecretariatTaskStatus = 'open' | 'done' | 'cancelled'
export type LodgeAgendaStatus = 'pending' | 'addressed' | 'deferred'

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
export interface LodgeMinutesResponse { total: number; items: LodgeMinute[] }

export interface LodgeCorrespondenceRecord {
  id: string
  organizationId: string
  folio: string
  direction: LodgeCorrespondenceDirection
  correspondenceDate: string
  subject: string
  counterparty: string
  channel: LodgeCorrespondenceChannel
  externalReference: string | null
  status: LodgeCorrespondenceStatus
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}
export interface LodgeCorrespondenceResponse { total: number; items: LodgeCorrespondenceRecord[] }
export interface CreateLodgeCorrespondenceRequest {
  folio: string
  direction: LodgeCorrespondenceDirection
  correspondenceDate: string
  subject: string
  counterparty: string
  channel: LodgeCorrespondenceChannel
  externalReference?: string | null
  notes?: string | null
}

export interface LodgeSecretariatTask {
  id: string
  organizationId: string
  title: string
  detail: string | null
  dueDate: string | null
  priority: LodgeSecretariatPriority
  responsibleLabel: string | null
  status: LodgeSecretariatTaskStatus
  createdAtUtc: string
  completedAtUtc: string | null
}
export interface LodgeSecretariatTasksResponse { total: number; items: LodgeSecretariatTask[] }
export interface CreateLodgeSecretariatTaskRequest {
  title: string
  detail?: string | null
  dueDate?: string | null
  priority: LodgeSecretariatPriority
  responsibleLabel?: string | null
}

export interface LodgeMeetingAgendaItem {
  id: string
  meetingId: string
  organizationId: string
  position: number
  title: string
  detail: string | null
  status: LodgeAgendaStatus
  createdAtUtc: string
  updatedAtUtc: string | null
}
export interface LodgeMeetingAgendaResponse { total: number; items: LodgeMeetingAgendaItem[] }
export interface CreateLodgeMeetingAgendaItemRequest { title: string; detail?: string | null }

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
} as const

export const demoLodgeSecretariatSeed = {
  correspondence: [
    {
      id: 'eeeeeeee-0001-0001-0001-000000000001', organizationId: DEMO_LODGE_23_ID, folio: 'SEC-2026-014', direction: 'incoming' as const,
      correspondenceDate: '2026-09-08', subject: 'Circular institucional demostrativa', counterparty: 'Gran Secretaría · demo', channel: 'platform' as const,
      externalReference: 'CIRC-DEMO-091', status: 'registered' as const, notes: 'Revisar en próxima reunión de Secretaría. Dato ficticio.',
      createdAtUtc: '2026-09-08T14:30:00Z', updatedAtUtc: null,
    },
    {
      id: 'eeeeeeee-0002-0002-0002-000000000002', organizationId: DEMO_LODGE_23_ID, folio: 'SEC-2026-013', direction: 'outgoing' as const,
      correspondenceDate: '2026-09-04', subject: 'Acuse de recibo demostrativo', counterparty: 'Órgano institucional · demo', channel: 'email' as const,
      externalReference: null, status: 'processed' as const, notes: null, createdAtUtc: '2026-09-04T18:15:00Z', updatedAtUtc: '2026-09-04T19:10:00Z',
    },
  ] satisfies LodgeCorrespondenceRecord[],
  tasks: [
    {
      id: 'ffffffff-0001-0001-0001-000000000001', organizationId: DEMO_LODGE_23_ID, title: 'Preparar tabla de la próxima Tenida · demo',
      detail: 'Consolidar puntos administrativos e instrucción.', dueDate: '2026-09-11', priority: 'high' as const, responsibleLabel: 'Secretaría',
      status: 'open' as const, createdAtUtc: '2026-09-07T16:00:00Z', completedAtUtc: null,
    },
    {
      id: 'ffffffff-0002-0002-0002-000000000002', organizationId: DEMO_LODGE_23_ID, title: 'Confirmar recepción de correspondencia · demo',
      detail: null, dueDate: '2026-09-15', priority: 'normal' as const, responsibleLabel: 'Secretaría', status: 'open' as const,
      createdAtUtc: '2026-09-08T16:00:00Z', completedAtUtc: null,
    },
  ] satisfies LodgeSecretariatTask[],
  agenda: [
    {
      id: 'abababab-0001-0001-0001-000000000001', meetingId: demoLodgeSeed.meetings[0].id, organizationId: DEMO_LODGE_23_ID,
      position: 1, title: 'Apertura y lectura del acta anterior · demo', detail: null, status: 'pending' as const, createdAtUtc: '2026-09-08T17:00:00Z', updatedAtUtc: null,
    },
    {
      id: 'abababab-0002-0002-0002-000000000002', meetingId: demoLodgeSeed.meetings[0].id, organizationId: DEMO_LODGE_23_ID,
      position: 2, title: 'Cuenta de Secretaría · demo', detail: 'Correspondencia recibida y enviada de la semana.', status: 'pending' as const, createdAtUtc: '2026-09-08T17:02:00Z', updatedAtUtc: null,
    },
    {
      id: 'abababab-0003-0003-0003-000000000003', meetingId: demoLodgeSeed.meetings[0].id, organizationId: DEMO_LODGE_23_ID,
      position: 3, title: 'Punto de instrucción · demo', detail: 'Tema demostrativo sujeto a grado y permisos.', status: 'pending' as const, createdAtUtc: '2026-09-08T17:04:00Z', updatedAtUtc: null,
    },
  ] satisfies LodgeMeetingAgendaItem[],
} as const

export class LodgeApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: LodgeAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockMeetings: LodgeMeeting[] = demoLodgeSeed.meetings.map(item => ({ ...item }))
  private readonly mockAttendance = new Map<string, LodgeAttendanceCurrent[]>([[demoLodgeSeed.meetings[2].id, demoLodgeSeed.attendance.map(item => ({ ...item }))]])
  private readonly mockMinutes = new Map<string, LodgeMinute[]>([[demoLodgeSeed.meetings[2].id, [{ ...demoLodgeSeed.minute }]]])
  private readonly mockCorrespondence: LodgeCorrespondenceRecord[] = demoLodgeSecretariatSeed.correspondence.map(item => ({ ...item }))
  private readonly mockSecretariatTasks: LodgeSecretariatTask[] = demoLodgeSecretariatSeed.tasks.map(item => ({ ...item }))
  private readonly mockAgenda = new Map<string, LodgeMeetingAgendaItem[]>([[demoLodgeSeed.meetings[0].id, demoLodgeSecretariatSeed.agenda.map(item => ({ ...item }))]])

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
        id: crypto.randomUUID(), organizationId, meetingDate: payload.meetingDate, meetingType: payload.meetingType, grade: payload.grade,
        title: payload.title?.trim() || null, status: 'scheduled', createdAtUtc: new Date().toISOString(), closedAtUtc: null,
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
      if (meeting.status === 'closed' || meeting.status === 'cancelled') throw new Error('No se puede registrar asistencia en una tenida cerrada o cancelada.')
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

  async getCorrespondence(organizationId: string): Promise<LodgeCorrespondenceResponse> {
    if (this.useMocks) {
      const items = this.mockCorrespondence.filter(item => item.organizationId === organizationId)
        .sort((a, b) => b.correspondenceDate.localeCompare(a.correspondenceDate)).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<LodgeCorrespondenceResponse>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/correspondencia`)
  }

  async createCorrespondence(organizationId: string, payload: CreateLodgeCorrespondenceRequest): Promise<LodgeCorrespondenceRecord> {
    if (this.useMocks) {
      if (this.mockCorrespondence.some(item => item.organizationId === organizationId && item.folio === payload.folio.trim())) throw new Error('El folio ya existe en este Taller.')
      const item: LodgeCorrespondenceRecord = {
        id: crypto.randomUUID(), organizationId, folio: payload.folio.trim(), direction: payload.direction, correspondenceDate: payload.correspondenceDate,
        subject: payload.subject.trim(), counterparty: payload.counterparty.trim(), channel: payload.channel,
        externalReference: payload.externalReference?.trim() || null, status: 'registered', notes: payload.notes?.trim() || null,
        createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
      }
      this.mockCorrespondence.unshift(item)
      return { ...item }
    }
    return this.postJson<LodgeCorrespondenceRecord>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/correspondencia`, payload)
  }

  async updateCorrespondenceStatus(organizationId: string, recordId: string, status: LodgeCorrespondenceStatus): Promise<LodgeCorrespondenceRecord> {
    if (this.useMocks) {
      const item = this.mockCorrespondence.find(row => row.id === recordId && row.organizationId === organizationId)
      if (!item) throw new Error('La correspondencia indicada no existe.')
      item.status = status; item.updatedAtUtc = new Date().toISOString()
      return { ...item }
    }
    return this.postJson<LodgeCorrespondenceRecord>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/correspondencia/${encodeURIComponent(recordId)}/estado`, { status })
  }

  async getSecretariatTasks(organizationId: string): Promise<LodgeSecretariatTasksResponse> {
    if (this.useMocks) {
      const items = this.mockSecretariatTasks.filter(item => item.organizationId === organizationId)
        .sort((a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999')).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<LodgeSecretariatTasksResponse>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/pendientes`)
  }

  async createSecretariatTask(organizationId: string, payload: CreateLodgeSecretariatTaskRequest): Promise<LodgeSecretariatTask> {
    if (this.useMocks) {
      const task: LodgeSecretariatTask = {
        id: crypto.randomUUID(), organizationId, title: payload.title.trim(), detail: payload.detail?.trim() || null,
        dueDate: payload.dueDate || null, priority: payload.priority, responsibleLabel: payload.responsibleLabel?.trim() || null,
        status: 'open', createdAtUtc: new Date().toISOString(), completedAtUtc: null,
      }
      this.mockSecretariatTasks.unshift(task)
      return { ...task }
    }
    return this.postJson<LodgeSecretariatTask>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/pendientes`, payload)
  }

  async updateSecretariatTaskStatus(organizationId: string, taskId: string, status: LodgeSecretariatTaskStatus): Promise<LodgeSecretariatTask> {
    if (this.useMocks) {
      const item = this.mockSecretariatTasks.find(row => row.id === taskId && row.organizationId === organizationId)
      if (!item) throw new Error('El pendiente indicado no existe.')
      item.status = status; item.completedAtUtc = status === 'open' ? null : new Date().toISOString()
      return { ...item }
    }
    return this.postJson<LodgeSecretariatTask>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/secretaria/pendientes/${encodeURIComponent(taskId)}/estado`, { status })
  }

  async getMeetingAgenda(meetingId: string): Promise<LodgeMeetingAgendaResponse> {
    if (this.useMocks) {
      const items = [...(this.mockAgenda.get(meetingId) ?? [])].sort((a, b) => a.position - b.position).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<LodgeMeetingAgendaResponse>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/tabla`)
  }

  async createMeetingAgendaItem(meetingId: string, payload: CreateLodgeMeetingAgendaItemRequest): Promise<LodgeMeetingAgendaItem> {
    if (this.useMocks) {
      const meeting = this.requireMeeting(meetingId)
      if (meeting.status === 'closed' || meeting.status === 'cancelled') throw new Error('No se puede modificar la tabla de una Tenida cerrada o cancelada.')
      const items = this.mockAgenda.get(meetingId) ?? []
      const item: LodgeMeetingAgendaItem = {
        id: crypto.randomUUID(), meetingId, organizationId: meeting.organizationId, position: Math.max(0, ...items.map(row => row.position)) + 1,
        title: payload.title.trim(), detail: payload.detail?.trim() || null, status: 'pending', createdAtUtc: new Date().toISOString(), updatedAtUtc: null,
      }
      items.push(item); this.mockAgenda.set(meetingId, items)
      return { ...item }
    }
    return this.postJson<LodgeMeetingAgendaItem>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/tabla`, payload)
  }

  async updateMeetingAgendaStatus(meetingId: string, itemId: string, status: LodgeAgendaStatus): Promise<LodgeMeetingAgendaItem> {
    if (this.useMocks) {
      const items = this.mockAgenda.get(meetingId) ?? []
      const item = items.find(row => row.id === itemId)
      if (!item) throw new Error('El punto de tabla indicado no existe.')
      item.status = status; item.updatedAtUtc = new Date().toISOString()
      return { ...item }
    }
    return this.postJson<LodgeMeetingAgendaItem>(`/api/gestion-logial/tenidas/${encodeURIComponent(meetingId)}/tabla/${encodeURIComponent(itemId)}/estado`, { status })
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
