import {describe,expect,it,vi} from 'vitest'
import {PmgmApiClient} from './pmgmApi'
import {technicalGrant} from './dynamicAccess'
describe('dynamic access contract',()=>{
 it('persists, isolates subject/organization/date and revokes',async()=>{
  const api=new PmgmApiClient({useMocks:true});const org=(await api.getOrganizationOptions()).items[0].id;const a=api.dynamicAccess;let c=await a.create({code:'apoyo',name:'Apoyo',scope:'lodge',menuCodes:['personal']},0)
  c=await a.grants('apoyo',[{viewCode:'calendar',actions:['view','print']}],c.version)
  c=await a.assign({subject:'Subject-A',profileCode:'apoyo',organizationId:org,effectiveFrom:'2026-10-04',effectiveTo:'2026-10-05'},c.version)
  expect(technicalGrant(c,'Subject-A','calendar','print',org,'2026-10-04')).toBe(true)
  for(const [s,testOrg,date,action] of [['subject-a',org,'2026-10-04','print'],['Subject-A','org-b','2026-10-04','print'],['Subject-A',org,'2026-10-06','print'],['Subject-A',org,'2026-10-04','edit']] as const)expect(technicalGrant(c,s,'calendar',action,testOrg,date)).toBe(false)
  await a.revoke(c.assignments[0].id,c.version);expect(technicalGrant(await a.load(),'Subject-A','calendar','print',org,'2026-10-04')).toBe(false)
 })
 it('rejects stale writes, protected profiles and invalid grants',async()=>{
  const a=new PmgmApiClient({useMocks:true}).dynamicAccess;const c=await a.create({code:'one',name:'Uno',scope:'order',menuCodes:['personal']},0)
  await expect(a.create({code:'two',name:'Dos',scope:'order',menuCodes:[]},0)).rejects.toThrow('cambió')
  await expect(a.remove('system-0',c.version)).rejects.toThrow('protegido')
  await expect(a.grants('one',[{viewCode:'system',actions:['view']}],c.version)).rejects.toThrow('inválidas')
  await expect(a.grants('one',[{viewCode:'calendar',actions:['print']}],c.version)).rejects.toThrow('inválidas')
  expect((await a.load()).version).toBe(c.version)
 })
 it('soft deletion revokes assignments and keeps history',async()=>{
  const a=new PmgmApiClient({useMocks:true}).dynamicAccess;let c=await a.create({code:'one',name:'Uno',scope:'order',menuCodes:['personal']},0)
  c=await a.assign({subject:'s',profileCode:'one',organizationId:null,effectiveFrom:'2026-01-01',effectiveTo:null},c.version)
  c=await a.remove('one',c.version);expect(c.profiles.find(p=>p.code==='one')?.isActive).toBe(false);expect(c.assignments).toHaveLength(1);expect(c.assignments[0].isActive).toBe(false)
 })
 it('uses authenticated native endpoint and versioned payload',async()=>{
  const fetch=vi.fn().mockResolvedValue(new Response(JSON.stringify({version:1,menus:[],profiles:[],assignments:[],actions:[]})));vi.stubGlobal('fetch',fetch)
  try{const a=new PmgmApiClient({getAccessToken:async()=>'test-token'}).dynamicAccess;await a.create({code:'one',name:'Uno',scope:'order',menuCodes:[]},0);const [path,init]=fetch.mock.calls[0];expect(path).toBe('/api/system/access/profiles');expect(new Headers(init.headers).get('Authorization')).toBe('Bearer test-token');expect(JSON.parse(init.body).expectedVersion).toBe(0)}finally{vi.unstubAllGlobals()}
 })
})
