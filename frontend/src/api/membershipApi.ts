export type MembershipStatus = 'active' | 'transferred' | 'closed'

export interface MemberDirectoryItem {
  memberId: string
  displayName: string
  institutionalNumber: string | null
  membershipStatus: MembershipStatus | string
  membershipType: string
  startDate: string
  endDate: string | null
  currentDegree: string | null
  institutionalStatus: string | null
}

export interface MemberDirectoryResponse {
  organization: { id: string; name: string; number: string | null; type: string }
  total: number
  returned: number
  items: MemberDirectoryItem[]
}

export interface MemberProfile {
  member: { id: string; personId: string; institutionalNumber: string | null; firstNames: string; lastNames: string; createdAtUtc: string }
  contactVisible: boolean
  contact: { email: string | null; phone: string | null; address: string | null } | null
  scope: 'order' | 'authorized_organizations'
  current: {
    membership: MemberMembership | null
    degree: MemberDegreeEvent | null
    institutionalStatus: MemberStatusEvent | null
    offices: MemberOffice[]
  }
  memberships: MemberMembership[]
  degreeEvents: MemberDegreeEvent[]
  offices: MemberOffice[]
  statusEvents: MemberStatusEvent[]
  transfers: MemberTransfer[]
  regularity: { financial: Regularity | null; hospitalaria: Regularity | null } | null
}

export interface MemberMembership { id: string; organizationId: string; organization: string; organizationNumber: string | null; organizationType: string; membershipType: string; startDate: string; endDate: string | null; status: string; endReason: string | null; evidenceReference: string | null }
export interface MemberDegreeEvent { id: string; degree: string; eventType: string; effectiveDate: string; organizationId: string; organization: string; evidenceReference: string | null }
export interface MemberOffice { id: string; officeType: string; period: string; organizationId: string; organization: string; startDate: string; endDate: string | null; evidenceReference: string | null }
export interface MemberStatusEvent { id: string; eventType: string; effectiveDate: string; recordedAtUtc: string; reason: string | null; organizationId: string | null; organization: string | null; evidenceReference: string | null; notes: string | null }
export interface MemberTransfer { id: string; sourceOrganizationId: string; sourceOrganization: string; targetOrganizationId: string; targetOrganization: string; requestedDate: string; proposedEffectiveDate: string; approvedEffectiveDate: string | null; status: string; reason: string | null; resolution: string | null; evidenceReference: string | null; executedAtUtc: string | null }
export interface Regularity { status: string; asOfDate: string; scope?: string; sourceReference: string | null }

export type MembershipAccessTokenProvider = () => Promise<string | null>
interface MembershipApiClientOptions { baseUrl?: string; getAccessToken?: MembershipAccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

const org1 = '11111111-1111-1111-1111-111111111111'
const org23 = '23232323-2323-2323-2323-232323232323'
const demoMembers: MemberDirectoryItem[] = [
  { memberId: 'aaaaaaaa-1111-1111-1111-111111111111', displayName: 'Ana María Rojas Salazar', institutionalNumber: 'GLMC-01842', membershipStatus: 'active', membershipType: 'regular', startDate: '2018-03-12', endDate: null, currentDegree: 'master', institutionalStatus: 'active' },
  { memberId: 'aaaaaaaa-2222-2222-2222-222222222222', displayName: 'Carla Fernández Morales', institutionalNumber: 'GLMC-01977', membershipStatus: 'active', membershipType: 'regular', startDate: '2019-05-15', endDate: null, currentDegree: 'fellowcraft', institutionalStatus: 'active' },
  { memberId: 'aaaaaaaa-3333-3333-3333-333333333333', displayName: 'Daniela Torres Alarcón', institutionalNumber: 'GLMC-02314', membershipStatus: 'active', membershipType: 'regular', startDate: '2023-08-10', endDate: null, currentDegree: 'apprentice', institutionalStatus: 'active' },
  { memberId: 'aaaaaaaa-4444-4444-4444-444444444444', displayName: 'Marcelo Fuentes Araya', institutionalNumber: 'GLMC-01503', membershipStatus: 'active', membershipType: 'regular', startDate: '2016-09-03', endDate: null, currentDegree: 'master', institutionalStatus: 'active' },
  { memberId: 'aaaaaaaa-6666-6666-6666-666666666666', displayName: 'Patricio Mendoza Silva', institutionalNumber: 'GLMC-01298', membershipStatus: 'transferred', membershipType: 'regular', startDate: '2014-04-18', endDate: '2026-06-30', currentDegree: 'master', institutionalStatus: 'workshop_transfer' },
]

export class MembershipApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: MembershipAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: MembershipApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getMembers(organizationId: string, filters: { query?: string; status?: string; limit?: number } = {}): Promise<MemberDirectoryResponse> {
    if (this.useMocks) {
      let items = organizationId === org23 ? demoMembers.filter(x => x.memberId.endsWith('666666666666')) : demoMembers.filter(x => !x.memberId.endsWith('666666666666'))
      if (filters.status) items = items.filter(x => x.membershipStatus === filters.status)
      if (filters.query?.trim()) { const q = normalize(filters.query); items = items.filter(x => normalize(`${x.displayName} ${x.institutionalNumber ?? ''}`).includes(q)) }
      const max = Math.max(1, Math.min(filters.limit ?? 100, 250))
      return { organization: { id: organizationId, name: organizationId === org23 ? 'Taller Demostrativo Nº 23' : 'Taller Demostrativo Nº 1', number: organizationId === org23 ? '23' : '1', type: 'workshop' }, total: items.length, returned: Math.min(items.length, max), items: items.slice(0, max).map(x => ({ ...x })) }
    }
    const query = new URLSearchParams({ organizationId })
    if (filters.query?.trim()) query.set('query', filters.query.trim())
    if (filters.status) query.set('status', filters.status)
    if (filters.limit) query.set('limit', String(filters.limit))
    return this.request<MemberDirectoryResponse>(`/api/members?${query}`)
  }

  async getProfile(memberId: string): Promise<MemberProfile> {
    if (this.useMocks) {
      const item = demoMembers.find(x => x.memberId === memberId)
      if (!item) throw new Error('La ficha solicitada no existe en el QA.')
      return mockProfile(item)
    }
    return this.request<MemberProfile>(`/api/members/${encodeURIComponent(memberId)}/profile`)
  }

  private async request<T>(path: string): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar fichas institucionales.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, { credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para consultar esta ficha institucional.')
      throw new Error(`La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

function mockProfile(item: MemberDirectoryItem): MemberProfile {
  const transferred = item.membershipStatus === 'transferred'
  const activeOrg = transferred ? org23 : org1
  const orgName = transferred ? 'Taller Demostrativo Nº 23' : 'Taller Demostrativo Nº 1'
  const currentMembership: MemberMembership = { id: `m-${item.memberId}`, organizationId: activeOrg, organization: orgName, organizationNumber: transferred ? '23' : '1', organizationType: 'workshop', membershipType: 'regular', startDate: transferred ? '2026-07-01' : item.startDate, endDate: null, status: 'active', endReason: null, evidenceReference: null }
  const degree: MemberDegreeEvent = { id: `d-${item.memberId}`, degree: item.currentDegree ?? 'apprentice', eventType: item.currentDegree === 'master' ? 'exaltation' : item.currentDegree === 'fellowcraft' ? 'wage_increase' : 'initiation', effectiveDate: item.currentDegree === 'master' ? '2020-08-12' : item.startDate, organizationId: activeOrg, organization: orgName, evidenceReference: null }
  const statusEvent: MemberStatusEvent = { id: `s-${item.memberId}`, eventType: 'active', effectiveDate: currentMembership.startDate, recordedAtUtc: '2026-09-08T12:00:00Z', reason: null, organizationId: activeOrg, organization: orgName, evidenceReference: null, notes: null }
  const office: MemberOffice[] = item.currentDegree === 'master' ? [{ id: `o-${item.memberId}`, officeType: 'master_of_ceremonies', period: '2026', organizationId: activeOrg, organization: orgName, startDate: '2026-01-01', endDate: '2026-12-31', evidenceReference: null }] : []
  const oldMembership: MemberMembership = { ...currentMembership, id: `old-${item.memberId}`, organizationId: org1, organization: 'Taller Demostrativo Nº 1', organizationNumber: '1', startDate: item.startDate, endDate: '2026-06-30', status: 'transferred', endReason: 'Cambio de Taller aprobado', evidenceReference: 'TR-DEMO-001' }
  const transfer: MemberTransfer = { id: `t-${item.memberId}`, sourceOrganizationId: org1, sourceOrganization: 'Taller Demostrativo Nº 1', targetOrganizationId: org23, targetOrganization: 'Taller Demostrativo Nº 23', requestedDate: '2026-06-10', proposedEffectiveDate: '2026-07-01', approvedEffectiveDate: '2026-07-01', status: 'executed', reason: 'Continuidad de trabajo logial.', resolution: 'Traslado aprobado con historial preservado.', evidenceReference: 'TR-DEMO-001', executedAtUtc: '2026-07-01T15:00:00Z' }
  const parts = item.displayName.split(' ')
  return { member: { id: item.memberId, personId: `p-${item.memberId}`, institutionalNumber: item.institutionalNumber, firstNames: parts.slice(0, -2).join(' ') || parts[0], lastNames: parts.slice(-2).join(' '), createdAtUtc: '2026-09-08T12:00:00Z' }, contactVisible: true, contact: { email: null, phone: null, address: null }, scope: 'order', current: { membership: currentMembership, degree, institutionalStatus: statusEvent, offices: office }, memberships: transferred ? [currentMembership, oldMembership] : [currentMembership], degreeEvents: [degree], offices: office, statusEvents: [statusEvent], transfers: transferred ? [transfer] : [], regularity: { financial: { status: item.displayName.includes('Marcelo') ? 'delinquent' : 'up_to_date', asOfDate: '2026-09-08', scope: 'member', sourceReference: 'TES-DEMO' }, hospitalaria: { status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-DEMO' } } }
}

export function createDefaultMembershipApiClient(getAccessToken?: MembershipAccessTokenProvider, onUnauthorized?: () => Promise<void>): MembershipApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('Fichas institucionales deben usar el mismo origen mediante el proxy institucional.')
  return new MembershipApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
