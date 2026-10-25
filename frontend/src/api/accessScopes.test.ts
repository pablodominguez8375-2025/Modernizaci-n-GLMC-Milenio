import { describe, expect, it } from 'vitest'
import { ACCESS_SCOPES, DynamicAccessClient, isDerivedScope, isLodgeScoped, scopeLabel } from './dynamicAccess'

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
  it('etapa 2: suma Venerable Maestro, Gran Secretaría y Régimen Interior', () => {
    expect(rolesForScope('own-lodge').some(r => r.value === 'lodge_venerable')).toBe(true)
    expect(rolesForScope('order').some(r => r.value === 'grand_secretariat')).toBe(true)
    expect(rolesForScope('order').some(r => r.value === 'internal_affairs')).toBe(true)
  })
})

import { CONFERRABLE_ROLES, isSensitiveRole, rolesForScope } from './dynamicAccess'
describe('cargos que concede un perfil', () => {
  it('ofrece cargos de Taller o de Gran Logia según el alcance y nunca administración ni Gran Maestría', () => {
    expect(rolesForScope('own-lodge').every(r => r.level === 'lodge')).toBe(true)
    expect(rolesForScope('order').some(r => r.value === 'librarian')).toBe(true)
    for (const sensitive of ['grand_lodge_admin', 'platform_superadmin', 'grand_master', 'lodge_admin'])
      expect(CONFERRABLE_ROLES.some(r => r.value === sensitive)).toBe(false)
  })
})

describe('etapa 2 entrega 2: áreas sensibles con resguardos', () => {
  it('Tesorería, Hospitalaria y Privacidad son concedibles pero marcadas como sensibles', () => {
    for (const r of ['grand_treasury', 'grand_hospitalaria', 'privacy_officer']) expect(CONFERRABLE_ROLES.find(x => x.value === r)?.sensitive).toBe(true)
    for (const r of ['lodge_treasury', 'lodge_hospitalaria']) expect(rolesForScope('own-lodge').find(x => x.value === r)?.sensitive).toBe(true)
    expect(isSensitiveRole('grand_secretariat')).toBe(false)
  })
  it('exige motivo al guardar y término de hasta 12 meses al asignar', async () => {
    const api = new DynamicAccessClient((async () => { throw new Error('sin red') }) as never, true)
    let v = api.snapshot().version
    const draft = { code: 'tesorero-suplente', name: 'Tesorero suplente', scope: 'order' as const, menuCodes: ['treasury'], baseRoles: ['grand_treasury'] }
    await expect(api.create(draft, v)).rejects.toThrow('motivo')
    await expect(api.create({ ...draft, sensitiveReason: 'corto' }, v)).rejects.toThrow('motivo')
    const c = await api.create({ ...draft, sensitiveReason: 'Reemplazo temporal del Gran Tesorero' }, v); v = c.version
    const base = { subject: 'sujeto-1', profileCode: 'tesorero-suplente', organizationId: null, effectiveFrom: '2026-10-25' }
    await expect(api.assign({ ...base, effectiveTo: null }, v)).rejects.toThrow('término')
    await expect(api.assign({ ...base, effectiveTo: '2028-01-01' }, v)).rejects.toThrow('término')
    await expect(api.assign({ ...base, effectiveTo: '2027-04-25' }, v)).resolves.toBeTruthy()
  })
})
