import { useEffect, useMemo, useState } from 'react'
import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'
import { type CalendarApiClient, type CalendarEvent } from './api/calendarApi'
import { type NotificationApiClient, type NotificationInboxItem } from './api/notificationApi'
import { type CandidatePortalResponse, type SessionProfile, type SystemInfo } from './api/pmgmApi'
import { greetingFor, totalPending, type PendingTarget, type PendingTask } from './rolePendingTasks'

interface DashboardPageProps {
  portal: CandidatePortalResponse | null
  systemInfo: SystemInfo | null
  profile: SessionProfile | null
  loading: boolean
  calendarApi: CalendarApiClient
  notificationApi: NotificationApiClient
  onOpenCandidates: () => void
  onOpenCalendar: () => void
  onOpenNotifications: () => void
  onOpenSecretariat?: () => void
  onOpenLodge?: () => void
  pendingTasks?: PendingTask[]
  operational?: boolean
  administrator?: boolean
  onOpenPending?: (target: PendingTarget) => void
}

export default function DashboardPage(props: DashboardPageProps) {
  const { portal, systemInfo, profile, loading, calendarApi, notificationApi } = props
  const pendingTasks = props.pendingTasks ?? []
  const operational = props.operational ?? false
  const administrator = props.administrator ?? false
  const showPendingInbox = (operational || administrator) && pendingTasks.length > 0
  const pendingTotal = totalPending(pendingTasks)
  const [events, setEvents] = useState<CalendarEvent[]>([])
  const [notifications, setNotifications] = useState<NotificationInboxItem[]>([])
  const [pulseLoading, setPulseLoading] = useState(true)

  useEffect(() => {
    let active = true
    const from = new Date()
    const to = new Date(from.getTime() + 21 * 24 * 60 * 60 * 1000)
    Promise.allSettled([
      calendarApi.getCalendar({ fromUtc: from.toISOString(), toUtc: to.toISOString() }),
      notificationApi.getMine({ unreadOnly: false, limit: 8 }),
    ]).then(results => {
      if (!active) return
      const calendarResult = results[0]
      const notificationResult = results[1]
      if (calendarResult.status === 'fulfilled') setEvents(calendarResult.value.events)
      if (notificationResult.status === 'fulfilled') setNotifications(notificationResult.value)
      setPulseLoading(false)
    })
    return () => { active = false }
  }, [calendarApi, notificationApi])

  const unread = notifications.filter(item => item.readAtUtc === null).length
  const upcoming = useMemo(() => [...events].filter(event => event.status !== 'cancelled').sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc)).slice(0, 5), [events])
  const pending = notifications.filter(item => item.readAtUtc === null).slice(0, 4)
  const nextEvent = upcoming.find(event => !event.isMasked) ?? null
  const greeting = `${greetingFor(new Date())}${profile?.displayName ? `, ${profile.displayName}` : ''}`
  const summary = showPendingInbox
    ? pendingTotal > 0 ? `Tienes ${pendingTotal} ${pendingTotal === 1 ? 'pendiente que requiere' : 'pendientes que requieren'} tu atención.` : 'No tienes pendientes por ahora. Revisa tu agenda y avisos.'
    : 'Tu agenda, avisos e insinuados publicados en un solo lugar.'

  const todayLabel = new Intl.DateTimeFormat('es-CL', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'America/Santiago' }).format(new Date())
  const agendaItems = upcoming.slice(0, 4)

  /* PMGM-UX-C: Inicio uniforme y compacto. Una franja de saludo, 4 accesos de igual tamaño y bloques de igual altura (máx. 4 ítems + «Ver todo»). */
  return <div className="dashboard-page home-compact">
    <section className="home-strip" aria-label="Saludo">
      <div className="home-strip-text">
        <h1>{greeting}</h1>
        <p>{summary}</p>
      </div>
      <p className="home-strip-meta"><span className="home-strip-date">{todayLabel}</span><span>{accessScopeLabel(profile?.accessScope)}</span></p>
      {administrator && <div className="executive-status-card home-strip-status">
        <span className="status-dot" aria-hidden="true" />
        <div><strong>{systemInfo ? 'Plataforma operativa' : loading ? 'Consultando plataforma' : 'Modo demostración'}</strong><small>{systemInfo?.runtime ?? '.NET 10'} · PostgreSQL · React</small></div>
      </div>}
    </section>

    <section className="metric-grid home-shortcuts" aria-label="Accesos rápidos">
      <Shortcut icon="calendar" value={pulseLoading ? '—' : String(upcoming.length)} label={nextEvent ? `Agenda · próxima ${shortDate(nextEvent.startsAtUtc)}` : 'Agenda · sin actividades próximas'} onClick={props.onOpenCalendar} />
      <Shortcut icon="bell" value={pulseLoading ? '—' : String(unread)} label={unread === 1 ? 'Aviso sin leer' : 'Avisos sin leer'} onClick={props.onOpenNotifications} tone={unread > 0 ? 'attention' : undefined} />
      <Shortcut icon="candidate" value={loading ? '—' : String(portal?.total ?? 0)} label="Insinuados publicados" onClick={props.onOpenCandidates} />
    </section>

    <section className={showPendingInbox ? 'home-grid has-inbox' : 'home-grid'}>
      {showPendingInbox && <article className="panel home-panel pending-inbox" id="mis-pendientes" aria-labelledby="mis-pendientes-titulo">
        <div className="panel-heading"><h2 id="mis-pendientes-titulo">Mis pendientes</h2>{pendingTotal > 0 && <span className="count-badge">{pendingTotal} por atender</span>}</div>
        <ul className="pending-list">
          {pendingTasks.map(task => <li key={task.id}><button type="button" className={task.count ? 'pending-row has-count' : 'pending-row'} onClick={() => props.onOpenPending?.(task.target)}>
            <span className="pending-row-icon" aria-hidden="true"><InstitutionalIcon name={task.icon} size={20} /></span>
            <span className="pending-row-text"><strong>{task.title}</strong><small>{task.detail}</small></span>
            {task.count !== null && <em className={task.count > 0 ? 'pending-count attention' : 'pending-count'}>{task.count > 0 ? task.count : 'Al día'}</em>}
            <span className="pending-row-chevron" aria-hidden="true">›</span>
          </button></li>)}
        </ul>
      </article>}

      <article className="panel home-panel command-panel" aria-labelledby="home-agenda-titulo">
        <div className="panel-heading"><h2 id="home-agenda-titulo">Próximas actividades</h2><button className="text-button" type="button" onClick={props.onOpenCalendar}>Ver todo</button></div>
        {pulseLoading ? <LoadingRows /> : agendaItems.length === 0 ? <EmptyState title="No hay actividades en los próximos 21 días" detail="Cuando se programe una tenida o ceremonia aparecerá aquí. Puedes revisar meses siguientes en «Ver todo»." /> : <div className="upcoming-list">{agendaItems.map(event => <UpcomingEvent key={event.id} event={event} />)}</div>}
      </article>

      <article className="panel home-panel command-panel" aria-labelledby="home-avisos-titulo">
        <div className="panel-heading"><h2 id="home-avisos-titulo">Avisos sin leer</h2><button className="text-button" type="button" onClick={props.onOpenNotifications}>Ver todo</button></div>
        {pulseLoading ? <LoadingRows /> : pending.length === 0 ? <EmptyState title="Estás al día" detail="No tienes avisos sin leer. Los avisos anteriores siguen disponibles en «Ver todo»." /> : <div className="alert-list">{pending.map(item => <DashboardAlert key={item.id} item={item} />)}</div>}
      </article>
    </section>

    {administrator && <section className="home-grid platform-grid">
      <article className="panel home-panel platform-health">
        <div className="panel-heading"><h2>Controles incorporados al núcleo</h2></div>
        <div className="control-grid">
          <ControlState title="Protección de datos" detail="Clasificación, minimización y trazabilidad Ley 21.719" />
          <ControlState title="Auditoría" detail="Eventos críticos persistidos con sujeto y resultado" />
          <ControlState title="Seguridad de acceso" detail="OIDC, RBAC y alcance Orden/Taller" />
          <ControlState title="Documentos" detail="Versionado, integridad, S3 privado y análisis malware" />
        </div>
      </article>

      <article className="panel home-panel maturity-panel">
        <div className="panel-heading"><h2>Flujos demostrables en QA</h2><strong className="maturity-value">86%</strong></div>
        <div className="maturity-meter"><span style={{ width: '86%' }} /></div>
        <p>El QA ya permite recorrer los procesos institucionales principales y demostrar integración transversal.</p>
        <div className="maturity-tags"><span>Miembros</span><span>Ceremonias</span><span>Secretaría</span><span>Gestión Logial</span><span>Calendario</span><span>Notificaciones</span><span>Documentos</span><span>Biblioteca</span></div>
      </article>
    </section>}
  </div>
}

function shortDate(value: string) {
  const date = new Date(value)
  return new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', timeZone: 'America/Santiago' }).format(date).replace('.', '')
}

function Shortcut({ icon, value, label, onClick, tone }: { icon: InstitutionalIconName; value: string; label: string; onClick: () => void; tone?: 'attention' }) {
  return <button type="button" className={`metric-card home-shortcut${tone ? ` metric-${tone}` : ''}`} onClick={onClick}>
    <span className="home-shortcut-icon" aria-hidden="true"><InstitutionalIcon name={icon} size={22} /></span>
    <strong>{value}</strong>
    <span className="home-shortcut-label">{label}</span>
  </button>
}

function UpcomingEvent({ event }: { event: CalendarEvent }) {
  const date = new Date(event.startsAtUtc)
  const day = new Intl.DateTimeFormat('es-CL', { day: '2-digit', timeZone: 'America/Santiago' }).format(date)
  const month = new Intl.DateTimeFormat('es-CL', { month: 'short', timeZone: 'America/Santiago' }).format(date).replace('.', '')
  return <div className={`upcoming-event${event.isMasked ? ' masked' : ''}`}><div className="date-block"><strong>{day}</strong><span>{month}</span></div><div><strong>{event.isMasked ? 'Ocupado' : event.title}</strong><small>{event.isMasked ? 'Detalle protegido por permisos' : event.locationDisplay ?? eventTypeLabel(event.eventType)}</small></div><span className={`status-pill ${event.status === 'confirmed' ? 'complete' : 'active'}`}>{event.status === 'confirmed' ? 'Confirmado' : statusLabel(event.status)}</span></div>
}

function DashboardAlert({ item }: { item: NotificationInboxItem }) {
  return <div className={`dashboard-alert${item.mandatory ? ' mandatory' : ''}`}><span aria-hidden="true"><InstitutionalIcon name={item.mandatory ? 'dataQuality' : 'bell'} size={14} /></span><div><strong>{item.subject}</strong><small>{truncate(item.body, 112)}</small></div><time dateTime={item.createdAtUtc}>{new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', timeZone: 'America/Santiago' }).format(new Date(item.createdAtUtc))}</time></div>
}

function ControlState({ title, detail }: { title: string; detail: string }) {
  return <div className="control-state"><span aria-hidden="true"><InstitutionalIcon name="check" size={15} /></span><div><strong>{title}</strong><small>{detail}</small></div></div>
}

function EmptyState({ title, detail }: { title: string; detail: string }) { return <div className="empty-state compact"><strong>{title}</strong><span>{detail}</span></div> }
function LoadingRows() { return <div className="loading-rows"><span /><span /><span /></div> }
function truncate(value: string, length: number) { return value.length <= length ? value : `${value.slice(0, length - 1)}…` }
function eventTypeLabel(value: string) { return value.includes('ceremony') ? 'Ceremonia' : value.includes('instruction') ? 'Docencia' : value.includes('meeting') ? 'Tenida' : value.includes('reservation') ? 'Reserva institucional' : 'Actividad institucional' }
function statusLabel(value: string) { return value === 'tentative' ? 'Tentativo' : value === 'completed' ? 'Realizado' : value === 'draft' ? 'Borrador' : value }
function accessScopeLabel(scope?: SessionProfile['accessScope']) { return scope === 'order' ? 'Orden' : scope === 'organization' ? 'Taller' : scope === 'authenticated' ? 'Autenticado' : '—' }
