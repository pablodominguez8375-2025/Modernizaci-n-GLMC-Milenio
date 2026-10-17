import type { OwnReceiptDocument } from './membershipApi'

/** Comprobante personal en PDF (A4, tipografía grande, sin librerías). */
const PAGE_W = 595.28
const PAGE_H = 841.89
const MARGIN = 48
const special: Record<number, number> = {
  0x20ac: 0x80, 0x2018: 0x91, 0x2019: 0x92, 0x201c: 0x93, 0x201d: 0x94,
  0x2022: 0x95, 0x2013: 0x96, 0x2014: 0x97, 0x2026: 0x85,
}
const BLUE = '0.094 0.204 0.357'
const GOLD = '0.659 0.455 0.114'

function clean(text: string): string {
  return Array.from(text.replace(/ | /g, ' ')).map(char => {
    const code = char.codePointAt(0) ?? 0
    return (code >= 32 && code <= 126) || (code >= 160 && code <= 255) || Object.hasOwn(special, code) ? char : '?'
  }).join('')
}
const hex = (text: string) => Array.from(clean(text)).map(char => {
  const code = char.codePointAt(0) ?? 63
  return (special[code] ?? code).toString(16).padStart(2, '0').toUpperCase()
}).join('')
const width = (text: string, size: number, bold: boolean) => clean(text).length * size * (bold ? 0.58 : 0.54)

const money = (amount: number, currency: string) =>
  new Intl.NumberFormat('es-CL', { style: 'currency', currency, maximumFractionDigits: currency === 'USD' ? 2 : 0 }).format(amount)
const dateText = (value: string) => value.slice(0, 10).split('-').reverse().join('/')
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre']
const periodText = (year?: number | null, month?: number | null) =>
  year && month && month >= 1 && month <= 12 ? ` (${MONTHS[month - 1]} ${year})` : ''
const methodText = (method: string) => ({ transferencia: 'Transferencia', efectivo: 'Efectivo', cheque: 'Cheque', tarjeta: 'Tarjeta' } as Record<string, string>)[method] ?? method

function wrapText(text: string, size: number, maxWidth: number, bold = false): string[] {
  const out: string[] = []
  let line = ''
  for (const word of clean(text).split(' ')) {
    const next = line ? `${line} ${word}` : word
    if (line && width(next, size, bold) > maxWidth) { out.push(line); line = word } else line = next
  }
  if (line) out.push(line)
  return out.length ? out : ['']
}

/** Frase única que resume el pago, antes del detalle. */
export function ownReceiptSummary(item: OwnReceiptDocument): string {
  const what = item.kind === 'hospitalaria' ? 'a la Gran Hospitalaria' : 'a Tesorería'
  return `Se recibió ${money(item.receivedAmount, item.currency)} ${what} del ${item.organization}, el ${dateText(item.paymentDate)}.`
}

export function renderOwnReceiptPdf(item: OwnReceiptDocument): Blob {
  const pages: string[] = []
  let ops = ''
  let y = PAGE_H - MARGIN
  const text = (value: string, x: number, size: number, bold = false, color = '0.086 0.141 0.243') => {
    ops += `BT /${bold ? 'F2' : 'F1'} ${size} Tf ${color} rg ${x.toFixed(2)} ${y.toFixed(2)} Td <${hex(value)}> Tj ET\n`
  }
  const rule = (color: string, thickness: number) => {
    ops += `${color} RG ${thickness} w ${MARGIN} ${y.toFixed(2)} m ${PAGE_W - MARGIN} ${y.toFixed(2)} l S\n`
  }
  const newPage = () => { pages.push(ops); ops = ''; y = PAGE_H - MARGIN }
  const need = (space: number) => { if (y - space < MARGIN + 40) newPage() }
  const right = (value: string, size: number, bold = false) => text(value, PAGE_W - MARGIN - width(value, size, bold), size, bold)
  const wrapped = (value: string, size: number, bold = false, gap = 6) => {
    for (const row of wrapText(value, size, PAGE_W - 2 * MARGIN, bold)) { need(size + gap); text(row, MARGIN, size, bold); y -= size + gap }
  }

  const title = item.kind === 'hospitalaria' ? 'Comprobante de Hospitalaria' : 'Comprobante de Tesorería'
  text('GRAN LOGIA MIXTA DE CHILE', MARGIN, 11, true, GOLD); y -= 28
  text(title, MARGIN, 26, true, BLUE); y -= 12
  rule(GOLD, 2); y -= 30

  wrapped(ownReceiptSummary(item), 15, true, 8); y -= 8
  const facts: Array<[string, string]> = [
    ['Recibo N°', item.receiptNumber], ['Taller', item.organization],
    ['Fecha de recepción', dateText(item.paymentDate)], ['Medio de pago', methodText(item.paymentMethod)],
    ['Importe recibido', money(item.receivedAmount, item.currency)],
  ]
  for (const [label, value] of facts) { need(24); text(`${label}:`, MARGIN, 14, true, BLUE); text(value, MARGIN + 150, 14); y -= 24 }
  y -= 10

  need(60); text('En qué se aplicó', MARGIN, 16, true, BLUE); y -= 10; rule(BLUE, 1); y -= 22
  if (!item.lines.length) { text('Sin imputaciones registradas.', MARGIN, 14); y -= 22 }
  for (const line of item.lines) {
    const label = wrapText(line.description + periodText(line.periodYear, line.periodMonth), 14, PAGE_W - 2 * MARGIN - 130)
    need(label.length * 20 + 6)
    const amount = money(line.amount, item.currency)
    right(amount, 14, true)
    for (const row of label) { text(row, MARGIN, 14); y -= 20 }
    y -= 4
  }
  if (item.unallocatedAmount > 0) {
    need(30); text('Saldo a favor sin aplicar', MARGIN, 14, true, BLUE); right(money(item.unallocatedAmount, item.currency), 14, true); y -= 26
  }
  if (item.corrections.length) {
    y -= 8; need(60); text('Ajustes posteriores', MARGIN, 16, true, BLUE); y -= 10; rule(BLUE, 1); y -= 22
    for (const change of item.corrections) {
      need(24)
      text(`${change.kind === 'void' ? 'Anulación' : 'Corrección'} del ${dateText(change.date)}`, MARGIN, 14)
      right(money(change.cashAmount, item.currency), 14, true); y -= 22
    }
  }
  y -= 14
  const note = 'Este documento es una copia de un registro institucional y no reemplaza una certificación bancaria. Las correcciones y anulaciones se informan aparte y no modifican el importe original recibido.'
  for (const row of wrapText(note, 11, PAGE_W - 2 * MARGIN)) { need(16); text(row, MARGIN, 11, false, '0.22 0.27 0.36'); y -= 16 }
  pages.push(ops)

  // Ensamblado: 1 catálogo, 2 páginas, 3-4 fuentes, luego (página, contenido) por página.
  const objects: string[] = []
  const pageIds = pages.map((_, i) => 5 + i * 2)
  objects[1] = '<< /Type /Catalog /Pages 2 0 R >>'
  objects[2] = `<< /Type /Pages /Kids [${pageIds.map(id => `${id} 0 R`).join(' ')}] /Count ${pages.length} >>`
  objects[3] = '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>'
  objects[4] = '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>'
  pages.forEach((content, i) => {
    const footer = `BT /F1 10 Tf 0.4 0.4 0.4 rg ${MARGIN} 28 Td <${hex(`Página ${i + 1} de ${pages.length} · ${item.receiptNumber}`)}> Tj ET\n`
    const stream = content + footer
    objects[5 + i * 2] = `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_W} ${PAGE_H}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents ${6 + i * 2} 0 R >>`
    objects[6 + i * 2] = `<< /Length ${stream.length} >>\nstream\n${stream}endstream`
  })
  let pdf = '%PDF-1.4\n'
  const offsets: number[] = []
  for (let id = 1; id < objects.length; id++) { offsets[id] = pdf.length; pdf += `${id} 0 obj\n${objects[id]}\nendobj\n` }
  const xref = pdf.length
  pdf += `xref\n0 ${objects.length}\n0000000000 65535 f \n`
  for (let id = 1; id < objects.length; id++) pdf += `${String(offsets[id]).padStart(10, '0')} 00000 n \n`
  pdf += `trailer\n<< /Size ${objects.length} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`
  const bytes = new Uint8Array(pdf.length)
  for (let i = 0; i < pdf.length; i++) bytes[i] = pdf.charCodeAt(i) & 0xff
  return new Blob([bytes], { type: 'application/pdf' })
}
