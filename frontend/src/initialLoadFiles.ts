export interface InitialLoadFileView { name: string; purpose: string; columns: string[] }

const purposes: Record<string, string> = {
  'Talleres.csv': 'Un Taller por fila: código, número, nombre, Oriente y ciudad.',
  'Hermanos.csv': 'Un hermano por fila, con su Taller, grado y estado institucional.',
  'CargosVigentes.csv': 'Un cargo vigente por fila: quién lo ejerce, en qué Taller y desde cuándo.',
}

/** Traduce el contrato del servidor a textos simples; archivos desconocidos conservan su nombre. */
export function describeInitialLoadFiles(files: { name: string; columns: string[] }[]): InitialLoadFileView[] {
  return files.map(file => ({ name: file.name, purpose: purposes[file.name] ?? 'Archivo de la carga inicial.', columns: file.columns }))
}
