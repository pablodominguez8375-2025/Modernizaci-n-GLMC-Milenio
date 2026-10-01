import { type InstitutionalIconName } from './InstitutionalIcon'

/** PMGM-UX-002 · Bandeja «Mis pendientes» por rol (Issue #241). */
export type PendingTarget = 'lodgeTreasury' | 'candidateProfile' | 'secretariat' | 'ceremonies' | 'lodge' | 'notifications' | 'system' | 'bootstrap'

export interface PendingTaskFlags {
  canApproveLodgeExpenses: boolean
  canLodgeTreasury: boolean
  canSecretariat: boolean
  canCeremonies: boolean
  canLodge: boolean
  canManageLodgeSecretariat: boolean
  canConfigureSystem: boolean
  canBootstrap: boolean
}

export interface PendingCounts {
  expensesToApprove?: number | null
  secretariatReviews?: number | null
  ceremonyReviews?: number | null
  workshopCandidates?: number | null
  secretariatTasks?: number | null
}

export interface PendingTask {
  id: string
  title: string
  detail: string
  icon: InstitutionalIconName
  target: PendingTarget
  count: number | null
}

export type NavigationBadges = Partial<Record<PendingTarget, number>>

export function isOperationalProfile(flags: PendingTaskFlags): boolean {
  return flags.canApproveLodgeExpenses || flags.canLodgeTreasury || flags.canSecretariat || flags.canCeremonies || flags.canLodge || flags.canManageLodgeSecretariat
}

export function isSystemAdministrator(flags: PendingTaskFlags): boolean {
  return flags.canConfigureSystem || flags.canBootstrap
}

/** Construye la bandeja según capacidades efectivas; nunca muestra tareas de un rol que el perfil no tiene. */
export function buildPendingTasks(flags: PendingTaskFlags, counts: PendingCounts): PendingTask[] {
  const tasks: PendingTask[] = []
  const count = (value: number | null | undefined) => (typeof value === 'number' && Number.isFinite(value) ? Math.max(0, Math.trunc(value)) : null)

  if (flags.canApproveLodgeExpenses) tasks.push({ id: 'expenses', title: 'Autorizar egresos', detail: 'Egresos del Taller que esperan tu firma', icon: 'treasury', target: 'lodgeTreasury', count: count(counts.expensesToApprove) })
  else if (flags.canLodgeTreasury) tasks.push({ id: 'treasury', title: 'Tesorería del Taller', detail: 'Cuotas, cobranzas y egresos en trámite', icon: 'treasury', target: 'lodgeTreasury', count: count(counts.expensesToApprove) })
  if (flags.canSecretariat) tasks.push({ id: 'secretariat-reviews', title: 'Revisar insinuados', detail: 'Expedientes en revisión de Gran Secretaría', icon: 'candidate', target: 'candidateProfile', count: count(counts.secretariatReviews) })
  if (flags.canCeremonies) tasks.push({ id: 'ceremonies', title: 'Ceremonias por revisar', detail: 'Solicitudes, requisitos y autorizaciones', icon: 'ceremony', target: 'ceremonies', count: count(counts.ceremonyReviews) })
  if (flags.canLodge && !flags.canSecretariat) tasks.push({ id: 'workshop-candidates', title: 'Insinuaciones del Taller', detail: 'Expedientes del Taller en trámite', icon: 'candidate', target: 'candidateProfile', count: count(counts.workshopCandidates) })
  if (flags.canManageLodgeSecretariat || flags.canLodge) tasks.push({ id: 'secretariat-tasks', title: flags.canManageLodgeSecretariat ? 'Pendientes de Secretaría' : 'Gestión Logial', detail: 'Tenidas, actas, agenda y correspondencia', icon: flags.canManageLodgeSecretariat ? 'secretariat' : 'lodge', target: 'lodge', count: count(counts.secretariatTasks) })
  if (flags.canConfigureSystem) tasks.push({ id: 'system', title: 'Parámetros del sistema', detail: 'Plazos, reglas y versiones vigentes', icon: 'settings', target: 'system', count: null })
  if (flags.canBootstrap) tasks.push({ id: 'bootstrap', title: 'Configuración inicial', detail: 'Carga institucional y estructura base', icon: 'settings', target: 'bootstrap', count: null })

  return tasks
}

export function totalPending(tasks: PendingTask[]): number {
  return tasks.reduce((sum, task) => sum + (task.count ?? 0), 0)
}

/** Contadores para el menú lateral: solo destinos con al menos un pendiente. */
export function navigationBadges(tasks: PendingTask[]): NavigationBadges {
  const badges: NavigationBadges = {}
  for (const task of tasks) {
    if (!task.count) continue
    badges[task.target] = (badges[task.target] ?? 0) + task.count
  }
  return badges
}

export function greetingFor(date: Date, timeZone = 'America/Santiago'): string {
  const hour = Number(new Intl.DateTimeFormat('es-CL', { hour: 'numeric', hourCycle: 'h23', timeZone }).format(date))
  if (hour >= 5 && hour < 12) return 'Buenos días'
  if (hour >= 12 && hour < 20) return 'Buenas tardes'
  return 'Buenas noches'
}
