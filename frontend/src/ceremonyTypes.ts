// Fuente única de etiquetas de tipo de ceremonia (PMGM-GAP-001 · GAP-001).
//
// Debe reflejar exactamente backend/src/PMGM.Api/Modules/Ceremonies/CeremonyCodes.cs
// (CeremonyCodes.Type). Antes existían cuatro copias locales que, ante cualquier
// valor distinto de Iniciación o Aumento de salario, mostraban "Exaltación" por
// defecto: una Afiliación o una Incorporación aparecían rotuladas como Exaltación.
//
// "Otra" figura en el formulario institucional 2026 pero NO existe como código en
// el backend; no se agrega aquí hasta que exista una decisión normativa y su
// contrato backend correspondiente.

export const CEREMONY_TYPE_LABELS = {
  initiation: 'Iniciación',
  affiliation: 'Afiliación',
  wage_increase: 'Aumento de salario',
  exaltation: 'Exaltación',
  incorporation: 'Incorporación',
} as const

export type KnownCeremonyType = keyof typeof CEREMONY_TYPE_LABELS

export function isKnownCeremonyType(value: string): value is KnownCeremonyType {
  return Object.prototype.hasOwnProperty.call(CEREMONY_TYPE_LABELS, value)
}

/**
 * Etiqueta institucional de un tipo de ceremonia. Un código desconocido nunca se
 * disfraza de otro grado: se muestra explícitamente como no reconocido para que
 * el problema sea visible en QA/UAT en lugar de silencioso.
 */
export function ceremonyTypeLabel(value: string | null | undefined): string {
  if (!value) return 'Sin tipo de ceremonia'
  return isKnownCeremonyType(value) ? CEREMONY_TYPE_LABELS[value] : `Tipo no reconocido (${value})`
}
