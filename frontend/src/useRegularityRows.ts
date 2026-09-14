import { useEffect, useState } from 'react'
import type { OrganizationOption, PmgmApiClient } from './api/pmgmApi'
import { loadRegularityRows, type RegularityKind, type RegularityRow } from './regularityDashboardModel'

export function useRegularityRows(api: PmgmApiClient, kind: RegularityKind, organizations: OrganizationOption[], asOfDate: string) {
  const [rows, setRows] = useState<RegularityRow[]>([])
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    if (!organizations.length) { setRows([]); setLoading(false); return }
    let active = true
    setLoading(true)
    void loadRegularityRows(api, kind, organizations, asOfDate).then(value => { if (active) setRows(value) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, kind, organizations, asOfDate])
  return { rows, loading }
}
