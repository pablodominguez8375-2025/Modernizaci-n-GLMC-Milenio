import { useEffect, useRef, useState } from 'react'
import InstitutionalIcon from './InstitutionalIcon'
import TextSizeControl from './TextSizeControl'

/** PMGM-UX-003 · Menú de usuario de la cabecera (Issue #243). */
interface UserMenuProps {
  displayName: string
  versionLabel: string
  onOpenProfile?: () => void
  onOpenCalendar?: () => void
  onLogout?: () => void
}

export function initialsFor(name: string) {
  const words = name.replace(/·.*$/, '').split(/\s+/).filter(word => /^[\p{L}]/u.test(word))
  return (words.slice(0, 2).map(word => word[0]).join('') || '?').toUpperCase()
}

export default function UserMenu({ displayName, versionLabel, onOpenProfile, onOpenCalendar, onLogout }: UserMenuProps) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => { if (!rootRef.current?.contains(event.target as Node)) setOpen(false) }
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    document.addEventListener('pointerdown', onPointer)
    window.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('pointerdown', onPointer); window.removeEventListener('keydown', onKey) }
  }, [open])
  const run = (action?: () => void) => () => { setOpen(false); action?.() }
  return <div className="user-menu" ref={rootRef}>
    <button type="button" className="user-menu-trigger" aria-haspopup="menu" aria-expanded={open} aria-label={`Menú de usuario de ${displayName}`} onClick={() => setOpen(value => !value)}>
      <span className="user-menu-avatar" aria-hidden="true">{initialsFor(displayName)}</span>
    </button>
    {open && <div className="user-menu-panel" role="menu">
      <p className="user-menu-name">{displayName}</p>
      {onOpenProfile && <button type="button" role="menuitem" onClick={run(onOpenProfile)}><InstitutionalIcon name="member" size={16} /> Mi ficha</button>}
      {onOpenCalendar && <button type="button" role="menuitem" onClick={run(onOpenCalendar)}><InstitutionalIcon name="calendar" size={16} /> Mi calendario</button>}
      {onLogout && <button type="button" role="menuitem" onClick={run(onLogout)}><InstitutionalIcon name="logout" size={16} /> Cerrar sesión</button>}
      <TextSizeControl className="in-user-menu" />
      <p className="user-menu-version">{versionLabel}</p>
    </div>}
  </div>
}
