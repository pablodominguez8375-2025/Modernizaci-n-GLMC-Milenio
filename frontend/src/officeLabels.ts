/** Nombres de cargos en español para mostrar a las personas (los códigos internos vienen en inglés). */
const labels: Record<string, string> = {
  worshipful_master: 'Venerable Maestro',
  venerable_master: 'Venerable Maestro',
  immediate_past_master: 'Inmediato Ex Venerable Maestro',
  first_warden: 'Primer Vigilante',
  second_warden: 'Segundo Vigilante',
  orator: 'Orador',
  secretary: 'Secretario/a',
  hospitalaria: 'Hospitalaria',
  treasurer: 'Tesorero/a',
  first_deacon: 'Primer Diácono',
  second_deacon: 'Segundo Diácono',
  master_of_ceremonies: 'Maestro de Ceremonias',
  inner_guard: 'Guarda Templo Interno',
  outer_guard: 'Guarda Templo Externo',
  master_of_harmony: 'Maestro de Armonía',
}

export function officeLabel(code: string): string {
  const key = code.trim().toLowerCase()
  return labels[key] ?? code.replaceAll('_', ' ').replace(/\b\w/g, char => char.toUpperCase())
}
