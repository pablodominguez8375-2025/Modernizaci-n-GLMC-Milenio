import { describe,expect,it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { initialDynamicCatalog,hospitalariaAccess } from './dynamicAccess'

describe('effective local Hospitalaria access',()=>{
 it('keeps revocation, chronology and inactive profiles denied and separates Taller/subject/actions',()=>{
  const c=initialDynamicCatalog(),org='qa',today='2026-10-04'
  c.profiles.push({id:'p',code:'consulta',name:'Consulta',scope:'lodge',isSystem:false,isActive:true,menuCodes:['hospitalaria'],grants:[{viewCode:'hospitalaria',actions:['view']}]})
  c.assignments.push({id:'a',subject:'qa',profileCode:'consulta',organizationId:org,effectiveFrom:today,effectiveTo:today,isActive:true})
  expect(hospitalariaAccess(c,'qa',org,today).actions).toEqual(['view'])
  expect(hospitalariaAccess(c,'qa',org,'2026-10-05').actions).toEqual([])
  expect(hospitalariaAccess(c,'qa',org,'2026-10-03').actions).toEqual([])
  expect(hospitalariaAccess(c,'other',org,today).managed).toBe(false)
  expect(hospitalariaAccess(c,'qa','other',today).managed).toBe(false)
  c.profiles[9].isActive=false
  expect(hospitalariaAccess(c,'qa',org,today).actions).toEqual([])
  c.profiles[9].isActive=true;c.assignments[0].isActive=false
  expect(hospitalariaAccess(c,'qa',org,today).actions).toEqual([])
 })
 it('prevents direct demo writes and same-client reads immediately after revocation, preserving ledger',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:lodgeHospitalaria'
  const org=(await api.getOrganizationOptions()).items.find(o=>o.type==='workshop')!.id
  const before=await api.getLodgeHospitalariaSummary(org)
  let c=await api.dynamicAccess.create({code:'consulta',name:'Consulta',scope:'lodge',menuCodes:['hospitalaria']},0)
  c=await api.dynamicAccess.grants('consulta',[{viewCode:'hospitalaria',actions:['view']}],c.version)
  c=await api.dynamicAccess.assign({subject:api.demoAccessSubject,profileCode:'consulta',organizationId:org,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  expect((await api.getHospitalariaAccess(org)).actions).toEqual(['view'])
  await expect(api.createLodgeHospitalariaMovement(org,{movementType:'income',category:'charity_bag',amount:1000,movementDate:'2026-10-04'})).rejects.toThrow('perfil no permite')
  await expect(api.upsertHospitalariaMonthlySubmission(org,2026,10,{replenishmentDueAmount:0,replenishmentPaidAmount:0})).rejects.toThrow('perfil no permite')
  const replenishments=await api.getWorkshopDeathReplenishments(org)
  const pending=replenishments.items.find(x=>x.balance>0)!
  await expect(api.addDeathReplenishmentPayment(pending.id,org,{amount:1,paymentMethod:'cash',paymentDate:'2026-10-04',reference:'QA'})).rejects.toThrow('perfil no permite')
  await expect(api.submitDeathReplenishmentTransfer(pending.caseId,org,{amount:1,transferDate:'2026-10-04',reference:'QA'})).rejects.toThrow('perfil no permite')
  expect((await api.getLodgeHospitalariaSummary(org)).movements).toBe(before.movements)
  await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
  for(const read of [()=>api.getLodgeHospitalariaSummary(org),()=>api.getHospitalariaMonthlySubmissions(org),()=>api.getHospitalariaCouncilAidDecisions(org),()=>api.getHospitalariaCouncilFinancialReviews(org),()=>api.getWorkshopDeathReplenishments(org)])await expect(read()).rejects.toThrow('perfil no permite')
 })
})
