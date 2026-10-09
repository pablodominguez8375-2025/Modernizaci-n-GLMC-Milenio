import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'

type ReportItem = { icon: InstitutionalIconName; label: string; hint: string; onOpen: () => void }

/** Vista previa PO 09-10-2026: una sola opción «Informes» en el menú; aquí se elige cuál abrir (tarjetas grandes, sin pestañas). */
export default function ReportsHub({ items }: { items: ReportItem[] }) {
  return <section className="reports-hub" aria-labelledby="informes-titulo">
    <h2 id="informes-titulo">Informes</h2>
    <p>Elige qué quieres revisar.</p>
    <div className="reports-hub-grid">
      {items.map(item => <button key={item.label} type="button" className="reports-hub-card" onClick={item.onOpen}>
        <span className="reports-hub-icon"><InstitutionalIcon name={item.icon} size={24} /></span>
        <strong>{item.label}</strong>
        <small>{item.hint}</small>
      </button>)}
    </div>
  </section>
}
