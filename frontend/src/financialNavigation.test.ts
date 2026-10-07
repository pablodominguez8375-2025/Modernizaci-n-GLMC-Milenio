import { describe, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './api/pmgmApi'
import { deniedFinancialNavigation, resolveFinancialNavigation } from './useFinancialNavigationAccess'

describe('Navegación financiera con permisos efectivos', () => {
  it('no consulta proyecciones fuera de la autoridad institucional', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const treasury = vi.spyOn(api, 'getGrandTreasuryAccess')
    const grand = vi.spyOn(api, 'getGrandHospitalariaAccess')
    const local = vi.spyOn(api, 'getOrganizationOptions')
    expect(await resolveFinancialNavigation(api, deniedFinancialNavigation)).toEqual(deniedFinancialNavigation)
    for (const request of [treasury, grand, local]) expect(request).not.toHaveBeenCalled()
  })

  it('conserva módulos permitidos cuando otra proyección falla y exige Ver', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    vi.spyOn(api, 'getGrandTreasuryAccess').mockRejectedValue(new Error('Sin permiso'))
    vi.spyOn(api, 'getGrandHospitalariaAccess').mockResolvedValue({ version: 1, managed: true, actions: ['write'] })
    const org = (await api.getOrganizationOptions()).items.find(o => o.type === 'workshop')!
    vi.spyOn(api, 'getOrganizationOptions').mockResolvedValue({ items: [org], total: 1 })
    vi.spyOn(api, 'getHospitalariaAccess').mockResolvedValue({ organizationId: org.id, version: 1, managed: true, actions: ['view'] })
    expect(await resolveFinancialNavigation(api, { treasury: true, grandHospitalaria: true, lodgeHospitalaria: true })).toEqual({ treasury: false, grandHospitalaria: false, lodgeHospitalaria: true })
  })

  it('retira Gran Tesorería tras revocar una asignación real de la demo', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    api.demoAccessSubject = 'demo:treasury'
    const enabled = { ...deniedFinancialNavigation, treasury: true }
    expect((await resolveFinancialNavigation(api, enabled)).treasury).toBe(true)
    let c = await api.dynamicAccess.create({ code: 'nav', name: 'Consulta', scope: 'order', menuCodes: ['treasury'] }, 0)
    c = await api.dynamicAccess.grants('nav', [{ viewCode: 'treasury', actions: ['view'] }], c.version)
    c = await api.dynamicAccess.assign({ subject: api.demoAccessSubject, profileCode: 'nav', organizationId: null, effectiveFrom: '2020-01-01', effectiveTo: null }, c.version)
    expect((await resolveFinancialNavigation(api, enabled)).treasury).toBe(true)
    await api.dynamicAccess.revoke(c.assignments[0].id, c.version)
    expect((await resolveFinancialNavigation(api, enabled)).treasury).toBe(false)
    api.demoAccessSubject = 'demo:member'
    expect((await resolveFinancialNavigation(api, enabled)).treasury).toBe(false)
  })
})
