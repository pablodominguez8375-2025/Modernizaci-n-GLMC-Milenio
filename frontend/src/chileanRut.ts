/**
 * Validación matemática del RUT/RUN chileno: módulo 11.
 * No acredita existencia del registro ni titularidad de una persona.
 */
export interface ParsedChileanRut {
  compact: string
  normalized: string
  formatted: string
}

export function parseChileanRut(input: string | null | undefined): ParsedChileanRut | null {
  if (typeof input !== 'string') return null
  const value = input.trim().toUpperCase()
  // Aceptar compacto, sin puntos con guion, o grupos de miles con guion opcional.
  // Rechazar separadores inesperados; no borrarlos indiscriminadamente.
  const raw = value.match(/^(\d{1,8})-?([0-9K])$/)
  const grouped = value.match(/^(\d{1,3}(?:\.\d{3}){1,2})-?([0-9K])$/)
  const bodyInput = raw?.[1] ?? grouped?.[1]?.replace(/\./g, '')
  const dv = raw?.[2] ?? grouped?.[2]
  if (!bodyInput || !dv || bodyInput.length > 8) return null
  const body = bodyInput.replace(/^0+/, '')
  if (!body) return null

  let sum = 0
  for (let i = 0; i < body.length; i++) {
    sum += Number(body[body.length - 1 - i]) * (2 + (i % 6))
  }
  const digit = 11 - (sum % 11)
  const expected = digit === 11 ? '0' : digit === 10 ? 'K' : String(digit)
  if (dv !== expected) return null
  return {
    compact: `${body}${dv}`,
    normalized: `${body}-${dv}`,
    formatted: `${body.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}-${dv}`,
  }
}

export function isValidChileanRut(value: string | null | undefined): boolean {
  return parseChileanRut(value) !== null
}

export function formatChileanRut(value: string | null | undefined): string | null {
  return parseChileanRut(value)?.formatted ?? null
}
