export const institutionalStatusOptions = [
  ['active', 'Activo'],
  ['past_active', 'Past Activo'],
  ['inactive', 'Inactivo'],
  ['voluntary_withdrawal', 'Retiro voluntario / En sueño'],
  ['forced_withdrawal', 'Retiro forzoso'],
  ['reinstated', 'Reintegrado'],
  ['deceased', 'Fallecido'],
] as const

export function institutionalStatusLabel(value?: string | null) {
  if (!value) return 'Sin registro'
  if (value === 'workshop_transfer') return 'Cambio de Taller'
  return institutionalStatusOptions.find(([code]) => code === value)?.[1] ?? value
}

export function isPastActiveStatus(value?: string | null) {
  return value === 'past_active'
}

export function isVoluntaryWithdrawalStatus(value?: string | null) {
  return value === 'voluntary_withdrawal'
}
