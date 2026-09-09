export interface ExecutiveEventCounters {
  voluntaryWithdrawals: number
  forcedWithdrawals: number
  reinstatements: number
  deaths: number
  transfers: number
}

export interface ExecutiveOrderFinancial {
  delinquentAffiliations: number
  delinquentMembersDistinct: number
  affiliationsWithoutStatus: number
  workshopsTreasuryDelinquent: number
  workshopsHospitalariaOverdue: number
}

export interface ExecutiveWorkshopFinancial {
  delinquentAffiliations: number
  affiliationsWithoutStatus: number
  workshopTreasuryStatus: string | null
  workshopTreasuryAsOf: string | null
  workshopHospitalariaStatus: string | null
  workshopHospitalariaAsOf: string | null
}

export interface ExecutiveWorkshopActivity {
  meetings: number
  instructionSessions: number
  pendingTransfers: number
}

export interface ExecutiveWorkshopRow {
  organizationId: string
  name: string
  number: string | null
  currentMembers: number
  activeMembers: number
  inactiveMembers: number
  blockingMembers: number
  pastActive: number
  degreeDistribution: Record<string, number>
  events: ExecutiveEventCounters
  financial: ExecutiveWorkshopFinancial
  activity: ExecutiveWorkshopActivity
  attentionRequired: boolean
  attentionFlags: string[]
}

export interface ExecutiveReport {
  asOf: string
  period: { from: string; to: string }
  pastActiveDefinition: string
  overview: {
    workshops: number
    currentMembers: number
    activeMembers: number
    inactiveMembers: number
    blockingMembers: number
    pastActive: number
    events: ExecutiveEventCounters
    financial: ExecutiveOrderFinancial
    pendingTransfers: number
    workshopsRequiringAttention: number
  }
  workshops: ExecutiveWorkshopRow[]
}

export type ReportingAccessTokenProvider = () => Promise<string | null>

interface ReportingApiClientOptions {
  baseUrl?: string
  getAccessToken?: ReportingAccessTokenProvider
  useMocks?: boolean
  onUnauthorized?: () => Promise<void>
}

export class ReportingApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: ReportingAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: ReportingApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getExecutiveReport(filters: { asOf?: string; from?: string } = {}): Promise<ExecutiveReport> {
    if (this.useMocks) return cloneReport(mockExecutiveReport(filters))
    const query = new URLSearchParams()
    if (filters.asOf) query.set('asOf', filters.asOf)
    if (filters.from) query.set('from', filters.from)
    return this.request<ExecutiveReport>(`/api/reporting/executive${query.size ? `?${query}` : ''}`)
  }

  private async request<T>(path: string): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar la reportería ejecutiva.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'GET', credentials: 'omit', redirect: 'error', cache: 'no-store', headers,
    })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para consultar la reportería ejecutiva.')
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

export function createDefaultReportingApiClient(getAccessToken?: ReportingAccessTokenProvider, onUnauthorized?: () => Promise<void>): ReportingApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) {
    throw new Error('Reportería ejecutiva debe usar el mismo origen mediante el proxy institucional.')
  }
  return new ReportingApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function mockExecutiveReport(filters: { asOf?: string; from?: string }): ExecutiveReport {
  const asOf = filters.asOf || '2026-09-09'
  const from = filters.from || '2026-01-01'
  const workshops: ExecutiveWorkshopRow[] = [
    {
      organizationId: '11111111-1111-1111-1111-111111111111', name: 'Taller Demostrativo Nº 1', number: '1',
      currentMembers: 47, activeMembers: 43, inactiveMembers: 3, blockingMembers: 1, pastActive: 12,
      degreeDistribution: { apprentice: 14, fellowcraft: 11, master: 22 },
      events: { voluntaryWithdrawals: 1, forcedWithdrawals: 0, reinstatements: 2, deaths: 0, transfers: 3 },
      financial: { delinquentAffiliations: 4, affiliationsWithoutStatus: 1, workshopTreasuryStatus: 'up_to_date', workshopTreasuryAsOf: '2026-09-01', workshopHospitalariaStatus: 'up_to_date', workshopHospitalariaAsOf: '2026-09-01' },
      activity: { meetings: 18, instructionSessions: 7, pendingTransfers: 1 }, attentionRequired: true, attentionFlags: ['blocking_member_status', 'pending_transfers'],
    },
    {
      organizationId: '23232323-2323-2323-2323-232323232323', name: 'Taller Demostrativo Nº 23', number: '23',
      currentMembers: 36, activeMembers: 31, inactiveMembers: 2, blockingMembers: 3, pastActive: 9,
      degreeDistribution: { apprentice: 10, fellowcraft: 8, master: 18 },
      events: { voluntaryWithdrawals: 2, forcedWithdrawals: 1, reinstatements: 0, deaths: 1, transfers: 2 },
      financial: { delinquentAffiliations: 7, affiliationsWithoutStatus: 2, workshopTreasuryStatus: 'delinquent', workshopTreasuryAsOf: '2026-09-01', workshopHospitalariaStatus: 'overdue', workshopHospitalariaAsOf: '2026-09-01' },
      activity: { meetings: 15, instructionSessions: 5, pendingTransfers: 2 }, attentionRequired: true, attentionFlags: ['treasury_delinquent', 'hospitalaria_overdue', 'blocking_member_status', 'pending_transfers'],
    },
    {
      organizationId: '45454545-4545-4545-4545-454545454545', name: 'Taller Demostrativo Nº 45', number: '45',
      currentMembers: 29, activeMembers: 28, inactiveMembers: 1, blockingMembers: 0, pastActive: 6,
      degreeDistribution: { apprentice: 9, fellowcraft: 7, master: 13 },
      events: { voluntaryWithdrawals: 0, forcedWithdrawals: 0, reinstatements: 1, deaths: 0, transfers: 1 },
      financial: { delinquentAffiliations: 1, affiliationsWithoutStatus: 0, workshopTreasuryStatus: 'up_to_date', workshopTreasuryAsOf: '2026-09-01', workshopHospitalariaStatus: 'up_to_date', workshopHospitalariaAsOf: '2026-09-01' },
      activity: { meetings: 17, instructionSessions: 8, pendingTransfers: 0 }, attentionRequired: false, attentionFlags: [],
    },
  ]
  return {
    asOf, period: { from, to: asOf },
    pastActiveDefinition: 'Miembro actualmente afiliado que registra al menos un cargo institucional finalizado antes de la fecha de corte.',
    overview: {
      workshops: workshops.length, currentMembers: 112, activeMembers: 102, inactiveMembers: 6, blockingMembers: 4, pastActive: 27,
      events: { voluntaryWithdrawals: 3, forcedWithdrawals: 1, reinstatements: 3, deaths: 1, transfers: 6 },
      financial: { delinquentAffiliations: 12, delinquentMembersDistinct: 11, affiliationsWithoutStatus: 3, workshopsTreasuryDelinquent: 1, workshopsHospitalariaOverdue: 1 },
      pendingTransfers: 3, workshopsRequiringAttention: 2,
    },
    workshops,
  }
}

function cloneReport(value: ExecutiveReport): ExecutiveReport {
  return JSON.parse(JSON.stringify(value)) as ExecutiveReport
}
