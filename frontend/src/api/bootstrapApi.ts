export interface BootstrapInstitution { name: string }
export interface BootstrapWorkshop { name: string; number: string }
export interface InstitutionalBootstrapRequest {
  packageKey: string
  packageVersion: number
  institution: BootstrapInstitution
  workshops: BootstrapWorkshop[]
}
export interface BootstrapChangeSummary {
  institutionsToCreate: number
  institutionsUnchanged: number
  workshopsToCreate: number
  workshopsUnchanged: number
  securityProfilesToCreate: number
  officeDefinitionsToCreate: number
}
export interface SecurityProfileDefinition {
  code: string; name: string; scope: string; category: string; description: string; isSystem: boolean; isActive: boolean
}
export interface OfficeDefinition {
  code: string; name: string; category: string; isPrimary: boolean; isActive: boolean; aliasesJson: string | null
}
export interface BootstrapCatalogResponse { securityProfiles: SecurityProfileDefinition[]; officeDefinitions: OfficeDefinition[] }
export interface BootstrapPlanResponse {
  valid: boolean; alreadyApplied: boolean; payloadSha256: string; errors: string[]; changes: BootstrapChangeSummary; catalog: BootstrapCatalogResponse
}
export interface OrganizationBootstrap { id: string; name: string; number: string | null; type: string; parentOrganizationId: string | null }
export interface BootstrapApplyResponse {
  applied: boolean; alreadyApplied: boolean; payloadSha256: string; errors: string[]; institution: OrganizationBootstrap | null; workshops: OrganizationBootstrap[]; catalog: BootstrapCatalogResponse
}

export type BootstrapTokenProvider = () => Promise<string | null>
interface BootstrapApiOptions { baseUrl?: string; getAccessToken?: BootstrapTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

const mockCatalog: BootstrapCatalogResponse = {
  securityProfiles: [
    { code: 'platform_superadmin', name: 'Superadmin de plataforma', scope: 'platform', category: 'system', description: 'Control total de plataforma.', isSystem: true, isActive: true },
    { code: 'grand_lodge_admin', name: 'Administrador de Gran Logia', scope: 'order', category: 'administrative', description: 'Administración institucional.', isSystem: true, isActive: true },
    { code: 'lodge_admin', name: 'Administrador de Taller', scope: 'organization', category: 'administrative', description: 'Administración del Taller.', isSystem: true, isActive: true },
  ],
  officeDefinitions: [
    'Venerable Maestro','Inmediato Ex Venerable Maestro','Primer Vigilante','Segundo Vigilante','Orador','Secretario/a','Hospitalaria','Tesorero/a','Primer Diácono','Segundo Diácono','Maestro de Ceremonias','Guarda Templo Interno','Guarda Templo Externo','Maestro de Armonía'
  ].map((name, index) => ({ code: `office-${index + 1}`, name, category: index < 8 ? 'administrative' : 'ritual', isPrimary: index < 8, isActive: true, aliasesJson: name === 'Guarda Templo Externo' ? '["Retejador"]' : null })),
}

export class BootstrapApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: BootstrapTokenProvider
  private readonly onUnauthorized?: () => Promise<void>
  readonly useMocks: boolean
  private mockAppliedHash: string | null = null

  constructor(options: BootstrapApiOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.onUnauthorized = options.onUnauthorized
    this.useMocks = options.useMocks ?? false
  }

  async plan(input: InstitutionalBootstrapRequest): Promise<BootstrapPlanResponse> {
    if (this.useMocks) {
      const hash = await mockHash(input)
      return { valid: true, alreadyApplied: this.mockAppliedHash === hash, payloadSha256: hash, errors: [], changes: this.mockAppliedHash === hash ? zeroChanges() : { institutionsToCreate: 1, institutionsUnchanged: 0, workshopsToCreate: input.workshops.length, workshopsUnchanged: 0, securityProfilesToCreate: 4, officeDefinitionsToCreate: 14 }, catalog: this.mockAppliedHash === hash ? mockCatalog : { securityProfiles: [], officeDefinitions: [] } }
    }
    return this.request<BootstrapPlanResponse>('/api/platform/bootstrap/plan', input)
  }

  async apply(input: InstitutionalBootstrapRequest): Promise<BootstrapApplyResponse> {
    if (this.useMocks) {
      const hash = await mockHash(input); const repeated = this.mockAppliedHash === hash; this.mockAppliedHash = hash
      return { applied: true, alreadyApplied: repeated, payloadSha256: hash, errors: [], institution: { id: '99999999-9999-9999-9999-999999999999', name: input.institution.name, number: null, type: 'grand_lodge', parentOrganizationId: null }, workshops: input.workshops.map((x, index) => ({ id: index === 0 ? '23232323-2323-2323-2323-232323232323' : crypto.randomUUID(), name: x.name, number: x.number, type: 'workshop', parentOrganizationId: '99999999-9999-9999-9999-999999999999' })), catalog: mockCatalog }
    }
    return this.request<BootstrapApplyResponse>('/api/platform/bootstrap/apply', input)
  }

  async catalog(): Promise<BootstrapCatalogResponse> {
    if (this.useMocks) return mockCatalog
    return this.get<BootstrapCatalogResponse>('/api/platform/bootstrap/catalog')
  }

  private async request<T>(path: string, body: InstitutionalBootstrapRequest): Promise<T> {
    return this.authorized<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  }
  private async get<T>(path: string): Promise<T> { return this.authorized<T>(path, {}) }
  private async authorized<T>(path: string, init: RequestInit): Promise<T> {
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar como Superadmin para configurar la plataforma.')
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json'); headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, headers, credentials: 'omit', cache: 'no-store', redirect: 'error' })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Sólo el Superadmin de plataforma puede ejecutar la configuración inicial.')
      let detail = ''
      try { const body = await response.clone().json() as { errors?: string[]; message?: string }; detail = body.errors?.join(' ') ?? body.message ?? '' } catch { /* sin JSON */ }
      throw new Error(detail || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultBootstrapApiClient(getAccessToken?: BootstrapTokenProvider, onUnauthorized?: () => Promise<void>): BootstrapApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Bootstrap institucional debe usar el mismo origen mediante el proxy institucional.')
  return new BootstrapApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function zeroChanges(): BootstrapChangeSummary { return { institutionsToCreate: 0, institutionsUnchanged: 0, workshopsToCreate: 0, workshopsUnchanged: 0, securityProfilesToCreate: 0, officeDefinitionsToCreate: 0 } }
async function mockHash(input: InstitutionalBootstrapRequest) { const bytes = new TextEncoder().encode(JSON.stringify(input)); const digest = await crypto.subtle.digest('SHA-256', bytes); return Array.from(new Uint8Array(digest)).map(x => x.toString(16).padStart(2, '0')).join('') }
