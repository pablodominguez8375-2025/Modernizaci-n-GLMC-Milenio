export interface NotificationInboxItem {
  id: string
  typeCode: string
  subject: string
  body: string
  actionUrl: string | null
  classification: string
  mandatory: boolean
  createdAtUtc: string
  readAtUtc: string | null
}

export type AccessTokenProvider = () => Promise<string | null>

export interface NotificationApiClientOptions {
  baseUrl?: string
  getAccessToken?: AccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

class NotificationApiHttpError extends Error {
  constructor(readonly status: number, message: string) {
    super(message)
    this.name = 'NotificationApiHttpError'
  }
}

const demoInbox: NotificationInboxItem[] = [
  {
    id: '9a000001-0000-0000-0000-000000000001',
    typeCode: 'ceremony.authorized',
    subject: 'Ceremonia autorizada',
    body: 'La solicitud de Aumento de Salario del Taller Libertad Nº 23 fue autorizada por Gran Secretaría y quedó incorporada al calendario institucional.',
    actionUrl: '/ceremonies',
    classification: 'internal',
    mandatory: true,
    createdAtUtc: '2026-09-09T13:20:00Z',
    readAtUtc: null,
  },
  {
    id: '9a000002-0000-0000-0000-000000000002',
    typeCode: 'candidate.publication.deadline',
    subject: 'Plazo de publicación próximo a cumplirse',
    body: 'Un insinuado del Taller Demostrativo Nº 1 completará el período mínimo de publicación dentro de los próximos días.',
    actionUrl: '/candidates',
    classification: 'internal',
    mandatory: false,
    createdAtUtc: '2026-09-09T11:10:00Z',
    readAtUtc: null,
  },
  {
    id: '9a000003-0000-0000-0000-000000000003',
    typeCode: 'calendar.space.reserved',
    subject: 'Templo Principal reservado',
    body: 'La reserva del Templo Principal para la ceremonia del 18 de septiembre quedó confirmada y sin conflictos detectados.',
    actionUrl: '/calendar',
    classification: 'internal',
    mandatory: false,
    createdAtUtc: '2026-09-08T19:45:00Z',
    readAtUtc: null,
  },
  {
    id: '9a000004-0000-0000-0000-000000000004',
    typeCode: 'privacy.retention.review',
    subject: 'Revisión de retención documental',
    body: 'Existe una revisión programada de conservación documental conforme al manifiesto de privacidad y Ley 21.719.',
    actionUrl: '/documents',
    classification: 'confidential',
    mandatory: true,
    createdAtUtc: '2026-09-08T15:30:00Z',
    readAtUtc: '2026-09-08T17:00:00Z',
  },
  {
    id: '9a000005-0000-0000-0000-000000000005',
    typeCode: 'lodge.minutes.approved',
    subject: 'Acta aprobada',
    body: 'La última versión del acta de Tenida Regular fue aprobada y quedó preservada con historial de versiones.',
    actionUrl: '/lodge',
    classification: 'internal',
    mandatory: false,
    createdAtUtc: '2026-09-07T22:10:00Z',
    readAtUtc: '2026-09-08T09:00:00Z',
  },
]

export class NotificationApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockInbox = demoInbox.map(item => ({ ...item }))

  constructor(options: NotificationApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getMine(filters: { unreadOnly?: boolean; limit?: number } = {}): Promise<NotificationInboxItem[]> {
    if (this.useMocks) {
      const unreadOnly = filters.unreadOnly ?? false
      const limit = Math.max(1, Math.min(filters.limit ?? 50, 100))
      return this.mockInbox
        .filter(item => !unreadOnly || item.readAtUtc === null)
        .slice(0, limit)
        .map(item => ({ ...item }))
    }

    const query = new URLSearchParams()
    query.set('unreadOnly', String(filters.unreadOnly ?? false))
    query.set('limit', String(filters.limit ?? 50))
    return this.request<NotificationInboxItem[]>(`/api/notifications/me?${query}`)
  }

  async markRead(messageId: string): Promise<void> {
    if (this.useMocks) {
      const item = this.mockInbox.find(value => value.id === messageId)
      if (item && item.readAtUtc === null) item.readAtUtc = new Date().toISOString()
      return
    }
    await this.request<void>(`/api/notifications/${encodeURIComponent(messageId)}/read`, { method: 'POST' }, false)
  }

  private async request<T>(path: string, init: RequestInit = {}, expectsJson = true): Promise<T> {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar las notificaciones institucionales.')
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
      throw new NotificationApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    if (!expectsJson || response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }
}

export function createDefaultNotificationApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): NotificationApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('La API debe usar el mismo origen mediante el proxy institucional.')
  }
  return new NotificationApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}
