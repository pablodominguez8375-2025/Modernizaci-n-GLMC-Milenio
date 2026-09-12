import { describe, expect, it } from 'vitest'
import { editableMemberFields, memberPortalDemoData } from './MemberPortalPage'

describe('MemberPortalPage contract', () => {
  it('keeps institutional identity fields outside member-editable fields', () => {
    expect(editableMemberFields).toEqual(['email', 'phone', 'address'])
    expect(editableMemberFields).not.toContain('degree')
    expect(editableMemberFields).not.toContain('lodge')
    expect(editableMemberFields).not.toContain('status')
    expect(editableMemberFields).not.toContain('city')
  })

  it('keeps city as fictitious presentation data until a versioned configurable field exists', () => {
    expect(memberPortalDemoData.personal.city).toContain('demo')
    expect(editableMemberFields).not.toContain('city')
  })

  it('includes the agreed personal operational summary', () => {
    expect(memberPortalDemoData.attendance.total).toBeGreaterThan(0)
    expect(memberPortalDemoData.instruction.length).toBeGreaterThanOrEqual(3)
    expect(memberPortalDemoData.treasury.status).toBeTruthy()
    expect(memberPortalDemoData.hospitalaria.status).toBeTruthy()
    expect(memberPortalDemoData.meetings.length).toBeGreaterThan(0)
    expect(memberPortalDemoData.notifications.length).toBeGreaterThan(0)
  })

  it('represents instructions as attendance history, never thematic progress', () => {
    for (const item of memberPortalDemoData.instruction) {
      expect(item.date).toBeTruthy()
      expect(item.degree).toBeTruthy()
      expect(item.topic).toBeTruthy()
      expect(item.attendance).toBeTruthy()
      expect(item.responsible).toBeTruthy()
      expect('progress' in item).toBe(false)
    }
  })

  it('keeps realistic attendance states in the fictitious instruction history', () => {
    const statuses = memberPortalDemoData.instruction.map(item => item.attendance)
    expect(statuses).toContain('Presente')
    expect(statuses).toContain('Justificada')
  })

  it('uses only explicitly fictitious public showcase data', () => {
    expect(memberPortalDemoData.fullName.toLowerCase()).toContain('demostrativo')
    expect(memberPortalDemoData.memberId).toContain('DEMO')
    expect(memberPortalDemoData.personal.email).toContain('ejemplo.cl')
  })
})
