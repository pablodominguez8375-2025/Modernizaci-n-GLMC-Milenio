import { describe,expect,it } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { initialDynamicCatalog,treasuryAccess } from './dynamicAccess'
import { renderToStaticMarkup } from 'react-dom/server'
import { createElement } from 'react'
import LodgeTreasuryPanel from '../LodgeTreasuryPanel'

describe('effective Treasury access',()=>{
 it('does not restore legacy privileges when an assignment is revoked, expired or for another subject',()=>{
  const catalog=initialDynamicCatalog(),org='org-qa',today='2026-10-04'
  catalog.profiles.push({id:'profile-qa',code:'consulta',name:'Consulta',scope:'lodge',isSystem:false,isActive:true,menuCodes:['treasury'],grants:[{viewCode:'lodgetreasury',actions:['view','print']}]})
  catalog.assignments.push({id:'a',subject:'qa',profileCode:'consulta',organizationId:org,effectiveFrom:today,effectiveTo:today,isActive:true})
  expect(treasuryAccess(catalog,'qa',org,today).actions).toEqual(['view','print'])
  expect(treasuryAccess(catalog,'qa',org,'2026-10-05').actions).toEqual([])
  catalog.assignments[0].isActive=false
  expect(treasuryAccess(catalog,'qa',org,today).actions).toEqual([])
  expect(treasuryAccess(catalog,'other',org,today).managed).toBe(false)
 })
 it('blocks direct demo mutations before changing the ledger and revocation takes effect in the same client',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:lodgeTreasurer'
  const org=(await api.getOrganizationOptions()).items.find(o=>o.type==='workshop')!.id
  let c=await api.dynamicAccess.create({code:'consulta',name:'Consulta',scope:'lodge',menuCodes:['treasury']},0)
  c=await api.dynamicAccess.grants('consulta',[{viewCode:'lodgetreasury',actions:['view']}],c.version)
  c=await api.dynamicAccess.assign({subject:api.demoAccessSubject,profileCode:'consulta',organizationId:org,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
  expect((await api.getTreasuryAccess(org)).actions).toEqual(['view'])
  await expect(api.createLodgeTreasuryIncome(org,{category:'QA',amount:1000,incomeDate:'2026-10-04',description:'QA'})).rejects.toThrow('perfil no permite')
  await expect(api.generateLodgeCharges(org,2026,10)).rejects.toThrow('perfil no permite')
  await expect(api.authorizeTreasuryPrint(org)).rejects.toThrow('perfil no permite')
  await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
  await expect(api.getLodgeTreasurySummary(org,2026,10)).rejects.toThrow('perfil no permite')
 })
 it('renders consultation without financial action forms',()=>{
  const api=new PmgmApiClient({useMocks:true})
  for(const section of ['collection','movements','settings','reports'] as const){
   const html=renderToStaticMarkup(createElement(LodgeTreasuryPanel,{api,organizationId:'qa',canManage:true,section,actions:['view']}))
   expect(html).not.toContain('action-trigger')
   expect(html).not.toContain('Imprimir vista')
   expect(html).not.toContain('Corregir o anular un registro')
  }
 })
})
