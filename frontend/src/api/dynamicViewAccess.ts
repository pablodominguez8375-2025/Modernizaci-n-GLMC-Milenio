import { technicalGrant, type AccessAction, type DynamicCatalog } from './dynamicAccess'
import { dynamicMethodPolicies } from './dynamicMethodPolicies'
import type { PmgmApiClient } from './pmgmApi'

export interface ViewAccess { version: number; views: Record<string, AccessAction[]> }
export function aggregateViewAccess(catalog: DynamicCatalog, subject: string, today: string): ViewAccess {
  const scopes = [...new Set(catalog.assignments.filter(a => a.subject === subject).map(a => a.organizationId))]
  if (!scopes.length) scopes.push(null)
  return { version: catalog.version, views: Object.fromEntries(catalog.menus.flatMap(m => m.views).map(view => [view.code, catalog.actions.filter(action => scopes.every(org => allowsView(catalog, subject, view.code, action, org, today)))])) }
}
export function allowsView(catalog: DynamicCatalog, subject: string, view: string, action: AccessAction, organization: string | null, today: string) {
  return !catalog.assignments.some(a => a.subject === subject && a.organizationId === organization) || technicalGrant(catalog, subject, view, action, organization, today)
}
export function chileDate() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()) }

// All clients in the app share the same live restriction. Demo checks precede reads/mutations,
// including indirect calls. Installed mode retains backend authorization as final authority.
export function restrictClient<T extends object>(client: T, kind: string, api: PmgmApiClient, current: () => ViewAccess | null): T {
  return new Proxy(client, { get(target, property, receiver) {
    const value = Reflect.get(target, property, receiver)
    if (typeof value !== 'function') return value
    const policy = dynamicMethodPolicies[kind]?.[String(property)]
    if (!policy) return value.bind(target)
    return async (...args: unknown[]) => {
      let permitted = false
      if (api.useMocks) {
        const catalog = api.dynamicAccess.snapshot()
        const organization = policy.organizationArg === undefined ? undefined : args[policy.organizationArg]
        const payloadOrg = args.find(arg => arg && typeof arg === 'object' && 'organizationId' in arg) as { organizationId?: string } | undefined
        const scope = typeof organization === 'string' ? organization : payloadOrg?.organizationId
        if (scope) {
          permitted = allowsView(catalog, api.demoAccessSubject, policy.view, policy.action, scope, chileDate()) &&
            allowsView(catalog, api.demoAccessSubject, policy.view, policy.action, null, chileDate())
        } else permitted = aggregateViewAccess(catalog, api.demoAccessSubject, chileDate()).views[policy.view]?.includes(policy.action) ?? false
      } else permitted = current()?.views[policy.view]?.includes(policy.action) ?? false
      if (!permitted) throw new Error('Tu perfil no permite esta acción en la vista seleccionada.')
      return value.apply(receiver, args)
    }
  } })
}
