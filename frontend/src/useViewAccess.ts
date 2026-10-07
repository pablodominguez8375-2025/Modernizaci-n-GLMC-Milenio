import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import { aggregateViewAccess, chileDate, type ViewAccess } from './api/dynamicViewAccess'

export function useViewAccess(api: PmgmApiClient, profileKey: string) {
  const key = `${api.demoAccessSubject}:${profileKey}`
  const [state, setState] = useState<{ api: PmgmApiClient; key: string; access: ViewAccess } | null>(null)
  useEffect(() => {
    let active = true
    let generation = 0
    const refresh = async (invalidate = true) => {
      const current = ++generation
      if (invalidate) setState(null)
      try {
        const access = api.useMocks ? aggregateViewAccess(api.dynamicAccess.snapshot(), api.demoAccessSubject, chileDate()) : await api.getViewAccess()
        if (active && current === generation) setState({ api, key, access })
      } catch { if (active && current === generation) setState(null) }
    }
    void refresh()
    const unsubscribe = api.dynamicAccess.subscribe(() => void refresh())
    const focus = () => void refresh(false)
    window.addEventListener('focus', focus)
    const interval = window.setInterval(focus, 30000)
    return () => { active = false; unsubscribe(); window.removeEventListener('focus', focus); window.clearInterval(interval) }
  }, [api, key])
  return state?.api === api && state.key === key ? state.access : null
}
