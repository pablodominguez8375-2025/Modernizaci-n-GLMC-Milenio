import { useEffect, useMemo, useState } from 'react'
import { type CalendarApiClient, type CalendarEvent } from './api/calendarApi'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'

type CalendarMode = 'agenda' | 'month'

export default function CalendarPage({ api, calendarApi, canManage }: { api: PmgmApiClient; calendarApi: CalendarApiClient; canManage: boolean }) {
  const [mode, setMode] = useState<CalendarMode>('agenda')
  const [anchor, setAnchor] = useState(() => monthStart(new Date()))
  const [events, setEvents] = useState<CalendarEvent[]>([])
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [status, setStatus] = useState('active')
  const [eventType, setEventType] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [operationMessage, setOperationMessage] = useState<string | null>(null)
  const [conflictCount, setConflictCount] = useState<number | null>(null)

  const window = useMemo(() => calendarWindow(anchor), [anchor])

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => { if (active) setOrganizations(response.items) })
      .catch(() => { /* el calendario sigue siendo utilizable sin catálogo auxiliar */ })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)
    calendarApi.getCalendar({ fromUtc: window.fromUtc, toUtc: window.toUtc, organizationId: organizationId || undefined })
      .then(response => { if (active) setEvents(response.events) })
      .catch((reason: unknown) => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar el calendario.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [calendarApi, organizationId, window.fromUtc, window.toUtc])

  const filtered = useMemo(() => events.filter(event => {
    const statusMatch = status === '' || (status === 'active' ? event.status !== 'cancelled' : event.status === status)
    const typeMatch = !eventType || event.eventType === eventType
    return statusMatch && typeMatch
  }), [events, status, eventType])

  const eventTypes = useMemo(() => Array.from(new Set(events.map(event => event.eventType))).sort(), [events])
  const maskedCount = filtered.filter(event => event.isMasked).length
  const confirmedCount = filtered.filter(event => event.status === 'confirmed').length
  const reservationCount = filtered.filter(event => event.spaceId).length

  async function reconcile() {
    setOperationMessage(null)
    try {
      const result = await calendarApi.reconcileSources()
      setOperationMessage(`Reconciliación completada: ${result.created} creados, ${result.updated} actualizados, ${result.skipped} omitidos.`)
      const response = await calendarApi.getCalendar({ fromUtc: window.fromUtc, toUtc: window.toUtc, organizationId: organizationId || undefined })
      setEvents(response.events)
    } catch (reason) {
      setOperationMessage(reason instanceof Error ? reason.message : 'No fue posible reconciliar las fuentes.')
    }
  }

  async function inspectConflicts() {
    setOperationMessage(null)
    try {
      const response = await calendarApi.getSpaceConflicts({ fromUtc: window.fromUtc, toUtc: window.toUtc })
      setConflictCount(response.total)
      setOperationMessage(response.total === 0 ? 'No se detectaron solapamientos de espacios en el período.' : `Se detectaron ${response.total} solapamientos que requieren revisión administrativa.`)
    } catch (reason) {
      setOperationMessage(reason instanceof Error ? reason.message : 'No fue posible revisar los conflictos.')
    }
  }

  return <>
    <section className="page-heading calendar-heading">
      <div>
        <p className="eyebrow">Agenda institucional unificada</p>
        <h1>Calendario Institucional</h1>
        <p>Tenidas, ceremonias, docencia y reservas de espacios, con visibilidad controlada por ámbito y permisos.</p>
      </div>
      <div className="calendar-heading-actions">
        <div className="segmented" aria-label="Vista de calendario">
          <button type="button" className={mode === 'agenda' ? 'active' : ''} onClick={() => setMode('agenda')}>Agenda</button>
          <button type="button" className={mode === 'month' ? 'active' : ''} onClick={() => setMode('month')}>Mes</button>
        </div>
      </div>
    </section>

    <section className="metric-grid calendar-metrics">
      <Metric label="Eventos visibles" value={String(filtered.length)} detail={monthLabel(anchor)} />
      <Metric label="Confirmados" value={String(confirmedCount)} detail="Incluye reservas y actividades vigentes" />
      <Metric label="Uso de espacios" value={String(reservationCount)} detail="Eventos con templo o sala asignada" />
      <Metric label="Protegidos" value={String(maskedCount)} detail="Se muestran sólo como Ocupado" />
    </section>

    <section className="panel calendar-toolbar">
      <div className="calendar-period-nav">
        <button type="button" aria-label="Mes anterior" onClick={() => setAnchor(addMonths(anchor, -1))}>‹</button>
        <div><strong>{monthLabel(anchor)}</strong><small>Zona horaria America/Santiago</small></div>
        <button type="button" aria-label="Mes siguiente" onClick={() => setAnchor(addMonths(anchor, 1))}>›</button>
        <button type="button" className="secondary-button" onClick={() => setAnchor(monthStart(new Date()))}>Hoy</button>
      </div>
      <div className="calendar-filters">
        <label><span>Taller</span><select value={organizationId} onChange={event => setOrganizationId(event.target.value)}><option value="">Todos los visibles</option>{organizations.map(org => <option key={org.id} value={org.id}>{org.name}{org.number ? ` Nº ${org.number}` : ''}</option>)}</select></label>
        <label><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="active">Vigentes</option><option value="">Todos</option><option value="confirmed">Confirmados</option><option value="tentative">Tentativos</option><option value="completed">Realizados</option><option value="cancelled">Cancelados</option></select></label>
        <label><span>Tipo</span><select value={eventType} onChange={event => setEventType(event.target.value)}><option value="">Todos</option>{eventTypes.map(type => <option key={type} value={type}>{eventTypeLabel(type)}</option>)}</select></label>
      </div>
      {canManage && <div className="calendar-admin-actions"><button type="button" className="secondary-button" onClick={() => void inspectConflicts()}>Revisar conflictos{conflictCount !== null ? ` (${conflictCount})` : ''}</button><button type="button" onClick={() => void reconcile()}>Reconciliar fuentes</button></div>}
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible cargar el calendario.</strong><span>{error}</span></div>}
    {operationMessage && <div className="calendar-operation-message" role="status">{operationMessage}</div>}

    {loading ? <section className="panel"><LoadingRows /></section> : mode === 'agenda'
      ? <Agenda events={filtered} organizations={organizations} />
      : <MonthView anchor={anchor} events={filtered} />}
  </>
}

function Agenda({ events, organizations }: { events: CalendarEvent[]; organizations: OrganizationOption[] }) {
  const groups = useMemo(() => {
    const result = new Map<string, CalendarEvent[]>()
    for (const event of [...events].sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc))) {
      const key = localDateKey(event.startsAtUtc)
      const existing = result.get(key) ?? []
      existing.push(event)
      result.set(key, existing)
    }
    return [...result.entries()]
  }, [events])

  if (!events.length) return <section className="panel empty-state"><strong>No hay eventos para los filtros seleccionados.</strong><span>Pruebe otro Taller, tipo o estado.</span></section>

  return <section className="calendar-agenda">{groups.map(([day, dayEvents]) => <article className="agenda-day" key={day}>
    <header><span className="agenda-day-number">{day.slice(8, 10)}</span><div><strong>{weekdayLabel(day)}</strong><small>{longDateLabel(day)}</small></div></header>
    <div className="agenda-events">{dayEvents.map(event => <EventCard key={event.id} event={event} organization={organizations.find(org => org.id === event.organizationId)} />)}</div>
  </article>)}</section>
}

function EventCard({ event, organization }: { event: CalendarEvent; organization?: OrganizationOption }) {
  const allDay = isInstitutionalAllDay(event)
  return <article className={event.isMasked ? 'calendar-event-card masked' : 'calendar-event-card'}>
    <div className="event-time"><strong>{allDay ? 'Todo el día' : timeRange(event)}</strong><small>{event.locationDisplay ?? (event.spaceId ? 'Espacio institucional' : 'Sin espacio asignado')}</small></div>
    <div className="event-main"><div className="event-title-row"><div><span className={`event-kind kind-${eventKind(event.eventType)}`}>{eventTypeLabel(event.eventType)}</span><h3>{event.title}</h3></div><span className={`status-pill calendar-status ${statusClass(event.status)}`}>{statusLabel(event.status)}</span></div>
      {!event.isMasked && <div className="event-meta"><span>{organization ? `${organization.name}${organization.number ? ` Nº ${organization.number}` : ''}` : 'Ámbito institucional'}</span>{event.sourceModule && <span>Fuente: {sourceLabel(event.sourceModule)}</span>}</div>}
      {event.isMasked && <p className="privacy-note">Detalle protegido. Sólo se expone la ocupación necesaria para coordinar agenda y espacios.</p>}
    </div>
  </article>
}

function MonthView({ anchor, events }: { anchor: Date; events: CalendarEvent[] }) {
  const days = monthGridDays(anchor)
  return <section className="panel month-calendar">
    <div className="month-weekdays">{['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'].map(day => <span key={day}>{day}</span>)}</div>
    <div className="month-grid">{days.map(day => {
      const key = dateKey(day)
      const dayEvents = events.filter(event => eventTouchesLocalDate(event, key))
      const outside = day.getMonth() !== anchor.getMonth()
      return <article className={outside ? 'month-day outside' : 'month-day'} key={key}><header><span>{day.getDate()}</span>{dayEvents.length > 0 && <small>{dayEvents.length}</small>}</header><div>{dayEvents.slice(0, 3).map(event => <div className={event.isMasked ? 'month-event masked' : 'month-event'} key={event.id} title={event.title}><span>{event.isMasked ? 'Ocupado' : event.title}</span></div>)}{dayEvents.length > 3 && <small className="month-more">+{dayEvents.length - 3} más</small>}</div></article>
    })}</div>
  </section>
}

function Metric({ label, value, detail }: { label: string; value: string; detail: string }) { return <article className="metric-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article> }
function LoadingRows() { return <div className="loading-rows"><span /><span /><span /><span /></div> }

function calendarWindow(anchor: Date) {
  const from = addMonths(monthStart(anchor), -1)
  const to = addMonths(monthStart(anchor), 2)
  return { fromUtc: from.toISOString(), toUtc: to.toISOString() }
}
function monthStart(date: Date) { return new Date(date.getFullYear(), date.getMonth(), 1, 0, 0, 0, 0) }
function addMonths(date: Date, amount: number) { return new Date(date.getFullYear(), date.getMonth() + amount, 1, 0, 0, 0, 0) }
function monthLabel(date: Date) { return capitalize(new Intl.DateTimeFormat('es-CL', { month: 'long', year: 'numeric', timeZone: 'America/Santiago' }).format(date)) }
function localDateKey(value: string) { return new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'America/Santiago' }).format(new Date(value)) }
function weekdayLabel(day: string) { return capitalize(new Intl.DateTimeFormat('es-CL', { weekday: 'long', timeZone: 'America/Santiago' }).format(new Date(`${day}T12:00:00-03:00`))) }
function longDateLabel(day: string) { return new Intl.DateTimeFormat('es-CL', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'America/Santiago' }).format(new Date(`${day}T12:00:00-03:00`)) }
function timeRange(event: CalendarEvent) { const formatter = new Intl.DateTimeFormat('es-CL', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'America/Santiago' }); return `${formatter.format(new Date(event.startsAtUtc))}–${formatter.format(new Date(event.endsAtUtc))}` }
function isInstitutionalAllDay(event: CalendarEvent) { const start = parts(event.startsAtUtc); const end = parts(event.endsAtUtc); return start.hour === '00' && start.minute === '00' && end.hour === '00' && end.minute === '00' && localDateKey(event.startsAtUtc) !== localDateKey(event.endsAtUtc) }
function parts(value: string) { const values: Record<string, string> = {}; for (const item of new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'America/Santiago' }).formatToParts(new Date(value))) values[item.type] = item.value; return values }
function monthGridDays(anchor: Date) { const first = monthStart(anchor); const mondayOffset = (first.getDay() + 6) % 7; const start = new Date(first); start.setDate(first.getDate() - mondayOffset); return Array.from({ length: 42 }, (_, index) => { const day = new Date(start); day.setDate(start.getDate() + index); return day }) }
function dateKey(date: Date) { const y = date.getFullYear(); const m = String(date.getMonth() + 1).padStart(2, '0'); const d = String(date.getDate()).padStart(2, '0'); return `${y}-${m}-${d}` }
function eventTouchesLocalDate(event: CalendarEvent, day: string) { const start = localDateKey(event.startsAtUtc); const endExclusive = localDateKey(new Date(new Date(event.endsAtUtc).getTime() - 1).toISOString()); return day >= start && day <= endExclusive }
function capitalize(value: string) { return value.charAt(0).toUpperCase() + value.slice(1) }
function statusClass(status: string) { return status === 'confirmed' || status === 'completed' ? 'complete' : status === 'cancelled' ? 'cancelled' : 'active' }
function statusLabel(status: string) { return status === 'confirmed' ? 'Confirmado' : status === 'completed' ? 'Realizado' : status === 'cancelled' ? 'Cancelado' : status === 'tentative' ? 'Tentativo' : 'Borrador' }
function eventKind(type: string) { return type.includes('ceremony') ? 'ceremony' : type.includes('meeting') ? 'meeting' : type.includes('instruction') ? 'instruction' : type.includes('reservation') ? 'space' : 'other' }
function eventTypeLabel(type: string) { return type === 'ceremony_day' ? 'Ceremonia' : type === 'ceremony_reservation' ? 'Reserva ceremonia' : type === 'space_reservation' ? 'Reserva espacio' : type === 'lodge_meeting_day' ? 'Tenida' : type === 'lodge_instruction_day' ? 'Docencia' : type === 'occupancy' ? 'Ocupación protegida' : type.replaceAll('_', ' ') }
function sourceLabel(source: string) { return source === 'grand-secretariat' ? 'Gran Secretaría' : source === 'lodge-management' ? 'Gestión Logial' : source === 'ceremonies' ? 'Ceremonias' : source }
