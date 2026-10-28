import type { AccountInventoryItem } from '../api/userAccounts'

/* Inventario de cuentas (solo el Máster único, solo lectura). Textos en lenguaje simple. */
export const PROTECTED_LABEL = 'Cuenta preexistente · Protegida'

export function inventoryKindLabel(item: AccountInventoryItem): string {
  if (item.kind === 'legacy_protected') return 'Cuenta preexistente'
  if (item.kind === 'superuser') return 'Superusuario de soporte'
  if (item.kind === 'member') return 'Hermano institucional'
  return 'Cuenta de acceso'
}

export function inventoryOriginLabel(item: AccountInventoryItem): string {
  return item.origin === 'managed' ? 'Creada desde este sistema' : 'Creada antes del sistema de altas'
}

export function inventoryStateLabel(item: AccountInventoryItem): string {
  return item.enabled ? 'Habilitada' : 'Deshabilitada'
}

export function summarizeInventory(items: AccountInventoryItem[]) {
  const protectedItems = items.filter(item => item.protected)
  return { total: items.length, protectedCount: protectedItems.length, managedCount: items.length - protectedItems.length, protectedItems }
}

export function inventoryErrorMessage(): string {
  return 'El inventario completo de cuentas no está disponible en este momento. El listado de arriba sigue funcionando.'
}
