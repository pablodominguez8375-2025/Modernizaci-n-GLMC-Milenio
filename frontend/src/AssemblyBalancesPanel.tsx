import { type FormEvent, useCallback, useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AssemblyBalanceSource, AssemblyBalancesOverviewItem } from './api/assemblyApi'
import { ActionDrawer } from './actionKit'

const money = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 })

/** Saldos de cada Taller para el Padrón de la Gran Asamblea. Solo los valida Gran Tesorería (o Gran Hospitalaria). */
export default function AssemblyBalancesPanel({ api, canWrite, source }: { api: PmgmApiClient; canWrite: boolean; source: AssemblyBalanceSource }) {
  const [items, setItems] = useState<AssemblyBalancesOverviewItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const load = useCallback(async () => {
    try { setItems(await api.getAssemblyBalances(source)); setError(null) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible cargar los saldos del Padrón.') }
  }, [api, source])
  useEffect(() => { void load() }, [load])

  async function save(event: FormEvent<HTMLFormElement>, assembly: AssemblyBalancesOverviewItem) {
    event.preventDefault()
    const d = new FormData(event.currentTarget)
    setError(null); setMessage(null)
    try {
      const rows = assembly.workshops.map(w => ({ organizationId: w.organizationId, amountPending: Number(String(d.get('saldo-' + w.organizationId) ?? '0').replace(/\./g, '').replace(',', '.') || '0') }))
      await api.recordAssemblyBalances(assembly.id, { source, sourceReference: String(d.get('oficio') ?? ''), currency: String(d.get('currency') ?? 'CLP'), items: rows })
      setMessage('Saldos validados y registrados, con su referencia, en la auditoría. Régimen Interior ya puede evaluar el Padrón.')
      await load()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible registrar los saldos.') }
  }

  return <section className="panel assembly-balances">
    <p className="eyebrow">Padrón de la Gran Asamblea</p>
    <h2>Saldos de los Talleres</h2>
    <p>{source === 'gran_hospitalaria'
      ? 'Anote lo que cada Taller adeuda por reposiciones de hermanos fallecidos y por su cuota anual de Hospitalaria. '
      : 'Anote lo que cada Taller adeuda a Gran Tesorería por cuotas y demás pagos. '}
      Cualquier saldo mayor que cero bloquea el sufragio de sus asambleístas; solo Régimen Interior puede levantar ese bloqueo. Los pagos solo los valida su responsable, y el Padrón necesita el saldo de ambos.</p>
    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}
    {items === null && !error && <p role="status">Cargando…</p>}
    {items?.length === 0 && <p role="status">No hay una Asamblea en preparación con asambleístas acreditados.</p>}
    {items?.map(a => <article key={a.id} className="assembly-balance-card">
      <h3>{a.name}</h3>
      <p>Corte {a.cutoffDate} · Asamblea {a.meetingDate}</p>
      <ul className="assembly-balance-summary">{a.workshops.map(w => <li key={w.organizationId}><strong>{w.name}</strong> <span>{w.amountPending === null ? 'Sin saldo registrado' : w.amountPending > 0 ? `Debe ${money.format(w.amountPending)}` : 'Sin deuda'}</span></li>)}</ul>
      {canWrite && <ActionDrawer label="Registrar saldos" title="Saldos al corte" description={source === 'gran_hospitalaria' ? 'Deuda de cada Taller por reposiciones de hermanos fallecidos y cuota anual de Hospitalaria.' : 'Saldo pendiente de cada Taller con Gran Tesorería.'} wide>
        <form className="assembly-form" onSubmit={e => save(e, a)}>
          <label className="field"><span>Referencia del oficio o registro</span><input name="oficio" required maxLength={500} placeholder="OFICIO-GT-2026-045" /></label>
          <label className="field"><span>Moneda</span><input name="currency" required maxLength={3} defaultValue="CLP" /></label>
          <div className="assembly-balance-list">{a.workshops.map(w => <label key={w.organizationId} className="field"><span>{w.name}</span><input name={`saldo-${w.organizationId}`} inputMode="decimal" defaultValue={w.amountPending ?? 0} aria-label={`Saldo pendiente de ${w.name}`} /></label>)}</div>
          <button type="submit" className="action-trigger primary">Registrar saldos</button>
        </form>
      </ActionDrawer>}
    </article>)}
  </section>
}
