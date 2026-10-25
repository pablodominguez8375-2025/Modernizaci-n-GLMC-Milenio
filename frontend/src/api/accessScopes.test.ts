import { describe, expect, it } from 'vitest'
import { ACCESS_SCOPES, isDerivedScope, isLodgeScoped, scopeLabel } from './dynamicAccess'

describe('alcances de perfiles dinámicos', () => {
  it('ofrece su Taller, todos los Talleres, Taller específico y Orden con textos simples', () => {
    expect(ACCESS_SCOPES.map(x => x.value)).toEqual(['own-lodge', 'all-lodges', 'lodge', 'order'])
    expect(scopeLabel('own-lodge')).toBe('Solo su propio Taller')
    expect(scopeLabel('order')).toContain('Gran Logia')
  })
  it('solo el Taller específico exige elegir Taller; los derivados no', () => {
    expect(isDerivedScope('own-lodge')).toBe(true)
    expect(isDerivedScope('all-lodges')).toBe(true)
    expect(isDerivedScope('lodge')).toBe(false)
    expect(isLodgeScoped('all-lodges')).toBe(true)
    expect(isLodgeScoped('order')).toBe(false)
  })
})

import { CONFERRABLE_ROLES, rolesForScope } from './dynamicAccess'
describe('cargos que concede un perfil', () => {
  it('ofrece cargos de Taller o de Gran Logia según el alcance y nunca áreas sensibles', () => {
    expect(rolesForScope('own-lodge').every(r => r.level === 'lodge')).toBe(true)
    expect(rolesForScope('order').some(r => r.value === 'librarian')).toBe(true)
    for (const sensitive of ['lodge_treasury', 'lodge_hospitalaria', 'grand_treasury', 'grand_hospitalaria', 'privacy_officer', 'grand_lodge_admin', 'platform_superadmin', 'internal_affairs', 'lodge_venerable'])
      expect(CONFERRABLE_ROLES.some(r => r.value === sensitive)).toBe(false)
  })
})
