import {describe,expect,it} from 'vitest'
import {PmgmApiClient} from './pmgmApi'
import {grandTreasuryAccess,initialDynamicCatalog} from './dynamicAccess'

describe('Gran Tesorería — permisos de Orden',()=>{
 it('evalúa vigencia inclusiva, perfiles activos, ámbito y revocación sin fallback',()=>{
  const c=initialDynamicCatalog(),date='2026-10-06'
  c.profiles.push({id:'p',code:'qa',name:'QA',scope:'order',isSystem:false,isActive:true,menuCodes:['treasury'],grants:[{viewCode:'treasury',actions:['view','write']}]})
  c.assignments.push({id:'a',subject:'qa',profileCode:'qa',organizationId:null,effectiveFrom:date,effectiveTo:date,isActive:true})
  expect(grandTreasuryAccess(c,'qa',date).actions).toEqual(['view','write'])
  expect(grandTreasuryAccess(c,'qa','2026-10-05').actions).toEqual([])
  expect(grandTreasuryAccess(c,'qa','2026-10-07').actions).toEqual([])
  expect(grandTreasuryAccess(c,'other',date).managed).toBe(false)
  c.assignments[0].isActive=false;expect(grandTreasuryAccess(c,'qa',date).actions).toEqual([])
  c.assignments[0].isActive=true;c.profiles[9].scope='lodge';expect(grandTreasuryAccess(c,'qa',date).actions).toEqual([])
  c.profiles[9].scope='order';c.profiles[9].isActive=false;expect(grandTreasuryAccess(c,'qa',date).actions).toEqual([])
 })
 it('consulta sin mutación; escrituras directas, revocación y autoridad institucional se verifican en demo',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:treasury'
  const org=(await api.getOrganizationOptions()).items.find(x=>x.type==='workshop')!.id
  const before=await api.getTreasuryWorkshopRegularity(org)
  let c=await api.dynamicAccess.create({code:'qa',name:'QA',scope:'order',menuCodes:['treasury']},0)
  c=await api.dynamicAccess.grants('qa',[{viewCode:'treasury',actions:['view']}],c.version)
  c=await api.dynamicAccess.assign({subject:api.demoAccessSubject,profileCode:'qa',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  expect((await api.getGrandTreasuryAccess()).actions).toEqual(['view'])
  await api.getTreasuryCeremonyRights();await api.listTreasuryStatements(org)
  expect(await api.getTreasuryWorkshopRegularity(org)).toEqual(before)
  const writes=[()=>api.reconcileTreasuryStatement('missing'),()=>api.setTreasuryWorkshopRegularity(org,{status:'up_to_date',asOfDate:'2026-10-06'}),()=>api.recordCeremonyRightPayment('missing',{amount:1,paymentMethod:'transfer',paymentDate:'2026-10-06',reference:'QA',idempotencyKey:'qa'})]
  for(const write of writes)await expect(write()).rejects.toThrow('perfil no permite')
  await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
  for(const read of [()=>api.getTreasuryCeremonyRights(),()=>api.getTreasuryWorkshopRegularity(org),()=>api.listTreasuryStatements(org)])await expect(read()).rejects.toThrow('perfil no permite')
  expect((await api.getGrandTreasuryAccess()).actions).toEqual([])
  api.demoAccessSubject='demo:regimen'
  expect(Object.keys((await api.getTreasuryWorkshopRegularity(org))!)).toEqual(['status','asOfDate'])
  api.demoAccessSubject='demo:lodgeTreasurer'
  await expect(api.getGrandTreasuryAccess()).rejects.toThrow('Sin permiso')
  for(const write of writes)await expect(write()).rejects.toThrow('perfil no permite')
 })
})
