import { type ReactNode, useId } from 'react'

/** Sección plegable de la ficha: título, estado (completa / pendiente) y contenido que se abre y cierra. */
export default function IntakeCollapsible({ id, icon, title, note, complete, missing, open, onToggle, className = '', children }: {
  id: string
  icon: string
  title: string
  note: string
  complete: boolean
  missing: string[]
  open: boolean
  onToggle: () => void
  className?: string
  children: ReactNode
}) {
  const bodyId = useId()
  return <section id={id} className={['candidate-product-card', 'intake-collapsible', open ? 'is-open' : '', className].filter(Boolean).join(' ')}>
    <div className="candidate-section-title intake-collapsible-head">
      <span aria-hidden="true">{icon}</span>
      <h2><button type="button" aria-expanded={open} aria-controls={bodyId} onClick={onToggle}>{title}</button></h2>
      <em>{note}</em>
      <b className={complete ? 'intake-badge complete' : 'intake-badge pending'}>{complete ? 'Completa' : `Faltan ${missing.length}`}</b>
      <i className="intake-chevron" aria-hidden="true">{open ? '▴' : '▾'}</i>
    </div>
    {!complete && !open && <p className="intake-missing">Falta: {missing.join(', ')}.</p>}
    <div id={bodyId} className="intake-collapsible-body" hidden={!open}>{children}</div>
  </section>
}
