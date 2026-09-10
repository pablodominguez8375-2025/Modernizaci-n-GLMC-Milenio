import { describe, expect, it } from 'vitest'
import { editableMemberFields, memberPortalDemoData } from './MemberPortalPage'

describe('MemberPortalPage contract', () => {
  it('keeps institutional identity fields outside member-editable fields', () => {
    expect(editableMemberFields).toEqual(['email', 'phone', 'city', 'address'])
    expect(editableMemberFields).not.toContain('degree')
    expect(editableMemberFields).not.toContain('lodge')
    expect(editableMemberFields).not.toContain('status')
  })

  it('includes the agreed personal operational summary', () => {
    expect(memberPortalDemoData.attendance.total).toBeGreaterThan(0)
    expect(memberPortalDemoData.instruction.length).toBeGreaterThanOrEqual(3)
    expect(memberPortalDemoData.treasury.status).toBeTruthy()
    expect(memberPortalDemoData.hospitalaria.status).toBeTruthy()
    expect(memberPortalDemoData.meetings.length).toBeGreaterThan(0)
    expect(memberPortalDemoData.notifications.length).toBeGreaterThan(0)
  })

  it('uses only explicitly fictitious public showcase data', () => {
    expect(memberPortalDemoData.fullName.toLowerCase()).toContain('demostrativo')
    expect(memberPortalDemoData.memberId).toContain('DEMO')
    expect(memberPortalDemoData.personal.email).toContain('ejemplo.cl')
  })
})
