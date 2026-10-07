import {describe,it,expect} from 'vitest'
import {PmgmApiClient} from './pmgmApi'
describe('Hospitalaria v2 aportes y fallecimiento',()=>{
 it('cobra6000 por Taller sin multiplicarlo por22 activos; observa, reenvía y concilia',async()=>{
  const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:hospitalaria'
  const organizationId='11111111-1111-1111-1111-111111111111'
  let r=await api.getHospitalariaContributions({organizationId});expect(r.items).toHaveLength(1);expect(r.items[0].amountDue).toBe(6000)
  const id=r.items[0].id,paymentDate=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())
  await expect(api.payHospitalariaContribution(id,{amount:1500,paymentDate,reference:'demo'})).rejects.toThrow()
  await api.payHospitalariaContribution(id,{amount:6000,paymentDate,reference:'demo'});await api.reviewHospitalariaContribution(id,'observed','Referencia ilegible')
  await api.payHospitalariaContribution(id,{amount:6000,paymentDate,reference:'demo corregido'});await api.reviewHospitalariaContribution(id,'reconciled')
  r=await api.getHospitalariaContributions({organizationId});expect(r.items[0].status).toBe('reconciled');expect(r.items[0].paymentReference).toBe('demo corregido')
  expect((await api.getHospitalariaDeathCandidates(organizationId)).items.some(x=>x.id==='member-demo-1-1')).toBe(true)
  const death=await api.recordHospitalariaDeath(organizationId,{memberId:'member-demo-1-1',deathDate:'2026-10-01',evidenceReference:'synthetic.pdf'})
  expect((await api.getHospitalariaDeathCandidates(organizationId)).items.some(x=>x.id==='member-demo-1-1')).toBe(false)
  expect(death.createdCases).toBe(1);await expect(api.recordHospitalariaDeath(organizationId,{memberId:'member-demo-1-1',deathDate:'2026-10-01',evidenceReference:'synthetic.pdf'})).rejects.toThrow('ya fue registrado')
  const cases=await api.getDeathReplenishmentCases();expect(cases.items.find(x=>x.id===death.id)?.amountPerActiveMember).toBe(1500)
 })
})
