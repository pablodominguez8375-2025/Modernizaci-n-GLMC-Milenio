import { useState } from 'react'
import { PmgmApiHttpError, type PmgmApiClient, type SupportLodgeDataHealthResponse } from './api/pmgmApi'
import { lodgeHealthErrorMessage, summarizeLodgeHealth } from './lodgeDataHealth'

/** #688 — «Estado de datos de Talleres»: verificación explícita y de solo lectura, apta para móvil. */
export default function LodgeDataHealthCard({ api }: { api: PmgmApiClient }) {
  const [result, setResult] = useState<SupportLodgeDataHealthResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function verify() {
    setLoading(true); setError(null)
    try { setResult(await api.getSupportLodgeDataHealth()) }
    catch (reason) { setResult(null); setError(lodgeHealthErrorMessage(reason instanceof PmgmApiHttpError ? reason.status : undefined)) }
    finally { setLoading(false) }
  }

  const resources = result ? summarizeLodgeHealth(result.items) : []
  return <section className="lodge-health" aria-labelledby="lodge-health-title">
    <header>
      <div>
        <p className="eyebrow">Soporte técnico · solo lectura</p>
        <h2 id="lodge-health-title">Estado de datos de Talleres</h2>
        <p>Revisa si el servidor tiene las tablas, columnas y permisos que necesitan las pantallas de Talleres. Solo se ejecuta cuando usted pulsa el botón y no modifica nada.</p>
      </div>
      <button type="button" className="action-trigger primary" disabled={loading} onClick={() => void verify()}>{loading ? 'Verificando…' : 'Verificar lectura'}</button>
    </header>
    {error && <p role="alert" className="error-banner">{error}</p>}
    {result && <>
      <ul className="lodge-health-list" aria-live="polite">
        {resources.map(item => <li key={item.resource} className={item.light}>
          <span className="lodge-health-dot" aria-hidden="true" />
          <div>
            <strong>{item.label}</strong>
            <small>{item.light === 'ok' ? 'Tablas, columnas y permisos en orden' : 'Requiere atención'}</small>
            {item.problems.length > 0 && <ul>{item.problems.map(problem => <li key={problem}>{problem}</li>)}</ul>}
          </div>
        </li>)}
      </ul>
      <p role="note" className="lodge-health-note">
        {result.status === 'ready'
          ? 'Los datos están en orden, pero esto no garantiza que todas las pantallas del Taller respondan bien: solo revisa la estructura y los permisos. '
          : 'Falta algo en la estructura o en los permisos. Avise a quien administra el servidor. '}
        Verificado el {new Date(result.checkedAtUtc).toLocaleString('es-CL')}.
      </p>
    </>}
    {!result && !error && !loading && <p className="lodge-health-note">Aún no se ha verificado. Pulse «Verificar lectura».</p>}
  </section>
}
