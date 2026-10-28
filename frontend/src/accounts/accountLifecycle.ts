import type { AccountInventoryItem, AccountLifecycleAction, AccountLifecyclePolicy } from '../api/userAccounts'

/* Ciclo de vida de cuentas (#768/#770) · reglas de pantalla; el servidor decide siempre (PR #772).
 * `admin` nunca se modifica; `qa.admin` solo admite eliminación excepcional; el resto de las preexistentes está protegido. */
export const REASON_MIN = 10
export const REASON_MAX = 200

export const LIFECYCLE_LABELS: Record<AccountLifecycleAction, string> = {
  activate: 'Activar',
  deactivate: 'Desactivar',
  'delete-permanently': 'Eliminar definitivamente',
}

export function availableLifecycleActions(item: Pick<AccountInventoryItem, 'username' | 'enabled' | 'protected'>): AccountLifecycleAction[] {
  if (item.username === 'admin') return []
  if (item.protected) return item.username === 'qa.admin' ? ['delete-permanently'] : []
  return [item.enabled ? 'deactivate' : 'activate', 'delete-permanently']
}

export function lifecycleReasonValid(reason: string, policy?: Pick<AccountLifecyclePolicy, 'reasonMinLength' | 'reasonMaxLength'> | null): boolean {
  const length = reason.trim().length
  return length >= (policy?.reasonMinLength ?? REASON_MIN) && length <= (policy?.reasonMaxLength ?? REASON_MAX)
}

/** Eliminar exige escribir el nombre de usuario exactamente (mayúsculas y puntos incluidos). */
export function lifecycleConfirmationValid(action: AccountLifecycleAction, username: string, typed: string): boolean {
  return action !== 'delete-permanently' || typed === username
}

export function lifecycleDescription(action: AccountLifecycleAction, name: string): string {
  if (action === 'activate') return `${name} podrá volver a ingresar al sistema.`
  if (action === 'deactivate') return `${name} no podrá ingresar y se cerrarán sus sesiones abiertas. Podrá activarla de nuevo cuando lo necesite.`
  return `Se elimina para siempre la cuenta de acceso de ${name}. No se puede deshacer. La cuenta deja de existir y se cierran sus sesiones.`
}

export function lifecycleDoneMessage(action: AccountLifecycleAction, name: string): string {
  if (action === 'activate') return `Cuenta de ${name} activada.`
  if (action === 'deactivate') return `Cuenta de ${name} desactivada.`
  return `Cuenta de ${name} eliminada definitivamente.`
}

export const LIFECYCLE_UNAVAILABLE = 'La gestión de cuentas aún no está habilitada en esta instalación. Se habilitará cuando el servidor y la revisión de seguridad estén aprobados.'

export function lifecycleErrorMessage(error: unknown): string {
  const status = (error as { status?: number } | null)?.status
  if (status === 400) return `Revise el motivo: debe tener entre ${REASON_MIN} y ${REASON_MAX} caracteres.`
  if (status === 403) return 'Su cuenta no tiene permiso para realizar esta operación.'
  if (status === 404) return 'La cuenta ya no existe. Actualice el listado.'
  if (status === 409) return 'No se pudo completar: la cuenta cambió o la política de seguridad no permite esta acción. Actualice el listado e intente de nuevo.'
  if (status === 503) return LIFECYCLE_UNAVAILABLE
  return error instanceof Error && error.message ? error.message : 'No se pudo completar la operación.'
}
