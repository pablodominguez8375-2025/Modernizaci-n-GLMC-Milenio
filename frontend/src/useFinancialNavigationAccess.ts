import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'

export interface FinancialNavigationAccess { treasury: boolean; grandHospitalaria: boolean; lodgeHospitalaria: boolean }
export const deniedFinancialNavigation: FinancialNavigationAccess = { treasury: false, grandHospitalaria: false, lodgeHospitalaria: false }

// Each projection checks institutional authority and technical grants in its own scope.
// An unavailable module must not hide another module whose projection succeeded.
export async function resolveFinancialNavigation(api: PmgmApiClient, enabled: FinancialNavigationAccess): Promise<FinancialNavigationAccess> {
  const [treasury, grandHospitalaria, lodgeHospitalaria] = await Promise.allSettled([
    enabled.treasury ? api.getGrandTreasuryAccess().then(a => a.actions.includes('view')) : false,
    enabled.grandHospitalaria ? api.getGrandHospitalariaAccess().then(a => a.actions.includes('view')) : false,
    enabled.lodgeHospitalaria ? api.getOrganizationOptions().then(async organizations => {
      const results = await Promise.allSettled(organizations.items.filter(o => o.type === 'workshop').map(o => api.getHospitalariaAccess(o.id)))
      return results.some(r => r.status === 'fulfilled' && r.value.actions.includes('view'))
    }) : false,
  ])
  const allowed = (r: PromiseSettledResult<boolean>) => r.status === 'fulfilled' && r.value
  return { treasury: allowed(treasury), grandHospitalaria: allowed(grandHospitalaria), lodgeHospitalaria: allowed(lodgeHospitalaria) }
}

export function useFinancialNavigationAccess(api: PmgmApiClient, enabled: FinancialNavigationAccess, profileKey: string) {
  const { treasury, grandHospitalaria, lodgeHospitalaria } = enabled
  const key = `${api.demoAccessSubject}:${profileKey}:${treasury}:${grandHospitalaria}:${lodgeHospitalaria}`
  const [state, setState] = useState<{ api: PmgmApiClient; key: string; access: FinancialNavigationAccess } | null>(null)
  useEffect(() => {
    let active = true
    let generation = 0
    const refresh = async (invalidate = true) => {
      const current = ++generation
      if (invalidate) setState(null)
      const access = await resolveFinancialNavigation(api, { treasury, grandHospitalaria, lodgeHospitalaria })
      if (active && current === generation) setState({ api, key, access })
    }
    if (!treasury && !grandHospitalaria && !lodgeHospitalaria) return
    void refresh()
    const unsubscribe = api.dynamicAccess.subscribe(() => void refresh())
    const onFocus = () => void refresh(false)
    window.addEventListener('focus', onFocus)
    const interval = window.setInterval(() => void refresh(false), 30000)
    return () => { active = false; unsubscribe(); window.removeEventListener('focus', onFocus); window.clearInterval(interval) }
  }, [api, key, treasury, grandHospitalaria, lodgeHospitalaria])
  return state?.api === api && state.key === key ? state.access : deniedFinancialNavigation
}
