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
 it('rechaza sujetos inexistentes y el Superusuario a administradores institucionales',async()=>{
  const api=admin();await expect(api.createUserAccount({memberId:'inactivo',organizationId:'taller'})).rejects.toThrow()
  api.demoAccessSubject='demo:grandLodge';expect((await api.getUserAccountCandidates()).canCreateSuperuser).toBe(false)
  await expect(api.createUserAccount({superuser:true,platformName:'Soporte ficticio',platformEmail:'soporte@example.invalid'})).rejects.toThrow('Máster')
  api.demoAccessSubject='demo:brother';await expect(api.getUserAccountCandidates()).rejects.toThrow('permiso')
 })
 it('permite el Superusuario solo al Máster, sin Hermano ni Taller, y nunca como Máster',async()=>{
  const api=admin();const r=await api.createUserAccount({superuser:true,platformName:'Soporte ficticio',platformEmail:'soporte@example.invalid'})
  expect(r.memberId).toBeNull();expect(r.organizationId).toBeNull();expect((await api.getUserAccounts()).items.at(-1)?.kind).toBe('superuser');expect(r.superuser).toBe(true)
  expect('platformAdministrator' in r).toBe(false)
 })
})

describe('restablecer acceso',()=>{
 const admin=()=>{const a=new PmgmApiClient({useMocks:true});a.demoAccessSubject='demo:systemAdmin';return a}
 it('solo el Máster; correo sin clave en la respuesta; clave temporal con motivo y única',async()=>{
  const api=admin();const c=(await api.getUserAccountCandidates()).items[0]
  const u=await api.createUserAccount({memberId:c.memberId,organizationId:c.organizationId})
  const a=await api.resetUserAccountAccess(u.subject,{modality:'email'});expect(a.temporaryPassword).toBeUndefined();expect(a.expiresInMinutes).toBe(30)
  await expect(api.resetUserAccountAccess(u.subject,{modality:'temporary',reason:'corto'})).rejects.toThrow('motivo')
  const b=await api.resetUserAccountAccess(u.subject,{modality:'temporary',reason:'Hermano sin correo, verificado en persona'})
  expect(b.temporaryPassword).toHaveLength(24);expect(b.requiresPasswordChange).toBe(true)
  await expect(api.resetUserAccountAccess('inexistente',{modality:'email'})).rejects.toThrow('no existe')
  api.demoAccessSubject='demo:grandLodge';await expect(api.resetUserAccountAccess(u.subject,{modality:'email'})).rejects.toThrow('Máster')
 })
})

describe('«Ver como» en la demostración (#574)', () => {
 it('exige Máster, motivo y una cuenta de Hermano; la sesión es de solo lectura y dura 30 minutos', async () => {
  const api=(()=>{const a=new PmgmApiClient({useMocks:true});a.demoAccessSubject='demo:systemAdmin';return a})()
  const target=(await api.getUserAccounts()).items.find(u=>u.kind==='member')!
  await expect(api.startSupportView({targetSubject:target.subject,reason:'corto'})).rejects.toThrow(/motivo/i)
  await expect(api.startSupportView({targetSubject:'no-existe',reason:'Revisar pantalla del Hermano'})).rejects.toThrow(/no existe/i)
  const s=await api.startSupportView({targetSubject:target.subject,reason:'Revisar pantalla del Hermano'})
  expect(s.readOnly).toBe(true);expect(Date.parse(s.expiresAtUtc)-Date.parse(s.startedAtUtc)).toBe(30*60000)
 })
})
