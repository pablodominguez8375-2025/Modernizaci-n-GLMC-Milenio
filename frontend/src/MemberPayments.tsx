import { useEffect, useState } from 'react'
import InstitutionalIcon from './InstitutionalIcon'
import MemberTreasuryCredits from './MemberTreasuryCredits'
import { ActionDrawer } from './actionKit'
import type { MemberTreasuryAccount, MemberTreasuryCharge, MembershipApiClient, OwnHospitalaria } from './api/membershipApi'

/* Mis pagos para el hermano (pedido del PO 08-10-2026): Tesorería y Hospitalaria en bloques separados,
   cada uno con una frase de estado, «Lo que debo» y «Mis pagos realizados» en lenguaje simple.
   La cartola completa y el detalle de reposiciones quedan en un panel que se abre con un botón. */

type Money = 'CLP' | 'USD'
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre']
const money = (amount: number, currency: Money = 'CLP') => new Intl.NumberFormat('es-CL', { style: 'currency', currency, maximumFractionDigits: currency === 'USD' ? 2 : 0 }).format(amount)
const day = (value?: string | null) => value ? value.slice(0, 10).split('-').reverse().join('-') : 'sin fecha'
const period = (item: Pick<MemberTreasuryCharge, 'periodMonth' | 'periodYear'>) => `${MONTHS[item.periodMonth - 1] ?? item.periodMonth} ${item.periodYear}`
const OWED: MemberTreasuryCharge['periodStatus'][] = ['overdue', 'due', 'partial']
const VISIBLE = 5

export interface TreasuryReceipt { receiptNumber: string; date: string; amount: number; currency: Money; periods: string[] }

/** Agrupa las imputaciones por recibo: un pago puede cubrir varios meses. Más reciente primero. */
export function treasuryReceipts(items: MemberTreasuryCharge[]): TreasuryReceipt[] {
  const byReceipt = new Map<string, TreasuryReceipt>()
  for (const item of items) for (const payment of item.payments) {
    const currency = (payment.currency ?? item.currency ?? 'CLP') as Money
    const key = `${payment.receiptNumber}|${currency}`
    const receipt = byReceipt.get(key) ?? { receiptNumber: payment.receiptNumber, date: payment.paymentDate, amount: 0, currency, periods: [] }
    receipt.amount += payment.amount
    if (payment.paymentDate > receipt.date) receipt.date = payment.paymentDate
    const label = period(item)
    if (!receipt.periods.includes(label)) receipt.periods.push(label)
    byReceipt.set(key, receipt)
  }
  return [...byReceipt.values()].sort((a, b) => b.date.localeCompare(a.date))
}

function Headline({ ok, text, detail }: { ok: boolean; text: string; detail?: string }) {
  return <div className={`member-pay-headline${ok ? ' is-ok' : ' is-due'}`} role="status">
    <strong>{ok ? '✓ ' : ''}{text}</strong>
    {detail && <p>{detail}</p>}
  </div>
}

function ShowMore({ total, open, onToggle }: { total: number; open: boolean; onToggle: () => void }) {
  if (total <= VISIBLE) return null
  return <button type="button" className="member-pay-more" aria-expanded={open} onClick={onToggle}>{open ? 'Ver menos' : `Ver todos (${total})`}</button>
}

export function TreasuryPayments({ account, fallback }: { account?: MemberTreasuryAccount | null; fallback: { status: string; detail: string } }) {
  const [all, setAll] = useState(false)
  const items = account?.items ?? []
  const books = account?.currencies?.length ? account.currencies : account ? [{ currency: (account.currency ?? 'CLP') as Money, totalCharged: account.totalCharged ?? 0, totalPaid: account.totalPaid ?? 0, balance: account.balance ?? 0, overdueBalance: account.overdueBalance ?? 0, currentPeriodBalance: account.currentPeriodBalance ?? 0, futurePeriodBalance: account.futurePeriodBalance ?? 0, futurePaidAmount: account.futurePaidAmount ?? 0 }] : []
  const owed = items.filter(item => item.balance > 0 && OWED.includes(item.periodStatus))
  const receipts = treasuryReceipts(items)
  const credits = account?.unappliedCredits ?? []
  const owedByCurrency = books.map(book => ({ currency: book.currency as Money, amount: owed.filter(item => (item.currency ?? 'CLP') === book.currency).reduce((sum, item) => sum + item.balance, 0) })).filter(x => x.amount > 0)
  const advance = books.filter(book => book.futurePaidAmount > 0).map(book => money(book.futurePaidAmount, book.currency as Money)).join(' y ')

  return <section className="member-card member-pay-block" aria-labelledby="pay-treasury-title">
    <header className="member-pay-header">
      <span className="member-status-icon" aria-hidden="true"><InstitutionalIcon name="treasury" size={22} /></span>
      <div><h2 id="pay-treasury-title">Tesorería</h2><p>Cuotas de tu Taller y de la Gran Logia.</p></div>
    </header>
    {!account ? <Headline ok={fallback.status === 'Al día' || fallback.status === 'Activo'} text={fallback.status} detail={fallback.detail} />
      : owedByCurrency.length === 0
        ? <Headline ok text="Estás al día" detail={advance ? `Tienes ${advance} pagado por adelantado.` : undefined} />
        : <Headline ok={false} text={`Debes ${owedByCurrency.map(x => money(x.amount, x.currency)).join(' y ')}`} detail={owed.length === 1 ? `Cuota de ${period(owed[0])}.` : `${owed.length} cuotas pendientes.`} />}

    {owed.length > 0 && <div className="member-pay-list">
      <h3>Lo que debo</h3>
      <ul>{owed.map(item => <li key={item.chargeId}><span>{period(item)}</span><strong>{money(item.balance, (item.currency ?? 'CLP') as Money)}</strong></li>)}</ul>
    </div>}

    <div className="member-pay-list">
      <h3>Mis pagos realizados</h3>
      {receipts.length === 0 ? <p>Aún no hay pagos registrados.</p> : <>
        <ul>{(all ? receipts : receipts.slice(0, VISIBLE)).map(r => <li key={`${r.receiptNumber}-${r.currency}`}>
          <span><b>{day(r.date)}</b> · Recibo {r.receiptNumber}<small>Cubrió {r.periods.join(', ')}</small></span>
          <strong>{money(r.amount, r.currency)}</strong>
        </li>)}</ul>
        <ShowMore total={receipts.length} open={all} onToggle={() => setAll(v => !v)} />
      </>}
    </div>

    {credits.length > 0 && <p className="member-pay-note">Tienes {credits.length === 1 ? 'un pago recibido' : `${credits.length} pagos recibidos`} que aún no se asignan a una cuota. Puedes verlos en tu cartola completa.</p>}

    {account && <ActionDrawer label="Ver mi cartola completa" tone="secondary" title="Cartola completa de Tesorería" description="Todas tus cuotas mes a mes. La fecha de pago se muestra separada del mes que cubrió.">
      <article className="member-treasury-account">
        {books.map(book => <div className="member-institutional-summary" key={book.currency}>
          {books.length > 1 && <h3>Cuotas en {book.currency === 'USD' ? 'dólares' : 'pesos'}</h3>}
          <p><span>Total cobrado</span> <strong>{money(book.totalCharged, book.currency as Money)}</strong></p>
          <p><span>Total pagado</span> <strong>{money(book.totalPaid, book.currency as Money)}</strong></p>
          <p><span>Deuda de meses anteriores</span> <strong>{money(book.overdueBalance, book.currency as Money)}</strong></p>
          <p><span>Pagado por adelantado</span> <strong>{money(book.futurePaidAmount, book.currency as Money)}</strong></p>
          <p><span>Saldo pendiente</span> <strong>{money(book.balance, book.currency as Money)}</strong></p>
        </div>)}
        <div className="table-scroll"><table className="treasury-table">
          <thead><tr><th>Mes</th><th>Estado</th><th>Taller</th><th>Cuota</th><th>Pagado</th><th>Debe</th><th>Recibo y fecha de pago</th></tr></thead>
          <tbody>{items.map(item => <tr key={item.chargeId}>
            <td>{period(item)}</td><td>{statusLabel(item.periodStatus)}</td><td>{item.organization}</td>
            <td>{money(item.chargedAmount, (item.currency ?? 'CLP') as Money)}</td><td>{money(item.paidAmount, (item.currency ?? 'CLP') as Money)}</td><td>{money(item.balance, (item.currency ?? 'CLP') as Money)}</td>
            <td>{item.payments.length ? item.payments.map(p => <small key={p.id}>{p.receiptNumber} · {day(p.paymentDate)} · {money(p.amount, (p.currency ?? item.currency ?? 'CLP') as Money)}</small>) : <small>Sin pagos</small>}</td>
          </tr>)}</tbody>
        </table></div>
        <MemberTreasuryCredits credits={credits} />
      </article>
    </ActionDrawer>}
  </section>
}

function statusLabel(value: MemberTreasuryCharge['periodStatus']) {
  const labels: Record<string, string> = { overdue: 'Atrasada', due: 'Por pagar', partial: 'Pagada en parte', paid: 'Pagada', future_due: 'Mes futuro', advance_partial: 'Adelanto en parte', advance_paid: 'Pagada por adelantado' }
  return labels[value] ?? 'Sin estado'
}

export function HospitalariaPayments({ api, fallback }: { api: MembershipApiClient; fallback: { status: string; detail: string } }) {
  const [result, setResult] = useState<{ api: MembershipApiClient; items: OwnHospitalaria[] } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [all, setAll] = useState(false)
  useEffect(() => {
    let active = true
    setResult(null); setError(null)
    api.getOwnHospitalaria().then(r => { if (active) setResult({ api, items: r.items }) }).catch(e => { if (active) setError(e instanceof Error ? e.message : 'No fue posible consultar tus reposiciones.') })
    return () => { active = false }
  }, [api])
  const items = result?.api === api ? result.items : null
  const owed = (items ?? []).filter(x => x.saldo > 0)
  const paid = (items ?? []).flatMap(x => x.comprobantes.map(c => ({ ...c, deceased: x.hermanoFallecido }))).sort((a, b) => b.paymentDate.localeCompare(a.paymentDate))
  const totalOwed = owed.reduce((sum, x) => sum + x.saldo, 0)

  return <section className="member-card member-pay-block member-hospitalaria-payments" aria-labelledby="pay-hospitalaria-title">
    <header className="member-pay-header">
      <span className="member-status-icon" aria-hidden="true"><InstitutionalIcon name="hospitalaria" size={22} /></span>
      <div><h2 id="pay-hospitalaria-title">Hospitalaria</h2><p>Reposiciones por hermanos fallecidos: $1.500 por cada fallecimiento, que cobra la Hospitalaria de tu Taller.</p></div>
    </header>
    {error ? <p role="alert">{error}</p>
      : !items ? <Headline ok={fallback.status === 'Al día' || fallback.status === 'Activo'} text={fallback.status} detail="Consultando tus reposiciones…" />
        : totalOwed === 0 ? <Headline ok text="Estás al día" />
          : <Headline ok={false} text={`Debes ${money(totalOwed)}`} detail={owed.length === 1 ? '1 reposición pendiente.' : `${owed.length} reposiciones pendientes.`} />}

    {owed.length > 0 && <div className="member-pay-list">
      <h3>Lo que debo</h3>
      <ul>{owed.map(x => <li key={x.id}><span>Reposición por {x.hermanoFallecido}<small>Fallecimiento registrado el {day(x.fecha)}</small></span><strong>{money(x.saldo)}</strong></li>)}</ul>
    </div>}

    {items && <div className="member-pay-list">
      <h3>Mis pagos realizados</h3>
      {paid.length === 0 ? <p>Aún no hay pagos de reposiciones registrados.</p> : <>
        <ul>{(all ? paid : paid.slice(0, VISIBLE)).map(p => <li key={p.receiptNumber}><span><b>{day(p.paymentDate)}</b> · Recibo {p.receiptNumber}<small>Reposición por {p.deceased}</small></span><strong>{money(p.amount)}</strong></li>)}</ul>
        <ShowMore total={paid.length} open={all} onToggle={() => setAll(v => !v)} />
      </>}
    </div>}

    {items && items.length > 0 && <ActionDrawer label="Ver detalle y decretos" tone="secondary" title="Detalle de reposiciones" description="Cada reposición con su decreto de respaldo.">
      <ul className="member-pay-detail">{items.map(x => <li key={x.id}>
        <strong>{x.hermanoFallecido}</strong> · {x.taller}
        <p>Registrado el {day(x.fecha)} · Monto {money(x.monto)} · Pagado {money(x.pagado)} · Debe {money(x.saldo)}</p>
        <p>{x.decreto ? `Decreto ${x.decreto.numero ?? 'sin número en el registro histórico'} del ${day(x.decreto.fecha)}, vigente desde ${day(x.decreto.vigencia)}` : 'Sin decreto registrado'}</p>
      </li>)}</ul>
    </ActionDrawer>}
  </section>
}
