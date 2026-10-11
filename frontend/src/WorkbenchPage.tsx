import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'

export type WorkbenchItem = { group: string; icon: InstitutionalIconName; label: string; hint: string; badge?: number; onOpen: () => void }

/** PO 10-10-2026: «Mi mesa de trabajo» reúne las funciones de gestión de la Orden en una sola opción del menú (tarjetas grandes con íconos, sin pestañas). */
export default function WorkbenchPage({ items }: { items: WorkbenchItem[] }) {
  const groups = [...new Set(items.map(item => item.group))]
  const today = items.filter(item => (item.badge ?? 0) > 0)
  return <section className="workbench" aria-labelledby="mesa-titulo">
    <h2 id="mesa-titulo">Mi mesa de trabajo</h2>
    <p>Elige qué quieres hacer.</p>
    <div className="workbench-today" role="status" aria-label="Lo de hoy">
      <strong>Lo de hoy</strong>
      {today.length === 0
        ? <span>Nada pendiente por ahora.</span>
        : <ul>{today.map(item => <li key={item.label}><button type="button" onClick={item.onOpen}><span className="workbench-count">{item.badge}</span> {item.label}</button></li>)}</ul>}
    </div>
    {groups.map(group => <section key={group} className="workbench-group" aria-label={group}>
      <h3>{group}</h3>
      <div className="workbench-grid">
        {items.filter(item => item.group === group).map(item => <button key={item.label} type="button" className="workbench-card" onClick={item.onOpen}>
          <span className="workbench-icon" aria-hidden="true"><InstitutionalIcon name={item.icon} size={26} /></span>
          <strong>{item.label}{(item.badge ?? 0) > 0 && <span className="workbench-count" aria-label={`${item.badge} pendientes`}>{item.badge}</span>}</strong>
          <small>{item.hint}</small>
        </button>)}
      </div>
    </section>)}
  </section>
}
