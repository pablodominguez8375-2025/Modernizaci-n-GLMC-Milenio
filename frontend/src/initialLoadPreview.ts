/** #694 / #750 — revisión (solo lectura) de los 3 CSV de la Carga inicial. Los datos nunca se guardan: viven en memoria hasta enviarlos. */
export const INITIAL_LOAD_FILE_NAMES = ['Talleres.csv', 'Hermanos.csv', 'CargosVigentes.csv'] as const
export type InitialLoadFileName = typeof INITIAL_LOAD_FILE_NAMES[number]

/** Mismos topes que el servidor (InitialLoadCsvShapePolicy): 800 000 caracteres por archivo, 2,5 MB por envío. */
export const MAX_CHARACTERS_PER_FILE = 800_000
export const MAX_REQUEST_CHARACTERS = 2_000_000

export interface InitialLoadFinding { sheet: string; row: number; field: string; code: string }
export interface InitialLoadPreviewResult {
  valid: boolean; canApply: boolean; writesPerformed: number
  status: 'PREVIEW_VALID' | 'PREVIEW_REQUIRES_CORRECTION' | string
  dataRowCounts: Record<string, number>
  findings: InitialLoadFinding[]
}

const findingTexts: Record<string, string> = {
  HEADER_MISMATCH: 'Los encabezados no coinciden con la plantilla',
  COLUMN_COUNT: 'La fila no tiene la cantidad de columnas de la plantilla',
  FIELD_TOO_LONG: 'El dato es demasiado largo',
  INVALID_CONTROL: 'Hay caracteres no permitidos',
  INVALID_CSV_STRUCTURE: 'El archivo no tiene forma de CSV',
  INVALID_FILE_SIZE: 'El archivo está vacío o es demasiado grande',
  INVALID_LINE_ENDING: 'Los saltos de línea no son válidos',
  INVALID_QUOTING: 'Las comillas no están bien cerradas',
  UNTERMINATED_QUOTE: 'Falta cerrar unas comillas',
  MULTILINE_NOT_SUPPORTED: 'Un dato ocupa más de una línea',
  TOO_MANY_ROWS: 'El archivo tiene más filas que el máximo (2 000)',
  REQUIRED_ROWS: 'El archivo no trae ninguna fila de datos',
  INVALID_SHEETS: 'Faltan archivos o sobran archivos: se necesitan los tres',
}

/** Texto simple para un hallazgo; nunca reproduce el contenido de las celdas. */
export function describeFinding(code: string): string { return findingTexts[code] ?? 'Hay un problema en este dato' }

const fieldTexts: Record<string, string> = { header: 'Encabezados', columns: 'Cantidad de columnas', rows: 'Filas', file: 'Archivo completo' }
/** Nombre simple de la columna con problema; si es un campo de la plantilla se muestra tal cual. */
export function describeField(field: string): string { return fieldTexts[field] ?? field }

export function describePreviewHttpError(status: number): string {
  switch (status) {
    case 400: return 'El servidor no pudo leer los archivos. Revise que sean los tres CSV de la plantilla.'
    case 401: return 'Su sesión venció. Ingrese de nuevo.'
    case 403: return 'Solo el Máster registrado en el servidor puede revisar los archivos de la carga inicial.'
    case 409: return 'La carga inicial ya se cerró: no se pueden revisar archivos nuevos.'
    case 413: return 'Los archivos son demasiado grandes. Divida la nómina o quite filas.'
    case 415: return 'El servidor rechazó el formato del envío.'
    case 503: return 'No fue posible confirmar el estado de la carga. Intente más tarde; no se asume que esté abierta.'
    default: return `El servidor respondió ${status}. No se guardó nada.`
  }
}

/** Valida el archivo elegido ANTES de leerlo (nombre y tamaño). Devuelve el mensaje de error o null. */
export function checkChosenFile(expected: InitialLoadFileName, file: { name: string; size: number }): string | null {
  if (!/\.csv$/i.test(file.name)) return `Elija un archivo CSV para ${expected}.`
  if (file.size === 0) return 'El archivo está vacío.'
  if (file.size > MAX_CHARACTERS_PER_FILE * 3) return 'El archivo es demasiado grande (máximo 800 000 caracteres).'
  return null
}

export function checkTexts(texts: Partial<Record<InitialLoadFileName, string>>): string | null {
  let total = 0
  for (const name of INITIAL_LOAD_FILE_NAMES) {
    const text = texts[name]
    if (text === undefined) return `Falta elegir ${name}.`
    if (text.length > MAX_CHARACTERS_PER_FILE) return `${name} supera el máximo de ${MAX_CHARACTERS_PER_FILE.toLocaleString('es-CL')} caracteres.`
    total += text.length
  }
  return total > MAX_REQUEST_CHARACTERS ? 'Los tres archivos juntos son demasiado grandes.' : null
}

export interface PreviewView { tone: 'ok' | 'warn'; title: string; detail: string; counts: { name: string; rows: number }[] }

/** Resumen en palabras simples. Nunca afirma que se guardó o se pueda aplicar nada. */
export function describePreview(result: InitialLoadPreviewResult): PreviewView {
  const counts = INITIAL_LOAD_FILE_NAMES.map(name => ({ name, rows: result.dataRowCounts[name] ?? 0 }))
  return result.valid
    ? { tone: 'ok', title: 'Los archivos están en orden', detail: 'La revisión no encontró problemas de forma. No se guardó ningún dato: esto es solo una revisión.', counts }
    : { tone: 'warn', title: 'Hay que corregir los archivos', detail: 'Corrija los puntos de la lista en sus archivos y vuelva a revisarlos. No se guardó ningún dato.', counts }
}
