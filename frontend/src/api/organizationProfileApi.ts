import { setDemoWorkshopLocation, zoneFromLocation } from './treasuryTariffs'
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
  organization: { id: string; name: string; number: string | null; type: string; parentOrganizationId: string | null; createdAtUtc: string; establishedOn: string | null; city: string | null; country: string | null; orienteCode?:string|null; treasuryTerritory: string | null; hasLogo?: boolean }
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

export interface LodgeSummaryMaster { memberId: string; displayName: string }
export interface LodgeSummaryGrant { id: string; memberId: string; displayName: string; grantedAtUtc: string; reason: string; isCurrentlyEligible: boolean }
export interface LodgeSummaryAccess { eligibleMasters: LodgeSummaryMaster[]; activeGrants: LodgeSummaryGrant[] }

export type OrganizationAccessTokenProvider = () => Promise<string | null>
interface OrganizationProfileApiClientOptions { baseUrl?: string; getAccessToken?: OrganizationAccessTokenProvider; useMocks?: boolean; onUnauthorized?: () => Promise<void> }

const ORG_1 = '11111111-1111-1111-1111-111111111111'
const ORG_23 = '23232323-2323-2323-2323-232323232323'
const demoSummaryGrants = new Map<string, LodgeSummaryGrant[]>([[ORG_1, []], [ORG_23, []]])
const demoWorkshopMetadata = new Map<string, { name: string; establishedOn: string | null; city: string | null; country: string | null }>()
const demoWorkshopLogos = new Map<string, Blob>()

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
    return this.request<OrganizationProfile>(organizationId, 'profile')
  }

  async updateWorkshopMetadata(organizationId: string, metadata: { name: string; establishedOn: string | null; city: string | null; country: string | null; orienteCode?:string|null }): Promise<void> {
    if (this.useMocks) {
      const code=metadata.orienteCode;
      if(code){metadata={...metadata,country:code==='peru'?'Perú':'Chile',city:code==='santiago'?'Santiago':metadata.city};if(!metadata.city?.trim()||zoneFromLocation(metadata.city,metadata.country)!==(code==='other_chile'?'other_oriente':code))throw new Error('Revise Oriente y ciudad del Taller.')}
      setDemoWorkshopLocation(organizationId,metadata.city,metadata.country)
      demoWorkshopMetadata.set(organizationId, metadata)
      return
    }
    await this.request(organizationId, 'profile/metadata', 'PUT', metadata)
  }

  async getWorkshopLogo(organizationId: string): Promise<Blob | null> {
    if (this.useMocks) return demoWorkshopLogos.get(organizationId) ?? null
    const response = await this.requestRaw(organizationId, 'profile/logo', 'GET')
    return response.status === 404 ? null : response.blob()
  }

  async uploadWorkshopLogo(organizationId: string, file: File): Promise<void> {
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 2 * 1024 * 1024 || file.size === 0) throw new Error('Use PNG o JPEG de hasta 2 MiB.')
    if (this.useMocks) { demoWorkshopLogos.set(organizationId, file); return }
    await this.requestRaw(organizationId, 'profile/logo', 'PUT', file, file.type)
  }

  async removeWorkshopLogo(organizationId: string): Promise<void> {
    if (this.useMocks) { demoWorkshopLogos.delete(organizationId); return }
    await this.requestRaw(organizationId, 'profile/logo', 'DELETE')
  }

  async getSummaryAccess(organizationId: string): Promise<LodgeSummaryAccess> {
    if (this.useMocks) return demoSummaryAccess(organizationId)
    return this.request<LodgeSummaryAccess>(organizationId, 'summary-access')
  }

  async grantSummaryAccess(organizationId: string, memberId: string, reason: string): Promise<void> {
    if (this.useMocks) {
      const state = demoSummaryAccess(organizationId)
      if (!state.eligibleMasters.some(item => item.memberId === memberId)) throw new Error('La delegación sólo puede otorgarse a un Maestro del Taller.')
      if (state.activeGrants.some(item => item.memberId === memberId)) throw new Error('Este Hermano ya tiene acceso vigente.')
      state.activeGrants.push({ id: crypto.randomUUID(), memberId, displayName: state.eligibleMasters.find(item => item.memberId === memberId)!.displayName, grantedAtUtc: new Date().toISOString(), reason, isCurrentlyEligible: true })
      return
    }
    await this.request(organizationId, 'summary-access', 'POST', { memberId, reason })
  }

  async revokeSummaryAccess(organizationId: string, grantId: string): Promise<void> {
    if (this.useMocks) {
      demoSummaryGrants.set(organizationId, demoSummaryAccess(organizationId).activeGrants.filter(item => item.id !== grantId))
      return
    }
    await this.request(organizationId, `summary-access/${encodeURIComponent(grantId)}`, 'DELETE')
  }

  private async request<T = void>(organizationId: string, endpoint: string, method = 'GET', body?: unknown): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar el Resumen del Taller.')
    headers.set('Authorization', `Bearer ${token}`)
    if (body !== undefined) headers.set('Content-Type', 'application/json')
    const response = await fetch(`${this.baseUrl}/api/institutional/organizations/${encodeURIComponent(organizationId)}/${endpoint}`, {
      method, credentials: 'omit', redirect: 'error', cache: 'no-store', headers, ...(body === undefined ? {} : { body: JSON.stringify(body) })
    })
    if (!response.ok) {
      if (response.status === 401) await this.onUnauthorized?.()
      if (response.status === 403) throw new Error('Su cuenta no tiene permiso para esta operación del Taller.')
      const error = await response.json().catch(() => null) as { message?: string } | null
      throw new Error(error?.message ?? `La API respondió ${response.status} ${response.statusText}.`)
    }
    if (response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }

  private async requestRaw(organizationId: string, endpoint: string, method: string, body?: BodyInit, contentType?: string): Promise<Response> {
    const token = await this.getAccessToken?.()
    if (!token) throw new Error('Debe ingresar para consultar la Ficha del Taller.')
    const headers = new Headers({ Authorization: `Bearer ${token}` })
    if (contentType) headers.set('Content-Type', contentType)
    const response = await fetch(`${this.baseUrl}/api/institutional/organizations/${encodeURIComponent(organizationId)}/${endpoint}`, { method, credentials: 'omit', redirect: 'error', cache: 'no-store', headers, ...(body === undefined ? {} : { body }) })
    if (response.ok || (method === 'GET' && response.status === 404)) return response
    if (response.status === 401) await this.onUnauthorized?.()
    if (response.status === 403) throw new Error('Su cuenta no tiene permiso para esta operación del Taller.')
    throw new Error(`La API respondió ${response.status} ${response.statusText}.`)
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
  const metadata = demoWorkshopMetadata.get(id)
  const name = metadata?.name ?? (second ? 'Taller Demostrativo Nº 23' : 'Taller Demostrativo Nº 1')
  const number = second ? '23' : '1'
  const other = second ? 'Taller Demostrativo Nº 1' : 'Taller Demostrativo Nº 23'
  const otherId = second ? ORG_1 : ORG_23
  return {
    organization: { id, name, number, type: 'workshop', parentOrganizationId: null, createdAtUtc: '2010-01-01T12:00:00Z', establishedOn: metadata ? metadata.establishedOn : (second ? '1984-03-10' : '1967-08-21'), city: metadata ? metadata.city : (second ? 'Valparaíso' : 'Santiago'), country: metadata ? metadata.country : 'Chile', treasuryTerritory: metadata?zoneFromLocation(metadata.city,metadata.country):second?'other_oriente':'santiago', hasLogo: demoWorkshopLogos.has(id) },
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

function demoSummaryAccess(organizationId: string): LodgeSummaryAccess {
  const second = organizationId === ORG_23
  const candidates = second
    ? [{ memberId: 'demo-master-23-1', displayName: 'Hermano Maestro Demostrativo Uno' }, { memberId: 'demo-master-23-2', displayName: 'Hermana Maestra Demostrativa Dos' }]
    : [{ memberId: 'demo-master-1-1', displayName: 'Hermano Maestro Demostrativo Uno' }, { memberId: 'demo-master-1-2', displayName: 'Hermana Maestra Demostrativa Dos' }]
  const grants = demoSummaryGrants.get(organizationId) ?? []
  demoSummaryGrants.set(organizationId, grants)
  return { eligibleMasters: candidates, activeGrants: grants }
}
