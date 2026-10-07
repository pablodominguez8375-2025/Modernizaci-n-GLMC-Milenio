import { type FormEvent, useEffect, useState } from 'react'
import type { PmgmApiClient, TreasuryCeremonyRightItem, TreasuryCeremonyRightsResponse } from './api/pmgmApi'
import { ceremonyTypeLabel } from './ceremonyTypes'
import './ceremony-rights.css'
import { ActionDrawer } from './actionKit'
import { organizationDisplayName } from './displayFormat'

export default function CeremonyRightsPage({ api, canWrite=true }: { api: PmgmApiClient; canWrite?:boolean }) {
  const [items, setItems] = useState<TreasuryCeremonyRightItem[]>([])
  const [unpriced,setUnpriced]=useState<NonNullable<TreasuryCeremonyRightsResponse['unpriced']>>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [working, setWorking] = useState(false)
  const refresh = async () => {const response=await api.getTreasuryCeremonyRights();setItems(response.items);setUnpriced(response.unpriced??[])}

  useEffect(() => {
    let active = true
    api.getTreasuryCeremonyRights().then(response => { if (active) {setItems(response.items);setUnpriced(response.unpriced??[])} })
      .catch(reason => { if (active) setError(errorText(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  const record = async (item: TreasuryCeremonyRightItem, payload: { amount: number; paymentMethod: 'cash'|'transfer'|'deposit'; paymentDate: string; reference: string|null; idempotencyKey: string }): Promise<boolean> => {
    if(!canWrite)return false
    setWorking(true); setError(null); setMessage(null)
    try {
      const receipt = await api.recordCeremonyRightPayment(item.id, payload)
      await refresh()
      setMessage(`Pago aplicado a ${item.subjectDisplayName}. Comprobante: ${receipt.receiptNumber}.`)
      return true
    } catch (reason) { setError(errorText(reason)); return false }
    finally { setWorking(false) }
  }

  return <section className="ceremony-rights-page">
    <header className="ceremony-rights-heading"><div><p className="eyebrow">Conciliación por expediente</p><h2>Derechos de ceremonia</h2><p>Registre abonos contra el monto oficial. La regularidad mensual del Taller se valida por separado.</p></div><span className="count-badge">{items.length} con saldo</span></header>
    {error && <div className="error-banner" role="alert">{error}</div>}{message && <div className="success-banner" role="status">{message}</div>}
    {unpriced.length>0&&<div className="panel" role="status"><h3>Derechos pendientes de tarifa o Ficha</h3>{unpriced.map(x=><p key={x.id}><strong>{x.organizationName}</strong> · {x.reason}</p>)}</div>}
    {loading ? <div className="panel loading-rows"><span/><span/><span/></div> : items.length === 0 ? <div className="panel empty-state"><strong>No hay derechos con saldo calculado pendientes de conciliación.</strong></div> :
      <div className="ceremony-rights-list">{items.map(item => <CeremonyRightCard key={item.id} item={item} working={working} canWrite={canWrite} onRecord={record}/>)}</div>}
  </section>
}

function CeremonyRightCard({ item, working, canWrite, onRecord }: { item: TreasuryCeremonyRightItem; working: boolean; canWrite:boolean; onRecord: (item: TreasuryCeremonyRightItem, payload: { amount: number; paymentMethod: 'cash'|'transfer'|'deposit'; paymentDate: string; reference: string|null; idempotencyKey: string }) => Promise<boolean> }) {
  const [amount, setAmount] = useState(item.balance)
  const [paymentMethod, setPaymentMethod] = useState<'transfer'|'deposit'|'cash'>('transfer')
  const [paymentDate, setPaymentDate] = useState(todayChile())
  const [reference, setReference] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  useEffect(() => setAmount(item.balance), [item.balance])
  const renewKey = () => setIdempotencyKey(crypto.randomUUID())
  const submit = (event: FormEvent) => {
    event.preventDefault()
    void onRecord(item, { amount, paymentMethod, paymentDate, reference: reference.trim() || null, idempotencyKey })
      .then(success => { if (success) { setReference(''); renewKey() } })
  }
  return <article className="panel ceremony-right-card">
    <div className="ceremony-right-card-heading"><div><p className="eyebrow">{ceremonyTypeLabel(item.ceremonyType)}</p><h3>{item.subjectDisplayName}</h3><p>{organizationDisplayName(item.organizationName, item.organizationNumber)}</p></div><span className="status-pill blocked">Saldo pendiente</span></div>
    <dl className="ceremony-right-values"><div><dt>Derecho oficial</dt><dd>{money(item.amount, item.currency)}</dd></div><div><dt>Pagado</dt><dd>{money(item.paid, item.currency)}</dd></div><div><dt>Saldo</dt><dd>{money(item.balance, item.currency)}</dd></div><div><dt>Fecha propuesta</dt><dd>{item.proposedDate ? dateLabel(item.proposedDate) : 'Sin fecha'}</dd></div></dl>
    <p className="ceremony-right-source">{item.source}</p>
    {canWrite&&<ActionDrawer label="Registrar abono" tone="secondary" title="Registrar abono y emitir comprobante" description="Monto, medio, fecha y referencia del pago del derecho de ceremonia." confirmMessage="Se registrará el abono y se emitirá su comprobante."><form className="ceremony-right-payment-form" onSubmit={submit}>
      <label><span>Monto ({item.currency})</span><input required type="number" min={item.currency==='USD'?'0.01':'1'} step={item.currency==='USD'?'0.01':'1'} max={item.balance} value={amount || ''} onChange={event => { setAmount(Number(event.target.value)); renewKey() }}/></label>
      <label><span>Medio</span><select value={paymentMethod} onChange={event => { setPaymentMethod(event.target.value as typeof paymentMethod); renewKey() }}><option value="transfer">Transferencia</option><option value="deposit">Depósito</option><option value="cash">Efectivo</option></select></label>
      <label><span>Fecha efectiva</span><input required type="date" value={paymentDate} onChange={event => { setPaymentDate(event.target.value); renewKey() }}/></label>
      <label><span>Referencia</span><input maxLength={500} value={reference} onChange={event => { setReference(event.target.value); renewKey() }} placeholder="Transferencia o depósito"/></label>
      <button className="primary-action" disabled={working || amount <= 0 || amount > item.balance}>Registrar pago</button>
    </form></ActionDrawer>}
  </article>
}

function money(value: number, currency: string) { return new Intl.NumberFormat('es-CL', { style: 'currency', currency, maximumFractionDigits: currency==='USD'?2:0 }).format(value) }
function dateLabel(value: string) { const [year, month, day] = value.split('-').map(Number); return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(year, month - 1, day, 12))) }
function todayChile() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()) }
function errorText(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible cargar o registrar el derecho ceremonial.' }
