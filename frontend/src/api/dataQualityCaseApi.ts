import { type DataQualityIssue } from './internalAffairsApi'

export interface DataQualityCaseEvent {
  id: string
  action: string
  fromStatus: string | null
  toStatus: string
  actorDisplayName: string | null
  occurredAtUtc: string
}

export interface DataQualityCase {
  id: string
  ruleCode: string
  severity: string
  memberId: string
  institutionalNumber: string | null
  displayName: string
  organizationId: string | null
  organizationName: string | null
  detectionAsOf: string
  primaryDate: string | null
  relatedDate: string | null
  status: string
  assignedToDisplayName: string | null
  assignedToCurrentUser: boolean
  createdByDisplayName: string | null
  createdAtUtc: string
  updatedAtUtc: string
  resolutionSummary: string | null
  evidenceReference: string | null
  resolvedAtUtc: string | null
  events: DataQualityCaseEvent[]
}

export interface DataQualityCaseListResponse { total: number; returned: number; items: DataQualityCase[] }
export interface DataQualityCaseFilters { status?: string; ruleCode?: string; organizationId?: string; assignedToMe?: boolean; limit?: number }
export interface OpenCaseInput { detectionAsOf: string; issue: DataQualityIssue }
export interface ResolveCaseInput { outcome: 'confirmed' | 'dismissed'; resolutionSummary: string; evidenceReference?: string }

export type DataQualityCaseTokenProvider = () => Promise<string | null>
interface DataQualityCaseApiOptions { baseUrl?: string; getAccessToken?: DataQualityCaseTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

export class DataQualityCaseApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: DataQualityCaseTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: DataQualityCaseApiOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async listCases(filters: DataQualityCaseFilters = {}): Promise<DataQualityCaseListResponse> {
    if (this.useMocks) return mockList(filters)
    const query = new URLSearchParams()
    if (filters.status) query.set('status', filters.status)
    if (filters.ruleCode) query.set('ruleCode', filters.ruleCode)
    if (filters.organizationId) query.set('organizationId', filters.organizationId)
    if (filters.assignedToMe) query.set('assignedToMe', 'true')
    if (filters.limit) query.set('limit', String(filters.limit))
    return this.request<DataQualityCaseListResponse>(`/api/regimen-interior/data-quality/cases/${query.size ? `?${query}` : ''}`)
  }

  async getCase(caseId: string): Promise<DataQualityCase> {
    if (this.useMocks) {
      const item = mockCases.find(x => x.id === caseId)
      if (!item) throw new Error('El caso no existe.')
      return clone(item)
    }
    return this.request<DataQualityCase>(`/api/regimen-interior/data-quality/cases/${encodeURIComponent(caseId)}`)
  }

  async openCase(input: OpenCaseInput): Promise<DataQualityCase> {
    if (this.useMocks) return mockOpen(input)
    return this.request<DataQualityCase>('/api/regimen-interior/data-quality/cases/', {
      method: 'POST',
      body: JSON.stringify({
        detectionAsOf: input.detectionAsOf,
        ruleCode: input.issue.code,
        severity: input.issue.severity,
        memberId: input.issue.memberId,
        organizationId: input.issue.organizationId,
        primaryDate: input.issue.primaryDate,
        relatedDate: input.issue.relatedDate,
      }),
    })
  }

  async claimCase(caseId: string): Promise<DataQualityCase> {
    if (this.useMocks) return mockClaim(caseId)
    return this.request<DataQualityCase>(`/api/regimen-interior/data-quality/cases/${encodeURIComponent(caseId)}/claim`, { method: 'POST' })
  }

  async resolveCase(caseId: string, input: ResolveCaseInput): Promise<DataQualityCase> {
    if (this.useMocks) return mockResolve(caseId, input)
    return this.request<DataQualityCase>(`/api/regimen-interior/data-quality/cases/${encodeURIComponent(caseId)}/resolve`, {
      method: 'POST', body: JSON.stringify(input),
    })
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers({ Accept: 'application/json', ...(init.body ? { 'Content-Type': 'application/json' } : {}) })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para operar la cola de corroboración.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { method: init.method ?? 'GET', body: init.body, credentials: 'omit', cache: 'no-store', redirect: 'error', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = body.message ?? '' } catch { /* respuesta sin JSON */ }
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para operar la cola de corroboración.')
      throw new Error(message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultDataQualityCaseApiClient(getAccessToken?: DataQualityCaseTokenProvider, onUnauthorized?: () => Promise<void>): DataQualityCaseApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La cola de corroboración debe usar el mismo origen mediante el proxy institucional.')
  return new DataQualityCaseApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

let sequence = 10
let mockCases: DataQualityCase[] = [
  seedCase('case-qa-1', 'exaltation_before_wage_increase', 'error', '20202020-2020-2020-2020-202020202020', 'GLM-0230', 'Hermano Trasladado Demostrativo', '23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', 'open', false),
  seedCase('case-qa-2', 'target_membership_date_mismatch', 'warning', '20202020-2020-2020-2020-202020202020', 'GLM-0230', 'Hermano Trasladado Demostrativo', '23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', 'under_review', true),
  { ...seedCase('case-qa-3', 'duplicate_degree_milestone', 'warning', '40404040-4040-4040-4040-404040404040', 'GLM-0404', 'Hermano Histórico Demostrativo', '45454545-4545-4545-4545-454545454545', 'Taller Demostrativo Nº 45', 'resolved_confirmed', true), resolutionSummary: 'Se confirmó duplicidad de registro y quedó derivada para rectificación con respaldo.', evidenceReference: 'ACTA-QA-045-2026', resolvedAtUtc: '2026-09-08T18:30:00Z' },
]

function mockList(filters: DataQualityCaseFilters): DataQualityCaseListResponse {
  let items = mockCases.map(clone)
  if (filters.status) items = items.filter(x => x.status === filters.status)
  if (filters.ruleCode) items = items.filter(x => x.ruleCode === filters.ruleCode)
  if (filters.organizationId) items = items.filter(x => x.organizationId === filters.organizationId)
  if (filters.assignedToMe) items = items.filter(x => x.assignedToCurrentUser)
  items.sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc))
  const total = items.length
  items = items.slice(0, Math.max(1, Math.min(filters.limit ?? 200, 500)))
  return { total, returned: items.length, items }
}

function mockOpen({ detectionAsOf, issue }: OpenCaseInput): DataQualityCase {
  const active = mockCases.find(x => x.ruleCode === issue.code && x.memberId === issue.memberId && x.organizationId === issue.organizationId && (x.status === 'open' || x.status === 'under_review'))
  if (active) return clone(active)
  const now = new Date().toISOString()
  const item: DataQualityCase = {
    id: `case-qa-${++sequence}`,
    ruleCode: issue.code, severity: issue.severity, memberId: issue.memberId, institutionalNumber: issue.institutionalNumber,
    displayName: issue.displayName, organizationId: issue.organizationId, organizationName: issue.organizationName,
    detectionAsOf, primaryDate: issue.primaryDate, relatedDate: issue.relatedDate, status: 'open', assignedToDisplayName: null, assignedToCurrentUser: false,
    createdByDisplayName: 'Revisor QA', createdAtUtc: now, updatedAtUtc: now,
    resolutionSummary: null, evidenceReference: null, resolvedAtUtc: null,
    events: [event('opened', null, 'open', now)],
  }
  mockCases = [item, ...mockCases]
  return clone(item)
}

function mockClaim(caseId: string): DataQualityCase {
  const item = required(caseId)
  if (item.status === 'under_review' && item.assignedToCurrentUser) return clone(item)
  if (item.status !== 'open') throw new Error('Sólo un caso abierto puede ser tomado.')
  const now = new Date().toISOString(); item.status = 'under_review'; item.assignedToCurrentUser = true; item.assignedToDisplayName = 'Revisor QA'; item.updatedAtUtc = now; item.events.push(event('claimed', 'open', 'under_review', now)); return clone(item)
}

function mockResolve(caseId: string, input: ResolveCaseInput): DataQualityCase {
  const item = required(caseId)
  if (item.status !== 'under_review') throw new Error('El caso debe estar en revisión antes de resolverlo.')
  if (!item.assignedToCurrentUser) throw new Error('El caso está asignado a otra persona.')
  const now = new Date().toISOString(); const target = input.outcome === 'confirmed' ? 'resolved_confirmed' : 'dismissed'
  item.status = target; item.resolutionSummary = input.resolutionSummary; item.evidenceReference = input.evidenceReference || null; item.resolvedAtUtc = now; item.updatedAtUtc = now; item.events.push(event(target, 'under_review', target, now)); return clone(item)
}

function required(caseId: string) { const item = mockCases.find(x => x.id === caseId); if (!item) throw new Error('El caso no existe.'); return item }
function seedCase(id: string, ruleCode: string, severity: string, memberId: string, institutionalNumber: string, displayName: string, organizationId: string, organizationName: string, status: string, assignedToCurrentUser: boolean): DataQualityCase {
  const created = '2026-09-08T15:00:00Z'; const events = [event('opened', null, 'open', created)]; if (status !== 'open') events.push(event('claimed', 'open', 'under_review', '2026-09-08T16:00:00Z')); if (status === 'resolved_confirmed') events.push(event('resolved_confirmed', 'under_review', 'resolved_confirmed', '2026-09-08T18:30:00Z'))
  return { id, ruleCode, severity, memberId, institutionalNumber, displayName, organizationId, organizationName, detectionAsOf: '2026-09-09', primaryDate: '2026-01-01', relatedDate: '2025-12-31', status, assignedToDisplayName: status === 'open' ? null : 'Revisor QA', assignedToCurrentUser, createdByDisplayName: 'Revisor QA', createdAtUtc: created, updatedAtUtc: status === 'resolved_confirmed' ? '2026-09-08T18:30:00Z' : status === 'under_review' ? '2026-09-08T16:00:00Z' : created, resolutionSummary: null, evidenceReference: null, resolvedAtUtc: null, events }
}
function event(action: string, fromStatus: string | null, toStatus: string, occurredAtUtc: string): DataQualityCaseEvent { return { id: `event-${++sequence}`, action, fromStatus, toStatus, actorDisplayName: 'Revisor QA', occurredAtUtc } }
function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T }
