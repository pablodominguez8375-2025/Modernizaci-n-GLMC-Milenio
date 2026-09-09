export interface MemberControlWorkshopRef {
  id: string
  name: string
  number: string | null
  startDate: string
  endDate: string | null
}

export interface MemberControlMilestones {
  initiation: string | null
  wageIncrease: string | null
  exaltation: string | null
  withdrawalType: string | null
  withdrawal: string | null
  reinstatement: string | null
  death: string | null
  transfer: string | null
}

export interface MemberControlRow {
  memberId: string
  institutionalNumber: string | null
  displayName: string
  relation: 'current' | 'historical' | string
  currentWorkshop: MemberControlWorkshopRef | null
  lastWorkshop: MemberControlWorkshopRef
  currentStatus: string
  statusEffectiveDate: string | null
  currentDegree: string | null
  milestones: MemberControlMilestones
  financialStatus: string | null
  pastActive: boolean
  pendingTransfer: boolean
  membershipHistoryCount: number
}

export interface MemberControlResponse {
  asOf: string
  total: number
  returned: number
  items: MemberControlRow[]
}

export interface MemberControlFilters {
  asOf?: string
  organizationId?: string
  status?: string
  degree?: string
  financialStatus?: string
  search?: string
  pastActiveOnly?: boolean
  pendingTransferOnly?: boolean
  limit?: number
}

export type InternalAffairsTokenProvider = () => Promise<string | null>

interface InternalAffairsApiClientOptions {
  baseUrl?: string
  getAccessToken?: InternalAffairsTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

export class InternalAffairsApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: InternalAffairsTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: InternalAffairsApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getMembers(filters: MemberControlFilters = {}): Promise<MemberControlResponse> {
    if (this.useMocks) return mockResponse(filters)
    const query = new URLSearchParams()
    if (filters.asOf) query.set('asOf', filters.asOf)
    if (filters.organizationId) query.set('organizationId', filters.organizationId)
    if (filters.status) query.set('status', filters.status)
    if (filters.degree) query.set('degree', filters.degree)
    if (filters.financialStatus) query.set('financialStatus', filters.financialStatus)
    if (filters.search?.trim()) query.set('search', filters.search.trim())
    if (filters.pastActiveOnly) query.set('pastActiveOnly', 'true')
    if (filters.pendingTransferOnly) query.set('pendingTransferOnly', 'true')
    if (filters.limit) query.set('limit', String(filters.limit))
    return this.request<MemberControlResponse>(`/api/regimen-interior/members${query.size ? `?${query}` : ''}`)
  }

  private async request<T>(path: string): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para operar Régimen Interior.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'GET', credentials: 'omit', cache: 'no-store', redirect: 'error', headers,
    })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para consultar el control de miembros de Régimen Interior.')
      let message = ''
      try {
        const body = await response.clone().json() as { message?: string }
        message = typeof body.message === 'string' ? body.message : ''
      } catch { /* sin cuerpo JSON */ }
      throw new Error(message || `La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<T>
  }
}

export function createDefaultInternalAffairsApiClient(getAccessToken?: InternalAffairsTokenProvider, onUnauthorized?: () => Promise<void>): InternalAffairsApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('Régimen Interior debe usar el mismo origen mediante el proxy institucional.')
  }
  return new InternalAffairsApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

const demoRows: MemberControlRow[] = [
  {
    memberId: '10101010-1010-1010-1010-101010101010', institutionalNumber: 'GLM-0101', displayName: 'Hermana Demostrativa Uno', relation: 'current',
    currentWorkshop: workshop('11111111-1111-1111-1111-111111111111', 'Taller Demostrativo Nº 1', '1', '2018-03-12'), lastWorkshop: workshop('11111111-1111-1111-1111-111111111111', 'Taller Demostrativo Nº 1', '1', '2018-03-12'),
    currentStatus: 'active', statusEffectiveDate: '2025-01-10', currentDegree: 'master', milestones: milestones('2018-03-12', '2019-05-20', '2020-08-14'), financialStatus: 'up_to_date', pastActive: true, pendingTransfer: false, membershipHistoryCount: 1,
  },
  {
    memberId: '20202020-2020-2020-2020-202020202020', institutionalNumber: 'GLM-0230', displayName: 'Hermano Trasladado Demostrativo', relation: 'historical',
    currentWorkshop: workshop('23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', '23', '2026-01-01'), lastWorkshop: workshop('23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', '23', '2026-01-01'),
    currentStatus: 'reinstated', statusEffectiveDate: '2024-03-15', currentDegree: 'master', milestones: { ...milestones('2017-04-08', '2018-06-11', '2019-09-21'), withdrawalType: 'voluntary_withdrawal', withdrawal: '2024-01-15', reinstatement: '2024-03-15', transfer: '2026-01-01' }, financialStatus: 'delinquent', pastActive: true, pendingTransfer: false, membershipHistoryCount: 2,
  },
  {
    memberId: '30303030-3030-3030-3030-303030303030', institutionalNumber: 'GLM-0303', displayName: 'Hermana Inactiva Demostrativa', relation: 'current',
    currentWorkshop: workshop('23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', '23', '2021-07-02'), lastWorkshop: workshop('23232323-2323-2323-2323-232323232323', 'Taller Demostrativo Nº 23', '23', '2021-07-02'),
    currentStatus: 'inactive', statusEffectiveDate: '2026-06-01', currentDegree: 'fellowcraft', milestones: milestones('2021-07-02', '2023-03-18', null), financialStatus: 'pending', pastActive: false, pendingTransfer: true, membershipHistoryCount: 1,
  },
  {
    memberId: '40404040-4040-4040-4040-404040404040', institutionalNumber: 'GLM-0404', displayName: 'Hermano Histórico Demostrativo', relation: 'historical', currentWorkshop: null,
    lastWorkshop: { ...workshop('45454545-4545-4545-4545-454545454545', 'Taller Demostrativo Nº 45', '45', '2012-02-04'), endDate: '2025-10-15' },
    currentStatus: 'voluntary_withdrawal', statusEffectiveDate: '2025-10-15', currentDegree: 'master', milestones: { ...milestones('2012-02-04', '2013-05-12', '2014-08-23'), withdrawalType: 'voluntary_withdrawal', withdrawal: '2025-10-15' }, financialStatus: null, pastActive: true, pendingTransfer: false, membershipHistoryCount: 1,
  },
]

function mockResponse(filters: MemberControlFilters): MemberControlResponse {
  let items = demoRows.map(row => JSON.parse(JSON.stringify(row)) as MemberControlRow)
  if (filters.organizationId) items = items.filter(row => row.currentWorkshop?.id === filters.organizationId || row.lastWorkshop.id === filters.organizationId)
  if (filters.status) items = items.filter(row => row.currentStatus === filters.status)
  if (filters.degree) items = items.filter(row => row.currentDegree === filters.degree)
  if (filters.financialStatus) items = items.filter(row => filters.financialStatus === 'no_status' ? row.financialStatus === null : row.financialStatus === filters.financialStatus)
  if (filters.pastActiveOnly) items = items.filter(row => row.pastActive)
  if (filters.pendingTransferOnly) items = items.filter(row => row.pendingTransfer)
  if (filters.search?.trim()) {
    const term = normalize(filters.search)
    items = items.filter(row => normalize(`${row.displayName} ${row.institutionalNumber ?? ''} ${row.currentWorkshop?.name ?? ''} ${row.lastWorkshop.name}`).includes(term))
  }
  const total = items.length
  const limit = Math.max(1, Math.min(filters.limit ?? 250, 1000))
  items = items.slice(0, limit)
  return { asOf: filters.asOf || '2026-09-09', total, returned: items.length, items }
}

function workshop(id: string, name: string, number: string, startDate: string): MemberControlWorkshopRef { return { id, name, number, startDate, endDate: null } }
function milestones(initiation: string | null, wageIncrease: string | null, exaltation: string | null): MemberControlMilestones { return { initiation, wageIncrease, exaltation, withdrawalType: null, withdrawal: null, reinstatement: null, death: null, transfer: null } }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
