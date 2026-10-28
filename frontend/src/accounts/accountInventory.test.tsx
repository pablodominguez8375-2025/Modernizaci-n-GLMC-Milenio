import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import { UserAccountsDemo, type AccountUser } from '../api/userAccounts'
import { PROTECTED_LABEL, inventoryKindLabel, inventoryOriginLabel, inventoryStateLabel, summarizeInventory } from './accountInventory'
import AccountInventoryCard from '../AccountInventoryCard'
import type { PmgmApiClient } from '../api/pmgmApi'

const seed: AccountUser[] = [
  { subject: 'demo:brother', name: 'Hermano Demostrativo', email: 'h@ejemplo.cl', enabled: true, kind: 'member' },
  { subject: 'demo:supportUser', name: 'Soporte Demostrativo', email: 's@ejemplo.cl', enabled: true, kind: 'superuser' },
]
const build = (master: boolean) => new UserAccountsDemo([], () => true, () => master, seed, () => 'demo:systemAdmin', () => false)

describe('Inventario de cuentas (Máster)', () => {
  it('solo el Máster ve el inventario; los demás reciben error y no datos', () => {
    expect(() => build(false).inventory()).toThrow()
    expect(build(true).inventory().items.length).toBeGreaterThan(0)
  })

  it('qa.admin y admin salen protegidos, de solo lectura y sin permiso de gestión', () => {
    const items = build(true).inventory().items
    for (const username of ['qa.admin', 'admin']) {
      const item = items.find(x => x.username === username)!
      expect(item.protected).toBe(true)
      expect(item.managed).toBe(false)
      expect(item.kind).toBe('legacy_protected')
    }
  })

  it('las cuentas creadas desde el sistema no son protegidas y llevan su origen', () => {
    const managed = build(true).inventory().items.filter(x => x.managed)
    expect(managed.map(x => x.kind).sort()).toEqual(['member', 'superuser'])
    expect(managed.every(x => !x.protected && x.origin === 'managed')).toBe(true)
  })

  it('los textos son simples y el resumen cuenta bien', () => {
    const items = build(true).inventory().items
    const summary = summarizeInventory(items)
    expect(summary.total).toBe(items.length)
    expect(summary.protectedCount + summary.managedCount).toBe(summary.total)
    const legacy = items.find(x => x.username === 'qa.admin')!
    expect(PROTECTED_LABEL).toBe('Cuenta preexistente · Protegida')
    expect(inventoryKindLabel(legacy)).toBe('Cuenta preexistente')
    expect(inventoryOriginLabel(legacy)).toContain('antes del sistema de altas')
    expect(inventoryStateLabel({ ...legacy, enabled: false })).toBe('Deshabilitada')
  })

  it('la tarjeta no ofrece activar, desactivar ni eliminar y avisa mientras consulta', () => {
    const fake = { getUserAccountInventory: () => new Promise(() => undefined) } as unknown as PmgmApiClient
    const html = renderToStaticMarkup(<AccountInventoryCard api={fake} />)
    expect(html).toContain('Consultando el inventario completo')
    expect(html.toLowerCase()).not.toContain('eliminar definitivamente')
  })

  it('el listado ordinario solo monta el inventario para el Máster (no se filtra en el navegador)', () => {
    const source = readFileSync(new URL('../UserAccountsPanel.tsx', import.meta.url), 'utf8')
    expect(source).toContain('corr?.isMaster&&<AccountInventoryCard')
    const card = readFileSync(new URL('../AccountInventoryCard.tsx', import.meta.url), 'utf8')
    expect(card).not.toMatch(/method:\s*'(POST|DELETE|PUT|PATCH)'/)
    expect(card).not.toContain('Keycloak')
  })
})
