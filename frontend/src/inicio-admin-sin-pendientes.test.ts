import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { buildPendingTasks, totalPending, type PendingTaskFlags } from './rolePendingTasks'

const dashboard = readFileSync(new URL('./DashboardPage.tsx', import.meta.url), 'utf8')
const none: PendingTaskFlags = { canApproveLodgeExpenses: false, canLodgeTreasury: false, canSecretariat: false, canCeremonies: false, canLodge: false, canManageLodgeSecretariat: false, canConfigureSystem: false, canBootstrap: false }

/* Decisión del PO 08-10-2026 (opción A): el Inicio del Administrador no repite el menú ni dice «sin pendientes» con un bloque de pendientes debajo. */
describe('PMGM-UX Inicio del Administrador sin accesos repetidos', () => {
  it('el Administrador solo recibe accesos sin contador, que no cuentan como pendientes', () => {
    const tasks = buildPendingTasks({ ...none, canConfigureSystem: true, canBootstrap: true }, {})
    expect(tasks.map(task => task.id)).toEqual(['system', 'bootstrap'])
    expect(totalPending(tasks)).toBe(0)
  })

  it('«Mis pendientes» se muestra a perfiles operativos con tareas y al Administrador solo con pendientes reales', () => {
    expect(dashboard).toContain('const showPendingInbox = (operational && pendingTasks.length > 0) || (administrator && pendingTotal > 0)')
    expect(dashboard).toContain("'Tu agenda, avisos e insinuados publicados en un solo lugar.'")
  })
})
