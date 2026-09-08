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
export type CeremonyType = 'initiation' | 'wage_increase' | 'exaltation'
export interface GrandSecretariatCeremonyQueueItem {
  id: string; organizationId: string; organizationName: string; organizationNumber: string | null; ceremonyType: CeremonyType; proposedDate: string | null; status: string; formalAuthorizationIssued: boolean
  spaceReservationId: string | null; spaceName: string | null; reservationStartsAtUtc: string | null; reservationEndsAtUtc: string | null; createdAtUtc: string
}
export interface GrandSecretariatCeremonyQueueResponse { total: number; items: GrandSecretariatCeremonyQueueItem[] }
export interface RegimenInteriorSummary {
  scope: 'order' | 'organization'; organizationId: string | null; asOf: string; period: { from: string; to: string }
  members: { totalRelated: number; currentlyAffiliated: number; active: number; inactive: number; currentWithBlockingStatus: number }
  events: { voluntaryWithdrawals: number; forcedWithdrawals: number; reinstatements: number; deaths: number; transfers: number }
  financialRegularity: { source: string; currentAffiliations: number; upToDate: number; delinquent: number; pending: number; exempt: number; withoutStatus: number; delinquentMembersDistinct: number }
  degreeDistribution: Record<string, number>; pendingTransfers: number
}
export type TreasuryRegularityStatus = 'up_to_date' | 'delinquent' | 'pending' | 'exempt'
export type HospitalariaRegularityStatus = 'up_to_date' | 'overdue' | 'pending' | 'exempt'
export interface WorkshopRegularitySnapshot {
  id?: string; organizationId?: string; memberId?: string | null; scope?: string; status: string; asOfDate: string; sourceReference?: string | null; notes?: string | null; recordedAtUtc?: string
}
export interface WorkshopRegularityRequest { status: string; asOfDate: string; sourceReference?: string | null; notes?: string | null }
export type AccessTokenProvider = () => Promise<string | null>
export interface PmgmApiClientOptions { baseUrl?: string; getAccessToken?: AccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

class PmgmApiHttpError extends Error { constructor(readonly status: number, message: string) { super(message); this.name = 'PmgmApiHttpError' } }

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
const defaultMockCeremonies: GrandSecretariatCeremonyQueueItem[] = [
  { id: 'cccccccc-1111-1111-1111-111111111111', organizationId: defaultMockOrganizations[1].id, organizationName: defaultMockOrganizations[1].name, organizationNumber: '23', ceremonyType: 'wage_increase', proposedDate: '2026-09-18', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: null, spaceName: null, reservationStartsAtUtc: null, reservationEndsAtUtc: null, createdAtUtc: '2026-09-07T16:00:00Z' },
  { id: 'cccccccc-2222-2222-2222-222222222222', organizationId: defaultMockOrganizations[0].id, organizationName: defaultMockOrganizations[0].name, organizationNumber: '1', ceremonyType: 'exaltation', proposedDate: '2026-09-20', status: 'authorized', formalAuthorizationIssued: false, spaceReservationId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', spaceName: defaultMockSpaces[0].name, reservationStartsAtUtc: '2026-09-20T22:00:00Z', reservationEndsAtUtc: '2026-09-21T01:00:00Z', createdAtUtc: '2026-09-07T17:00:00Z' },
]

export class PmgmApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: AccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>
  private readonly mockOrganizations = [...defaultMockOrganizations]
  private readonly mockSpaces = [...defaultMockSpaces]
  private readonly mockBusySpaces = new Set<string>([defaultMockSpaces[0].id])
  private readonly mockDocuments: SecretariatDocument[] = []
  private readonly mockCeremonies = defaultMockCeremonies.map(item => ({ ...item }))
  private readonly mockTreasury = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'treasury-demo-1', organizationId: defaultMockOrganizations[0].id, scope: 'organization', status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'TES-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:00:00Z' }]])
  private readonly mockHospitalaria = new Map<string, WorkshopRegularitySnapshot>([[defaultMockOrganizations[0].id, { id: 'hospitalaria-demo-1', organizationId: defaultMockOrganizations[0].id, status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-DEMO-001', notes: null, recordedAtUtc: '2026-09-08T12:05:00Z' }]])

  constructor(options: PmgmApiClientOptions = {}) { this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, ''); this.getAccessToken = options.getAccessToken; this.useMocks = options.useMocks ?? false; this.onUnauthorized = options.onUnauthorized }

  async getCandidatePortal(): Promise<CandidatePortalResponse> { if (this.useMocks) { await sleep(120); return { culture: 'es-CL', portal: 'Insinuados en período de publicación', total: mockCandidates.length, items: mockCandidates } } return this.request<CandidatePortalResponse>('/api/ceremonias/portal-insinuados') }
  async getSystemInfo(): Promise<SystemInfo> { if (this.useMocks) return { project: 'Proyecto Milenio — Modernización Gran Logia Mixta de Chile', api: 'PMGM.Api', version: '0.12.0', runtime: '.NET 10', culture: 'es-CL', institutionalTimeZone: 'America/Santiago', defaultCurrency: 'CLP' }; return this.request<SystemInfo>('/api/system/info') }
  async getSessionProfile(): Promise<SessionProfile> { if (this.useMocks) return mockSession; return this.request<SessionProfile>('/api/session/me') }
  async getOrganizationOptions(): Promise<OrganizationOptionsResponse> { if (this.useMocks) return { total: this.mockOrganizations.length, items: [...this.mockOrganizations] }; return this.request<OrganizationOptionsResponse>('/api/institutional/organizations/options') }

  async getRegimenInteriorSummary(filters: { organizationId?: string; asOf?: string; from?: string } = {}): Promise<RegimenInteriorSummary> {
    if (this.useMocks) return mockRegimenSummary(filters)
    const query = new URLSearchParams(); if (filters.organizationId) query.set('organizationId', filters.organizationId); if (filters.asOf) query.set('asOf', filters.asOf); if (filters.from) query.set('from', filters.from)
    return this.request<RegimenInteriorSummary>(`/api/regimen-interior/summary${query.size ? `?${query}` : ''}`)
  }

  async getTreasuryWorkshopRegularity(organizationId: string, asOf?: string): Promise<WorkshopRegularitySnapshot | null> {
    if (this.useMocks) return mockSnapshotAsOf(this.mockTreasury.get(organizationId), asOf)
    const query = new URLSearchParams(); if (asOf) query.set('asOf', asOf)
    return this.optionalGet<WorkshopRegularitySnapshot>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/regularidad${query.size ? `?${query}` : ''}`)
  }
  async setTreasuryWorkshopRegularity(organizationId: string, payload: WorkshopRegularityRequest): Promise<WorkshopRegularitySnapshot> {
    if (this.useMocks) { const snapshot = mockRegularitySnapshot(organizationId, payload, 'treasury'); this.mockTreasury.set(organizationId, snapshot); return snapshot }
    return this.postJson<WorkshopRegularitySnapshot>(`/api/tesoreria/talleres/${encodeURIComponent(organizationId)}/regularidad`, payload)
  }
  async getHospitalariaWorkshopRegularity(organizationId: string, asOf?: string): Promise<WorkshopRegularitySnapshot | null> {
    if (this.useMocks) return mockSnapshotAsOf(this.mockHospitalaria.get(organizationId), asOf)
    const query = new URLSearchParams(); if (asOf) query.set('asOf', asOf)
    return this.optionalGet<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad${query.size ? `?${query}` : ''}`)
  }
  async setHospitalariaWorkshopRegularity(organizationId: string, payload: WorkshopRegularityRequest): Promise<WorkshopRegularitySnapshot> {
    if (this.useMocks) { const snapshot = mockRegularitySnapshot(organizationId, payload, 'hospitalaria'); this.mockHospitalaria.set(organizationId, snapshot); return snapshot }
    return this.postJson<WorkshopRegularitySnapshot>(`/api/hospitalaria/talleres/${encodeURIComponent(organizationId)}/regularidad`, payload)
  }

  async getSecretariatAvailability(fromUtc: string, toUtc: string): Promise<SpaceAvailabilityResponse> { if (this.useMocks) { const items = this.mockSpaces.map(space => ({ ...space, isAvailable: !this.mockBusySpaces.has(space.id) })); return { fromUtc, toUtc, total: items.length, available: items.filter(x => x.isAvailable).length, items } } const query = new URLSearchParams({ fromUtc, toUtc }); return this.request<SpaceAvailabilityResponse>(`/api/gran-secretaria/espacios/disponibilidad?${query}`) }
  async getSecretariatDocuments(): Promise<SecretariatDocumentsResponse> { if (this.useMocks) return { total: this.mockDocuments.length, items: [...this.mockDocuments] }; return this.request<SecretariatDocumentsResponse>('/api/gran-secretaria/documentos') }
  async getSecretariatCeremonyQueue(): Promise<GrandSecretariatCeremonyQueueResponse> { if (this.useMocks) return { total: this.mockCeremonies.length, items: this.mockCeremonies.map(item => ({ ...item })) }; return this.request<GrandSecretariatCeremonyQueueResponse>('/api/institutional/gran-secretaria/ceremonias-autorizadas') }
  async createSecretariatSpace(payload: CreateSpaceRequest): Promise<InstitutionalSpace> { if (this.useMocks) { const space: InstitutionalSpace = { id: crypto.randomUUID(), ...payload, location: payload.location ?? null, capacity: payload.capacity ?? null, status: 'active' }; this.mockSpaces.push(space); return space } return this.postJson<InstitutionalSpace>('/api/gran-secretaria/espacios', payload) }
  async createSecretariatReservation(payload: CreateReservationRequest): Promise<{ id: string; status: string }> {
    if (this.useMocks) {
      if (this.mockBusySpaces.has(payload.spaceId)) throw new Error('El templo o sala ya está reservado en ese horario.')
      const ceremony = payload.ceremonyRequestId ? this.mockCeremonies.find(item => item.id === payload.ceremonyRequestId) : undefined
      if (payload.ceremonyRequestId && !ceremony) throw new Error('La ceremonia indicada no existe en la bandeja autorizada.')
      if (ceremony && ceremony.organizationId !== payload.organizationId) throw new Error('La ceremonia no corresponde al Taller indicado.')
      const id = crypto.randomUUID(); this.mockBusySpaces.add(payload.spaceId)
      if (ceremony) { const space = this.mockSpaces.find(item => item.id === payload.spaceId); ceremony.spaceReservationId = id; ceremony.spaceName = space?.name ?? 'Espacio institucional'; ceremony.reservationStartsAtUtc = payload.startsAtUtc; ceremony.reservationEndsAtUtc = payload.endsAtUtc }
      return { id, status: 'reserved' }
    }
    return this.postJson<{ id: string; status: string }>('/api/gran-secretaria/reservas', payload)
  }
  async issueSecretariatDocument(payload: IssueDocumentRequest): Promise<SecretariatDocument> { if (this.useMocks) { const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: payload.documentType, documentCode: `${payload.documentType === 'decree' ? 'DEC' : 'COM'}-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: payload.title, content: payload.content, organizationId: payload.organizationId ?? null, relatedCeremonyRequestId: null, spaceReservationId: null, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document } return this.postJson<SecretariatDocument>('/api/gran-secretaria/documentos', payload) }
  async issueSecretariatCeremonyAuthorization(ceremonyRequestId: string, spaceReservationId: string | null = null): Promise<SecretariatDocument> {
    if (this.useMocks) {
      const ceremony = this.mockCeremonies.find(item => item.id === ceremonyRequestId); if (!ceremony) throw new Error('La ceremonia indicada no existe.'); if (ceremony.formalAuthorizationIssued) throw new Error('La ceremonia ya cuenta con autorización formal vigente.'); if (spaceReservationId && ceremony.spaceReservationId !== spaceReservationId) throw new Error('La reserva indicada no corresponde a esta ceremonia.'); ceremony.formalAuthorizationIssued = true
      const document: SecretariatDocument = { id: crypto.randomUUID(), documentType: 'ceremony_authorization', documentCode: `AUT-CER-DEMO-${String(this.mockDocuments.length + 1).padStart(3, '0')}`, title: `Autorización de ceremonia — ${ceremonyTypeLabel(ceremony.ceremonyType)}`, content: `Autorización institucional demostrativa para ${ceremony.organizationName}.`, organizationId: ceremony.organizationId, relatedCeremonyRequestId: ceremony.id, spaceReservationId, status: 'issued', issuedAtUtc: new Date().toISOString(), issuedBySubject: 'demo' }; this.mockDocuments.unshift(document); return document
    }
    return this.postJson<SecretariatDocument>(`/api/gran-secretaria/ceremonias/${encodeURIComponent(ceremonyRequestId)}/autorizacion`, { spaceReservationId })
  }

  private postJson<T>(path: string, payload: unknown): Promise<T> { return this.request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }) }
  private async optionalGet<T>(path: string): Promise<T | null> { try { return await this.request<T>(path) } catch (error) { if (error instanceof PmgmApiHttpError && error.status === 404) return null; throw error } }
  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers); headers.set('Accept', 'application/json')
    const token = await this.getAccessToken?.(); if (!token) throw new Error('Debe ingresar para consultar la información institucional.'); headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      let message = ''
      try { const body = await response.clone().json() as { message?: string }; message = typeof body.message === 'string' ? body.message : '' } catch { /* respuesta sin JSON */ }
      if (response.status === 403) message = 'Su cuenta no tiene permiso para realizar esta operación.'
      throw new PmgmApiHttpError(response.status, message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultPmgmApiClient(getAccessToken?: AccessTokenProvider, onUnauthorized?: () => Promise<void>): PmgmApiClient { const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''; const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'; const url = new URL(baseUrl || '/', window.location.origin); if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La API debe usar el mismo origen mediante el proxy institucional.'); return new PmgmApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized }) }
function sleep(milliseconds: number): Promise<void> { return new Promise(resolve => window.setTimeout(resolve, milliseconds)) }
function ceremonyTypeLabel(type: CeremonyType) { return type === 'initiation' ? 'Iniciación' : type === 'wage_increase' ? 'Aumento de salario' : 'Exaltación' }
function mockSnapshotAsOf(snapshot: WorkshopRegularitySnapshot | undefined, asOf?: string): WorkshopRegularitySnapshot | null { if (!snapshot) return null; if (asOf && snapshot.asOfDate > asOf) return null; return { ...snapshot } }
function mockRegularitySnapshot(organizationId: string, payload: WorkshopRegularityRequest, prefix: string): WorkshopRegularitySnapshot { return { id: `${prefix}-${crypto.randomUUID()}`, organizationId, scope: 'organization', status: payload.status, asOfDate: payload.asOfDate, sourceReference: payload.sourceReference ?? null, notes: payload.notes ?? null, recordedAtUtc: new Date().toISOString() } }
function mockRegimenSummary(filters: { organizationId?: string; asOf?: string; from?: string }): RegimenInteriorSummary { const asOf = filters.asOf ?? '2026-09-08'; const from = filters.from ?? '2026-01-01'; const scoped = !!filters.organizationId; return { scope: scoped ? 'organization' : 'order', organizationId: filters.organizationId ?? null, asOf, period: { from, to: asOf }, members: { totalRelated: scoped ? 41 : 315, currentlyAffiliated: scoped ? 34 : 268, active: scoped ? 31 : 241, inactive: scoped ? 3 : 27, currentWithBlockingStatus: scoped ? 2 : 18 }, events: { voluntaryWithdrawals: scoped ? 1 : 9, forcedWithdrawals: scoped ? 0 : 3, reinstatements: scoped ? 1 : 7, deaths: scoped ? 0 : 4, transfers: scoped ? 2 : 13 }, financialRegularity: { source: 'Gran Tesorería', currentAffiliations: scoped ? 34 : 268, upToDate: scoped ? 28 : 221, delinquent: scoped ? 4 : 29, pending: scoped ? 1 : 10, exempt: scoped ? 1 : 5, withoutStatus: scoped ? 0 : 3, delinquentMembersDistinct: scoped ? 4 : 28 }, degreeDistribution: scoped ? { apprentice: 9, fellowcraft: 8, master: 17 } : { apprentice: 71, fellowcraft: 63, master: 134 }, pendingTransfers: scoped ? 1 : 6 } }
