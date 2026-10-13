import type { CandidateFormCaptureDetail, FormValue } from './candidateFormTypes'

export const PRIVATE_DOCX_MEDIA =
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document'

/**
 * In-memory OpenXML-only DOCX for the synthetic showcase. Production export
 * must be performed by the authenticated/audited backend endpoint instead.
 * All values remain plain text; no hyperlinks, images, macros or external parts.
 */
export function renderDemoCandidateCaptureDocx(capture: CandidateFormCaptureDetail): Blob {
  const fields = (capture.fields ?? []).filter(field => !field.isCore)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.key.localeCompare(b.key))
  const known = new Set(fields.map(field => field.key))
  if (Object.keys(capture.values).some(key => !known.has(key)))
    throw new Error('La captura incluye un campo sin definición histórica.')

  const lines: { text: string; bold?: boolean }[] = [
    { text: 'GRAN LOGIA MIXTA DE CHILE', bold: true },
    { text: 'PROYECTO CENTENARIO · DEMOSTRACIÓN SINTÉTICA', bold: true },
    { text: 'Ficha de Insinuado — campos adicionales históricos', bold: true },
    { text: 'Documento ficticio sin identidad ni RUT del expediente institucional.' },
    { text: 'Versión del formulario: ' + capture.schemaVersion +
        ' · Revisión: ' + capture.captureRevision, bold: true },
    { text: 'Fecha de captura UTC: ' + capture.capturedAtUtc },
  ]
  for (const field of fields) {
    if (!Object.hasOwn(capture.values, field.key)) continue
    lines.push({ text: field.label, bold: true })
    lines.push({ text: formatValue(capture.values[field.key]) })
    lines.push({
      text: 'Clave: ' + field.key + ' · Tipo: ' + field.typeCode +
        ' · Clasificación: ' + field.classification,
    })
  }
  lines.push({ text: 'Documento reservado: mantener bajo custodia institucional.' })
  const paragraphs = lines.map(line =>
    '<w:p><w:r>' + (line.bold ? '<w:rPr><w:b/></w:rPr>' : '') +
      '<w:t xml:space="preserve">' + escapeXml(line.text) + '</w:t></w:r></w:p>',
  ).join('')
  const documentXml =
    '<?xml version="1.0" encoding="utf-8"?>' +
    '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">' +
    '<w:body>' + paragraphs + '<w:sectPr/></w:body></w:document>'
  const contentTypes =
    '<?xml version="1.0" encoding="utf-8"?>' +
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
    '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
    '<Default Extension="xml" ContentType="application/xml"/>' +
    '<Override PartName="/word/document.xml" ' +
    'ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>' +
    '</Types>'
  const relationships =
    '<?xml version="1.0" encoding="utf-8"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
    '<Relationship Id="rId1" ' +
    'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" ' +
    'Target="word/document.xml"/></Relationships>'
  const archive = zipStore([
    { name: '[Content_Types].xml', text: contentTypes },
    { name: '_rels/.rels', text: relationships },
    { name: 'word/document.xml', text: documentXml },
  ])
  return new Blob([archive], { type: PRIVATE_DOCX_MEDIA })
}

function formatValue(value: FormValue): string {
  if (value === null) return 'No informado'
  if (typeof value === 'boolean') return value ? 'Sí' : 'No'
  return Array.isArray(value) ? value.join(', ') : String(value)
}

function escapeXml(value: string): string {
  return value
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;')
}

interface Part { name: string; text: string }
function zipStore(parts: Part[]): ArrayBuffer {
  const encoder = new TextEncoder()
  const entries = parts.map(part => ({
    filename: encoder.encode(part.name),
    data: encoder.encode(part.text),
  }))
  let localBytes = 0
  let directoryBytes = 0
  for (const entry of entries) {
    localBytes += 30 + entry.filename.length + entry.data.length
    directoryBytes += 46 + entry.filename.length
  }
  const output = new Uint8Array(localBytes + directoryBytes + 22)
  const view = new DataView(output.buffer)
  const little = true
  const short = (offset: number, value: number) => view.setUint16(offset, value, little)
  const word = (offset: number, value: number) => view.setUint32(offset, value, little)
  let pos = 0
  const central: { filename: Uint8Array; data: Uint8Array; crc: number; offset: number }[] = []
  for (const entry of entries) {
    const offset = pos
    const crc = crc32(entry.data)
    word(pos, 0x04034b50); short(pos + 4, 20)
    short(pos + 6, 0); short(pos + 8, 0); short(pos + 10, 0)
    short(pos + 12, 0x2821) // 2000-01-01, deterministic ZIP metadata
    word(pos + 14, crc); word(pos + 18, entry.data.length)
    word(pos + 22, entry.data.length)
    short(pos + 26, entry.filename.length); short(pos + 28, 0)
    pos += 30
    output.set(entry.filename, pos); pos += entry.filename.length
    output.set(entry.data, pos); pos += entry.data.length
    central.push({ ...entry, crc, offset })
  }
  const directoryStart = pos
  for (const entry of central) {
    word(pos, 0x02014b50)
    short(pos + 4, 20); short(pos + 6, 20); short(pos + 8, 0)
    short(pos + 10, 0); short(pos + 12, 0); short(pos + 14, 0x2821)
    word(pos + 16, entry.crc); word(pos + 20, entry.data.length)
    word(pos + 24, entry.data.length); short(pos + 28, entry.filename.length)
    short(pos + 30, 0); short(pos + 32, 0); short(pos + 34, 0)
    short(pos + 36, 0); word(pos + 38, 0); word(pos + 42, entry.offset)
    pos += 46
    output.set(entry.filename, pos); pos += entry.filename.length
  }
  word(pos, 0x06054b50); short(pos + 4, 0); short(pos + 6, 0)
  short(pos + 8, central.length); short(pos + 10, central.length)
  word(pos + 12, pos - directoryStart); word(pos + 16, directoryStart)
  short(pos + 20, 0)
  return output.buffer
}

function crc32(bytes: Uint8Array): number {
  let crc = 0xffffffff
  for (const byte of bytes) {
    crc ^= byte
    for (let bit = 0; bit < 8; bit++)
      crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0)
  }
  return (crc ^ 0xffffffff) >>> 0
}
