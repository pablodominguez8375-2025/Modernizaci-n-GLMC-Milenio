import { describe, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { initialDynamicCatalog, tariffAccess } from './dynamicAccess'
import { chileCivilDate } from '../admissionDates'
import { nextTariffPeriod } from './treasuryTariffs'

describe('acceso efectivo al tarifario',()=>{
 it('permite consulta, bloquea registro, habilita creación explícita y revoca sin retorno',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:treasury'
  const initial=await api.getTariffVersions();const a=api.dynamicAccess
  let c=await a.create({code:'consulta',name:'Consulta',scope:'order',menuCodes:['treasury']},0)
  c=await a.grants('consulta',[{viewCode:'treasury',actions:['view']}],c.version)
  c=await a.assign({subject:api.demoAccessSubject,profileCode:'consulta',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  const payload={...initial.items[0],number:'QA acceso',expectedVersion:initial.version,decreeDate:chileCivilDate(),effectiveFrom:nextTariffPeriod(chileCivilDate())}
  expect((await api.getTariffAccess()).actions).toEqual(['view'])
  await expect(api.registerTariff(payload)).rejects.toThrow('perfil')
  expect((await api.getTariffVersions()).total).toBe(initial.total)
  c=await a.grants('consulta',[{viewCode:'treasury',actions:['view','create']}],c.version)
  expect((await api.registerTariff(payload)).version).toBe(initial.version+1)
  await a.revoke(c.assignments[0].id,c.version)
  expect((await api.getTariffAccess()).actions).toEqual([])
  await expect(api.getTariffVersions()).rejects.toThrow('perfil')
  await expect(api.registerTariff({...payload,expectedVersion:initial.version+1})).rejects.toThrow('perfil')
 })
 it('respeta sujeto, ámbito Orden, vigencia inclusiva y perfil/menú/vista activos',()=>{
  const c=initialDynamicCatalog();const subject='qa';const today='2026-10-04'
  c.profiles.push({id:'p',code:'p',name:'QA',scope:'order',isSystem:false,isActive:true,menuCodes:['treasury'],grants:[{viewCode:'treasury',actions:['view','create']}]})
  c.assignments.push({id:'a',subject,profileCode:'p',organizationId:null,effectiveFrom:today,effectiveTo:today,isActive:true})
  expect(tariffAccess(c,subject,today).actions).toEqual(['view','create'])
  for(const date of ['2026-10-03','2026-10-05'])expect(tariffAccess(c,subject,date).actions).toEqual([])
  expect(tariffAccess(c,'other',today).managed).toBe(false)
  for(const kind of ['profile','menu','view','revoke'] as const){const copy=structuredClone(c);if(kind==='profile')copy.profiles.at(-1)!.isActive=false;if(kind==='menu')copy.menus.find(m=>m.code==='treasury')!.isActive=false;if(kind==='view')copy.menus.find(m=>m.code==='treasury')!.views.find(v=>v.code==='treasury')!.isActive=false;if(kind==='revoke')copy.assignments[0].isActive=false;expect(tariffAccess(copy,subject,today).actions).toEqual([])}
  const local=structuredClone(c);local.assignments[0].organizationId='a-workshop';local.profiles.at(-1)!.scope='lodge'
  expect(tariffAccess(local,subject,today).managed).toBe(false)
 })
 it('un grant completo no crea autoridad institucional para un perfil local',async()=>{
  const api=new PmgmApiClient({useMocks:true});const a=api.dynamicAccess
  let c=await a.create({code:'full',name:'Full',scope:'order',menuCodes:['treasury']},0)
  c=await a.grants('full',[{viewCode:'treasury',actions:['view','create']}],c.version)
  await a.assign({subject:api.demoAccessSubject,profileCode:'full',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  await expect(api.getTariffAccess()).rejects.toThrow('permiso')
  await expect(api.getTariffVersions()).rejects.toThrow('permiso')
 })
 it('consulta sólo la proyección propia mediante transporte autenticado',async()=>{
  const fetch=vi.fn().mockResolvedValue(new Response(JSON.stringify({version:9,managed:true,actions:['view']})));vi.stubGlobal('fetch',fetch)
  try{const api=new PmgmApiClient({getAccessToken:async()=>'test-token'});expect((await api.getTariffAccess()).actions).toEqual(['view']);const [path,init]=fetch.mock.calls[0];expect(path).toBe('/api/tesoreria/tarifarios/decretos/acceso');expect(new Headers(init.headers).get('Authorization')).toBe('Bearer test-token')}finally{vi.unstubAllGlobals()}
 })
})
