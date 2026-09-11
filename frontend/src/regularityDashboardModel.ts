import type { OrganizationOption, PmgmApiClient, WorkshopRegularitySnapshot } from './api/pmgmApi'

export type RegularityKind = 'treasury' | 'hospitalaria'
export type RegularityRow = { organization: OrganizationOption; snapshot: WorkshopRegularitySnapshot | null }

export async function loadRegularityRows(api: PmgmApiClient, kind: RegularityKind, organizations: OrganizationOption[], asOfDate: string) {
  const load = kind === 'treasury'
    ? (id: string) => api.getTreasuryWorkshopRegularity(id, asOfDate)
    : (id: string) => api.getHospitalariaWorkshopRegularity(id, asOfDate)
  return Promise.all(organizations.map(async organization => ({ organization, snapshot: await load(organization.id) })))
}

export function summarizeRegularity(kind: RegularityKind, snapshots: Array<WorkshopRegularitySnapshot | null>) {
  const good = snapshots.filter(item => item && ['up_to_date', 'exempt'].includes(item.status)).length
  const attention = snapshots.filter(item => item && (item.status === 'pending' || (kind === 'treasury' ? item.status === 'delinquent' : item.status === 'overdue'))).length
  return { total: snapshots.length, good, attention, missing: snapshots.filter(item => item === null).length }
}

export function regularityStatusLabel(kind: RegularityKind, status: string) {
  if (status === 'up_to_date') return 'Al día'
  if (status === 'exempt') return 'Exento'
  if (status === 'pending') return 'Pendiente'
  if (kind === 'treasury' && status === 'delinquent') return 'Moroso'
  if (kind === 'hospitalaria' && status === 'overdue') return 'Reposiciones pendientes'
  return status
}

export function regularityStatusClass(status: string) {
  if (status === 'up_to_date' || status === 'exempt') return 'regularity-status good'
  if (status === 'pending') return 'regularity-status pending'
  return 'regularity-status blocked'
}
