import { describe, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { aggregateViewAccess, allowsView, restrictClient } from './dynamicViewAccess'
import { initialDynamicCatalog, type DynamicCatalog } from './dynamicAccess'
import { dynamicMethodPolicies } from './dynamicMethodPolicies'

const subject = 'demo:secretary'
const today = '2026-10-07'
function catalog(actions: string[] = ['view']): DynamicCatalog {
  const c = initialDynamicCatalog()
  c.profiles.push({ id:'profile', code:'test', name:'Consulta', scope:'order', isSystem:false, isActive:true, menuCodes:['lodge'], grants:[{viewCode:'lodge',actions:actions as DynamicCatalog['actions']}] })
  c.assignments.push({id:'assignment',subject,profileCode:'test',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null,isActive:true})
  return c
}
describe('restricciones comunes por vista', () => {
  it('no vuelve al acceso legado al revocar, expirar o programar una asignación', () => {
    for (const change of [{isActive:false},{effectiveTo:'2026-10-06'},{effectiveFrom:'2026-10-08'}]) {
      const c=catalog(); Object.assign(c.assignments[0],change)
      expect(aggregateViewAccess(c,subject,today).views.lodge).toEqual([])
    }
    expect(aggregateViewAccess(catalog(),subject,today).views.lodge).toEqual(['view'])
    expect(aggregateViewAccess(catalog(),subject,today).views.documentmanager).toEqual([])
    expect(aggregateViewAccess(catalog(),'other',today).views.lodge).toContain('create')
  })
  it('no combina grants entre Talleres ni expone una colección parcialmente denegada', () => {
    const c=catalog(['view','create']); c.profiles[9].scope='lodge'; c.assignments[0].organizationId='A'
    c.assignments.push({...c.assignments[0],id:'revoked',organizationId:'B',isActive:false})
    expect(allowsView(c,subject,'lodge','create','A',today)).toBe(true)
    expect(allowsView(c,subject,'lodge','create','B',today)).toBe(false)
    expect(aggregateViewAccess(c,subject,today).views.lodge).toEqual([])
  })
  it('el guard impide la mutación y las llamadas indirectas antes de cambiar datos', async () => {
    const api=new PmgmApiClient({useMocks:true}); api.demoAccessSubject=subject
    let c=await api.dynamicAccess.create({code:'test',name:'Consulta',scope:'order',menuCodes:['lodge']},0)
    c=await api.dynamicAccess.grants('test',[{viewCode:'lodge',actions:['view','create']}],c.version)
    c=await api.dynamicAccess.assign({subject,profileCode:'test',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
    const write=vi.fn(async()=>true)
    const client={createMeeting:vi.fn(async()=>true),recordAttendance:write,async createMinute(){return this.recordAttendance()}}
    const guarded=restrictClient(client,'lodgeApi',api,()=>null)
    await expect(guarded.createMeeting()).resolves.toBe(true)
    await expect(guarded.recordAttendance()).rejects.toThrow('no permite')
    await expect(guarded.createMinute()).rejects.toThrow('no permite')
    expect(write).not.toHaveBeenCalled()
    await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
    await expect(guarded.createMeeting()).rejects.toThrow('no permite')
    expect(client.createMeeting).toHaveBeenCalledTimes(1)
  })
  it('en modo instalado una proyección no disponible deniega sin llamar al transporte', async () => {
    const api=new PmgmApiClient({useMocks:false}); const read=vi.fn(async()=>[])
    const client=restrictClient({getMine:read},'notificationApi',api,()=>null)
    await expect(client.getMine()).rejects.toThrow('no permite'); expect(read).not.toHaveBeenCalled()
  })
  it('imprimir y descargar/exportar son permisos distintos', () => {
    const c=catalog(); expect(aggregateViewAccess(c,subject,today).views.lodge).not.toContain('print')
    expect(dynamicMethodPolicies.lodgeApi.downloadHistoricalTemplate.action).toBe('view')
    expect(dynamicMethodPolicies.documentApi.downloadLibraryDocument.action).toBe('view')
    expect(dynamicMethodPolicies.grandArchiveApi.withdraw.action).toBe('delete')
  })
})
