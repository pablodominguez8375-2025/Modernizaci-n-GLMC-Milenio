import { useEffect, useMemo, useState, type ReactNode } from 'react'
import './listing.css'

/* PMGM-UX-003 · Barra común de listados (P3-1), exportación CSV por permiso (P3-2) e índice de secciones (P3-4). */

export interface SortOption<T> { id: string; label: string; compare: (a: T, b: T) => number }
export interface ActiveFilter { id: string; label: string; onClear: () => void }

export const LISTING_PAGE_SIZE = 50

export function useListing<T>(items: T[], sorts: SortOption<T>[], pageSize = LISTING_PAGE_SIZE) {
  const [sortId, setSortId] = useState(sorts[0]?.id ?? '')
  const [limit, setLimit] = useState(pageSize)
  useEffect(() => { setLimit(pageSize) }, [items, pageSize])
  const sorted = useMemo(() => {
    const sort = sorts.find(item => item.id === sortId)
    return sort ? [...items].sort(sort.compare) : items
  }, [items, sorts, sortId])
  return {
    sortId, setSortId, sorted,
    visible: sorted.slice(0, limit),
    total: sorted.length,
    hasMore: sorted.length > limit,
    loadMore: () => setLimit(current => current + pageSize),
  }
}

export function ListingToolbar<T>({ children, filters = [], total, shown, sorts, sortId, onSort, onExport, exportLabel = 'Exportar CSV', viewMode, onViewMode }: {
  children?: ReactNode
  filters?: ActiveFilter[]
  total: number
  shown: number
  sorts?: SortOption<T>[]
  sortId?: string
  onSort?: (id: string) => void
  onExport?: () => void
  exportLabel?: string
  viewMode?: 'cards' | 'table'
  onViewMode?: (mode: 'cards' | 'table') => void
}) {
  return <section className="panel listing-toolbar" aria-label="Herramientas del listado">
    {children && <div className="listing-filters">{children}</div>}
    <div className="listing-summary">
      <p className="listing-count" aria-live="polite">Mostrando <strong>{shown}</strong> de <strong>{total}</strong></p>
      {filters.length > 0 && <div className="listing-chips">
        {filters.map(filter => <button key={filter.id} type="button" className="listing-chip" onClick={filter.onClear} aria-label={`Quitar filtro ${filter.label}`}>{filter.label}<span aria-hidden="true">×</span></button>)}
        <button type="button" className="listing-clear" onClick={() => filters.forEach(filter => filter.onClear())}>Limpiar</button>
      </div>}
      <div className="listing-actions">
        {sorts && sorts.length > 1 && onSort && <label className="listing-sort"><span>Ordenar</span><select value={sortId} onChange={event => onSort(event.target.value)}>{sorts.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>}
        {viewMode && onViewMode && <div className="listing-view-toggle" role="group" aria-label="Vista del listado">
          <button type="button" aria-pressed={viewMode === 'cards'} className={viewMode === 'cards' ? 'active' : ''} onClick={() => onViewMode('cards')}>Tarjetas</button>
          <button type="button" aria-pressed={viewMode === 'table'} className={viewMode === 'table' ? 'active' : ''} onClick={() => onViewMode('table')}>Tabla</button>
        </div>}
        {onExport && <button type="button" className="secondary listing-export" onClick={onExport} disabled={total === 0}>{exportLabel}</button>}
      </div>
    </div>
  </section>
}

export function LoadMore({ hasMore, onMore, remaining }: { hasMore: boolean; onMore: () => void; remaining: number }) {
  if (!hasMore) return null
  return <div className="listing-more"><button type="button" className="secondary" onClick={onMore}>Cargar más ({remaining} restantes)</button></div>
}

/** Arma un CSV compatible con Excel en español (separador «;», BOM UTF-8) con solo las columnas visibles para el perfil. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type CsvColumn = { header: string; value: (row: any) => unknown }

export function buildCsv(columns: CsvColumn[], rows: unknown[], footer?: string) {
  const escape = (value: unknown) => {
    const raw = value === null || value === undefined ? '' : String(value)
    // Seguridad: evita la inyección de fórmulas en Excel/Sheets (=, +, -, @, tabulación) en textos; los números quedan intactos.
    const text = typeof value !== 'number' && /^[=+\-@\t\r]/.test(raw) ? `'${raw}` : raw
    return /[;"\n\r]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
  }
  const lines = [columns.map(column => escape(column.header)).join(';'), ...rows.map(row => columns.map(column => escape(column.value(row))).join(';'))]
  if (footer) lines.push('', escape(footer))
  return '\uFEFF' + lines.join('\r\n')
}

export function exportCsv(filename: string, columns: CsvColumn[], rows: unknown[], context: string) {
  const stamp = new Intl.DateTimeFormat('es-CL', { dateStyle: 'short', timeStyle: 'short' }).format(new Date())
  const csv = buildCsv(columns, rows, `${context} · ${rows.length} registros · exportado el ${stamp} · uso interno, Ley 21.719`)
  try {
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${filename}-${new Date().toISOString().slice(0, 10)}.csv`
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch {
    /* El navegador puede bloquear descargas; la vista sigue operativa. */
  }
}

/** Índice fijo de secciones con anclas para pantallas largas. */
export function SectionIndex({ sections, label = 'Ir a la sección' }: { sections: { id: string; label: string }[]; label?: string }) {
  if (sections.length < 2) return null
  return <nav className="section-index" aria-label={label}>
    {sections.map(section => <a key={section.id} href={`#${section.id}`} onClick={event => { event.preventDefault(); document.getElementById(section.id)?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }}>{section.label}</a>)}
  </nav>
}
