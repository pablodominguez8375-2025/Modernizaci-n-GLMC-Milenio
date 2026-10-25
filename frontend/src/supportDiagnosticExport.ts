import type { SupportDiagnosticEntry, SupportDiagnosticsResponse } from './api/pmgmApi'

// CSV sanitization: all values are quoted and spreadsheet-formula prefixes
// are escaped (including those preceded by spaces/tabs/newlines).
export function safeCsvCell(value: string | number): string {
  const raw = String(value)
  const escaped = /^[\s]*[=+\-@]/.test(raw) || /^[\t\r]/.test(raw) ? "'" + raw : raw
  return '"' + escaped.replaceAll('"','""') + '"'
}

export function diagnosticCsv(items: SupportDiagnosticEntry[]): string {
  const rows: Array<Array<string | number>> = [
    ['Fecha UTC','Gravedad','HTTP','Método','Ruta técnica','ID de correlación'],
    ...items.map(x => [
      x.occurredAtUtc, x.severity, x.statusCode, x.method,
      x.route, x.correlationId
    ])
  ]
  return '\uFEFF' + rows.map(x => x.map(safeCsvCell).join(';')).join('\r\n') + '\r\n'
}

export function diagnosticJson(snapshot: SupportDiagnosticsResponse): string {
  return JSON.stringify({
    source: snapshot.source, volatileStorage: snapshot.volatileStorage,
    capacity: snapshot.capacity, total: snapshot.total,
    exportedAtUtc: new Date().toISOString(),
    items: snapshot.items
  }, null, 2)
}
