import { useEffect, useId, useMemo, useRef, useState } from 'react'
import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'

/** PMGM-UX-003 · Búsqueda global de módulos y de hermanos (Issue #243). */
export interface SearchEntry {
  id: string
  label: string
  section: string
  icon: InstitutionalIconName
  keywords?: string
  onSelect: () => void
}

interface GlobalSearchProps {
  entries: SearchEntry[]
  onSearchMembers?: (query: string) => void
}

interface SearchResult {
  key: string
  label: string
  detail: string
  icon: InstitutionalIconName
  run: () => void
}

export function normalizeSearch(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()
}

export function rankEntries(entries: SearchEntry[], query: string): SearchEntry[] {
  const q = normalizeSearch(query)
  if (!q) return []
  return entries
    .map(entry => {
      const label = normalizeSearch(entry.label)
      const haystack = `${label} ${normalizeSearch(entry.section)} ${normalizeSearch(entry.keywords ?? '')}`
      const score = label.startsWith(q) ? 0 : label.includes(q) ? 1 : haystack.includes(q) ? 2 : -1
      return { entry, score }
    })
    .filter(item => item.score >= 0)
    .sort((a, b) => a.score - b.score || a.entry.label.localeCompare(b.entry.label, 'es'))
    .slice(0, 7)
    .map(item => item.entry)
}

export default function GlobalSearch({ entries, onSearchMembers }: GlobalSearchProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const rootRef = useRef<HTMLDivElement>(null)
  const listId = useId()

  const results = useMemo<SearchResult[]>(() => {
    const trimmed = query.trim()
    const modules = rankEntries(entries, trimmed).map(entry => ({ key: entry.id, label: entry.label, detail: entry.section, icon: entry.icon, run: entry.onSelect }))
    const members = onSearchMembers && trimmed.length >= 2
      ? [{ key: 'members-search', label: `Buscar «${trimmed}» en Fichas de miembros`, detail: 'Hermanos por nombre o identificador', icon: 'members' as const, run: () => onSearchMembers(trimmed) }]
      : []
    return [...modules, ...members]
  }, [entries, onSearchMembers, query])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        inputRef.current?.focus()
        setOpen(true)
      }
    }
    const onPointer = (event: PointerEvent) => { if (!rootRef.current?.contains(event.target as Node)) setOpen(false) }
    window.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    return () => { window.removeEventListener('keydown', onKey); document.removeEventListener('pointerdown', onPointer) }
  }, [])

  const choose = (result: SearchResult | undefined) => {
    if (!result) return
    result.run()
    setQuery('')
    setOpen(false)
    inputRef.current?.blur()
  }

  const showList = open && query.trim().length > 0
  return <div className="global-search" ref={rootRef} role="search">
    <label className="global-search-field">
      <span className="sr-only">Buscar módulos o hermanos</span>
      <InstitutionalIcon name="search" size={16} />
      <input
        ref={inputRef}
        type="search"
        value={query}
        placeholder="Buscar módulo o hermano"
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={showList && results[activeIndex] ? `${listId}-${activeIndex}` : undefined}
        onFocus={() => setOpen(true)}
        onChange={event => { setQuery(event.target.value); setActiveIndex(0); setOpen(true) }}
        onKeyDown={event => {
          if (event.key === 'ArrowDown') { event.preventDefault(); setActiveIndex(index => Math.min(index + 1, Math.max(results.length - 1, 0))) }
          else if (event.key === 'ArrowUp') { event.preventDefault(); setActiveIndex(index => Math.max(index - 1, 0)) }
          else if (event.key === 'Enter') { event.preventDefault(); choose(results[activeIndex]) }
          else if (event.key === 'Escape') { setOpen(false); setQuery('') }
        }}
      />
      <kbd aria-hidden="true">Ctrl K</kbd>
    </label>
    {showList && <ul className="global-search-results" id={listId} role="listbox" aria-label="Resultados de búsqueda">
      {results.length === 0
        ? <li className="global-search-empty" role="option" aria-selected="false" aria-disabled="true">Sin resultados para «{query.trim()}». Prueba con el nombre de un módulo.</li>
        : results.map((result, index) => <li key={result.key} id={`${listId}-${index}`} role="option" aria-selected={index === activeIndex} className={index === activeIndex ? 'is-active' : undefined} onPointerDown={event => event.preventDefault()} onClick={() => choose(result)} onMouseEnter={() => setActiveIndex(index)}>
          <span className="global-search-icon" aria-hidden="true"><InstitutionalIcon name={result.icon} size={18} /></span>
          <span><strong>{result.label}</strong><small>{result.detail}</small></span>
        </li>)}
    </ul>}
  </div>
}
