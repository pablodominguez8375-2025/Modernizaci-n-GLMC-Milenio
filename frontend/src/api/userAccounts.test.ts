import { describe,it,expect } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
import { MembershipApiClient } from './membershipApi'

describe('alta de cuentas',()=>{
 const admin=()=>{const a=new PmgmApiClient({useMocks:true});a.demoAccessSubject='demo:systemAdmin';return a}
 it('toma el correo de una ficha activa, exige Taller y nunca devuelve una clave',async()=>{
  const api=admin();const c=(await api.getUserAccountCandidates()).items[0]
  const ficha=await new MembershipApiClient({useMocks:true}).getProfile(c.memberId)
  expect(ficha.contact?.email).toBe(c.email);expect(ficha.current.membership?.status).toBe('active')
  await expect(api.createUserAccount({memberId:c.memberId,organizationId:'taller-ajeno'})).rejects.toThrow('Hermano activo')
  await expect(api.createUserAccount({memberId:c.memberId,organizationId:c.organizationId,platformEmail:'otro@example.invalid'})).rejects.toThrow('Hermano activo')
  const r=await api.createUserAccount({memberId:c.memberId,organizationId:c.organizationId})
  expect(r.email).toBe(c.email);expect(r.requiresPasswordChange).toBe(true);expect(r.initialPasswordEmailSent).toBe(false)
  expect(JSON.stringify(r)).not.toMatch(/"(?:password|clave|initialPassword)":/)
  await expect(api.createUserAccount({memberId:c.memberId,organizationId:c.organizationId})).rejects.toThrow('Ya existe')
 })
 it('rechaza sujetos inexistentes y la excepción a administradores institucionales',async()=>{
  const api=admin();await expect(api.createUserAccount({memberId:'inactivo',organizationId:'taller'})).rejects.toThrow()
  api.demoAccessSubject='demo:grandLodge';expect((await api.getUserAccountCandidates()).canCreatePlatformAdministrator).toBe(false)
  await expect(api.createUserAccount({platformAdministrator:true,platformName:'Soporte ficticio',platformEmail:'soporte@example.invalid'})).rejects.toThrow('plataforma')
  api.demoAccessSubject='demo:brother';await expect(api.getUserAccountCandidates()).rejects.toThrow('permiso')
 })
 it('permite la excepción solamente a plataforma y no le asigna Hermano o Taller',async()=>{
  const api=admin();const r=await api.createUserAccount({platformAdministrator:true,platformName:'Soporte ficticio',platformEmail:'soporte@example.invalid'})
  expect(r.memberId).toBeNull();expect(r.organizationId).toBeNull();expect((await api.getUserAccounts()).items[0].kind).toBe('platform')
 })
})
