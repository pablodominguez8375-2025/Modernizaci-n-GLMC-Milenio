export interface CandidatePublication { displayName: string; workshopName: string; workshopNumber: string | null; publishedFromUtc: string; publishedUntilUtc: string | null; requiredDays: number; elapsedDays: number; complianceDateUtc: string; ruleCode: string; status: string }
export interface CandidatePortalResponse { culture: string; portal: string; total: number; items: CandidatePublication[] }
export interface SystemInfo { project: string; api: string; version: string; runtime: string; culture: string; institutionalTimeZone: string; defaultCurrency: string }
export interface SessionCapabilities { canApproveTransfers: boolean; canRunRegimenInteriorReports: boolean; canManageGrandSecretariat: boolean; canManageTreasuryRegularity: boolean; canManageHospitalariaRegularity: boolean; canEvaluateCeremonies: boolean; canManagePrivacy: boolean }
export interface SessionProfile { displayName: string; accessScope: 'order' | 'organization' | 'authenticated'; capabilities: SessionCapabilities }
export interface OrganizationOption { id: string; name: string; number: string | null; type: string }
export interface OrganizationOptionsResponse { total: number; items: OrganizationOption[] }
export interface InstitutionalSpace { id: string; code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location: string | null; capacity: number | null; status?: string; isAvailable?: boolean }
export interface SpaceAvailabilityResponse { fromUtc: string; toUtc: string; total: number; available: number; items: InstitutionalSpace[] }
export interface SecretariatDocument { id: string; documentType: 'decree' | 'communication' | 'ceremony_authorization'; documentCode: string; title: string; content: string; organizationId: string | null; relatedCeremonyRequestId: string | null; spaceReservationId: string | null; status: string; issuedAtUtc: string; issuedBySubject: string }
export interface SecretariatDocumentsResponse { total: number; items: SecretariatDocument[] }
export interface CreateSpaceRequest { code: string; name: string; spaceType: 'temple' | 'secretariat_room'; location?: string | null; capacity?: number | null }
export interface CreateReservationRequest { spaceId: string; organizationId: string; ceremonyRequestId?: string | null; purpose: string; startsAtUtc: string; endsAtUtc: string; notes?: string | null }
export interface IssueDocumentRequest { documentType: 'decree' | 'communication'; title: string; content: string; organizationId?: string | null }
export interface RegimenInteriorSummary {
  scope: 'order' | 'organization'; organizationId: string | null; asOf: string; period: { from: string; to: string }
  members: { totalRelated: number; currentlyAffiliated: number; active: number; inactive: number; currentWithBlockingStatus: number }
  events: { voluntaryWithdrawals: number; forcedWithdrawals: number; reinstatements: number; deaths: number; transfers: number }
  financialRegularity: { source: string; currentAffiliations: number; upToDate: number; delinquent: number; pending: number; exempt: number; withoutStatus: number; delinquentMembersDistinct: number }
  degreeDistribution: Record<string, number>; pendingTransfers: number
}
export type AccessTokenProvider = () => Promise<string | null>
export interface PmgmApiClientOptions { baseUrl?: string; getAccessToken?: AccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

const mockCandidates: CandidatePublication[] = [
  { displayName: 'Persona Demostrativa Uno', workshopName: 'Taller Demostrativo Nº 1', workshopNumber: '1', publishedFromUtc: '2026-08-25T15:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 13, complianceDateUtc: '2026-09-14T15:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
  { displayName: 'Persona Demostrativa Dos', workshopName: 'Taller Demostrativo Nº 23', workshopNumber: '23', publishedFromUtc: '2026-08-15T18:00:00Z', publishedUntilUtc: null, requiredDays: 20, elapsedDays: 23, complianceDateUtc: '2026-09-04T18:00:00Z', ruleCode: 'initiation.publication.minimum_days', status: 'published' },
]
const mockSession: SessionProfile = { displayName: 'Usuario demostrativo', accessScope: 'order', capabilities: { canApproveTransfers: true, canRunRegimenInteriorReports: true, canManageGrandSecretariat: true, canManageTreasuryRegularity: true, canManageHospitalariaRegularity: true, canEvaluateCeremonies: true, canManagePrivacy: true } }
const defaultMockOrganizations: OrganizationOption[] = [
  { id: '11111111-1111-1111-1111-111111111111', name: 'Taller Demostrativo Nº 1', number: '1', type: 'workshop' },
  { id: '23232323-2323-2323-2323-232323232323', name: 'Taller Demostrativo Nº 23', number: '23', type: 'workshop' },
]
const defaultMockSpaces: InstitutionalSpace[] = [
  { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', code: 'TEMP-01', name: 'Templo Principal', spaceType: 'temple', location: 'Sede institucional', capacity: 80, status: 'active' },
  { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', code: 'SEC-01', name: 'Sala de Secretaría', spaceType: 'secretariat_room', location: 'Sede institucional', capacity: 16, status: 'active' },
]

export class PmgmApiClient {
  private readonly baseUrl: string; private readonly getAccessToken?: AccessTokenProvider; readonly useMocks: boolean; private readonly onUnauthorized?: () => Promise<void>
  private readonly mockOrganizations = [...defaultMockOrganizations]; private readonly mockSpaces = [...defaultMockSpaces]; private readonly mockBusySpaces = new Set<string>(); private readonly mockDocuments: SecretariatDocument[] = []
  constructor(options: PmgmApiClientOptions = {}) { this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, ''); this.getAccessToken = options.getAccessToken; this.useMocks = options.useMocks ?? false; this.onUnauthorized = options.onUnauthorized }

  async getCandidatePortal(): Promise<CandidatePortalResponse> { if (this.useMocks) { await sleep(120); return { culture: 'es-CL', portal: 'Insinuados en período de publicación', total: mockCandidates.length, items: mockCandidates } } return this.request<CandidatePortalResponse>('/api/ceremonias/portal-insinuados') }
  async getSystemInfo(): Promise<SystemInfo> { if (this.useMocks) return { project: 'Proyecto Milenio — Modernización Gran Logia Mixta de Chile', api: 'PMGM.Api', version: '0.11.1', runtime: '.NET 10', culture: 'es-CL', institutionalTimeZone: 'America/Santiago', defaultCurrency: 'CLP' }; return this.request<SystemInfo>('/api/system/info') }
  async getSessionProfile(): Promise<SessionProfile> { if (this.useMocks) return mockSession; return this.request<SessionProfile>('/api/session/me') }
  async getOrganizationOptions(): Promise<OrganizationOptionsResponse> { if (this.useMocks) return { total: this.mockOrganizations.length, items: [...this.mockOrganizations] }; return this.request<OrganizationOptionsResponse>('/api/institutional/organizations/options') }

  async getRegimenInteriorSummary(filters: { organizationId?: string; asOf?: string; from?: string } = {}): Promise<RegimenInteriorSummary> {
    if (this.useMocks) return mockRegimenSummary(filters)
    const query = new URLSearchParams(); if (filters.organizationId) query.set('organizationId', filters.organizationId); if (filters.asOf) query.set('asOf', filters.asOf); if (filters.from) query.set('from', filters.from)
    return this.request<RegimenInteriorSummary>(`/api/regimen-interior/summary${query.size ? `?${query}` : ''}`)
  }

  async getSecretariatAvailability(fromUtc: string, toUtc: string): Promise<SpaceAvailabilityResponse> { if (this.useMocks) { const items = this.mockSpaces.map(space => ({ ...space, isAvailable: !this.mockBusySpaces.has(space.id) })); return { fromUtc, toUtc, total: items.length, available: items.filter(x => x.isAvailable).length, items } } const query = new URLSearchParams({ fromUtc, toUtc }); return this.request<SpaceAvailabilityResponse>(`/api/gran-secretaria/espacios/disponibilidad?${query}`) }
  async getSecretariatDocuments(): Promise<SecretariatDocumentsResponse> { if (this.useMocks) return { total: this.mockDocuments.length, items: [...this.mockDocuments] }; return this.request<SecretariatDocumentsResponse>('/api/gran-secretaria/documentos') }
  async createSecretariatSpace(payload: CreateSpaceRequest): Promise<InstitutionalSpace> { if (this.useMocks) { const space: InstitutionalSpace = { id: crypto.randomUUID(), ...payload, location: payload.location ?? null, capacity: payload.capacity ?? null, status: 'active' }; this.mockSpaces.push(space); return space } return this.postJson<InstitutionalSpace>('/api/gran-secretaria/espacios', payload) }
  async createSecretariatReservation(payload: CreateReservationRequest): Promise<{ id: string; status: string }> { if (this.useMocks) { if (this.mockBusySpaces.has(payload.spaceId)) throw new Error('El templo o sala ya está reservado en ese horario.'); this.mockBusySpaces.add(payload.spaceId); return { id: crypto.randomUUID(), status: 'reserved' } } return this.postJson<{ id: string; status: string }>('/api/gran-secretaria/reservas', payload) }
  async issueSecretariatDocument(payload: IssueDocumentRequest): Promise<SecretariatDocument> { if (this.useMocks) { const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: payload.documentType, documentCode: `${payload.documentType === 'decree' ? 'DEC' : 'COM'}-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: payload.title, content: payload.content, organizationId: payload.organizationId ?? null, relatedCeremonyRequestId: null, spaceReservationId: null, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document } return this.postJson<SecretariatDocument>('/api/gran-secretaria/documentos', payload) }

  private postJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json'); const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para consultar la información institucional.'); headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) { if (response.status === 401) await this.onUnauthorized?.(); if (response.status === 403) throw new Error('Su cuenta no tiene permiso para realizar esta operación.'); let message = ''; try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* respuesta sin JSON */ } throw new Error(message || `La API respondió ${response.status} ${response.statusText}.`) }
    return response.json() as Promise<T>
  }
}

export function createDefaultPmgmApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): PmgmApiClient { const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''; const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'; const url = new URL(baseUrl || '/', window.location.origin); if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La API debe usar el mismo origen mediante el proxy institucional.'); return new PmgmApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized }) }
function sleep(milliseconds: number): Promise<void> { return new Promise(resolve => window.setTimeout(resolve, milliseconds)) }
function mockRegimenSummary(filters: { organizationId?: string; asOf?: string; from?: string }): RegimenInteriorSummary { const asOf = filters.asOf ?? '2026-09-08'; const from = filters.from ?? '2026-01-01'; const scoped = !!filters.organizationId; return { scope: scoped ? 'organization' : 'order', organizationId: filters.organizationId ?? null, asOf, period: { from, to: asOf }, members: { totalRelated: scoped ? 41 : 315, currentlyAffiliated: scoped ? 34 : 268, active: scoped ? 31 : 241, inactive: scoped ? 3 : 27, currentWithBlockingStatus: scoped ? 2 : 18 }, events: { voluntaryWithdrawals: scoped ? 1 : 9, forcedWithdrawals: scoped ? 0 : 3, reinstatements: scoped ? 1 : 7, deaths: scoped ? 0 : 4, transfers: scoped ? 2 : 13 }, financialRegularity: { source: 'Gran Tesorería', currentAffiliations: scoped ? 34 : 268, upToDate: scoped ? 28 : 221, delinquent: scoped ? 4 : 29, pending: scoped ? 1 : 10, exempt: scoped ? 1 : 5, withoutStatus: scoped ? 0 : 3, delinquentMembersDistinct: scoped ? 4 : 28 }, degreeDistribution: scoped ? { apprentice: 9, fellowcraft: 8, master: 17 } : { apprentice: 71, fellowcraft: 63, master: 134 }, pendingTransfers: scoped ? 1 : 6 } }
