import { describe,expect,it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { initialDynamicCatalog,grandHospitalariaAccess } from './dynamicAccess'

describe('Gran Hospitalaria — Orden, capacidades y revocación',()=>{
 it('conserva el bloqueo por vigencia/perfil/ámbito y no mezcla sujetos',()=>{
  const c=initialDynamicCatalog(),today='2026-10-06'
  c.profiles.push({id:'p',code:'consulta',name:'Consulta',scope:'order',isSystem:false,isActive:true,menuCodes:['hospitalaria'],grants:[{viewCode:'hospitalaria',actions:['view']}]})
  c.assignments.push({id:'a',subject:'qa',profileCode:'consulta',organizationId:null,effectiveFrom:today,effectiveTo:today,isActive:true})
  expect(grandHospitalariaAccess(c,'qa',today).actions).toEqual(['view'])
  expect(grandHospitalariaAccess(c,'qa','2026-10-05').actions).toEqual([])
  expect(grandHospitalariaAccess(c,'qa','2026-10-07').actions).toEqual([])
  expect(grandHospitalariaAccess(c,'other',today).managed).toBe(false)
  c.assignments[0].isActive=false
  expect(grandHospitalariaAccess(c,'qa',today).actions).toEqual([])
  c.assignments[0].isActive=true;c.profiles[9].scope='lodge'
  expect(grandHospitalariaAccess(c,'qa',today).actions).toEqual([])
  c.profiles[9].scope='order';c.profiles[9].isActive=false
  expect(grandHospitalariaAccess(c,'qa',today).actions).toEqual([])
 })
 it('consulta no modifica y rechaza escrituras directas antes y después de revocar, sin conceder cargo',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:hospitalaria'
  const org=(await api.getOrganizationOptions()).items.find(o=>o.type==='workshop')!.id
  const before=await api.getHospitalariaWorkshopRegularity(org)
  let c=await api.dynamicAccess.create({code:'consulta',name:'Consulta',scope:'order',menuCodes:['hospitalaria']},0)
  c=await api.dynamicAccess.grants('consulta',[{viewCode:'hospitalaria',actions:['view']}],c.version)
  c=await api.dynamicAccess.assign({subject:api.demoAccessSubject,profileCode:'consulta',organizationId:null,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  expect((await api.getGrandHospitalariaAccess()).actions).toEqual(['view'])
  await api.getGrandHospitalariaSubmissions();await api.getDeathReplenishmentCases();await api.getHospitalariaReplenishmentRate()
  expect(await api.getHospitalariaWorkshopRegularity(org)).toEqual(before)
  const writes=[()=>api.syncDeathReplenishmentCases(),()=>api.setHospitalariaReplenishmentRate({amountPerActiveMember:999,effectiveFrom:'2026-10-06',sourceReference:'QA'}),()=>api.reviewGrandHospitalariaSubmission('missing','observed','QA'),()=>api.reviewDeathReplenishmentTransfer('missing','observed','QA'),()=>api.setHospitalariaWorkshopRegularity(org,{status:'up_to_date',asOfDate:'2026-10-06',sourceReference:'QA'})]
  for(const write of writes)await expect(write()).rejects.toThrow('perfil no permite')
  await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
  for(const read of [()=>api.getGrandHospitalariaSubmissions(),()=>api.getDeathReplenishmentCases(),()=>api.getHospitalariaReplenishmentRate(),()=>api.getHospitalariaWorkshopRegularity(org)])await expect(read()).rejects.toThrow('perfil no permite')
  expect((await api.getGrandHospitalariaAccess()).actions).toEqual([])
  api.demoAccessSubject='demo:lodgeHospitalaria'
  await expect(api.syncDeathReplenishmentCases()).rejects.toThrow('perfil no permite')
  await expect(api.getGrandHospitalariaAccess()).rejects.toThrow('Sin permiso')
 })
})
