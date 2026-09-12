import { afterEach, expect, it, vi } from 'vitest'
import { LodgeApiClient } from './lodgeApi'

afterEach(() => vi.unstubAllGlobals())

it('uses the same bearer-only boundary for Lodge Management', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, items: [] })))
  vi.stubGlobal('fetch', fetch)
  await new LodgeApiClient({ getAccessToken: async () => 'lodge-token' }).getMeetings('o1')
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/gestion-logial/talleres/o1/tenidas')
  expect(options.headers.get('Authorization')).toBe('Bearer lodge-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('uses only the minimized lodge member selector', async () => {
  const response = { total: 1, items: [{ id: 'm1', displayName: 'Hermana Uno' }] }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)))
  vi.stubGlobal('fetch', fetch)
  const result = await new LodgeApiClient({ getAccessToken: async () => 'token' }).getMemberOptions('o1')
  expect(result.items[0]).toEqual({ id: 'm1', displayName: 'Hermana Uno' })
  expect(fetch.mock.calls[0][0]).toBe('/api/gestion-logial/talleres/o1/miembros/opciones')
  expect(JSON.stringify(result)).not.toContain('institutionalNumber')
  expect(JSON.stringify(result)).not.toContain('email')
})

it('routes attendance and minute operations through dedicated lodge endpoints', async () => {
  const responses = [
    new Response(JSON.stringify({ id: 'a1' }), { status: 201 }),
    new Response(JSON.stringify({ total: 0, items: [] })),
    new Response(JSON.stringify({ id: 'min1', meetingId: 't1', version: 1, content: 'Acta', status: 'draft', createdAtUtc: '2026-09-08T13:00:00Z', approvedAtUtc: null }), { status: 201 }),
    new Response(JSON.stringify({ id: 'min1', meetingId: 't1', version: 1, content: 'Acta', status: 'approved', createdAtUtc: '2026-09-08T13:00:00Z', approvedAtUtc: '2026-09-08T14:00:00Z' })),
  ]
  const fetch = vi.fn().mockImplementation(() => Promise.resolve(responses.shift()!))
  vi.stubGlobal('fetch', fetch)
  const client = new LodgeApiClient({ getAccessToken: async () => 'token' })

  await client.recordAttendance('t1', { memberId: 'm1', status: 'present' })
  await client.getAttendance('t1')
  await client.createMinute('t1', 'Acta')
  await client.approveMinute('t1', 'min1')

  expect(fetch.mock.calls.map(call => call[0])).toEqual([
    '/api/gestion-logial/tenidas/t1/asistencia',
    '/api/gestion-logial/tenidas/t1/asistencia',
    '/api/gestion-logial/tenidas/t1/actas',
    '/api/gestion-logial/tenidas/t1/actas/min1/aprobar',
  ])
})

it('uses the same instruction contract for list, creation and attendance', async () => {
  const instruction = { id: 'i1', organizationId: 'o1', instructionDate: '2026-09-12', grade: 'apprentice', topic: 'Símbolos', responsibleOffice: 'second_warden', instructorMemberId: null, status: 'held' }
  const responses = [
    new Response(JSON.stringify({ total: 0, items: [] })),
    new Response(JSON.stringify(instruction), { status: 201 }),
    new Response(JSON.stringify({ instructionId: 'i1', recorded: 1 })),
  ]
  const fetch = vi.fn().mockImplementation(() => Promise.resolve(responses.shift()!))
  vi.stubGlobal('fetch', fetch)
  const client = new LodgeApiClient({ getAccessToken: async () => 'token' })

  await client.getInstructions('o1')
  await client.createInstruction('o1', { instructionDate: '2026-09-12', grade: 'apprentice', topic: 'Símbolos' })
  await client.recordInstructionAttendance('i1', [{ memberId: 'm1', status: 'present' }])

  expect(fetch.mock.calls.map(call => call[0])).toEqual([
    '/api/gestion-logial/talleres/o1/instrucciones',
    '/api/gestion-logial/talleres/o1/instrucciones',
    '/api/gestion-logial/instrucciones/i1/asistencia',
  ])
})

it('demo mode preserves corrections and minute versions without token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new LodgeApiClient({ useMocks: true, getAccessToken: token })
  const meeting = await client.createMeeting('o1', { meetingDate: '2026-09-08', meetingType: 'regular', grade: 'all' })
  const members = await client.getMemberOptions('o1')
  await client.recordAttendance(meeting.id, { memberId: members.items[0].id, status: 'present' })
  await client.recordAttendance(meeting.id, { memberId: members.items[0].id, status: 'excused', excuseReason: 'Rectificación' })
  const attendance = await client.getAttendance(meeting.id)
  expect(attendance.total).toBe(1)
  expect(attendance.items[0].status).toBe('excused')

  const v1 = await client.createMinute(meeting.id, 'Versión uno')
  await client.approveMinute(meeting.id, v1.id)
  const v2 = await client.createMinute(meeting.id, 'Versión dos')
  await client.approveMinute(meeting.id, v2.id)
  const minutes = await client.getMinutes(meeting.id)
  expect(minutes.total).toBe(2)
  expect(minutes.items.find(item => item.version === 1)?.status).toBe('superseded')
  expect(minutes.items.find(item => item.version === 2)?.status).toBe('approved')

  const instruction = await client.createInstruction('23232323-2323-2323-2323-232323232323', { instructionDate: '2026-09-12', grade: 'master', topic: 'Docencia de Maestros' })
  await client.recordInstructionAttendance(instruction.id, [{ memberId: members.items[0].id, status: 'present' }])
  const instructions = await client.getInstructions('23232323-2323-2323-2323-232323232323')
  expect(instructions.items.some(item => item.id === instruction.id)).toBe(true)
  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})
