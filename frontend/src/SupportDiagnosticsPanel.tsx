import { useCallback, useEffect, useState } from 'react'
import type { PmgmApiClient, SupportDiagnosticsResponse } from './api/pmgmApi'
import { diagnosticCsv, diagnosticJson } from './supportDiagnosticExport'
import LodgeDataHealthCard from './LodgeDataHealthCard'
import LodgeQueryProbeCard from './LodgeQueryProbeCard'
import './auditableEventLog.css'

function exportFile(content: string, type: string, name: string) {
  const url = URL.createObjectURL(new Blob([content], { type }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = name
  document.body.append(anchor)
  anchor.click()
  anchor.remove()
  // Keep the object URL valid long enough for the browser's download handler.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export default function SupportDiagnosticsPanel({
  api, onOpenAudit
}: {
  api: PmgmApiClient
  onOpenAudit: () => void
}) {
  const [severity, setSeverity] = useState<'all' | 'error' | 'warning'>('all')
  const [route, setRoute] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [snapshot, setSnapshot] = useState<SupportDiagnosticsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const fromIso = from ? new Date(from + 'T00:00:00').toISOString() : undefined
      const toIso = to ? new Date(to + 'T23:59:59').toISOString() : undefined
      if (fromIso && toIso && fromIso > toIso) {
        setError('La fecha inicial no puede ser posterior a la final.')
        return
      }
      const result = await api.getSupportDiagnostics({
        severity: severity === 'all' ? undefined : severity,
        route: route.trim().slice(0, 80) || undefined,
        from: fromIso, to: toIso, limit: 200
      })
      setSnapshot(result)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible consultar los registros.')
    } finally {
      setLoading(false)
    }
  }, [api, from, route, severity, to])

  useEffect(() => { void load() }, [load])

  return <>
  <LodgeDataHealthCard api={api} />
  <LodgeQueryProbeCard api={api} />
  <section className="system-event-log" aria-label="Diagnóstico de soporte">
    <header>
      <div>
        <p className="eyebrow">Soporte técnico · solo lectura</p>
        <h2>Errores y advertencias</h2>
        <p>Consulte errores HTTP 5xx y advertencias 4xx. Use el ID de correlación para localizar el incidente en los registros privados del servidor.</p>
      </div>
      <span className="system-demo-badge">QA / Diagnóstico</span>
    </header>
    <p role="note">Los resultados son metadatos sanitizados de la API actual. No incluyen información personal, contenido de solicitudes, contraseñas ni trazas internas. El historial en memoria se pierde al reiniciar la API y no abarca otras instancias.</p>

    <div className="system-event-toolbar">
      <label><span>Gravedad</span><select value={severity} onChange={e => setSeverity(e.target.value as typeof severity)}>
        <option value="all">Todas</option><option value="error">Errores (5xx)</option><option value="warning">Advertencias (4xx)</option>
      </select></label>
      <label><span>Ruta del módulo</span><input aria-label="Filtrar ruta técnica" value={route} onChange={e => setRoute(e.target.value)} placeholder="Ej.: gestion-logial" maxLength={80} /></label>
      <label><span>Desde</span><input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
      <label><span>Hasta</span><input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
      <button type="button" disabled={loading} onClick={() => void load()}>{loading ? 'Consultando…' : 'Actualizar'}</button>
    </div>

    {error && <p role="alert" className="error-banner">{error}</p>}
    <p aria-live="polite">{snapshot ? `${snapshot.total} registros encontrados; se muestran hasta ${snapshot.limit}.` : 'Consultando registros…'}</p>
    <div className="system-event-toolbar">
      <button type="button" onClick={onOpenAudit}>Ir a auditoría institucional</button>
      <button type="button" disabled={!snapshot?.items.length} onClick={() => snapshot && exportFile(diagnosticCsv(snapshot.items), 'text/csv;charset=utf-8', 'soporte-diagnosticos-qa.csv')}>Exportar CSV</button>
      <button type="button" disabled={!snapshot?.items.length} onClick={() => snapshot && exportFile(diagnosticJson(snapshot), 'application/json', 'soporte-diagnosticos-qa.json')}>Exportar JSON</button>
    </div>

    <div className="system-event-table">
      <table>
        <thead><tr><th>Fecha</th><th>Gravedad</th><th>HTTP</th><th>Método / ruta</th><th>Correlación</th></tr></thead>
        <tbody>{snapshot?.items.map((item, index) => <tr key={item.correlationId + '-' + index}>
          <td>{new Date(item.occurredAtUtc).toLocaleString('es-CL')}</td>
          <td>{item.severity === 'error' ? 'Error' : 'Advertencia'}</td>
          <td>{item.statusCode}</td>
          <td><strong>{item.method}</strong><small>{item.route}</small></td>
          <td><code>{item.correlationId}</code></td>
        </tr>)}</tbody>
      </table>
    </div>
    {snapshot?.items.length === 0 && !loading && <p>No hay eventos en el intervalo consultado. Esto no demuestra ausencia de errores anteriores al inicio de la API.</p>}
  </section>
  </>
}
