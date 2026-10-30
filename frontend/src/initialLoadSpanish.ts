/** Carga inicial amigable: plantillas en español y traducción al contrato del servidor (encabezados y códigos en inglés).
    El servidor NO cambia: antes de revisar, cada archivo se convierte al formato exacto que él exige. Nada se guarda. */
import { INITIAL_LOAD_FILE_NAMES, type InitialLoadFileName } from './initialLoadPreview'

export interface InitialLoadColumn { key: string; label: string; required: boolean; help: string; example: string }

export const initialLoadColumns: Record<InitialLoadFileName, InitialLoadColumn[]> = {
  'Talleres.csv': [
    { key: 'workshopCode', label: 'Código del Taller', required: true, help: 'Un código corto que usted inventa para identificar el Taller en los otros dos archivos. Solo letras, números, guion o guion bajo, sin espacios (máximo 32). No se repite.', example: 'T23' },
    { key: 'number', label: 'Número', required: true, help: 'Número oficial del Taller.', example: '23' },
    { key: 'name', label: 'Nombre del Taller', required: true, help: 'Nombre completo del Taller.', example: 'Taller Ejemplo Nº 23' },
    { key: 'orienteCode', label: 'Código del Oriente', required: false, help: 'Código del Oriente al que pertenece. Puede quedar vacío si aún no está definido.', example: 'SCL' },
    { key: 'city', label: 'Ciudad', required: true, help: 'Ciudad donde funciona el Taller.', example: 'Santiago' },
    { key: 'country', label: 'País', required: true, help: 'País del Taller.', example: 'Chile' },
  ],
  'Hermanos.csv': [
    { key: 'personRef', label: 'Referencia del hermano', required: true, help: 'Un código corto que usted inventa para cada hermano y que sirve para unir este archivo con el de cargos. Sin espacios, no se repite.', example: 'H0001' },
    { key: 'identityType', label: 'Tipo de documento', required: true, help: 'Escriba «RUT» o «Pasaporte».', example: 'RUT' },
    { key: 'identityNumber', label: 'Número de documento', required: true, help: 'RUT con su dígito verificador, o número de pasaporte (4 a 24 letras o números). No se puede repetir entre hermanos.', example: '12.345.678-5' },
    { key: 'firstNames', label: 'Nombres', required: true, help: 'Nombres del hermano.', example: 'Juan Andrés' },
    { key: 'lastNames', label: 'Apellidos', required: true, help: 'Apellidos del hermano.', example: 'Pérez Soto' },
    { key: 'email', label: 'Correo electrónico', required: true, help: 'Un correo válido de la persona.', example: 'juan.perez@ejemplo.cl' },
    { key: 'workshopCode', label: 'Código del Taller', required: true, help: 'El mismo código que puso en el archivo de Talleres.', example: 'T23' },
    { key: 'grade', label: 'Grado', required: true, help: 'Escriba «Aprendiz», «Compañero» o «Maestro» (también sirve 1, 2 o 3).', example: 'Maestro' },
    { key: 'institutionalStatus', label: 'Estado institucional', required: true, help: 'Uno de: Activo, Inactivo, Ex activo, Retiro voluntario, Retiro forzoso, Reincorporado, Fallecido, Traslado de Taller.', example: 'Activo' },
  ],
  'CargosVigentes.csv': [
    { key: 'personRef', label: 'Referencia del hermano', required: true, help: 'La misma referencia del hermano que puso en el archivo de Hermanos.', example: 'H0001' },
    { key: 'workshopCode', label: 'Código del Taller', required: true, help: 'El Taller donde ejerce el cargo; debe ser el mismo Taller de ese hermano.', example: 'T23' },
    { key: 'officeCode', label: 'Cargo', required: true, help: 'Nombre del cargo, tal como aparece en la lista de cargos de esta guía.', example: 'Venerable Maestro' },
    { key: 'startDate', label: 'Fecha de inicio', required: true, help: 'Desde cuándo ejerce el cargo, como día-mes-año.', example: '15-03-2026' },
  ],
}

export const initialLoadFilePurpose: Record<InitialLoadFileName, string> = {
  'Talleres.csv': 'Una fila por cada Taller.',
  'Hermanos.csv': 'Una fila por cada hermano, con su Taller, grado y estado.',
  'CargosVigentes.csv': 'Una fila por cada cargo que hoy ejerce un hermano.',
}

/* ---------------------------------------------------------------- Listas de valores */
export const gradeOptions: { code: string; label: string }[] = [{ code: '1', label: 'Aprendiz' }, { code: '2', label: 'Compañero' }, { code: '3', label: 'Maestro' }]
export const statusOptions: { code: string; label: string }[] = [
  { code: 'active', label: 'Activo' }, { code: 'inactive', label: 'Inactivo' }, { code: 'past_active', label: 'Ex activo' },
  { code: 'voluntary_withdrawal', label: 'Retiro voluntario' }, { code: 'forced_withdrawal', label: 'Retiro forzoso' },
  { code: 'reinstated', label: 'Reincorporado' }, { code: 'deceased', label: 'Fallecido' }, { code: 'workshop_transfer', label: 'Traslado de Taller' },
]
export const officeOptions: { code: string; label: string }[] = [
  { code: 'worshipful_master', label: 'Venerable Maestro' }, { code: 'immediate_past_master', label: 'Inmediato Ex Venerable Maestro' },
  { code: 'first_warden', label: 'Primer Vigilante' }, { code: 'second_warden', label: 'Segundo Vigilante' }, { code: 'orator', label: 'Orador' },
  { code: 'secretary', label: 'Secretario/a' }, { code: 'hospitalaria', label: 'Hospitalaria' }, { code: 'treasurer', label: 'Tesorero/a' },
  { code: 'first_deacon', label: 'Primer Diácono' }, { code: 'second_deacon', label: 'Segundo Diácono' }, { code: 'master_of_ceremonies', label: 'Maestro de Ceremonias' },
  { code: 'inner_guard', label: 'Guarda Templo Interno' }, { code: 'outer_guard', label: 'Guarda Templo Externo (Retejador)' }, { code: 'master_of_harmony', label: 'Maestro de Armonía' },
]

const norm = (value: string) => value.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9/ ]+/g, ' ').replace(/\s+/g, ' ').trim()

function lookup(options: { code: string; label: string }[], extra: Record<string, string> = {}) {
  const map = new Map<string, string>()
  for (const option of options) { map.set(norm(option.label), option.code); map.set(norm(option.code), option.code) }
  for (const [alias, code] of Object.entries(extra)) map.set(norm(alias), code)
  return map
}
const gradeMap = lookup(gradeOptions, { 'aprendiz (1)': '1', 'companero (2)': '2', 'maestro (3)': '3', aprendiza: '1', companera: '2', maestra: '3' })
const statusMap = lookup(statusOptions, { 'ex activo (past active)': 'past_active', 'retiro forzado': 'forced_withdrawal', traslado: 'workshop_transfer' })
const officeMap = lookup(officeOptions, {
  'guarda templo externo': 'outer_guard', retejador: 'outer_guard', secretario: 'secretary', secretaria: 'secretary', tesorero: 'treasurer', tesorera: 'treasurer',
  oradora: 'orator', hospitalario: 'hospitalaria', 'venerable maestra': 'worshipful_master', 'primer vigilante': 'first_warden', 'segundo vigilante': 'second_warden',
})
const identityMap = lookup([{ code: 'rut', label: 'RUT' }, { code: 'passport', label: 'Pasaporte' }])

/* ---------------------------------------------------------------- CSV */
export function parseCsv(text: string, delimiter: string): string[][] {
  const rows: string[][] = []; let row: string[] = []; let field = ''; let quoted = false
  const body = text.charCodeAt(0) === 0xFEFF ? text.slice(1) : text
  for (let i = 0; i < body.length; i++) {
    const c = body[i]
    if (quoted) {
      if (c === '"') { if (body[i + 1] === '"') { field += '"'; i++ } else quoted = false } else field += c
    } else if (c === '"') quoted = true
    else if (c === delimiter) { row.push(field); field = '' }
    else if (c === '\r' || c === '\n') { if (c === '\r' && body[i + 1] === '\n') i++; row.push(field); field = ''; rows.push(row); row = [] }
    else field += c
  }
  if (field !== '' || row.length) { row.push(field); rows.push(row) }
  return rows.filter(r => r.some(cell => cell.trim() !== ''))
}
const quote = (value: string) => /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value
export const toCsv = (rows: string[][], delimiter = ',') => rows.map(r => r.map(c => delimiter === ',' ? quote(c) : (c.includes(delimiter) || /["\r\n]/.test(c) ? `"${c.replace(/"/g, '""')}"` : c)).join(delimiter)).join('\r\n') + '\r\n'

function detectDelimiter(text: string): string {
  const first = (text.charCodeAt(0) === 0xFEFF ? text.slice(1) : text).split(/\r?\n/, 1)[0] ?? ''
  const counts: [string, number][] = [[';', first.split(';').length], [',', first.split(',').length], ['\t', first.split('\t').length]]
  return counts.sort((a, b) => b[1] - a[1])[0][0]
}

function dateToIso(value: string): string {
  const match = /^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})$/.exec(value.trim())
  return match ? `${match[3]}-${match[2].padStart(2, '0')}-${match[1].padStart(2, '0')}` : value
}

/** Texto de plantilla en español (solo encabezados), separado por punto y coma para que Excel en español lo abra en columnas. */
export function spanishTemplate(name: InitialLoadFileName): string {
  return '﻿' + toCsv([initialLoadColumns[name].map(c => c.label)], ';')
}

export interface NormalizedCsv { text: string; translated: boolean; notes: string[] }

/** Convierte un CSV (en español o ya en el formato del servidor; con «;» o «,»; con o sin BOM) al formato exacto del servidor. */
export function normalizeInitialLoadCsv(name: InitialLoadFileName, text: string): NormalizedCsv {
  const columns = initialLoadColumns[name]
  const rows = parseCsv(text, detectDelimiter(text))
  if (rows.length === 0) return { text, translated: false, notes: [] }
  const header = rows[0].map(cell => norm(cell))
  const byLabel = columns.map(c => header.indexOf(norm(c.label)))
  const byKey = columns.map(c => header.indexOf(norm(c.key)))
  const useLabels = byLabel.every(i => i >= 0)
  const useKeys = byKey.every(i => i >= 0)
  if (!useLabels && !useKeys) return { text, translated: false, notes: [] } // el servidor informará el encabezado
  const index = useLabels ? byLabel : byKey
  const out: string[][] = [columns.map(c => c.key)]
  const notes: string[] = []
  const translate = (key: string, value: string): string => {
    const v = value.trim()
    const pick = (map: Map<string, string>) => map.get(norm(v)) ?? v
    switch (key) {
      case 'identityType': return pick(identityMap)
      case 'grade': return pick(gradeMap)
      case 'institutionalStatus': return pick(statusMap)
      case 'officeCode': return pick(officeMap)
      case 'startDate': return dateToIso(v)
      default: return v
    }
  }
  for (const row of rows.slice(1)) out.push(columns.map((c, i) => translate(c.key, row[index[i]] ?? '')))
  if (useLabels) notes.push('Encabezados en español convertidos al formato del servidor.')
  return { text: toCsv(out), translated: useLabels || out.length > 1, notes }
}

export function normalizeAll(texts: Partial<Record<InitialLoadFileName, string>>): Record<string, string> {
  const result: Record<string, string> = {}
  for (const name of INITIAL_LOAD_FILE_NAMES) { const t = texts[name]; if (t !== undefined) result[name] = normalizeInitialLoadCsv(name, t).text }
  return result
}

/* ---------------------------------------------------------------- Textos de la revisión */


const spanishFindingTexts: Record<string, string> = {
  REQUIRED: 'Falta este dato (es obligatorio)',
  INVALID_CODE: 'El código solo puede tener letras, números, guion o guion bajo, sin espacios (máximo 32)',
  DUPLICATE: 'Este dato está repetido en otra fila',
  UNKNOWN_WORKSHOP: 'Este código de Taller no existe en el archivo de Talleres',
  UNKNOWN_PERSON: 'Esta referencia de hermano no existe en el archivo de Hermanos',
  PERSON_WORKSHOP_MISMATCH: 'El Taller del cargo no es el mismo Taller de ese hermano',
  UNKNOWN_OFFICE: 'Cargo no reconocido: use un nombre de la lista de cargos de la guía',
  INVALID_STATUS: 'Estado no reconocido: use uno de la lista de estados de la guía',
  INVALID_GRADE: 'Grado no reconocido: escriba Aprendiz, Compañero o Maestro',
  INVALID_EMAIL: 'El correo no es válido',
  INVALID_IDENTITY_TYPE: 'Tipo de documento no reconocido: escriba RUT o Pasaporte',
  INVALID_IDENTITY: 'El RUT o pasaporte no es válido',
  INVALID_DATE: 'La fecha no es válida: use día-mes-año, por ejemplo 15-03-2026',
  INVALID_COLUMNS: 'La fila no tiene las columnas de la plantilla',
  INVALID_ROW_COUNT: 'El archivo tiene más filas que el máximo (2 000)',
}
const spanishFieldLabels: Record<string, string> = {
  workshopCode: 'Código del Taller', number: 'Número', name: 'Nombre del Taller', orienteCode: 'Código del Oriente', city: 'Ciudad', country: 'País',
  personRef: 'Referencia del hermano', identityType: 'Tipo de documento', identityNumber: 'Número de documento', firstNames: 'Nombres', lastNames: 'Apellidos',
  email: 'Correo electrónico', grade: 'Grado', institutionalStatus: 'Estado institucional', officeCode: 'Cargo', startDate: 'Fecha de inicio',
}

export const describeFindingEs = (code: string) => spanishFindingTexts[code]
export const describeFieldEs = (field: string) => spanishFieldLabels[field]
