import { beforeEach, describe, expect, it } from 'vitest'
import { demoHistoryApplyToProfile, demoHistoryPending, demoHistoryPropose, demoHistoryReset, demoHistoryResolve } from './api/memberHistory'

const profile = () => ({
  member: { id: 'm1' },
  offices: [{ id: 'o1', officeType: 'secretary', period: '2020', organizationId: 'org', organization: 'Taller', startDate: '2020-01-01', endDate: '2020-12-31', evidenceReference: null }],
  degreeEvents: [{ id: 'd1', degree: 'apprentice', eventType: 'initiation', effectiveDate: '2010-01-01', organizationId: 'org', organization: 'Taller', evidenceReference: null }],
})

describe('historia del hermano (demo)', () => {
  beforeEach(() => demoHistoryReset())

  it('no cambia la ficha hasta que Régimen Interior aprueba', () => {
    demoHistoryPropose('org', 'Taller', 'm1', 'Hermano', null, { evidenceReference: 'Acta 1', reason: 'Corrección de fecha', items: [{ kind: 'degree', action: 'add', eventType: 'initiation', eventDate: '2009-03-14' }] })
    expect(demoHistoryApplyToProfile(profile(), 'Taller').degreeEvents[0].effectiveDate).toBe('2010-01-01')
    const [pending] = demoHistoryPending('org').items
    demoHistoryResolve(pending.id, 'approve')
    expect(demoHistoryApplyToProfile(profile(), 'Taller').degreeEvents[0].effectiveDate).toBe('2009-03-14')
  })

  it('agrega y corrige cargos al aprobar', () => {
    demoHistoryPropose('org', 'Taller', 'm1', 'Hermano', null, { evidenceReference: 'Acta 2', reason: 'Completar historia', items: [
      { kind: 'office', action: 'add', officeType: 'orator', period: '2018', startDate: '2018-01-01', endDate: '2018-12-31' },
      { kind: 'office', action: 'replace', targetRecordId: 'o1', officeType: 'treasurer', period: '2020', startDate: '2020-01-01', endDate: '2020-12-31' },
    ] })
    demoHistoryResolve(demoHistoryPending().items[0].id, 'approve')
    const offices = demoHistoryApplyToProfile(profile(), 'Taller').offices
    expect(offices).toHaveLength(2)
    expect(offices.find(x => x.id === 'o1')?.officeType).toBe('treasurer')
  })

  it('exige motivo al observar y bloquea una segunda propuesta pendiente', () => {
    const request = { evidenceReference: 'Acta', reason: 'Motivo suficiente', items: [{ kind: 'degree' as const, action: 'add' as const, eventType: 'exaltation', eventDate: '2015-05-05' }] }
    demoHistoryPropose('org', 'Taller', 'm1', 'Hermano', null, request)
    expect(() => demoHistoryPropose('org', 'Taller', 'm1', 'Hermano', null, request)).toThrow(/pendiente/)
    expect(() => demoHistoryResolve(demoHistoryPending().items[0].id, 'observe', '')).toThrow(/motivo/)
  })
})
