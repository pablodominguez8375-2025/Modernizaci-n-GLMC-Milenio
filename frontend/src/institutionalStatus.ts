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
  // workshop_transfer es un hito histórico; con el segmento destino vigente la
  // condición institucional presentada al usuario sigue siendo Activo.
  if (value === 'workshop_transfer') return 'Activo'
  return institutionalStatusOptions.find(([code]) => code === value)?.[1] ?? value
}

export function isPastActiveStatus(value?: string | null) {
  return value === 'past_active'
}

export function isVoluntaryWithdrawalStatus(value?: string | null) {
  return value === 'voluntary_withdrawal'
}
