import { useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient, type TreasuryStatement } from './api/pmgmApi'
import './treasuryStatement.css'

const money = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 })
const degreeLabel: Record<string, string> = { master: 'Maestro/a', fellowcraft: 'Compañero/a', apprentice: 'Aprendiz' }
const statusLabel: Record<string, string> = { draft: 'Borrador', submitted: 'Enviado', observed: 'Observado', reconciled: 'Conciliado', closed: 'Cerrado' }

export default function TreasuryStatementPage({ api }: { api: PmgmApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [statement, setStatement] = useState<TreasuryStatement | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => { api.getOrganizationOptions().then(result => { const items = result.items.filter(item => item.type !== 'order'); setOrganizations(items); setOrganizationId(items[0]?.id ?? '') }).catch(reason => setError(toMessage(reason))) }, [api])
  const run = async (operation: () => Promise<TreasuryStatement>, success: string) => { setBusy(true); setError(null); setMessage(null); try { setStatement(await operation()); setMessage(success) } catch (reason) { setError(toMessage(reason)) } finally { setBusy(false) } }
  const create = () => run(async () => { const now = new Date(); return api.createTreasuryStatement(organizationId, { periodYear: now.getFullYear(), periodMonth: now.getMonth() + 1, cutoffDate: todayInChile(), sourceReference: 'Cuadro institucional 2026' }) }, 'Cuadro mensual creado en borrador.')
  const generate = () => statement && run(() => api.generateTreasuryStatementLines(statement.id, { apprenticeAmount: 21000, fellowcraftAmount: 21000, masterAmount: 21000 }), 'Nómina generada desde las pertenencias vigentes.')
  const payDifference = () => statement && run(() => api.addTreasuryStatementPayment(statement.id, { paymentMethod: 'transfer', paymentDate: todayInChile(), amount: statement.differenceAmount, payerDisplayName: 'Tesorería del Taller', reference: 'Transferencia demostrativa' }), 'Transferencia registrada y totales recalculados.')
  const submit = () => statement && run(() => api.submitTreasuryStatement(statement.id), 'Cuadro enviado a Gran Tesorería.')
  const reconcile = () => statement && run(() => api.reconcileTreasuryStatement(statement.id), 'Cuadro conciliado. La regularidad financiera quedó actualizada.')

  return <>
    <section className="page-heading"><div><p className="eyebrow">Gran Tesorería · cuadro institucional 2026</p><h1>Nómina mensual y conciliación</h1><p>Genera el cuadro desde las membresías vigentes, aplica rebajas autorizadas y concilia transferencias o depósitos.</p></div>{statement && <span className={`treasury-state ${statement.status}`}>{statusLabel[statement.status] ?? statement.status}</span>}</section>
    {message && <div className="regularity-success" role="status">{message}</div>}
    {error && <div className="error-banner" role="alert">{error}</div>}
    <section className="panel treasury-toolbar">
      <label className="regularity-field"><span>Taller</span><select value={organizationId} disabled={!!statement || busy} onChange={event => setOrganizationId(event.target.value)}>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      {!statement && <button className="regularity-primary" type="button" disabled={!organizationId || busy} onClick={create}>Crear cuadro del mes</button>}
      {statement?.status === 'draft' && statement.lines.length === 0 && <button className="regularity-primary" type="button" disabled={busy} onClick={generate}>Generar nómina</button>}
      {statement?.status === 'draft' && statement.lines.length > 0 && statement.differenceAmount > 0 && <button className="regularity-secondary" type="button" disabled={busy} onClick={payDifference}>Registrar transferencia por {money.format(statement.differenceAmount)}</button>}
      {statement?.status === 'draft' && statement.lines.length > 0 && <button className="regularity-primary" type="button" disabled={busy} onClick={submit}>Enviar a Gran Tesorería</button>}
      {statement?.status === 'submitted' && <button className="regularity-primary" type="button" disabled={busy || statement.differenceAmount !== 0} onClick={reconcile}>Conciliar cuadro</button>}
    </section>
    {!statement ? <section className="panel treasury-empty"><strong>Comienza creando el cuadro del período.</strong><p>La demostración usa valores ficticios y reproduce la estructura del archivo oficial: Maestros, Compañeros, Aprendices, rebajas, transferencias, depósitos y diferencia.</p></section> : <>
      <section className="treasury-kpis"><article><span>Total esperado</span><strong>{money.format(statement.expectedAmount)}</strong></article><article><span>Transferencias</span><strong>{money.format(statement.transferAmount)}</strong></article><article><span>Depósitos</span><strong>{money.format(statement.depositAmount)}</strong></article><article className={statement.differenceAmount === 0 ? 'balanced' : 'difference'}><span>Diferencia</span><strong>{money.format(statement.differenceAmount)}</strong></article></section>
      <section className="panel treasury-table-panel"><div className="section-title"><div><p className="eyebrow">Cuadro del Taller</p><h2>{statement.periodMonth.toString().padStart(2, '0')}/{statement.periodYear}</h2></div><span>{statement.lines.length} integrantes</span></div>
        {statement.lines.length === 0 ? <div className="empty-state">La nómina aún no ha sido generada.</div> : <div className="table-scroll"><table className="treasury-table"><thead><tr><th>Nº</th><th>Grado</th><th>Cargo</th><th>Integrante</th><th>Cuota</th><th>Ajuste</th><th>Total</th><th>Respaldo</th></tr></thead><tbody>{statement.lines.map((line, index) => <tr key={line.id}><td>{index + 1}</td><td>{degreeLabel[line.degreeCodeAtCutoff] ?? line.degreeCodeAtCutoff}</td><td>{line.officeCodeAtCutoff ?? '—'}</td><td>{line.observation ?? `Integrante ${index + 1}`}</td><td>{money.format(line.baseAmount)}</td><td>{money.format(line.adjustmentAmount)}</td><td><strong>{money.format(line.payableAmount)}</strong></td><td>{line.authorizationReference ?? '—'}</td></tr>)}</tbody></table></div>}
      </section>
      <section className="panel treasury-payments"><div><p className="eyebrow">Forma de pago</p><h2>Conciliación</h2></div>{statement.payments.length === 0 ? <p>No hay pagos registrados.</p> : statement.payments.map(payment => <div className="payment-row" key={payment.id}><span>{payment.paymentMethod === 'transfer' ? 'Transferencia' : 'Depósito'} · {payment.paymentDate}</span><strong>{money.format(payment.amount)}</strong><small>{payment.reference}</small></div>)}</section>
    </>}
  </>
}

function todayInChile() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
