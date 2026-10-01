import { describe, expect, it } from 'vitest'
import { buildPendingTasks, greetingFor, isOperationalProfile, isSystemAdministrator, navigationBadges, totalPending, type PendingTaskFlags } from './rolePendingTasks'

const none: PendingTaskFlags = { canApproveLodgeExpenses: false, canLodgeTreasury: false, canSecretariat: false, canCeremonies: false, canLodge: false, canManageLodgeSecretariat: false, canConfigureSystem: false, canBootstrap: false }

describe('PMGM-UX-002 bandeja «Mis pendientes» por rol', () => {
  it('no muestra tareas operativas a un Hermano sin cargos', () => {
    expect(buildPendingTasks(none, {})).toEqual([])
    expect(isOperationalProfile(none)).toBe(false)
  })

  it('el Venerable Maestro ve egresos por autorizar, insinuaciones y Gestión Logial con contadores', () => {
    const flags = { ...none, canApproveLodgeExpenses: true, canLodge: true }
    const tasks = buildPendingTasks(flags, { expensesToApprove: 2, workshopCandidates: 1, secretariatTasks: 0 })
    expect(tasks.map(task => [task.id, task.target, task.count])).toEqual([
      ['expenses', 'lodgeTreasury', 2],
      ['workshop-candidates', 'candidateProfile', 1],
      ['secretariat-tasks', 'lodge', 0],
    ])
    expect(totalPending(tasks)).toBe(3)
    expect(navigationBadges(tasks)).toEqual({ lodgeTreasury: 2, candidateProfile: 1 })
  })

  it('el Tesorero del Taller entra a su Tesorería sin tareas de autorización', () => {
    const tasks = buildPendingTasks({ ...none, canLodgeTreasury: true }, { expensesToApprove: 4 })
    expect(tasks).toHaveLength(1)
    expect(tasks[0]).toMatchObject({ id: 'treasury', title: 'Tesorería del Taller', target: 'lodgeTreasury' })
  })

  it('Gran Secretaría revisa insinuados y ceremonias, no las insinuaciones del Taller', () => {
    const tasks = buildPendingTasks({ ...none, canSecretariat: true, canCeremonies: true, canLodge: true }, { secretariatReviews: 3, ceremonyReviews: 2, workshopCandidates: 9 })
    expect(tasks.map(task => task.id)).toEqual(['secretariat-reviews', 'ceremonies', 'secretariat-tasks'])
  })

  it('Administración ve sus accesos de sistema sin contador inventado', () => {
    const flags = { ...none, canConfigureSystem: true, canBootstrap: true }
    expect(isSystemAdministrator(flags)).toBe(true)
    expect(isOperationalProfile(flags)).toBe(false)
    expect(buildPendingTasks(flags, {}).map(task => [task.target, task.count])).toEqual([['system', null], ['bootstrap', null]])
  })

  it('descarta contadores inválidos o negativos', () => {
    const tasks = buildPendingTasks({ ...none, canCeremonies: true }, { ceremonyReviews: Number.NaN })
    expect(tasks[0].count).toBeNull()
    expect(buildPendingTasks({ ...none, canCeremonies: true }, { ceremonyReviews: -3 })[0].count).toBe(0)
  })

  it('saluda según la hora de Santiago', () => {
    expect(greetingFor(new Date('2026-10-01T13:00:00Z'))).toBe('Buenos días')
    expect(greetingFor(new Date('2026-10-01T18:00:00Z'))).toBe('Buenas tardes')
    expect(greetingFor(new Date('2026-10-02T02:00:00Z'))).toBe('Buenas noches')
  })
})
