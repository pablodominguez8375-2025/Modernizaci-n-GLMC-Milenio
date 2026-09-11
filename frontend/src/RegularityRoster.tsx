import type { RegularityKind, RegularityRow } from './regularityDashboardModel'
import { regularityStatusClass, regularityStatusLabel } from './regularityDashboardModel'

export default function RegularityRoster({ rows, loading, kind }: { rows: RegularityRow[]; loading: boolean; kind: RegularityKind }) {
  const priority = rows.filter(row => !row.snapshot || !['up_to_date', 'exempt'].includes(row.snapshot.status))
  const coverage = rows.length ? Math.round(((rows.length - rows.filter(row => !row.snapshot).length) / rows.length) * 100) : 0
  return <div className="regularity-operations-grid">
    <article className="panel regularity-health-panel">
      <div className="regularity-panel-heading"><div><p className="eyebrow">Semáforo institucional</p><h2>Estado general de Talleres</h2></div><strong>{loading ? '—' : `${coverage}%`}</strong></div>
      <div className="regularity-health-track"><span style={{ width: `${coverage}%` }} /></div>
      <div className="regularity-roster">{rows.map(row => <div className="regularity-roster-row" key={row.organization.id}><span><strong>{row.organization.name}</strong><small>{row.snapshot ? `Corte ${row.snapshot.asOfDate}` : 'Sin registro vigente'}</small></span>{row.snapshot ? <span className={regularityStatusClass(row.snapshot.status)}>{regularityStatusLabel(kind, row.snapshot.status)}</span> : <span className="regularity-status missing">Sin estado</span>}</div>)}</div>
    </article>
    <article className="panel regularity-priority-panel">
      <div className="regularity-panel-heading"><div><p className="eyebrow">Gestión prioritaria</p><h2>Casos que requieren acción</h2></div><strong>{loading ? '—' : priority.length}</strong></div>
      <div className="regularity-priority-list">{priority.map(row => <div className="regularity-priority-item" key={row.organization.id}><strong>{row.organization.name}</strong><small>{row.snapshot ? 'Requiere regularización o revisión antes de completar controles ceremoniales.' : 'Debe registrarse un estado vigente.'}</small></div>)}</div>
      {!loading && !priority.length && <p className="regularity-priority-empty">Sin casos pendientes por esta área.</p>}
    </article>
  </div>
}
