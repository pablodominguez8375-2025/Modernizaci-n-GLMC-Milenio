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

export interface CandidateWorkshopQueueItem {
  ceremonyRequestId: string
  firstNames: string
  lastNames: string
  displayName: string
  workshopName: string
  workshopNumber: string | null
  proposedDate: string | null
  requestStatus: string
  profileAvailable: boolean
  photoAvailable: boolean
  reviewStatus: CandidateReviewStatus | string
  createdAtUtc: string
}

export interface CandidateWorkshopQueueResponse {
  total: number
  items: CandidateWorkshopQueueItem[]
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
  employerName: string | null
  workAddress: string | null
  workPosition: string | null
  workPhone: string | null
  phone: string | null
  email: string | null
  address: string | null
  city: string | null
  workshopName: string
  workshopNumber: string | null
  orient: string | null
  presenters: string[]
  insinuationDate: string
  firstDegreePresentationDate: string | null
  responsibleSecretaryName: string | null
  reviewStatus: CandidateReviewStatus | string
  photoAvailable: boolean
  completenessPercent: number
  missingRequirements: string[]
  interviewSummary: string | null
  internalObservations: string | null
  submittedAtUtc: string
  updatedAtUtc: string
}

export interface CandidateIntakeUpsertPayload {
  firstNames: string
  paternalSurname: string
  maternalSurname?: string | null
  rutOrInstitutionalId?: string | null
  birthDate?: string | null
  nationality?: string | null
  civilStatus?: string | null
  occupation?: string | null
  employerName?: string | null
  workAddress?: string | null
  workPosition?: string | null
  workPhone?: string | null
  phone?: string | null
  email?: string | null
  address?: string | null
  city?: string | null
  orient?: string | null
  presenters: string[]
  insinuationDate: string
  firstDegreePresentationDate?: string | null
  responsibleSecretaryName?: string | null
  interviewSummary?: string | null
  internalObservations?: string | null
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
const demoSecondRequestId = 'eeeeeeee-3333-3333-3333-333333333333'

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
  employerName: 'Organización Demostrativa SpA',
  workAddress: 'Avenida Ficticia 1000, Santiago',
  workPosition: 'Coordinador de proyectos · demo',
  workPhone: '+56 2 2000 0000',
  phone: '+56 9 0000 4321',
  email: 'insinuado.demo@ejemplo.cl',
  address: 'Dirección ficticia 2345, Depto. 702',
  city: 'Santiago · demo',
  workshopName: 'Taller Demostrativo Nº 23',
  workshopNumber: '23',
  orient: 'Santiago',
  presenters: ['H∴ Presentante Uno · demo', 'H∴ Presentante Dos · demo'],
  insinuationDate: '2026-08-12',
  firstDegreePresentationDate: '2026-08-28',
  responsibleSecretaryName: 'H∴ Secretario Demostrativo',
  reviewStatus: 'pending_grand_secretariat',
  photoAvailable: true,
  completenessPercent: 100,
  missingRequirements: [],
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
    ceremonyRequestId: demoSecondRequestId,
    displayName: 'Persona Demostrativa Dos',
    workshopName: 'Taller Demostrativo Nº 1',
    workshopNumber: '1',
    insinuationDate: '2026-08-20',
    submittedAtUtc: '2026-09-09T17:15:00Z',
    photoAvailable: false,
    reviewStatus: 'observed',
  },
]

const demoWorkshopQueueSeed: CandidateWorkshopQueueItem[] = [
  {
    ceremonyRequestId: demoRequestId,
    firstNames: 'Tomás Ignacio',
    lastNames: 'Valdés Riquelme',
    displayName: 'Tomás Ignacio Valdés Riquelme',
    workshopName: 'Taller Demostrativo Nº 23',
    workshopNumber: '23',
    proposedDate: '2026-10-03',
    requestStatus: 'under_review',
    profileAvailable: true,
    photoAvailable: true,
    reviewStatus: 'pending_grand_secretariat',
    createdAtUtc: '2026-09-08T13:15:00Z',
  },
  {
    ceremonyRequestId: demoSecondRequestId,
    firstNames: 'Persona Demostrativa',
    lastNames: 'Dos',
    displayName: 'Persona Demostrativa Dos',
    workshopName: 'Taller Demostrativo Nº 23',
    workshopNumber: '23',
    proposedDate: null,
    requestStatus: 'under_review',
    profileAvailable: false,
    photoAvailable: false,
    reviewStatus: 'pending_grand_secretariat',
    createdAtUtc: '2026-09-10T12:00:00Z',
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
  private readonly mockWorkshopQueue = demoWorkshopQueueSeed.map(item => ({ ...item }))
  private readonly mockProfiles = new Map<string, CandidateIntakeProfile>([[demoRequestId, { ...demoProfile, presenters: [...demoProfile.presenters] }]])
  private readonly mockPhotos = new Map<string, Blob>()

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

  async getWorkshopQueue(): Promise<CandidateWorkshopQueueResponse> {
    if (this.useMocks) return { total: this.mockWorkshopQueue.length, items: this.mockWorkshopQueue.map(item => ({ ...item })) }
    return this.request<CandidateWorkshopQueueResponse>('/api/insinuados/taller/solicitudes')
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
      const profile = this.mockProfiles.get(requestId)
      if (!profile) throw new CandidateIntakeApiHttpError(404, 'La ficha aún no ha sido ingresada.')
      return { ...profile, presenters: [...profile.presenters] }
    }
    return this.request<CandidateIntakeProfile>(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/ficha`)
  }

  async saveProfile(requestId: string, payload: CandidateIntakeUpsertPayload): Promise<CandidateIntakeProfile> {
    if (this.useMocks) {
      const queueItem = this.mockWorkshopQueue.find(item => item.ceremonyRequestId === requestId)
      if (!queueItem) throw new CandidateIntakeApiHttpError(404, 'La solicitud demostrativa no existe.')
      const now = new Date().toISOString()
      const existing = this.mockProfiles.get(requestId)
      const profile: CandidateIntakeProfile = {
        ceremonyRequestId: requestId,
        firstNames: payload.firstNames.trim(),
        paternalSurname: payload.paternalSurname.trim(),
        maternalSurname: payload.maternalSurname?.trim() || null,
        rutOrInstitutionalId: payload.rutOrInstitutionalId?.trim() || null,
        birthDate: payload.birthDate || null,
        nationality: payload.nationality?.trim() || null,
        civilStatus: payload.civilStatus?.trim() || null,
        occupation: payload.occupation?.trim() || null,
        employerName: payload.employerName?.trim() || null,
        workAddress: payload.workAddress?.trim() || null,
        workPosition: payload.workPosition?.trim() || null,
        workPhone: payload.workPhone?.trim() || null,
        phone: payload.phone?.trim() || null,
        email: payload.email?.trim() || null,
        address: payload.address?.trim() || null,
        city: payload.city?.trim() || null,
        workshopName: queueItem.workshopName,
        workshopNumber: queueItem.workshopNumber,
        orient: payload.orient?.trim() || null,
        presenters: payload.presenters.map(value => value.trim()).filter(Boolean),
        insinuationDate: payload.insinuationDate,
        firstDegreePresentationDate: payload.firstDegreePresentationDate || null,
        responsibleSecretaryName: payload.responsibleSecretaryName?.trim() || null,
        reviewStatus: 'pending_grand_secretariat',
        photoAvailable: existing?.photoAvailable ?? false,
        completenessPercent: 0,
        missingRequirements: [],
        interviewSummary: payload.interviewSummary?.trim() || null,
        internalObservations: payload.internalObservations?.trim() || null,
        submittedAtUtc: existing?.submittedAtUtc ?? now,
        updatedAtUtc: now,
      }
      profile.missingRequirements = candidateMissingRequirements(profile)
      profile.completenessPercent = Math.floor(((20 - profile.missingRequirements.length) * 100) / 20)
      this.mockProfiles.set(requestId, profile)
      queueItem.firstNames = profile.firstNames
      queueItem.lastNames = [profile.paternalSurname, profile.maternalSurname].filter(Boolean).join(' ')
      queueItem.displayName = [profile.firstNames, queueItem.lastNames].filter(Boolean).join(' ')
      queueItem.profileAvailable = true
      queueItem.reviewStatus = 'pending_grand_secretariat'
      return { ...profile, presenters: [...profile.presenters] }
    }
    return this.request<CandidateIntakeProfile>(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/ficha`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
  }

  async attachPhotoVersion(requestId: string, photoVersionId: string): Promise<void> {
    if (this.useMocks) {
      const profile = this.mockProfiles.get(requestId)
      if (!profile) throw new CandidateIntakeApiHttpError(404, 'Primero debe registrar la ficha del insinuado.')
      profile.photoAvailable = true
      profile.missingRequirements = candidateMissingRequirements(profile)
      profile.completenessPercent = Math.floor(((20 - profile.missingRequirements.length) * 100) / 20)
      profile.reviewStatus = 'pending_grand_secretariat'
      profile.updatedAtUtc = new Date().toISOString()
      const queueItem = this.mockWorkshopQueue.find(item => item.ceremonyRequestId === requestId)
      if (queueItem) {
        queueItem.photoAvailable = true
        queueItem.reviewStatus = 'pending_grand_secretariat'
      }
      return
    }
    await this.request(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/foto`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ photoVersionId }),
    })
  }

  async uploadPhoto(requestId: string, file: File): Promise<void> {
    if (this.useMocks) {
      const profile = this.mockProfiles.get(requestId)
      if (!profile) throw new CandidateIntakeApiHttpError(404, 'Primero debe registrar la ficha del insinuado.')
      this.mockPhotos.set(requestId, file)
      profile.photoAvailable = true
      profile.reviewStatus = 'pending_grand_secretariat'
      profile.updatedAtUtc = new Date().toISOString()
      profile.missingRequirements = candidateMissingRequirements(profile)
      profile.completenessPercent = Math.floor(((20 - profile.missingRequirements.length) * 100) / 20)
      const queueItem = this.mockWorkshopQueue.find(item => item.ceremonyRequestId === requestId)
      if (queueItem) { queueItem.photoAvailable = true; queueItem.reviewStatus = 'pending_grand_secretariat' }
      return
    }
    const response = await this.fetchAuthorized(`/api/insinuados/solicitudes/${encodeURIComponent(requestId)}/foto/contenido`, {
      method: 'PUT',
      headers: { 'Content-Type': file.type, 'X-File-Name': encodeURIComponent(file.name) },
      body: file,
    })
    if (!response.ok) throw await this.toError(response)
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
      const workshopItem = this.mockWorkshopQueue.find(value => value.ceremonyRequestId === requestId)
      if (workshopItem) workshopItem.reviewStatus = decision
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
      const profile = this.mockProfiles.get(requestId)
      if (profile?.missingRequirements.length) throw new CandidateIntakeApiHttpError(409, `La ficha no está completa: ${profile.missingRequirements.join(', ')}.`)
      const item = this.mockQueue.find(value => value.ceremonyRequestId === requestId)
      if (item) item.reviewStatus = 'approved'
      if (profile) {
        profile.reviewStatus = 'approved'
        profile.updatedAtUtc = new Date().toISOString()
      }
      const workshopItem = this.mockWorkshopQueue.find(value => value.ceremonyRequestId === requestId)
      if (workshopItem) workshopItem.reviewStatus = 'approved'
      return
    }
    await this.request(`/api/ceremonias/solicitudes/${encodeURIComponent(requestId)}/aprobar-publicacion-insinuado`, { method: 'POST' })
  }

  async getPrivatePhoto(requestId: string): Promise<Blob | null> {
    if (this.useMocks) return this.mockPhotos.get(requestId) ?? null
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
    if (response.status === 403) message = 'Su cuenta no tiene permiso para acceder a este expediente.'
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

function candidateMissingRequirements(profile: CandidateIntakeProfile): string[] {
  return [
    [profile.firstNames, 'Nombres'], [profile.paternalSurname, 'Apellido paterno'], [profile.rutOrInstitutionalId, 'RUT o identificación'],
    [profile.birthDate, 'Fecha de nacimiento'], [profile.nationality, 'Nacionalidad'], [profile.civilStatus, 'Estado civil'],
    [profile.phone, 'Teléfono personal'], [profile.email, 'Correo electrónico'], [profile.address, 'Dirección personal'], [profile.city, 'Ciudad'],
    [profile.occupation, 'Actividad, profesión u oficio'], [profile.employerName, 'Empleador'], [profile.workAddress, 'Dirección laboral'],
    [profile.workPosition, 'Cargo o función'], [profile.workPhone, 'Teléfono laboral'], [profile.orient, 'Oriente'],
    [profile.presenters.length ? 'sí' : '', 'Presentantes'], [profile.firstDegreePresentationDate, 'Fecha de presentación en primer grado'],
    [profile.responsibleSecretaryName, 'Secretario responsable'], [profile.photoAvailable ? 'sí' : '', 'Fotografía tipo pasaporte'],
  ].filter(([value]) => !value).map(([, label]) => label as string)
}
