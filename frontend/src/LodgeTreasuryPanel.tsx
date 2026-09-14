import { useEffect, useState } from 'react'
import { type LodgeFeePlan, type LodgeTreasurySummary, type PmgmApiClient } from './api/pmgmApi'
import './lodgeTreasury.css'

const money = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 })

export default function LodgeTreasuryPanel({ api, organizationId }: { api: PmgmApiClient; organizationId: string }) {
  const now = new Date()
  const [plans, setPlans] = useState<LodgeFeePlan[]>([])
  const [summary, setSummary] = useState<LodgeTreasurySummary | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)

  useEffect(() => {
    if (!organizationId) return
    let active = true
    Promise.all([api.getLodgeFeePlans(organizationId), api.getLodgeTreasurySummary(organizationId, Number(period.slice(0, 4)), Number(period.slice(5, 7)))])
      .then(([feeResponse, report]) => { if (active) { setPlans(feeResponse.items); setSummary(report) } })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [api, organizationId, period])

  const generate = async () => {
    setBusy(true); setMessage(null); setError(null)
    try {
      const report = await api.generateLodgeCharges(organizationId, Number(period.slice(0, 4)), Number(period.slice(5, 7)))
      setSummary(report); setMessage('Cargos mensuales generados. La cartola y la obligación con Gran Tesorería quedaron calculadas.')
    } catch (reason) { setError(toMessage(reason)) } finally { setBusy(false) }
  }

  return <section className="lodge-treasury-panel">
    <div className="lodge-management-heading"><div><p className="lodge-kicker">Gestión Logial › Tesorería</p><h2>Cuotas y estado financiero del Taller</h2></div><label><span>Período</span><input type="month" value={period} onChange={event => setPeriod(event.target.value)} /></label></div>
    {message && <div className="regularity-success" role="status">{message}</div>}
    {error && <div className="regularity-error" role="alert">{error}</div>}
    <div className="lodge-fee-grid">{plans.map(plan => <article key={plan.id}><span>{feeLabel(plan.feeType)}</span><strong>{money.format(plan.memberAmount)}</strong><small>Gran Tesorería: {money.format(plan.grandTreasuryAmount)}</small><em>Disponible para el Taller: {money.format(plan.workshopAmount)}</em></article>)}</div>
    <div className="lodge-treasury-summary">
      <article><small>Por cobrar a hermanos</small><strong>{money.format(summary?.memberExpected ?? 0)}</strong></article>
      <article><small>Recaudado</small><strong>{money.format(summary?.collected ?? 0)}</strong></article>
      <article><small>Cuenta por cobrar</small><strong>{money.format(summary?.receivable ?? 0)}</strong></article>
      <article><small>Por pagar a Gran Tesorería</small><strong>{money.format(summary?.grandTreasuryExpected ?? 0)}</strong></article>
      <article><small>Margen proyectado del Taller</small><strong>{money.format(summary?.workshopMarginProjected ?? 0)}</strong></article>
    </div>
    <div className="lodge-treasury-action"><div><span className={`treasury-light ${summary?.trafficLight ?? 'no_data'}`} /><p><strong>{summary?.members ?? 0} hermanos cargados</strong><small>{summary?.paid ?? 0} pagados · {summary?.partial ?? 0} parciales · {summary?.overdue ?? 0} pendientes</small></p></div><button className="lodge-blue-button" type="button" disabled={busy || !organizationId} onClick={() => void generate()}>{busy ? 'Generando…' : 'Generar cierre mensual'}</button></div>
    <p className="lodge-treasury-note">Los montos son parametrizables por vigencia. Cada cargo conserva por separado lo cobrado al hermano y lo que corresponde pagar a Gran Tesorería.</p>
  </section>
}

function feeLabel(value: LodgeFeePlan['feeType']) { return value === 'student' ? 'Cuota estudiante' : value === 'senior' ? 'Cuota tercera edad' : 'Cuota normal' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación de Tesorería.' }
