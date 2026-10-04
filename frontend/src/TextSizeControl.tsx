import { useEffect, useState } from 'react'
import { readTextSize, saveTextSize, TEXT_SIZE_OPTIONS, type TextSize } from './textSize'

/** PMGM-UX-004 · Control «Tamaño de letra»: tres botones grandes, uno marcado. */
export default function TextSizeControl({ className = '' }: { className?: string }) {
  const [size, setSize] = useState<TextSize>(() => readTextSize())
  useEffect(() => {
    const onChange = (event: Event) => setSize((event as CustomEvent<TextSize>).detail)
    window.addEventListener('pmgm:text-size', onChange)
    return () => window.removeEventListener('pmgm:text-size', onChange)
  }, [])
  return <div className={`text-size-control ${className}`.trim()} role="group" aria-label="Tamaño de letra">
    <span className="text-size-title">Tamaño de letra</span>
    <div className="text-size-options">
      {TEXT_SIZE_OPTIONS.map(option => <button key={option.value} type="button" aria-pressed={size === option.value} title={option.label} onClick={() => saveTextSize(option.value)}>
        <span className={`text-size-sample is-${option.value}`} aria-hidden="true">A</span>
        <span className="text-size-label">{option.label}</span>
      </button>)}
    </div>
  </div>
}

/** PMGM-UX menús simples · Botón «Aa» de la cabecera (PC) que abre el control de tamaño de letra. */
export function TextSizeMenu() {
  const [open, setOpen] = useState(false)
  useEffect(() => {
    if (!open) return
    const close = (event: Event) => {
      if (event instanceof KeyboardEvent && event.key !== 'Escape') return
      if (event instanceof MouseEvent && (event.target as HTMLElement).closest('.text-size-menu')) return
      setOpen(false)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => { document.removeEventListener('mousedown', close); document.removeEventListener('keydown', close) }
  }, [open])
  return <div className="text-size-menu">
    <button type="button" className="text-size-menu-trigger" aria-expanded={open} aria-haspopup="true" title="Tamaño de letra" onClick={() => setOpen(value => !value)}>
      <span aria-hidden="true">Aa</span><span className="sr-only">Tamaño de letra</span>
    </button>
    {open && <div className="text-size-menu-panel"><TextSizeControl /></div>}
  </div>
}
