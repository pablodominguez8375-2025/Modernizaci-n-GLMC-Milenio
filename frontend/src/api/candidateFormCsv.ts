import type { CandidateFormCaptureDetail, FormFieldDefinition, FormValue } from './candidateFormTypes'

/**
 * Synthetic showcase-only representation. Real CSV is always generated and
 * audited server-side; never invent an export with information from the core dossier.
 */
export function renderDemoCandidateCaptureCsv(capture: CandidateFormCaptureDetail): string {
  const fields = (capture.fields ?? []).filter(field => !field.isCore)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.key.localeCompare(b.key))
  const indexed = new Map<string, FormFieldDefinition>(fields.map(field => [field.key, field]))
  if (Object.keys(capture.values).some(key => !indexed.has(key)))
    throw new Error('La captura incluye un campo sin definición histórica.')

  const header = 'Campo;Clave;Valor;Clasificación;Tipo;Versión;Revisión;Fecha captura (UTC)'
  const time = new Date(capture.capturedAtUtc)
  if (Number.isNaN(time.getTime())) throw new Error('Fecha histórica inválida.')
  const recorded = time.toISOString().slice(0, 19).replace('T', ' ')
  const rows = fields.filter(field => Object.hasOwn(capture.values, field.key)).map(field => {
    const value = capture.values[field.key]
    return [
      quote(field.label), quote(field.key), quote(format(value), typeof value === 'number'),
      quote(field.classification), quote(field.typeCode),
      quote(String(capture.schemaVersion)), quote(String(capture.captureRevision)), quote(recorded),
    ].join(';')
  })
  return '\uFEFF' + [header, ...rows].join('\r\n') + '\r\n'
}

function format(value: FormValue): string {
  if (value === null) return ''
  if (typeof value === 'boolean') return value ? 'Sí' : 'No'
  return Array.isArray(value) ? value.join(' | ') : String(value)
}

function quote(value: string, numeric = false): string {
  const neutralized = !numeric && /^[\s\u0000-\u001f]*[=+\-@]/u.test(value) ? "'" + value : value
  return '"' + neutralized.replace(/"/g, '""') + '"'
}
