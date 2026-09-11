import type { OrganizationOption, PmgmApiClient } from './api/pmgmApi'

export default function RegularityDashboard({ organizations }: { api: PmgmApiClient; kind: 'treasury' | 'hospitalaria'; organizations: OrganizationOption[]; asOfDate: string }) {
  return <section className="regularity-dashboard" aria-label="Tablero institucional"><strong>{organizations.length} Talleres visibles</strong></section>
}
