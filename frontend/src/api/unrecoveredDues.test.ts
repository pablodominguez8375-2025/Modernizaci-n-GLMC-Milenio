import {describe,it,expect} from 'vitest'
import {createElement} from 'react'
import {renderToStaticMarkup} from 'react-dom/server'
import UnrecoveredDuesPanel from '../UnrecoveredDuesPanel'
import {PmgmApiClient} from './pmgmApi'

const today=()=>new Intl.DateTimeFormat('sv-SE',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())
describe('Cuotas no recuperadas, separadas de caja',()=>{
  for(const currency of ['CLP','USD'])it(`conserva caja y abonos parciales; no duplica la pérdida ${currency}`,async()=>{
    const api=new PmgmApiClient({useMocks:true});const date=today()
    const candidates=await api.getUnrecoveredDueCandidates('org-1',currency)
    const before=await api.getLodgeTreasuryReport('org-1',date,date,null,currency)
    const payload={withdrawalRequestId:candidates.items[0].withdrawalRequestId,recognitionDate:date,currency,nonPaymentConfirmed:true,evidenceReference:'RES-FICTICIA'}
    const saved=await api.recognizeUnrecoveredDues('org-1',payload)
    expect(saved.items).toHaveLength(2);expect(saved.items[0].paidAmount).toBeGreaterThan(0)
    const replay=await api.recognizeUnrecoveredDues('org-1',payload)
    expect(replay.items.map(x=>x.id)).toEqual(saved.items.map(x=>x.id));expect(replay.alreadyRecorded).toBe(true)
    const after=await api.getLodgeTreasuryReport('org-1',date,date,null,currency)
    expect(after.income).toBe(before.income);expect(after.closingBalance).toBe(before.closingBalance);expect(after.movements).toEqual(before.movements)
    expect(after.unrecoveredDuesTotal).toBe(currency==='USD'?9:30000)
    expect((await api.getLodgeTreasuryReport('org-1','2024-01-01','2024-12-31',null,currency)).unrecoveredDuesTotal).toBe(0)
    expect((await api.getLodgeTreasuryReport('org-1',date,date,null,currency==='CLP'?'USD':'CLP')).unrecoveredDuesTotal).toBe(0)
  })
  it('consulta no puede registrar pérdidas y la revocación bloquea lectura',async()=>{
    const api=new PmgmApiClient({useMocks:true});api.demoAccessSubject='demo:lodgeTreasurer'
    const org=(await api.getOrganizationOptions()).items.find(o=>o.type==='workshop')!.id
    let c=await api.dynamicAccess.create({code:'loss-view',name:'Consulta',scope:'lodge',menuCodes:['treasury']},0)
    c=await api.dynamicAccess.grants('loss-view',[{viewCode:'lodgetreasury',actions:['view']}],c.version)
    c=await api.dynamicAccess.assign({subject:api.demoAccessSubject,profileCode:'loss-view',organizationId:org,effectiveFrom:'2020-01-01',effectiveTo:null},c.version)
    await expect(api.recognizeUnrecoveredDues(org,{withdrawalRequestId:'demo-forced-nonpayment',recognitionDate:today(),currency:'CLP',nonPaymentConfirmed:true,evidenceReference:'RES'})).rejects.toThrow('perfil no permite')
    const report=await api.getLodgeTreasuryReport(org,today(),today())
    const html=renderToStaticMarkup(createElement(UnrecoveredDuesPanel,{api,organizationId:org,currency:'CLP',report,canWrite:false,onSaved:async()=>{}}))
    expect(html).toContain('Pérdida por cuotas no recuperadas');expect(html).not.toContain('Registrar pérdida por no pago')
    await api.dynamicAccess.revoke(c.assignments[0].id,c.version)
    await expect(api.getUnrecoveredDueCandidates(org,'CLP')).rejects.toThrow('perfil no permite')
  })
  it('exige confirmación, respaldo y retiro formal; conserva pérdidas ante error',async()=>{
    const api=new PmgmApiClient({useMocks:true});const base={withdrawalRequestId:'demo-forced-nonpayment',recognitionDate:today(),currency:'CLP',nonPaymentConfirmed:true,evidenceReference:'RES'}
    await expect(api.recognizeUnrecoveredDues('org-1',{...base,nonPaymentConfirmed:false})).rejects.toThrow()
    await expect(api.recognizeUnrecoveredDues('org-1',{...base,evidenceReference:''})).rejects.toThrow()
    await expect(api.recognizeUnrecoveredDues('org-1',{...base,withdrawalRequestId:'not-approved'})).rejects.toThrow()
    expect((await api.getLodgeTreasuryReport('org-1',today(),today())).unrecoveredDuesTotal).toBe(0)
  })
})
