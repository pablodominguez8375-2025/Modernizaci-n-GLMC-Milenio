import { useEffect, useState } from 'react'
import { PmgmApiHttpError, type OrganizationOption, type PmgmApiClient, type SupportLodgeProbeResponse } from './api/pmgmApi'
import { lodgeProbeErrorMessage, summarizeLodgeProbe } from './lodgeDataHealth'

/** #688 — «Verificar lecturas del Taller»: repite en solo lectura las lecturas de Talleres (supervisión) para el Taller elegido. */
export default function LodgeQueryProbeCard({ api }: { api: PmgmApiClient }) {
  const [workshops, setWorkshops] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [result, setResult] = useState<SupportLodgeProbeResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    let alive = true
    api.getOrganizationOptions().then(response => { if (alive) setWorkshops(response.items.filter(x => x.type === 'workshop')) }).catch(() => undefined)
    return () => { alive = false }
  }, [api])

  async function verify() {
    if (!organizationId) return
    setLoading(true); setError(null)
    try { setResult(await api.getSupportLodgeQueryProbe(organizationId)) }
    catch (reason) { setResult(null); setError(lodgeProbeErrorMessage(reason instanceof PmgmApiHttpError ? reason.status : undefined)) }
    finally { setLoading(false) }
  }

  const lines = result ? summarizeLodgeProbe(result.items) : []
  return <section className="lodge-health" aria-labelledby="lodge-probe-title">
    <header>
      <div>
        <p className="eyebrow">Soporte técnico · solo lectura</p>
        <h2 id="lodge-probe-title">Verificar lecturas del Taller</h2>
        <p>Elige un Taller y comprueba si el servidor logra leer sus tenidas, Hermanos e instrucciones. No muestra ni modifica datos; solo informa si cada lectura respondió.</p>
      </div>
    </header>
    <div className="lodge-probe-form">
      <label><span>Taller</span>
        <select value={organizationId} disabled={loading} onChange={event => { setOrganizationId(event.target.value); setResult(null); setError(null) }}>
          <option value="">Seleccione un Taller</option>
          {workshops.map(w => <option key={w.id} value={w.id}>{w.number ? `N.º ${w.number} · ` : ''}{w.name}</option>)}
        </select>
      </label>
      <button type="button" className="action-trigger primary" disabled={loading || !organizationId} onClick={() => void verify()}>{loading ? 'Verificando…' : 'Verificar lecturas del Taller'}</button>
    </div>
    {error && <p role="alert" className="error-banner">{error}</p>}
    {result && <>
      <ul className="lodge-health-list" aria-live="polite">
        {lines.map(line => <li key={line.resource} className={line.ok ? 'ok' : 'attention'}>
          <span className="lodge-health-dot" aria-hidden="true" />
          <div><strong>{line.label}</strong><small>{line.text}</small></div>
        </li>)}
      </ul>
      <p role="note" className="lodge-health-note">
        {result.status === 'ready' ? 'Las tres lecturas respondieron. ' : 'Alguna lectura falló. '}
        Esto no demuestra que las pantallas con sesión de usuario respondan bien ni resuelve por sí solo el error 500 de Talleres (supervisión).
      </p>
    </>}
    {!result && !error && !loading && <p className="lodge-health-note">Seleccione un Taller y pulse «Verificar lecturas del Taller».</p>}
  </section>
}
