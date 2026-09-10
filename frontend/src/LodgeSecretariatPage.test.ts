import { describe, expect, it } from 'vitest'
import { LodgeApiClient, demoLodgeSecretariatSeed } from './api/lodgeApi'

const lodge23 = '23232323-2323-2323-2323-232323232323'

describe('Secretaría Logial showcase contract', () => {
  it('uses only explicit fictitious seed records', () => {
    expect(demoLodgeSecretariatSeed.correspondence.length).toBeGreaterThanOrEqual(2)
    expect(demoLodgeSecretariatSeed.tasks.length).toBeGreaterThanOrEqual(2)
    expect(demoLodgeSecretariatSeed.agenda.length).toBeGreaterThanOrEqual(3)
    expect(demoLodgeSecretariatSeed.correspondence.every(item => `${item.subject} ${item.counterparty} ${item.notes ?? ''}`.toLowerCase().includes('demo'))).toBe(true)
  })

  it('supports correspondence lifecycle in mock mode', async () => {
    const api = new LodgeApiClient({ useMocks: true })
    const created = await api.createCorrespondence(lodge23, {
      folio: 'SEC-DEMO-999',
      direction: 'incoming',
      correspondenceDate: '2026-09-10',
      subject: 'Asunto demostrativo nuevo',
      counterparty: 'Contraparte demo',
      channel: 'platform',
    })
    expect(created.status).toBe('registered')

    const processed = await api.updateCorrespondenceStatus(lodge23, created.id, 'processed')
    expect(processed.status).toBe('processed')
    expect((await api.getCorrespondence(lodge23)).items.some(item => item.id === created.id)).toBe(true)
  })

  it('supports tasks and meeting agenda in mock mode', async () => {
    const api = new LodgeApiClient({ useMocks: true })
    const task = await api.createSecretariatTask(lodge23, { title: 'Pendiente demo nuevo', priority: 'high', responsibleLabel: 'Secretaría demo' })
    expect((await api.updateSecretariatTaskStatus(lodge23, task.id, 'done')).status).toBe('done')

    const meetingId = 'bbbbbbbb-2309-0012-0000-000000000001'
    const point = await api.createMeetingAgendaItem(meetingId, { title: 'Punto de tabla demo nuevo' })
    expect(point.position).toBeGreaterThan(3)
    expect((await api.updateMeetingAgendaStatus(meetingId, point.id, 'addressed')).status).toBe('addressed')
  })
})
