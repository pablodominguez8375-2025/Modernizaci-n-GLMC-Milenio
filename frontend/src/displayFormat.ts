/**
 * PMGM-UI · Formatos de presentación compartidos (es-CL, America/Santiago).
 * Evita duplicar «Nº» en nombres de Taller que ya incluyen su número,
 * concuerda singular/plural en contadores y muestra fechas sin hora en formato chileno.
 */

type NumberLike = string | number | null | undefined

function escapeRegExp(value: string) { return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') }

/** «Taller X · Nº 12», salvo que el nombre ya incluya ese número («Taller Demostrativo Nº 1»). */
export function organizationDisplayName(name: string | null | undefined, number?: NumberLike): string {
  const base = (name ?? '').trim()
  const num = number === null || number === undefined ? '' : String(number).trim()
  if (!num) return base
  if (!base) return `Nº ${num}`
  const alreadyNumbered = new RegExp(`N\\s*[º°o]\\.?\\s*${escapeRegExp(num)}(?!\\d)`, 'i')
  return alreadyNumbered.test(base) ? base : `${base} · Nº ${num}`
}

/** Número del Taller para sellos/insignias: número explícito o el que figure en el nombre. */
export function organizationNumberOf(name: string | null | undefined, number?: NumberLike): string | null {
  const num = number === null || number === undefined ? '' : String(number).trim()
  if (num) return num
  const match = /N\s*[º°o]\.?\s*(\d+)/i.exec(name ?? '')
  return match ? match[1] : null
}

/** «1 pendiente», «3 pendientes». */
export function countLabel(count: number, singular: string, plural = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : plural}`
}

/** Fecha sin hora (AAAA-MM-DD) en formato es-CL, sin corrimiento por zona horaria. */
export function formatDateOnlyCl(value: string | null | undefined): string {
  if (!value) return '—'
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (!match) return value
  const [, y, m, d] = match
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' })
    .format(new Date(Date.UTC(Number(y), Number(m) - 1, Number(d), 12)))
}
