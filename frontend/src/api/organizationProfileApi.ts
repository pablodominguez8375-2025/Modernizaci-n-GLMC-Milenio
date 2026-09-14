export interface OrganizationAuthority {
  id: string
  officeType: string
  period: string
  memberId: string
  displayName: string
  startDate: string
  endDate: string | null
}

export interface OrganizationMeeting {
  id: string
  meetingDate: string
  meetingType: string
  grade: string
  title: string | null
  status: string
  closedAtUtc: string | null
}

export interface OrganizationInstruction {
  id: string
  instructionDate: string
  grade: string
  topic: string
  responsibleOffice: string
  status: string
}

export interface OrganizationTransfer {
  id: string
  memberId: string
  memberDisplayName: string
  sourceOrganizationId: string
  sourceOrganization: string
  targetOrganizationId: string
  targetOrganization: string
  requestedDate: string
  approvedEffectiveDate: string | null
  status: string
  direction: 'incoming' | 'outgoing'
}

export interface OrganizationProfile {
  organization: { id: string; name: string; number: string | null; type: string; parentOrganizationId: string | null; createdAtUtc: string }
  members: { active: number; degreeDistribution: Record<string, number> }
  authorities: OrganizationAuthority[]
  regularity: {
    financial: { status: string; asOfDate: string; sourceReference: string | null } | null
    hospitalaria: { status: string; asOfDate: string; sourceReference: string | null } | null
  } | null
  activity: {
    recentMeetings: OrganizationMeeting[]
    recentInstruction: OrganizationInstruction[]
    recentTransfers: OrganizationTransfer[]
  }
}

export type OrganizationAccessTokenProvider = () => Promise<string | null>
interface OrganizationProfileApiClientOptions { baseUrl?: string; getAccessToken?: OrganizationAccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

const ORG_1 = '11111111-1111-1111-1111-111111111111'
const ORG_23 = '23232323-2323-2323-2323-232323232323'

export class OrganizationProfileApiClient {
  private readonly baseUrl: string
  private readonly getAccessToken?: OrganizationAccessTokenProvider
  readonly useMocks: boolean
  private readonly onUnauthorized?: () => Promise<void>

  constructor(options: OrganizationProfileApiClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? '').replace(/\/$/, '')
    this.getAccessToken = options.getAccessToken
    this.useMocks = options.useMocks ?? false
    this.onUnauthorized = options.onUnauthorized
  }

  async getProfile(organizationId: string): Promise<OrganizationProfile> {
    if (this.useMocks) return demoProfile(organizationId)
    const headers = new Headers({ Accept: 'application/json' })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar la ficha del Taller.')
    headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${this.baseUrl}/api/institutional/organizations/${encodeURIComponent(organizationId)}/profile`, { credentials: 'omit', redirect: 'error', cache: 'no-store', headers })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para consultar este Taller.')
      throw new Error(`La API respondió ${response.status} ${response.statusText}.`)
    }
    return response.json() as Promise<OrganizationProfile>
  }
}

export function createDefaultOrganizationProfileApiClient(getAccessToken?: OrganizationAccessTokenProvider, onUnauthorized?: () => Promise<void>): OrganizationProfileApiClient {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const useMocks = import.meta.env.VITE_USE_MOCKS === 'true'
  const url = new URL(baseUrl || '/', window.location.origin)
  if (url.origin !== window.location.origin || url.username || url.password || url.search || url.hash) throw new Error('La ficha del Taller debe usar el mismo origen mediante el proxy institucional.')
  return new OrganizationProfileApiClient({ baseUrl, useMocks, getAccessToken, onUnauthorized })
}

function demoProfile(organizationId: string): OrganizationProfile {
  const second = organizationId === ORG_23
  const id = second ? ORG_23 : ORG_1
  const name = second ? 'Taller Demostrativo Nº 23' : 'Taller Demostrativo Nº 1'
  const number = second ? '23' : '1'
  const other = second ? 'Taller Demostrativo Nº 1' : 'Taller Demostrativo Nº 23'
  const otherId = second ? ORG_1 : ORG_23
  return {
    organization: { id, name, number, type: 'workshop', parentOrganizationId: null, createdAtUtc: '2010-01-01T12:00:00Z' },
    members: { active: second ? 19 : 27, degreeDistribution: second ? { apprentice: 6, fellowcraft: 5, master: 8 } : { apprentice: 8, fellowcraft: 7, master: 12 } },
    authorities: [
      { id: `${id}-vm`, officeType: 'venerable_master', period: '2026', memberId: 'demo-vm', displayName: second ? 'Valentina Torres' : 'Alejandra Rojas', startDate: '2026-01-01', endDate: '2026-12-31' },
      { id: `${id}-sec`, officeType: 'secretary', period: '2026', memberId: 'demo-sec', displayName: second ? 'Patricio Mendoza' : 'Ana María Rojas', startDate: '2026-01-01', endDate: '2026-12-31' },
      { id: `${id}-tre`, officeType: 'treasurer', period: '2026', memberId: 'demo-tre', displayName: second ? 'Daniela Pérez' : 'Carla Fernández', startDate: '2026-01-01', endDate: '2026-12-31' },
    ],
    regularity: { financial: { status: second ? 'pending' : 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'TES-QA-2026' }, hospitalaria: { status: 'up_to_date', asOfDate: '2026-09-08', sourceReference: 'HOSP-QA-2026' } },
    activity: {
      recentMeetings: [
        { id: `${id}-m1`, meetingDate: '2026-09-07', meetingType: 'regular', grade: 'first', title: 'Tenida regular', status: 'closed', closedAtUtc: '2026-09-08T01:10:00Z' },
        { id: `${id}-m2`, meetingDate: '2026-08-24', meetingType: 'instruction', grade: 'first', title: 'Tenida de instrucción', status: 'closed', closedAtUtc: '2026-08-25T01:00:00Z' },
        { id: `${id}-m3`, meetingDate: '2026-09-21', meetingType: 'regular', grade: 'first', title: 'Próxima Tenida', status: 'scheduled', closedAtUtc: null },
      ],
      recentInstruction: [
        { id: `${id}-i1`, instructionDate: '2026-09-14', grade: 'apprentice', topic: 'Simbología y herramientas del Aprendiz', responsibleOffice: 'second_warden', status: 'scheduled' },
        { id: `${id}-i2`, instructionDate: '2026-08-31', grade: 'fellowcraft', topic: 'Las artes liberales', responsibleOffice: 'first_warden', status: 'completed' },
      ],
      recentTransfers: [
        { id: `${id}-t1`, memberId: 'demo-transfer', memberDisplayName: 'Patricio Mendoza Silva', sourceOrganizationId: second ? otherId : id, sourceOrganization: second ? other : name, targetOrganizationId: second ? id : otherId, targetOrganization: second ? name : other, requestedDate: '2026-06-10', approvedEffectiveDate: '2026-07-01', status: 'executed', direction: second ? 'incoming' : 'outgoing' },
      ],
    },
  }
}
