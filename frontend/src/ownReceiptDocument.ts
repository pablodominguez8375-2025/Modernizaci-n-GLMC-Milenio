import type { OwnReceiptDocument } from './api/membershipApi'

const escapeHtml = (value: unknown) => String(value ?? '').replace(/[&<>"']/g, char => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
})[char] ?? char)
const money = (amount: number, currency: string) =>
  new Intl.NumberFormat('es-CL', { style: 'currency', currency, maximumFractionDigits: currency === 'USD' ? 2 : 0 }).format(amount)
const date = (value: string) => value.slice(0, 10).split('-').reverse().join('/')
const month = (year?: number | null, month?: number | null) => year && month ? ` · Período ${String(month).padStart(2, '0')}/${year}` : ''

/** Comprobante con datos originales de recepción, no con montos reconstruidos desde cuotas. */
export function ownReceiptHtml(item: OwnReceiptDocument): string {
  const title = item.kind === 'hospitalaria' ? 'Comprobante de Hospitalaria' : 'Comprobante de Tesorería'
  const lineRows = item.lines.map(line => `<tr><td>${escapeHtml(line.description + month(line.periodYear, line.periodMonth))}</td><td class="amount">${escapeHtml(money(line.amount, item.currency))}</td></tr>`).join('')
  const adjustments = item.corrections.map(change => `<tr><td>${escapeHtml(change.kind === 'void' ? 'Anulación' : 'Corrección')} · ${escapeHtml(date(change.date))}</td><td class="amount">${escapeHtml(money(change.cashAmount, item.currency))}</td></tr>`).join('')
  return `<!doctype html>
<html lang="es-CL"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
<title>${escapeHtml(title)} · ${escapeHtml(item.receiptNumber)}</title>
<style>body{font:16px system-ui,Arial,sans-serif;max-width:760px;margin:3rem auto;padding:0 1.25rem;color:#16243e}h1{color:#173764}header{border-bottom:2px solid #bfa15a;padding-bottom:1rem}table{border-collapse:collapse;width:100%;margin-top:1.2rem}th,td{border-bottom:1px solid #d5d9e1;padding:.7rem;text-align:left}th{background:#f3f5fa}.amount{text-align:right;white-space:nowrap}.note{margin-top:1.6rem;color:#38455b}strong{font-weight:700}@media print{body{max-width:none;margin:0}button{display:none}}</style>
</head><body><header><p>Proyecto Centenario · Comprobante personal</p><h1>${escapeHtml(title)}</h1>
<p><strong>Recibo:</strong> ${escapeHtml(item.receiptNumber)}</p><p><strong>Taller:</strong> ${escapeHtml(item.organization)}</p>
<p><strong>Fecha efectiva de recepción:</strong> ${escapeHtml(date(item.paymentDate))}</p>
<p><strong>Medio de pago:</strong> ${escapeHtml(item.paymentMethod)}</p>
<p><strong>Importe originalmente recibido:</strong> ${escapeHtml(money(item.receivedAmount, item.currency))}</p></header>
<h2>Aplicaciones del importe</h2><table><thead><tr><th>Concepto</th><th class="amount">Monto aplicado</th></tr></thead>
<tbody>${lineRows || '<tr><td colspan="2">Sin imputaciones registradas.</td></tr>'}</tbody></table>
${adjustments ? `<h2>Ajustes posteriores</h2><table><thead><tr><th>Movimiento</th><th class="amount">Efecto en caja</th></tr></thead><tbody>${adjustments}</tbody></table>` : ''}
<p><strong>Saldo sin imputar:</strong> ${escapeHtml(money(item.unallocatedAmount, item.currency))}</p>
<p class="note">La fecha de recepción y el período de cuota son controles diferentes. Las correcciones y anulaciones se informan por separado y no reescriben el importe original. Este archivo es una copia descargada de un registro institucional; no es certificación bancaria.</p>
</body></html>`
}

export function downloadOwnReceiptDocument(item: OwnReceiptDocument): void {
  const html = ownReceiptHtml(item)
  const blobUrl = URL.createObjectURL(new Blob([html], { type: 'text/html;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = blobUrl
  link.download = `comprobante-${item.kind}-${item.receiptNumber.replace(/[^A-Za-z0-9_-]/g, '-')}.html`
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(blobUrl)
}
