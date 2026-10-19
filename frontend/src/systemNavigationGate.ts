/**
 * G10 - Evitar devolver a Inicio silenciosamente al validar permisos.
 *
 * Un snapshot ausente no demuestra que el servidor haya denegado una vista.
 * Para Sistema, incluso una denegación definitiva debe explicarse en pantalla:
 * jamás se renderiza contenido protegido sin la autorización vigente.
 */
export type SystemGate = 'not-system' | 'checking' | 'denied' | 'ready'

export function systemNavigationGate(view: string, snapshotReady: boolean, permitted: boolean): SystemGate {
  if (view !== 'system' && view !== 'bootstrap') return 'not-system'
  if (!snapshotReady) return 'checking'
  return permitted ? 'ready' : 'denied'
}

export function shouldReturnHomeForDeniedView(
  view: string,
  protectedView: boolean,
  snapshotReady: boolean,
  permitted: boolean,
): boolean {
  if (!protectedView || !snapshotReady || permitted) return false
  // Mantener la vista Sistema para mostrar un bloqueo explícito, no un Inicio engañoso.
  return view !== 'system' && view !== 'bootstrap'
}
