import { describe,expect,it } from 'vitest'
import { getDemoProfile } from '../demoProfiles'
import { DEMO_PROFILE_FOR,SUPPORT_VIEW_PROFILES,profileDisplayName } from './supportViewProfiles'
import { UserAccountsDemo } from './userAccounts'

describe('«Ver como un perfil»',()=>{
 it('tiene claves únicas y un perfil de demostración válido para cada una',()=>{
  expect(new Set(SUPPORT_VIEW_PROFILES.map(p=>p.key)).size).toBe(SUPPORT_VIEW_PROFILES.length)
  for(const p of SUPPORT_VIEW_PROFILES){expect(DEMO_PROFILE_FOR[p.key]).toBeTruthy();expect(getDemoProfile(DEMO_PROFILE_FOR[p.key])).toBeTruthy()}
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
