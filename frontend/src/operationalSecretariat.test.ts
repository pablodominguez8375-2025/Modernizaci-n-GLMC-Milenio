import { describe, expect, it } from 'vitest'
import { LodgeApiClient } from './api/lodgeApi'

const lodge='23232323-2323-2323-2323-232323232323'

describe('operational workshop secretariat mock parity',()=>{
 it('registers correspondence and closes it',async()=>{
  const api=new LodgeApiClient({useMocks:true})
  const created=await api.createCorrespondence(lodge,{direction:'sent',folio:'ENV-2026-009',correspondenceDate:'2026-09-19',subject:'Respuesta demostrativa',counterparty:'Gran Secretaría',channel:'email',reference:null})
  expect(created.status).toBe('registered')
  expect((await api.updateCorrespondenceStatus(created.id,'closed')).status).toBe('closed')
 })
 it('tracks tasks and agenda states',async()=>{
  const api=new LodgeApiClient({useMocks:true})
  const task=await api.createSecretariatTask(lodge,{title:'Preparar tabla',priority:'urgent'})
  expect((await api.updateSecretariatTaskStatus(task.id,'completed')).completedAtUtc).toBeTruthy()
  const item=await api.createAgendaItem(lodge,{order:9,title:'Asunto demostrativo'})
  expect((await api.updateAgendaStatus(item.id,'deferred')).status).toBe('deferred')
 })
})
