import { describe, expect, it } from 'vitest'
import { instructionResponsibilityByGrade, lodgeCockpitDemoData } from './LodgeManagementPage'
import { demoLodgeSeed, LodgeApiClient } from './api/lodgeApi'

const lodge23 = '23232323-2323-2323-2323-232323232323'

describe('Gestión Logial product cockpit', () => {
  it('keeps the agreed product cockpit areas in fictitious showcase data', () => {
    expect(lodgeCockpitDemoData.lodge.name).toContain('Demostrativo')
    expect(lodgeCockpitDemoData.members.active).toBeGreaterThan(0)
    expect(lodgeCockpitDemoData.officers.length).toBeGreaterThanOrEqual(6)
    expect(lodgeCockpitDemoData.officers.some(([role]) => role === 'Ex Venerable Maestro')).toBe(true)
    expect(lodgeCockpitDemoData.managementAreas.map(([area]) => area)).toEqual([
      'Secretaría del Taller',
      'Tesorería del Taller',
      'Hospitalaria del Taller',
      'Docencia e instrucción',
    ])
    expect(lodgeCockpitDemoData.managementAreas.find(([area]) => area === 'Docencia e instrucción')?.[1]).toBe('Vigilantes y Ex Venerable Maestro')
    expect(lodgeCockpitDemoData.instruction.length).toBeGreaterThanOrEqual(4)
    expect(lodgeCockpitDemoData.notifications.length).toBeGreaterThanOrEqual(3)
  })

  it('assigns instruction by grade and exposes attendance-ready demo members', () => {
    expect(instructionResponsibilityByGrade).toEqual({
      apprentice: 'Segundo Vigilante',
      fellowcraft: 'Primer Vigilante',
      master: 'Ex Venerable Maestro',
    })
  })

  it('starts the public showcase with operational fictitious meetings', async () => {
    const api = new LodgeApiClient({ useMocks: true })
    const meetings = await api.getMeetings(lodge23)

    expect(meetings.total).toBeGreaterThanOrEqual(3)
    expect(meetings.items.some(item => item.status === 'scheduled')).toBe(true)
    expect(meetings.items.some(item => item.status === 'closed')).toBe(true)
  })

  it('keeps attendance and an approved minute for the historical demo meeting', async () => {
    const api = new LodgeApiClient({ useMocks: true })
    const historicalMeeting = demoLodgeSeed.meetings.find(item => item.status === 'closed')
    expect(historicalMeeting).toBeDefined()

    const attendance = await api.getAttendance(historicalMeeting!.id)
    const minutes = await api.getMinutes(historicalMeeting!.id)

    expect(attendance.total).toBeGreaterThanOrEqual(3)
    expect(attendance.items.some(item => item.status === 'present')).toBe(true)
    expect(minutes.items.some(item => item.status === 'approved')).toBe(true)
  })
})
