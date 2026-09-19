import { useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient, type TreasuryStatement } from './api/pmgmApi'
import './treasuryStatement.css'

const money = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 })
const degreeLabel: Record<string, string> = { master: 'Maestro/a', fellowcraft: 'Compañero/a', apprentice: 'Aprendiz' }
const statusLabel: Record<string, string> = { draft: 'Borrador', submitted: 'Enviado', observed: 'Observado', reconciled: 'Conciliado', closed: 'Cerrado' }

interface Props {
  api: PmgmApiClient
  canPrepare: boolean
  canReview: boolean
}

export default function TreasuryStatementPage({ api, canPrepare, canReview }: Props) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [period, setPeriod] = useState(currentPeriodInChile())
  const [baseAmount, setBaseAmount] = useState(21000)
  const [paymentMethod, setPaymentMethod] = useState<'transfer' | 'deposit'>('transfer')
  const [paymentDate, setPaymentDate] = useState(todayInChile())
  const [paymentAmount, setPaymentAmount] = useState(0)
  const [payerDisplayName, setPayerDisplayName] = useState('Tesorería del Taller')
  const [paymentReference, setPaymentReference] = useState('')
  const [statement, setStatement] = useState<TreasuryStatement | null>(null)
  const [busy, setBusy] = useState(false)
  const [loadingStatement, setLoadingStatement] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(result => {
        if (!active) return
        const items = result.items.filter(item => item.type !== 'order')
        setOrganizations(items)
        setOrganizationId(current => current || items[0]?.id || '')
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    if (!organizationId || !period) return
    let active = true
    const [year, month] = period.split('-').map(Number)
    setLoadingStatement(true)
    setError(null)
    setMessage(null)
    api.listTreasuryStatements(organizationId, year, month)
      .then(async result => {
        const existing = result.items[0]
        if (!active) return
        if (!existing) {
          setStatement(null)
          return
        }
        const full = await api.getTreasuryStatement(existing.id)
        if (active) setStatement(full)
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoadingStatement(false) })
    return () => { active = false }
  }, [api, organizationId, period])

  const run = async (operation: () => Promise<TreasuryStatement>, success: string) => {
    setBusy(true); setError(null); setMessage(null)
    try { setStatement(await operation()); setMessage(success) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setBusy(false) }
  }

  const create = () => {
    const [periodYear, periodMonth] = period.split('-').map(Number)
    return run(
      () => api.createTreasuryStatement(organizationId, {
        periodYear,
        periodMonth,
        cutoffDate: `${period}-10`,
        sourceReference: 'Cuadro de Pago Gran Tesorería · formato institucional 2026',
      }),
      'Cuadro mensual creado en borrador para Tesorería del Taller.',
    )
  }

  const generate = () => statement && run(
    () => api.generateTreasuryStatementLines(statement.id, {
      apprenticeAmount: baseAmount,
      fellowcraftAmount: baseAmount,
      masterAmount: baseAmount,
    }),
    'Nómina generada desde el Cuadro vigente del Taller y sus ajustes autorizados.',
  )

  const registerPayment = () => {
    if (!statement) return
    const amount = paymentAmount
    void run(
      () => api.addTreasuryStatementPayment(statement.id, {
        paymentMethod,
        paymentDate,
        amount,
        payerDisplayName: payerDisplayName.trim() || null,
        reference: paymentReference.trim() || null,
      }),
      'Pago registrado y diferencia recalculada.',
    ).then(() => {
      setPaymentAmount(0)
      setPaymentReference('')
    })
  }

  const submit = () => statement && run(
    () => api.submitTreasuryStatement(statement.id),
    'Cuadro cuadrado y enviado a Gran Tesorería para revisión institucional.',
  )

  const reconcile = () => statement && run(
    () => api.reconcileTreasuryStatement(statement.id),
    'Gran Tesorería concilió el Cuadro. La regularidad financiera institucional quedó actualizada.',
  )

  const canSubmit = !!statement &&
    statement.status === 'draft' &&
    statement.lines.length > 0 &&
    statement.differenceAmount === 0 &&
    statement.unresolvedIdentities === 0

  const roleCaption = canReview
    ? 'Gran Tesorería · revisión institucional'
    : 'Tesorería del Taller · Cuadro mensual'

  return <>
    <section className="page-heading">
      <div>
        <p className="eyebrow">{roleCaption}</p>
        <h1>Cuadro mensual y conciliación</h1>
        <p>{canReview
          ? 'Revisa los Cuadros enviados por los Talleres y concilia sólo cuando pagos, nómina e identidades estén cuadrados.'
          : 'Prepara el Cuadro completo, registra transferencias o depósitos y envíalo cuadrado a Gran Tesorería.'}</p>
      </div>
      {statement && <span className={`treasury-state ${statement.status}`}>{statusLabel[statement.status] ?? statement.status}</span>}
    </section>

    {message && <div className="regularity-success" role="status">{message}</div>}
    {error && <div className="error-banner" role="alert">{error}</div>}

    <section className="panel treasury-toolbar">
      <label className="regularity-field"><span>Taller</span><select value={organizationId} disabled={busy || loadingStatement} onChange={event => setOrganizationId(event.target.value)}>{organizations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <label className="regularity-field"><span>Período</span><input type="month" value={period} disabled={busy || loadingStatement} onChange={event => setPeriod(event.target.value)} /></label>
      {canPrepare && !statement && <button className="regularity-primary" type="button" disabled={!organizationId || !period || busy || loadingStatement} onClick={create}>Crear Cuadro</button>}
      {canPrepare && statement?.status === 'draft' && statement.lines.length === 0 && <>
        <label className="regularity-field"><span>Cuota base Gran Tesorería</span><input type="number" min="0" step="1000" value={baseAmount} disabled={busy} onChange={event => setBaseAmount(Number(event.target.value))} /></label>
        <button className="regularity-primary" type="button" disabled={busy || baseAmount < 0} onClick={generate}>Generar nómina</button>
      </>}
      {canPrepare && statement?.status === 'draft' && statement.lines.length > 0 && <button className="regularity-primary" type="button" disabled={busy || !canSubmit} onClick={submit}>Enviar a Gran Tesorería</button>}
      {canReview && (statement?.status === 'submitted' || statement?.status === 'observed') && <button className="regularity-primary" type="button" disabled={busy || statement.differenceAmount !== 0 || statement.unresolvedIdentities !== 0} onClick={reconcile}>Conciliar institucionalmente</button>}
    </section>

    {loadingStatement && <section className="panel treasury-empty"><strong>Cargando Cuadro del período…</strong></section>}

    {!loadingStatement && !statement
      ? <section className="panel treasury-empty"><strong>{canPrepare ? 'No existe un Cuadro para este período.' : 'Gran Tesorería aún no ha recibido un Cuadro para este Taller y período.'}</strong><p>La demo utiliza datos ficticios y conserva la estructura oficial: Cuadro completo, rebajas respaldadas por Plancha, transferencias/depósitos y Diferencia.</p></section>
      : statement && <>
        <section className="treasury-kpis">
          <article><span>Total esperado</span><strong>{money.format(statement.expectedAmount)}</strong></article>
          <article><span>Transferencias</span><strong>{money.format(statement.transferAmount)}</strong></article>
          <article><span>Depósitos</span><strong>{money.format(statement.depositAmount)}</strong></article>
          <article className={statement.differenceAmount === 0 ? 'balanced' : 'difference'}><span>Diferencia</span><strong>{money.format(statement.differenceAmount)}</strong></article>
        </section>

        <section className="panel treasury-payments">
          <div><p className="eyebrow">Control previo al envío</p><h2>Cuadre obligatorio</h2></div>
          <div className="payment-row"><span>Total pagado vs. Cuadro</span><strong>{statement.differenceAmount === 0 ? '✅ Cuadrado' : '❌ Diferencia pendiente'}</strong><small>{money.format(statement.differenceAmount)}</small></div>
          <div className="payment-row"><span>Identidades del Cuadro</span><strong>{statement.unresolvedIdentities === 0 ? '✅ Conciliadas' : '❌ Pendientes'}</strong><small>{statement.unresolvedIdentities} sin conciliar</small></div>
          {statement.status === 'draft' && canPrepare && !canSubmit && <p>El sistema bloqueará el envío hasta que la Diferencia sea 0 y todas las identidades estén conciliadas.</p>}
          {statement.status === 'submitted' && <p>Cuadro enviado por Tesorería del Taller. La conciliación institucional corresponde a Gran Tesorería.</p>}
        </section>

        <section className="panel treasury-table-panel">
          <div className="section-title"><div><p className="eyebrow">Cuadro del Taller al día 10</p><h2>{statement.periodMonth.toString().padStart(2, '0')}/{statement.periodYear}</h2></div><span>{statement.lines.length} integrantes</span></div>
          {statement.lines.length === 0
            ? <div className="empty-state">La nómina aún no ha sido generada.</div>
            : <div className="table-scroll"><table className="treasury-table"><thead><tr><th>Nº</th><th>Grado</th><th>Cargo</th><th>Integrante</th><th>Cuota</th><th>Ajuste</th><th>Total</th><th>Respaldo</th></tr></thead><tbody>{statement.lines.map((line, index) => <tr key={line.id}><td>{index + 1}</td><td>{degreeLabel[line.degreeCodeAtCutoff] ?? line.degreeCodeAtCutoff}</td><td>{line.officeCodeAtCutoff ?? '—'}</td><td>{line.observation ?? `Integrante ${index + 1}`}</td><td>{money.format(line.baseAmount)}</td><td>{money.format(line.adjustmentAmount)}</td><td><strong>{money.format(line.payableAmount)}</strong></td><td>{line.authorizationReference ?? '—'}</td></tr>)}</tbody></table></div>}
        </section>

        <section className="panel treasury-payments">
          <div><p className="eyebrow">Forma de pago</p><h2>Transferencias y depósitos</h2></div>
          {canPrepare && statement.status === 'draft' && <div className="treasury-toolbar">
            <label className="regularity-field"><span>Medio</span><select value={paymentMethod} disabled={busy} onChange={event => setPaymentMethod(event.target.value as 'transfer' | 'deposit')}><option value="transfer">Transferencia</option><option value="deposit">Depósito</option></select></label>
            <label className="regularity-field"><span>Fecha</span><input type="date" value={paymentDate} disabled={busy} onChange={event => setPaymentDate(event.target.value)} /></label>
            <label className="regularity-field"><span>Monto</span><input type="number" min="1" step="1" value={paymentAmount || ''} disabled={busy} onChange={event => setPaymentAmount(Number(event.target.value))} /></label>
            <label className="regularity-field"><span>Pagador</span><input value={payerDisplayName} disabled={busy} onChange={event => setPayerDisplayName(event.target.value)} /></label>
            <label className="regularity-field"><span>Referencia / comprobante</span><input value={paymentReference} disabled={busy} onChange={event => setPaymentReference(event.target.value)} placeholder="Ej.: TRX-2026-000123" /></label>
            <button className="regularity-secondary" type="button" disabled={busy || paymentAmount <= 0 || !paymentReference.trim()} onClick={registerPayment}>Registrar pago</button>
          </div>}
          {statement.payments.length === 0
            ? <p>No hay pagos registrados.</p>
            : statement.payments.map(payment => <div className="payment-row" key={payment.id}><span>{payment.paymentMethod === 'transfer' ? 'Transferencia' : 'Depósito'} · {payment.paymentDate}</span><strong>{money.format(payment.amount)}</strong><small>{payment.reference || 'Sin referencia'}</small></div>)}
        </section>
      </>}
  </>
}

function todayInChile() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
}

function currentPeriodInChile() {
  return todayInChile().slice(0, 7)
}

function toMessage(reason: unknown) {
  return reason instanceof Error ? reason.message : 'No fue posible completar la operación.'
}
