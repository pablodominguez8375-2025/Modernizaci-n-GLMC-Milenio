import type { OrganizationOption, PmgmApiClient } from './api/pmgmApi'
import { summarizeRegularity, type RegularityKind } from './regularityDashboardModel'
import { useRegularityRows } from './useRegularityRows'
import RegularityKpis from './RegularityKpis'
import RegularityRoster from './RegularityRoster'
import './regularity-dashboard.css'

export default function RegularityDashboard({ api, kind, organizations, asOfDate }: { api: PmgmApiClient; kind: RegularityKind; organizations: OrganizationOption[]; asOfDate: string }) {
  const { rows, loading } = useRegularityRows(api, kind, organizations, asOfDate)
  const summary = summarizeRegularity(kind, rows.map(row => row.snapshot))
  return <section className={`regularity-dashboard ${kind}`}><RegularityKpis {...summary} loading={loading} kind={kind} /><RegularityRoster rows={rows} loading={loading} kind={kind} /></section>
}
