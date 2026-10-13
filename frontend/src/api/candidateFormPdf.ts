import type { CandidateFormCaptureDetail, FormValue } from './candidateFormTypes'

/** A fictional in-memory PDF for the static showcase; real exports use the audited API. */
export const PRIVATE_PDF_MEDIA = 'application/pdf'
const PAGE_W = 595.28
const PAGE_H = 841.89
const specialWinAnsi: Record<number, number> = {
  0x20ac: 0x80, 0x201a: 0x82, 0x0192: 0x83, 0x201e: 0x84,
  0x2026: 0x85, 0x2020: 0x86, 0x2021: 0x87, 0x02c6: 0x88,
  0x2030: 0x89, 0x0160: 0x8a, 0x2039: 0x8b, 0x0152: 0x8c,
  0x017d: 0x8e, 0x2018: 0x91, 0x2019: 0x92, 0x201c: 0x93,
  0x201d: 0x94, 0x2022: 0x95, 0x2013: 0x96, 0x2014: 0x97,
  0x02dc: 0x98, 0x2122: 0x99, 0x0161: 0x9a, 0x203a: 0x9b,
  0x0153: 0x9c, 0x017e: 0x9e, 0x0178: 0x9f,
}

function printable(text: string): string {
  return Array.from(text).map(char => {
    const scalar = char.codePointAt(0) ?? 0
    return (scalar >= 32 && scalar <= 126) || (scalar >= 160 && scalar <= 255) ||
      Object.hasOwn(specialWinAnsi, scalar)
      ? char : '[U+' + scalar.toString(16).toUpperCase() + ']'
  }).join('')
}

function hexWinAnsi(text: string): string {
  return Array.from(printable(text)).map(char => {
    const scalar = char.codePointAt(0) ?? 0
    const byte = specialWinAnsi[scalar] ?? scalar
    if (byte > 255) throw new Error('Error de representación PDF.')
    return byte.toString(16).padStart(2, '0').toUpperCase()
  }).join('')
}

function wrap(text: string, size: number): string[] {
  const lineLimit = Math.floor(487 / size / 1.02)
  if (!text.length) return ['']
  const result: string[] = []
  let current = ''
  for (const token of printable(text).split(' ')) {
    if (current && current.length + 1 + token.length > lineLimit) {
      result.push(current)
      current = ''
    }
    let rest = token
    while (rest.length > lineLimit) {
      if (current) { result.push(current); current = '' }
      result.push(rest.slice(0, lineLimit))
      rest = rest.slice(lineLimit)
    }
    if (rest) current = current ? current + ' ' + rest : rest
  }
  if (current) result.push(current)
  return result.length ? result : ['']
}

function formatValue(value: FormValue): string {
  if (value === null) return 'No informado'
  if (typeof value === 'boolean') return value ? 'Sí' : 'No'
  return Array.isArray(value) ? value.join(', ') : String(value)
}

function combine(parts: Uint8Array[]): Uint8Array<ArrayBuffer> {
  const output = new Uint8Array(parts.reduce((total, bytes) => total + bytes.length, 0))
  let cursor = 0
  for (const part of parts) { output.set(part, cursor); cursor += part.length }
  return output
}

export function renderDemoCandidateCapturePdf(capture: CandidateFormCaptureDetail): Blob {
  if (capture.captureRevision < 1 || capture.schemaVersion < 1)
    throw new Error('La revisión histórica ficticia es inválida.')
  const fields = (capture.fields ?? []).filter(field => !field.isCore)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.key.localeCompare(b.key))
  const allowed = new Set(fields.map(field => field.key))
  if (Object.keys(capture.values).some(key => !allowed.has(key)))
    throw new Error('La captura incluye un campo sin definición histórica.')

  const pages: string[] = []
  let content = ''
  let y = PAGE_H - 72
  function newPage() {
    pages.push(content)
    if (pages.length >= 120) throw new Error('El documento excede el máximo de páginas.')
    content = ''
    y = PAGE_H - 72
  }
  function add(text: string, fontSize: number, bold: boolean, color: string, gap: number) {
    for (const paragraph of text.replace(/\r\n?/g, '\n').split('\n')) {
      for (const line of wrap(paragraph, fontSize)) {
        if (y - fontSize - 5 < 68) newPage()
        content += color + ' rg\nBT /' + (bold ? 'F2 ' : 'F1 ') +
          fontSize + ' Tf 1 0 0 1 54 ' + y.toFixed(2) +
          ' Tm <' + hexWinAnsi(line) + '> Tj ET\n'
        y -= fontSize + 5
      }
    }
    y -= gap
  }
  add('Ficha de Insinuado', 17, true, '0.094 0.204 0.357', 22)
  add('CAMPOS ADICIONALES - DEMOSTRACIÓN PRIVADA', 10, true, '0.659 0.455 0.114', 18)
  add('Gran Logia Mixta de Chile - Proyecto Centenario', 10, true, '0.094 0.204 0.357', 18)
  add('Expediente y datos exclusivamente sintéticos. No incluye identidad canónica ni RUT.', 9,
    false, '0.300 0.360 0.430', 20)
  add('Versión del esquema: ' + capture.schemaVersion + '   Revisión: ' + capture.captureRevision,
    10, true, '0.094 0.204 0.357', 14)
  add('Capturada (UTC): ' + capture.capturedAtUtc, 9, false, '0.300 0.360 0.430', 22)
  for (const field of fields) {
    if (!Object.hasOwn(capture.values, field.key)) continue
    add(field.label, 11, true, '0.094 0.204 0.357', 6)
    add(formatValue(capture.values[field.key]), 10, false, '0.117 0.170 0.244', 11)
    add('Clave: ' + field.key + '   Tipo: ' + field.typeCode +
      '   Clasificación: ' + field.classification, 8, false, '0.320 0.360 0.420', 18)
  }
  add('DOCUMENTO RESERVADO - CUSTODIA INSTITUCIONAL', 9, true, '0.520 0.340 0.100', 0)
  pages.push(content)

  const objects: string[] = [
    '',
    '<< /Type /Catalog /Pages 2 0 R >>',
    '',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>',
  ]
  const refs: string[] = []
  pages.forEach((page, i) => {
    const pageId = objects.length
    refs.push(pageId + ' 0 R')
    const stream = page + '0.4 0.43 0.49 rg\nBT /F1 8 Tf 1 0 0 1 54 40 Tm <' +
      hexWinAnsi('Documento ficticio - Página ' + (i + 1) + ' de ' + pages.length) + '> Tj ET\n'
    objects.push('<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ' + PAGE_W + ' ' + PAGE_H +
      '] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents ' +
      (pageId + 1) + ' 0 R >>')
    objects.push('<< /Length ' + stream.length + ' >>\nstream\n' + stream + '\nendstream')
  })
  objects[2] = '<< /Type /Pages /Count ' + pages.length + ' /Kids [' + refs.join(' ') + '] >>'
  const encoder = new TextEncoder()
  const pieces: Uint8Array[] = [
    encoder.encode('%PDF-1.4\n'),
    new Uint8Array([0x25, 0xe2, 0xe3, 0xcf, 0xd3, 0x0a]),
  ]
  let size = pieces.reduce((n, part) => n + part.length, 0)
  const offsets = [0]
  function append(text: string) {
    const bytes = encoder.encode(text)
    pieces.push(bytes)
    size += bytes.length
  }
  for (let i = 1; i < objects.length; i++) {
    offsets[i] = size
    append(i + ' 0 obj\n' + objects[i] + '\nendobj\n')
  }
  const xref = size
  append('xref\n0 ' + objects.length + '\n0000000000 65535 f \n')
  for (let i = 1; i < objects.length; i++)
    append(String(offsets[i]).padStart(10, '0') + ' 00000 n \n')
  append('trailer\n<< /Size ' + objects.length + ' /Root 1 0 R >>\nstartxref\n' +
    xref + '\n%%EOF\n')
  const output = combine(pieces)
  return new Blob([output.buffer], { type: PRIVATE_PDF_MEDIA })
}
