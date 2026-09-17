export type CouncilSessionStatus = 'scheduled' | 'closed' | 'cancelled'
export type CouncilAttendanceStatus = 'present' | 'absent' | 'excused'
export type CouncilParticipationType = 'member' | 'guest'
export type CouncilDecisionOutcome = 'approved' | 'rejected' | 'approved_for_referral' | 'recorded'
export type CouncilDecisionCategory = 'handover' | 'financial_control' | 'dues_relief' | 'benevolence_aid_proposal' | 'budget_proposal' | 'instruction_program_proposal' | 'officer_change_proposal' | 'forced_withdrawal_proposal' | 'other'
export type CouncilControlArea = 'treasury' | 'hospitalaria' | 'instruction_columns'

export interface LodgeCouncilSession {
  id: string
  organizationId: string
  sessionDate: string
  title: string | null
  status: CouncilSessionStatus
  qualifiedQuorumConfirmed: boolean
  quorumConfirmedBySubject: string | null
  quorumConfirmedAtUtc: string | null
  createdAtUtc: string
  closedAtUtc: string | null
}

export interface LodgeCouncilAttendance {
  id: string
  sessionId: string
  memberId: string | null
  displayName: string
  institutionalRole: string | null
  participationType: CouncilParticipationType
  status: CouncilAttendanceStatus
  hasVoice: boolean
  hasVote: boolean
  recordedAtUtc: string
}

export interface LodgeCouncilDecision {
  id: string
  sessionId: string
  category: CouncilDecisionCategory
  subject: string
  resolution: string
  outcome: CouncilDecisionOutcome
  requiresChamberReview: boolean
  chamberReference: string | null
  supportingDocumentId: string | null
  amount: number | null
  recordedAtUtc: string
}

export interface LodgeCouncilFinancialReview {
  id: string
  sessionId: string
  controlArea: CouncilControlArea
  periodLabel: string
  conclusion: string
  observations: string | null
  supportingDocumentId: string | null
  recordedAtUtc: string
}

export interface LodgeCouncilQuorumResponse {
  sessionId: string
  confirmed: boolean
  presentVotingMembers: number
  confirmedAtUtc: string | null
}

export interface CreateCouncilSessionRequest { sessionDate: string; title?: string | null }
export interface CouncilAttendanceRequest { memberId?: string | null; displayName?: string | null; institutionalRole?: string | null; participationType: CouncilParticipationType; status: CouncilAttendanceStatus; hasVoice?: boolean | null }
export interface CouncilDecisionRequest { category: CouncilDecisionCategory; subject: string; resolution: string; outcome: CouncilDecisionOutcome; chamberReference?: string | null; supportingDocumentId?: string | null; amount?: number | null }
export interface CouncilFinancialReviewRequest { controlArea: CouncilControlArea; periodLabel: string; conclusion: string; observations?: string | null; supportingDocumentId?: string | null }

export type CouncilAccessTokenProvider = () => Promise<string | null>

interface Options {
  baseUrl?: string
  getAccessToken?: CouncilAccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

const DEMO_LODGE_23_ID = '23232323-2323-2323-2323-232323232323'
const DEMO_SESSION_ID = '91919191-2323-2323-2323-000000000001'

const demoSession: LodgeCouncilSession = {
  id: DEMO_SESSION_ID,
  organizationId: DEMO_LODGE_23_ID,
  sessionDate: '2026-09-10',
  title: 'Consejo de Administración · septiembre',
  status: 'scheduled',
  qualifiedQuorumConfirmed: true,
  quorumConfirmedBySubject: 'demo-secretaria',
  quorumConfirmedAtUtc: '2026-09-10T22:05:00Z',
  createdAtUtc: '2026-09-01T15:00:00Z',
  closedAtUtc: null,
}

const demoAttendance: LodgeCouncilAttendance[] = [
  ['01', 'Venerable Maestro', 'lodge_venerable'],
  ['02', 'Inmediato Ex-Venerable Maestro', 'lodge_past_master'],
  ['03', 'Primer Vigilante', 'lodge_first_warden'],
  ['04', 'Segundo Vigilante', 'lodge_second_warden'],
  ['05', 'Orador/a', 'lodge_orator'],
  ['06', 'Secretario/a', 'lodge_secretariat'],
  ['07', 'Tesorero/a', 'lodge_treasury'],
  ['08', 'Hospitalario/a', 'lodge_hospitalaria'],
].map(([suffix, displayName, institutionalRole]) => ({
  id: `92929292-2323-2323-2323-0000000000${suffix}`,
  sessionId: DEMO_SESSION_ID,
  memberId: `aaaaaaaa-9999-9999-9999-0000000000${suffix}`,
  displayName: `${displayName} · demo`,
  institutionalRole,
  participationType: 'member' as const,
  status: 'present' as const,
  hasVoice: true,
  hasVote: true,
  recordedAtUtc: '2026-09-10T22:00:00Z',
}))

const demoDecisions: LodgeCouncilDecision[] = [
  {
    id: '93939393-2323-2323-2323-000000000001', sessionId: DEMO_SESSION_ID,
    category: 'financial_control', subject: 'Revisión mensual de cuentas',
    resolution: 'Se deja constancia de la revisión demostrativa de Tesorería y Hospitalaria.', outcome: 'recorded',
    requiresChamberReview: false, chamberReference: null, supportingDocumentId: null, amount: null,
    recordedAtUtc: '2026-09-10T22:20:00Z',
  },
  {
    id: '93939393-2323-2323-2323-000000000002', sessionId: DEMO_SESSION_ID,
    category: 'budget_proposal', subject: 'Ajuste presupuestario anual',
    resolution: 'Propuesta aprobada por el Consejo para su remisión a Cámara del Medio.', outcome: 'approved_for_referral',
    requiresChamberReview: true, chamberReference: null, supportingDocumentId: null, amount: 250000,
    recordedAtUtc: '2026-09-10T22:35:00Z',
  },
]

const demoReviews: LodgeCouncilFinancialReview[] = [
  {
    id: '94949494-2323-2323-2323-000000000001', sessionId: DEMO_SESSION_ID,
    controlArea: 'treasury', periodLabel: 'Agosto 2026', conclusion: 'Revisión demostrativa registrada sin observaciones críticas.',
    observations: 'Datos ficticios para validar la experiencia de usuario.', supportingDocumentId: null,
    recordedAtUtc: '2026-09-10T22:15:00Z',
  },
]

export class LodgeCouncilApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: CouncilAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockSessions: LodgeCouncilSession[] = [{ ...demoSession }]
  private readonly mockAttendance = new Map<string, LodgeCouncilAttendance[]>([[DEMO_SESSION_ID, demoAttendance.map(item => ({ ...item }))]])
  private readonly mockDecisions = new Map<string, LodgeCouncilDecision[]>([[DEMO_SESSION_ID, demoDecisions.map(item => ({ ...item }))]])
  private readonly mockReviews = new Map<string, LodgeCouncilFinancialReview[]>([[DEMO_SESSION_ID, demoReviews.map(item => ({ ...item }))]])

  constructor(options: Options = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getSessions(organizationId: string) {
    if (this.useMocks) {
      const items = this.mockSessions.filter(item => item.organizationId === organizationId).sort((a, b) => b.sessionDate.localeCompare(a.sessionDate)).map(item => ({ ...item }))
      return { total: items.length, items }
    }
    return this.request<{ total: number; items: LodgeCouncilSession[] }>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/consejo/sesiones`)
  }

  async createSession(organizationId: string, payload: CreateCouncilSessionRequest) {
    if (this.useMocks) {
      const item: LodgeCouncilSession = { id: crypto.randomUUID(), organizationId, sessionDate: payload.sessionDate, title: payload.title?.trim() || null, status: 'scheduled', qualifiedQuorumConfirmed: false, quorumConfirmedBySubject: null, quorumConfirmedAtUtc: null, createdAtUtc: new Date().toISOString(), closedAtUtc: null }
      this.mockSessions.unshift(item); return { ...item }
    }
    return this.postJson<LodgeCouncilSession>(`/api/gestion-logial/talleres/${encodeURIComponent(organizationId)}/consejo/sesiones`, payload)
  }

  async getAttendance(sessionId: string) {
    if (this.useMocks) { const items = (this.mockAttendance.get(sessionId) ?? []).map(item => ({ ...item })); return { total: items.length, items } }
    return this.request<{ total: number; items: LodgeCouncilAttendance[] }>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/asistencia`)
  }

  async recordAttendance(sessionId: string, payload: CouncilAttendanceRequest) {
    if (this.useMocks) {
      const item: LodgeCouncilAttendance = { id: crypto.randomUUID(), sessionId, memberId: payload.memberId ?? null, displayName: payload.displayName?.trim() || 'Integrante demo', institutionalRole: payload.institutionalRole ?? null, participationType: payload.participationType, status: payload.status, hasVoice: payload.hasVoice ?? true, hasVote: payload.participationType === 'member', recordedAtUtc: new Date().toISOString() }
      const rows = this.mockAttendance.get(sessionId) ?? []; rows.push(item); this.mockAttendance.set(sessionId, rows); return { ...item }
    }
    return this.postJson<LodgeCouncilAttendance>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/asistencia`, payload)
  }

  async confirmQuorum(sessionId: string) {
    if (this.useMocks) {
      const session = this.requireSession(sessionId); const attendance = (this.mockAttendance.get(sessionId) ?? []).filter(item => item.status === 'present' && item.hasVote)
      session.qualifiedQuorumConfirmed = true; session.quorumConfirmedAtUtc = new Date().toISOString(); session.quorumConfirmedBySubject = 'demo-secretaria'
      return { sessionId, confirmed: true, presentVotingMembers: attendance.length, confirmedAtUtc: session.quorumConfirmedAtUtc } satisfies LodgeCouncilQuorumResponse
    }
    return this.postJson<LodgeCouncilQuorumResponse>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/confirmar-quorum`, { confirmed: true })
  }

  async getDecisions(sessionId: string) {
    if (this.useMocks) { const items = (this.mockDecisions.get(sessionId) ?? []).map(item => ({ ...item })); return { total: items.length, items } }
    return this.request<{ total: number; items: LodgeCouncilDecision[] }>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/acuerdos`)
  }

  async recordDecision(sessionId: string, payload: CouncilDecisionRequest) {
    if (this.useMocks) {
      const requiresChamberReview = ['budget_proposal', 'instruction_program_proposal', 'officer_change_proposal', 'forced_withdrawal_proposal'].includes(payload.category)
      const item: LodgeCouncilDecision = { id: crypto.randomUUID(), sessionId, category: payload.category, subject: payload.subject.trim(), resolution: payload.resolution.trim(), outcome: payload.outcome, requiresChamberReview, chamberReference: payload.chamberReference?.trim() || null, supportingDocumentId: payload.supportingDocumentId ?? null, amount: payload.amount ?? null, recordedAtUtc: new Date().toISOString() }
      const rows = this.mockDecisions.get(sessionId) ?? []; rows.unshift(item); this.mockDecisions.set(sessionId, rows); return { ...item }
    }
    return this.postJson<LodgeCouncilDecision>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/acuerdos`, payload)
  }

  async getReviews(sessionId: string) {
    if (this.useMocks) { const items = (this.mockReviews.get(sessionId) ?? []).map(item => ({ ...item })); return { total: items.length, items } }
    return this.request<{ total: number; items: LodgeCouncilFinancialReview[] }>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/revisiones`)
  }

  async recordReview(sessionId: string, payload: CouncilFinancialReviewRequest) {
    if (this.useMocks) {
      const item: LodgeCouncilFinancialReview = { id: crypto.randomUUID(), sessionId, controlArea: payload.controlArea, periodLabel: payload.periodLabel.trim(), conclusion: payload.conclusion.trim(), observations: payload.observations?.trim() || null, supportingDocumentId: payload.supportingDocumentId ?? null, recordedAtUtc: new Date().toISOString() }
      const rows = this.mockReviews.get(sessionId) ?? []; rows.unshift(item); this.mockReviews.set(sessionId, rows); return { ...item }
    }
    return this.postJson<LodgeCouncilFinancialReview>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/revisiones`, payload)
  }

  async closeSession(sessionId: string) {
    if (this.useMocks) { const session = this.requireSession(sessionId); if (!session.qualifiedQuorumConfirmed) throw new Error('Debe confirmar quórum calificado antes de cerrar.'); session.status = 'closed'; session.closedAtUtc = new Date().toISOString(); return { ...session } }
    return this.request<LodgeCouncilSession>(`/api/gestion-logial/consejo/sesiones/${encodeURIComponent(sessionId)}/cerrar`, { method: 'POST' })
  }

  private requireSession(id: string) { const item = this.mockSessions.find(session => session.id === id); if (!item) throw new Error('La sesión de Consejo indicada no existe.'); return item }
  private postJson<T>(path: string, payload: unknown) { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para operar el Consejo de Administración.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''; try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* sin JSON */ }
      if (response.status === 403) message = 'Su cargo no tiene permiso para operar el Consejo de Administración de este Taller.'
      throw new Error(message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultLodgeCouncilApiClient(getAccessToken?: CouncilAccessTokenProvider, onUnauthorized?: () => Promise<void>) {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Consejo de Administración debe usar el mismo origen mediante el proxy institucional.')
  return new LodgeCouncilApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}
