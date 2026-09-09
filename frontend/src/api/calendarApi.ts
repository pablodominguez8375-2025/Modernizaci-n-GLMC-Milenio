export type CalendarStatus = 'draft' | 'tentative' | 'confirmed' | 'completed' | 'cancelled'

export interface CalendarEvent {
  id: string
  title: string
  eventType: string
  startsAtUtc: string
  endsAtUtc: string
  timeZoneId: string
  locationDisplay: string | null
  spaceId: string | null
  organizationId: string | null
  visibility: string
  status: CalendarStatus | string
  sourceModule: string | null
  sourceEntityType: string | null
  sourceEntityId: string | null
  responsibleSubject: string | null
  isMasked: boolean
}

export interface CalendarResponse {
  fromUtc: string
  toUtc: string
  institutionalTimeZone: string
  events: CalendarEvent[]
}

export interface CalendarSpaceConflict {
  spaceId: string
  firstEventId: string
  firstTitle: string
  secondEventId: string
  secondTitle: string
  overlapStartsAtUtc: string
  overlapEndsAtUtc: string
}

export interface CalendarSpaceConflictResponse {
  fromUtc: string
  toUtc: string
  total: number
  conflicts: CalendarSpaceConflict[]
}

export type AccessTokenProvider = () => Promise<string | null>

export interface CalendarApiClientOptions {
  baseUrl?: string
  getAccessToken?: AccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

class CalendarApiHttpError extends Error {
  constructor(readonly status: number, message: string) {
    super(message)
    this.name = 'CalendarApiHttpError'
  }
}

const demoOrganizations = {
  libertad23: '23232323-2323-2323-2323-232323232323',
  igualdad1: '11111111-1111-1111-1111-111111111111',
}

const demoEvents: CalendarEvent[] = [
  {
    id: 'ca000001-0000-0000-0000-000000000001',
    title: 'Tenida Regular — Libertad Nº 23',
    eventType: 'lodge_meeting_day',
    startsAtUtc: '2026-09-10T04:00:00Z',
    endsAtUtc: '2026-09-11T04:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: null,
    spaceId: null,
    organizationId: demoOrganizations.libertad23,
    visibility: 'lodge',
    status: 'confirmed',
    sourceModule: 'lodge-management',
    sourceEntityType: 'lodge-meeting',
    sourceEntityId: 'demo-meeting-23',
    responsibleSubject: null,
    isMasked: false,
  },
  {
    id: 'ca000002-0000-0000-0000-000000000002',
    title: 'Docencia de Compañeros',
    eventType: 'lodge_instruction_day',
    startsAtUtc: '2026-09-12T04:00:00Z',
    endsAtUtc: '2026-09-13T04:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: null,
    spaceId: null,
    organizationId: demoOrganizations.libertad23,
    visibility: 'lodge',
    status: 'confirmed',
    sourceModule: 'lodge-management',
    sourceEntityType: 'lodge-instruction',
    sourceEntityId: 'demo-instruction-23',
    responsibleSubject: null,
    isMasked: false,
  },
  {
    id: 'ca000003-0000-0000-0000-000000000003',
    title: 'Ceremonia — Aumento de salario',
    eventType: 'ceremony_day',
    startsAtUtc: '2026-09-18T04:00:00Z',
    endsAtUtc: '2026-09-19T04:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: null,
    spaceId: null,
    organizationId: demoOrganizations.libertad23,
    visibility: 'restricted',
    status: 'confirmed',
    sourceModule: 'ceremonies',
    sourceEntityType: 'ceremony-request',
    sourceEntityId: 'demo-ceremony-23',
    responsibleSubject: null,
    isMasked: false,
  },
  {
    id: 'ca000004-0000-0000-0000-000000000004',
    title: 'Reserva Templo Principal — Ceremonia',
    eventType: 'ceremony_reservation',
    startsAtUtc: '2026-09-18T22:30:00Z',
    endsAtUtc: '2026-09-19T02:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: 'Templo Principal — Sede institucional',
    spaceId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    organizationId: demoOrganizations.libertad23,
    visibility: 'restricted',
    status: 'confirmed',
    sourceModule: 'grand-secretariat',
    sourceEntityType: 'space-reservation',
    sourceEntityId: 'demo-reservation-23',
    responsibleSubject: null,
    isMasked: false,
  },
  {
    id: 'ca000005-0000-0000-0000-000000000005',
    title: 'Ocupado',
    eventType: 'occupancy',
    startsAtUtc: '2026-09-20T21:00:00Z',
    endsAtUtc: '2026-09-21T00:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: 'Sala de Secretaría',
    spaceId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    organizationId: demoOrganizations.igualdad1,
    visibility: 'restricted',
    status: 'confirmed',
    sourceModule: null,
    sourceEntityType: null,
    sourceEntityId: null,
    responsibleSubject: null,
    isMasked: true,
  },
  {
    id: 'ca000006-0000-0000-0000-000000000006',
    title: 'Tenida Solemne — Aniversario',
    eventType: 'lodge_meeting_day',
    startsAtUtc: '2026-09-26T04:00:00Z',
    endsAtUtc: '2026-09-27T04:00:00Z',
    timeZoneId: 'America/Santiago',
    locationDisplay: null,
    spaceId: null,
    organizationId: demoOrganizations.igualdad1,
    visibility: 'lodge',
    status: 'tentative',
    sourceModule: 'lodge-management',
    sourceEntityType: 'lodge-meeting',
    sourceEntityId: 'demo-meeting-1',
    responsibleSubject: null,
    isMasked: false,
  },
]

export class CalendarApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: CalendarApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getCalendar(filters: { fromUtc: string; toUtc: string; organizationId?: string } ): Promise<CalendarResponse> {
    if (this.useMocks) {
      const from = new Date(filters.fromUtc).getTime()
      const to = new Date(filters.toUtc).getTime()
      const events = demoEvents.filter(event => {
        const inWindow = new Date(event.endsAtUtc).getTime() > from && new Date(event.startsAtUtc).getTime() < to
        const inOrganization = !filters.organizationId || event.organizationId === filters.organizationId || event.organizationId === null
        return inWindow && inOrganization
      })
      return { fromUtc: filters.fromUtc, toUtc: filters.toUtc, institutionalTimeZone: 'America/Santiago', events: events.map(event => ({ ...event })) }
    }

    const query = new URLSearchParams({ fromUtc: filters.fromUtc, toUtc: filters.toUtc })
    if (filters.organizationId) query.set('organizationId', filters.organizationId)
    return this.request<CalendarResponse>(`/api/calendar?${query}`)
  }

  async getSpaceConflicts(filters: { fromUtc: string; toUtc: string }): Promise<CalendarSpaceConflictResponse> {
    if (this.useMocks) return { fromUtc: filters.fromUtc, toUtc: filters.toUtc, total: 0, conflicts: [] }
    const query = new URLSearchParams({ fromUtc: filters.fromUtc, toUtc: filters.toUtc })
    return this.request<CalendarSpaceConflictResponse>(`/api/calendar/sources/space-conflicts?${query}`)
  }

  async reconcileSources(): Promise<{ created: number; updated: number; skipped: number }> {
    if (this.useMocks) return { created: 4, updated: 2, skipped: 0 }
    return this.request('/api/calendar/sources/reconcile', { method: 'POST' })
  }

  buildIcsUrl(filters: { fromUtc: string; toUtc: string; organizationId?: string }): string {
    const query = new URLSearchParams({ fromUtc: filters.fromUtc, toUtc: filters.toUtc })
    if (filters.organizationId) query.set('organizationId', filters.organizationId)
    return `${this.baseUrl}/api/calendar/ics?${query}`
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar el calendario institucional.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try {
        const body = await response.clone().json() as { message?: string }
        message = typeof body.message === 'string' ? body.message : ''
      } catch { /* respuesta sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para realizar esta operación.'
      throw new CalendarApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultCalendarApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): CalendarApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('La API debe usar el mismo origen mediante el proxy institucional.')
  }
  return new CalendarApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}
