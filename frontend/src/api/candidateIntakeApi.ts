import type { CandidatePortalResponse } from './pmgmApi'

export type CandidateReviewStatus = 'pending_grand_secretariat' | 'observed' | 'rejected' | 'approved'

export interface CandidateReviewQueueItem {
  ceremonyRequestId: string
  displayName: string
  workshopName: string
  workshopNumber: string | null
  insinuationDate: string
  submittedAtUtc: string
  photoAvailable: boolean
  reviewStatus: CandidateReviewStatus | string
}

export interface CandidateReviewQueueResponse {
  total: number
  items: CandidateReviewQueueItem[]
}

export interface CandidateIntakeProfile {
  ceremonyRequestId: string
  firstNames: string
  paternalSurname: string | null
  maternalSurname: string | null
  rutOrInstitutionalId: string | null
  birthDate: string | null
  nationality: string | null
  civilStatus: string | null
  occupation: string | null
  phone: string | null
  email: string | null
  address: string | null
  city: string | null
  workshopName: string
  workshopNumber: string | null
  orient: string | null
  presenters: string[]
  insinuationDate: string
  reviewStatus: CandidateReviewStatus | string
  photoAvailable: boolean
  interviewSummary: string | null
  internalObservations: string | null
  submittedAtUtc: string
  updatedAtUtc: string
}

export type PublishedCandidate = CandidatePortalResponse['items'][number] & { photoUrl: string | null }
export type PublishedCandidatePortalResponse = Omit<CandidatePortalResponse, 'items'> & { items: PublishedCandidate[] }
export type CandidateReviewDecision = 'observed' | 'rejected'
export type AccessTokenProvider = () => Promise<string | null>

export interface CandidateIntakeApiClientOptions {
  baseUrl?: string
  getAccessToken?: AccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

class CandidateIntakeApiHttpError extends Error {
  constructor(readonly status: number, message: string) {
    super(message)
    this.name = 'CandidateIntakeApiHttpError'
  }
}

const demoRequestId = 'eeeeeeee-2222-2222-2222-222222222222'

const demoProfile: CandidateIntakeProfile = {
  ceremonyRequestId: demoRequestId,
  firstNames: 'Tomás Ignacio',
  paternalSurname: 'Valdés',
  maternalSurname: 'Riquelme',
  rutOrInstitutionalId: 'DEMO-16.543.219-X',
  birthDate: '1990-08-14',
  nationality: 'Chilena · demo',
  civilStatus: 'Soltero · demo',
  occupation: 'Profesional · dato ficticio',
  phone: '+56 9 0000 4321',
  email: 'insinuado.demo@ejemplo.cl',
  address: 'Dirección ficticia 2345, Depto. 702',
  city: 'Santiago · demo',
  workshopName: 'Taller Demostrativo Nº 23',
  workshopNumber: '23',
  orient: 'Santiago',
  presenters: ['H∴ Presentante Uno · demo', 'H∴ Presentante Dos · demo'],
  insinuationDate: '2026-08-12',
  reviewStatus: 'pending_grand_secretariat',
  photoAvailable: true,
  interviewSummary: 'Registro demostrativo: la entrevista evidencia interés por el conocimiento, el servicio y el perfeccionamiento personal.',
  internalObservations: 'Expediente ficticio utilizado exclusivamente para QA y demostración.',
  submittedAtUtc: '2026-09-10T14:30:00Z',
  updatedAtUtc: '2026-09-10T14:30:00Z',
}

const demoQueueSeed: CandidateReviewQueueItem[] = [
  {
    ceremonyRequestId: demoRequestId,
    displayName: 'Tomás Ignacio Valdés Riquelme',
    workshopName: 'Taller Demostrativo Nº 23',
    workshopNumber: '23',
    insinuationDate: '2026-08-12',
    submittedAtUtc: '2026-09-10T14:30:00Z',
    photoAvailable: true,
    reviewStatus: 'pending_grand_secretariat',
  },
  {
    ceremonyRequestId: 'eeeeeeee-3333-3333-3333-333333333333',
    displayName: 'Persona Demostrativa Dos',
    workshopName: 'Taller Demostrativo Nº 1',
    workshopNumber: '1',
    insinuationDate: '2026-08-20',
    submittedAtUtc: '2026-09-09T17:15:00Z',
    photoAvailable: false,
    reviewStatus: 'observed',
  },
]

const demoPublishedCandidates: PublishedCandidatePortalResponse = {
  culture: 'es-CL',
  portal: 'Insinuados en período de publicación',
  total: 2,
  items: [
    {
      displayName: 'Persona Demostrativa Uno',
      workshopName: 'Taller Demostrativo Nº 1',
      workshopNumber: '1',
      publishedFromUtc: '2026-08-25T15:00:00Z',
      publishedUntilUtc: null,
      requiredDays: 20,
      elapsedDays: 13,
      complianceDateUtc: '2026-09-14T15:00:00Z',
      ruleCode: 'initiation.publication.minimum_days',
      status: 'published',
      photoUrl: '/api/candidate-publications/11111111-aaaa-4aaa-8aaa-111111111111/photo',
    },
    {
      displayName: 'Persona Demostrativa Dos',
      workshopName: 'Taller Demostrativo Nº 23',
      workshopNumber: '23',
      publishedFromUtc: '2026-08-15T18:00:00Z',
      publishedUntilUtc: null,
      requiredDays: 20,
      elapsedDays: 23,
      complianceDateUtc: '2026-09-04T18:00:00Z',
      ruleCode: 'initiation.publication.minimum_days',
      status: 'published',
      photoUrl: null,
    },
  ],
}

export class CandidateIntakeApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockQueue = demoQueueSeed.map(item => ({ ...item }))
  private readonly mockProfiles = new Map<string, CandidateIntakeProfile>([[demoRequestId, { ...demoProfile, presenters: [...demoProfile.presenters] }]])

  constructor(options: CandidateIntakeApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getPublishedCandidates(): Promise<PublishedCandidatePortalResponse> {
    if (this.useMocks) {
      return { ...demoPublishedCandidates, items: demoPublishedCandidates.items.map(item => ({ ...item })) }
    }
    return this.request<PublishedCandidatePortalResponse>('/api/candidate-publications/active')
  }

  async getGrandSecretariatQueue(status?: CandidateReviewStatus | string): Promise<CandidateReviewQueueResponse> {
    if (this.useMocks) {
      const items = status ? this.mockQueue.filter(item => item.reviewStatus === status) : this.mockQueue
      return { total: items.length, items: items.map(item => ({ ...item })) }
    }
    const query = new URLSearchParams()
    if (status) query.set('status', status)
    return this.request<CandidateReviewQueueResponse>(`/api/insinuados/revision-gran-secretaria${query.size ? `?${query}` : ''}`)
  }

  async getProfile(requestId: string): Promise<CandidateIntakeProfile> {
    if (this.useMocks) {
      const profile = this.mockProfiles.get(requestId) ?? { ...demoProfile, ceremonyRequestId: requestId, photoAvailable: false }
      return { ...profile, presenters: [...profile.presenters] }
    }
    return this.request<CandidateIntakeProfile>(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/ficha`)
  }

  async review(requestId: string, decision: CandidateReviewDecision, notes?: string): Promise<void> {
    if (this.useMocks) {
      const item = this.mockQueue.find(value => value.ceremonyRequestId === requestId)
      if (item) item.reviewStatus = decision
      const profile = this.mockProfiles.get(requestId)
      if (profile) {
        profile.reviewStatus = decision
        profile.updatedAtUtc = new Date().toISOString()
        profile.internalObservations = notes?.trim() || profile.internalObservations
      }
      return
    }
    await this.request(`/api/ceremonias/solicitudes/${encodeURIComponent(requestId)}/revision-publicacion-insinuado`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ decision, sourceReference: 'gran-secretaria-ui', notes: notes?.trim() || null }),
    })
  }

  async approveAndPublish(requestId: string): Promise<void> {
    if (this.useMocks) {
      const item = this.mockQueue.find(value => value.ceremonyRequestId === requestId)
      if (item) item.reviewStatus = 'approved'
      const profile = this.mockProfiles.get(requestId)
      if (profile) {
        profile.reviewStatus = 'approved'
        profile.updatedAtUtc = new Date().toISOString()
      }
      return
    }
    await this.request(`/api/ceremonias/solicitudes/${encodeURIComponent(requestId)}/aprobar-publicacion-insinuado`, { method: 'POST' })
  }

  async getPrivatePhoto(requestId: string): Promise<Blob | null> {
    if (this.useMocks) return null
    const response = await this.fetchAuthorized(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/foto`, { headers: { Accept: 'image/jpeg,image/png' } })
    if (response.status === 404) return null
    if (!response.ok) throw await this.toError(response)
    return response.blob()
  }

  async getPublishedPhoto(photoUrl: string): Promise<Blob | null> {
    if (this.useMocks) return null
    if (!photoUrl.startsWith('/api/candidate-publications/')) throw new Error('La ruta de fotografía publicada no es válida.')
    const response = await this.fetchAuthorized(photoUrl, { headers: { Accept: 'image/jpeg,image/png' } })
    if (response.status === 404) return null
    if (!response.ok) throw await this.toError(response)
    return response.blob()
  }

  private async request<T = unknown>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await this.fetchAuthorized(path, init)
    if (!response.ok) throw await this.toError(response)
    if (response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }

  private async fetchAuthorized(path: string, init: RequestInit): Promise<Response> {
    const headers = new Headers(init.headers)
    if (!headers.has('Accept')) headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar el expediente de insinuados.')
    headers.set('Authorization', `Bearer ${token}`)
    return fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
  }

  private async toError(response: Response): Promise<CandidateIntakeApiHttpError> {
    if (response.status === 401) await this.onUnauthorized?.()
    let message = ''
    try {
      const body = await response.clone().json() as { message?: string }
      message = typeof body.message === 'string' ? body.message : ''
    } catch { /* respuesta sin JSON */ }
    if (response.status === 403) message = 'Su cuenta no tiene permiso para revisar este expediente.'
    return new CandidateIntakeApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
  }
}

export function createDefaultCandidateIntakeApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): CandidateIntakeApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('La API debe usar el mismo origen mediante el proxy institucional.')
  }
  return new CandidateIntakeApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}
