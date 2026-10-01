/* PMGM-UX-004 · «Lista primero, acción bajo demanda».
 * Componentes de presentación para separar listados de acciones sin cambiar reglas, permisos ni llamadas a la API.
 * Los formularios se mantienen montados (ocultos) para conservar lo escrito si el usuario cierra el panel. */
import { type FormEvent, type ReactNode, useEffect, useId, useRef, useState } from 'react'

type ButtonTone = 'primary' | 'secondary'

export function ActionDrawer({ label, title, description, confirmMessage, tone = 'primary', disabled = false, keepOpen = false, children }: {
  label: string
  title?: string
  description?: string
  /** Si se indica, al enviar el formulario se pide confirmar antes de ejecutar. */
  confirmMessage?: string
  tone?: ButtonTone
  disabled?: boolean
  /** Para registros repetidos (p. ej. asistencia hermano por hermano): el panel sigue abierto después de guardar. */
  keepOpen?: boolean
  children: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [pendingForm, setPendingForm] = useState<HTMLFormElement | null>(null)
  const confirmedRef = useRef(false)
  const headingId = useId()
  const headingRef = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    if (!open) return
    headingRef.current?.focus()
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') close() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  function close() { setOpen(false); setPendingForm(null); confirmedRef.current = false }

  function onSubmitCapture(event: FormEvent<HTMLDivElement>) {
    const form = event.target as HTMLFormElement
    if (confirmMessage && !confirmedRef.current) {
      event.preventDefault()
      event.stopPropagation()
      setPendingForm(form)
      return
    }
    confirmedRef.current = false
    // El formulario ya pasó la validación nativa: se ejecuta y el panel se cierra; el resultado se informa en la página.
    if (!keepOpen) window.setTimeout(close, 0)
  }

  function confirm() {
    if (!pendingForm) return
    confirmedRef.current = true
    const form = pendingForm
    setPendingForm(null)
    form.requestSubmit()
  }

  return <>
    <button type="button" className={`action-trigger ${tone}`} disabled={disabled} aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen(true)}>{label}</button>
    <div className="action-drawer-layer" hidden={!open}>
      <button type="button" className="action-drawer-backdrop" aria-label="Cerrar panel" tabIndex={-1} onClick={close} />
      <section className="action-drawer" role="dialog" aria-modal="true" aria-labelledby={headingId}>
        <header className="action-drawer-header">
          <div><h2 id={headingId} ref={headingRef} tabIndex={-1}>{title ?? label}</h2>{description && <p>{description}</p>}</div>
          <button type="button" className="action-drawer-close" aria-label="Cerrar" onClick={close}>×</button>
        </header>
        <div className="action-drawer-body" onSubmitCapture={onSubmitCapture}>{children}</div>
        {pendingForm && <div className="action-confirm" role="alertdialog" aria-live="assertive">
          <p><strong>Revise antes de confirmar.</strong> {confirmMessage}</p>
          <div><button type="button" className="action-trigger secondary" onClick={() => setPendingForm(null)}>Volver</button><button type="button" className="action-trigger primary" onClick={confirm}>Confirmar</button></div>
        </div>}
        <footer className="action-drawer-footer"><button type="button" className="action-link" onClick={close}>Cancelar y cerrar</button></footer>
      </section>
    </div>
  </>
}

export function ConfirmAction({ label, message, confirmLabel = 'Confirmar', tone = 'primary', disabled = false, onConfirm }: {
  label: string; message: string; confirmLabel?: string; tone?: ButtonTone; disabled?: boolean; onConfirm: () => void
}) {
  const [asking, setAsking] = useState(false)
  if (!asking) return <button type="button" className={`action-trigger ${tone}`} disabled={disabled} onClick={() => setAsking(true)}>{label}</button>
  return <div className="action-inline-confirm" role="alertdialog" aria-live="assertive">
    <p>{message}</p>
    <div><button type="button" className="action-trigger secondary" onClick={() => setAsking(false)}>Cancelar</button><button type="button" className="action-trigger primary" disabled={disabled} onClick={() => { setAsking(false); onConfirm() }}>{confirmLabel}</button></div>
  </div>
}

export type RowMenuItem = { label: string; onSelect: () => void; disabled?: boolean }

export function RowMenu({ items, label = 'Más acciones' }: { items: RowMenuItem[]; label?: string }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const onDown = (event: MouseEvent) => { if (!ref.current?.contains(event.target as Node)) setOpen(false) }
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDown); window.removeEventListener('keydown', onKey) }
  }, [open])
  if (items.length === 0) return null
  return <div className="row-menu" ref={ref}>
    <button type="button" className="row-menu-trigger" aria-haspopup="menu" aria-expanded={open} aria-label={label} title={label} onClick={() => setOpen(value => !value)}>⋯</button>
    {open && <div className="row-menu-list" role="menu">
      {items.map(item => <button key={item.label} type="button" role="menuitem" disabled={item.disabled} onClick={() => { setOpen(false); item.onSelect() }}>{item.label}</button>)}
    </div>}
  </div>
}

export type WorkspaceTab<T extends string> = { id: T; label: string; badge?: number }

export function WorkspaceTabs<T extends string>({ tabs, active, onChange, label }: { tabs: WorkspaceTab<T>[]; active: T; onChange: (id: T) => void; label: string }) {
  return <nav className="workspace-tabs" aria-label={label}>
    <div role="tablist">
      {tabs.map(tab => <button key={tab.id} type="button" role="tab" id={`tab-${tab.id}`} aria-selected={tab.id === active} aria-controls={`panel-${tab.id}`} className={tab.id === active ? 'active' : ''} onClick={() => onChange(tab.id)}>
        {tab.label}{tab.badge ? <span className="workspace-tab-badge">{tab.badge}</span> : null}
      </button>)}
    </div>
  </nav>
}

export function WorkspacePanel({ id, active, children }: { id: string; active: boolean; children: ReactNode }) {
  return <div role="tabpanel" id={`panel-${id}`} aria-labelledby={`tab-${id}`} hidden={!active} className="workspace-panel">{children}</div>
}

export function HelpNote({ children }: { children: ReactNode }) {
  return <details className="help-note"><summary>¿Qué hago aquí?</summary><div>{children}</div></details>
}

export function ActionBar({ children }: { children: ReactNode }) {
  return <div className="action-bar">{children}</div>
}
