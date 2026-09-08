export interface CandidatePublication {
  displayName: string
  workshopName: string
  workshopNumber: string | null
  publishedFromUtc: string
  publishedUntilUtc: string | null
  requiredDays: number
  elapsedDays: number
  complianceDateUtc: string
  ruleCode: string
  status: string
}

export interface CandidatePortalResponse {
  culture: string
  portal: string
  total: number
  items: CandidatePublication[]
}

export interface SystemInfo {
  project: string
  api: string
  version: string
  runtime: string
  culture: string
  institutionalTimeZone: string
  defaultCurrency: string
}

export interface SessionCapabilities {
  canApproveTransfers: boolean
  canRunRegimenInteriorReports: boolean
  canManageGrandSecretariat: boolean
  canManageTreasuryRegularity: boolean
  canManageHospitalariaRegularity: boolean
  canEvaluateCeremonies: boolean
  canManagePrivacy: boolean
}

export interface SessionProfile {
  displayName: string
  accessScope: 'order' | 'organization' | 'authenticated'
  capabilities: SessionCapabilities
}

export type AccessTokenProvider = () => Promise<string | null>

export interface PmgmApiClientOptions {
  baseUrl?: string
  getAccessToken?: AccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

const mockCandidates: CandidatePublication[] = [
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
  },
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
    canManagePrivacy: true,
  },
}

export class PmgmApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: PmgmApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getCandidatePortal(): Promise<CandidatePortalResponse> {
    if (this.useMocks) {
      await sleep(250)
      return {
        culture: 'es-CL',
        portal: 'Insinuados en período de publicación',
        total: mockCandidates.length,
        items: mockCandidates,
      }
    }

    return this.request<CandidatePortalResponse>('/api/ceremonias/portal-insinuados')
  }

  async getSystemInfo(): Promise<SystemInfo> {
    if (this.useMocks) {
      return {
        project: 'Proyecto Milenio — Modernización Gran Logia Mixta de Chile',
        api: 'PMGM.Api',
        version: '0.11.0',
        runtime: '.NET 10',
        culture: 'es-CL',
        institutionalTimeZone: 'America/Santiago',
        defaultCurrency: 'CLP',
      }
    }

    return this.request<SystemInfo>('/api/system/info')
  }

  async getSessionProfile(): Promise<SessionProfile> {
    if (this.useMocks) return mockSession
    return this.request<SessionProfile>('/api/session/me')
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')

    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar la información institucional.')
    headers.set('Authorization', `Bearer ${token}`)

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      credentials: 'omit',
      redirect: 'error',
      cache: 'no-store',
      headers,
    })

    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para consultar esta información.')
      throw new Error(`La API respondió ${response.status} ${response.statusText}.`)
    }

    return response.json() as Promise<T>
  }
}

export function createDefaultPmgmApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): PmgmApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('La API debe usar el mismo origen mediante el proxy institucional.')
  }
  return new PmgmApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds))
}
