import { describe,it,expect,vi } from 'vitest'
import { MembershipApiClient } from './membershipApi'
describe('contratos propios Mi ficha v2',()=>{
 it('usa rutas propias y token sin selector de hermano',async()=>{
  const fetch=vi.fn().mockResolvedValue({ok:true,json:async()=>({total:0,items:[]})});vi.stubGlobal('fetch',fetch)
  const client=new MembershipApiClient({useMocks:false,getAccessToken:async()=> 'own-token'})
  await client.getOwnOffices();await client.getOwnHospitalaria();await client.getOwnAttendance({tipo:'ceremonia',desde:'2026-09-01',hasta:'2026-10-07'})
  expect(fetch.mock.calls[0][0]).toContain('/api/membership/me/cargos');expect(fetch.mock.calls[1][0]).toContain('/api/membership/me/hospitalaria')
  expect(fetch.mock.calls[2][0]).toContain('tipo=ceremonia');expect(fetch.mock.calls[2][0]).not.toContain('memberId')
  vi.unstubAllGlobals()
 })
 it('demo distingue cargos, estados de asistencia y reposición propia del aporte por Taller',async()=>{
  const client=new MembershipApiClient({useMocks:true});expect((await client.getOwnOffices()).total).toBe(1)
  const attendance=await client.getOwnAttendance({desde:'2026-01-01',hasta:'2026-12-31'});expect(attendance.resumen).toEqual({total:3,presente:1,justificado:1,ausente:1})
  expect((await client.getOwnAttendance({tipo:'ceremonia',desde:'2026-01-01',hasta:'2026-12-31'})).items).toHaveLength(1)
  const h=await client.getOwnHospitalaria();expect(h.items[0].monto).toBe(1500);expect(h.items[0].decreto?.numero).toBe('DEMO-1500')
  await expect(client.getOwnAttendance({desde:'2026-10-08',hasta:'2026-10-07'})).rejects.toThrow()
 })
})
