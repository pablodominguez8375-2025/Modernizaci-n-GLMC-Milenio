import { describe,expect,it } from 'vitest'
import { getDemoProfile } from '../demoProfiles'
import { DEMO_PROFILE_FOR,SUPPORT_VIEW_PROFILES,profileDisplayName } from './supportViewProfiles'
import { UserAccountsDemo } from './userAccounts'

describe('«Ver como un perfil»',()=>{
 it('tiene claves únicas y un perfil de demostración válido para cada una',()=>{
  expect(new Set(SUPPORT_VIEW_PROFILES.map(p=>p.key)).size).toBe(SUPPORT_VIEW_PROFILES.length)
  for(const p of SUPPORT_VIEW_PROFILES){expect(DEMO_PROFILE_FOR[p.key]).toBeTruthy();expect(getDemoProfile(DEMO_PROFILE_FOR[p.key])).toBeTruthy()}
 })
 it('el catálogo contiene Docencia completa y Gran Hospitalaria con demostraciones equivalentes',()=>{
  const expected=[
   ['inmediato-ex-venerable','Inmediato Ex Venerable Maestro','Taller','lodgePastMaster'],
   ['gran-segundo-vigilante','Gran Segundo Vigilante','Gran Logia','grandSecondWarden'],
   ['gran-primer-vigilante','Gran Primer Vigilante','Gran Logia','grandFirstWarden'],
   ['inmediato-ex-gran-maestro','Inmediato Ex Gran Maestro','Gran Logia','immediatePastGrandMaster'],
   ['jefatura-docencia','Jefatura de Docencia','Gran Logia','instructionDepartmentHead'],
   ['gran-hospitalaria','Gran Hospitalaria','Gran Logia','hospitalaria'],
  ] as const
  for(const [key,label,group,demoKey] of expected){
   expect(SUPPORT_VIEW_PROFILES.find(p=>p.key===key)).toEqual({key,label,group})
   expect(DEMO_PROFILE_FOR[key]).toBe(demoKey)
   expect(getDemoProfile(demoKey)).toBeTruthy()
  }
  expect(SUPPORT_VIEW_PROFILES).toHaveLength(21)
  expect(SUPPORT_VIEW_PROFILES.some(p=>p.key==='platform_superadmin')).toBe(false)
 })
 it('los perfiles de Taller se muestran ligados al Taller 00 y los de Gran Logia no',()=>{
  expect(profileDisplayName(SUPPORT_VIEW_PROFILES.find(p=>p.key==='tesorero')!)).toBe('Tesorero · Taller 00')
  expect(profileDisplayName(SUPPORT_VIEW_PROFILES.find(p=>p.key==='gran-secretario')!)).toBe('Gran Secretario')
 })
 it('solo el Máster inicia la vista por perfil: 30 minutos y solo lectura',()=>{
  const demo=(master:boolean)=>new UserAccountsDemo([],()=>true,()=>master,[],()=>'demo:systemAdmin',()=>false)
  expect(()=>demo(false).startViewAs({profileKey:'tesorero'})).toThrow()
  const s=demo(true).startViewAs({profileKey:'tesorero'})
  expect(s.readOnly).toBe(true);expect(s.targetName).toBe('Tesorero · Taller 00')
  expect(Date.parse(s.expiresAtUtc)-Date.parse(s.startedAtUtc)).toBe(30*60000)
  expect(()=>demo(true).startViewAs({profileKey:'platform_superadmin'})).toThrow()
 })
})
