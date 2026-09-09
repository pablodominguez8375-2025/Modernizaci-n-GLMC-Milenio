import { useEffect, useMemo, useState } from 'react'
import { type CalendarApiClient, type CalendarEvent } from './api/calendarApi'
import { type NotificationApiClient, type NotificationInboxItem } from './api/notificationApi'
import { type CandidatePortalResponse, type SessionProfile, type SystemInfo } from './api/pmgmApi'

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
}

export default function DashboardPage(props: DashboardPageProps) {
  const { portal, systemInfo, profile, loading, calendarApi, notificationApi } = props
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

  const completed = portal?.items.filter(item => item.elapsedDays >= item.requiredDays).length ?? 0
  const unread = notifications.filter(item => item.readAtUtc === null).length
  const upcoming = useMemo(() => [...events].filter(event => event.status !== 'cancelled').sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc)).slice(0, 5), [events])
  const pending = notifications.filter(item => item.readAtUtc === null).slice(0, 4)
  const capabilities = profile ? Object.values(profile.capabilities).filter(Boolean).length : 0

  return <>
    <section className="hero-panel executive-hero">
      <div>
        <p className="eyebrow">Centro de mando · Proyecto Milenio</p>
        <h1>Visión institucional en una sola plataforma</h1>
        <p className="lead">Seguimiento de personas, Talleres, ceremonias, agenda, comunicaciones y documentos con trazabilidad y control de acceso.</p>
        <div className="hero-assurance" aria-label="Controles activos">
          <span>✓ Ley 21.719 incorporada</span>
          <span>✓ Auditoría persistente</span>
          <span>✓ Acceso por rol y ámbito</span>
        </div>
      </div>
      <div className="executive-status-card">
        <span className="status-dot" aria-hidden="true" />
        <div><strong>{systemInfo ? 'Plataforma operativa' : loading ? 'Consultando plataforma' : 'Modo demostración'}</strong><small>{systemInfo?.runtime ?? '.NET 10'} · PostgreSQL · React</small></div>
      </div>
    </section>

    <section className="metric-grid executive-metrics">
      <MetricCard label="Avisos pendientes" value={pulseLoading ? '—' : String(unread)} detail="Bandeja personal institucional" tone={unread > 0 ? 'attention' : 'success'} />
      <MetricCard label="Próximos hitos" value={pulseLoading ? '—' : String(upcoming.length)} detail="Ventana de 21 días" />
      <MetricCard label="Insinuados publicados" value={loading ? '—' : String(portal?.total ?? 0)} detail={`${completed} con plazo cumplido`} />
      <MetricCard label="Ámbito de acceso" value={loading ? '—' : accessScopeLabel(profile?.accessScope)} detail={`${capabilities} capacidades habilitadas`} />
    </section>

    <section className="quick-actions panel">
      <div className="panel-heading"><div><p className="eyebrow">Acciones rápidas</p><h2>Operación diaria</h2></div><span className="count-badge">QA ejecutivo</span></div>
      <div className="quick-action-grid">
        <QuickAction icon="▣" title="Revisar agenda" detail="Tenidas, ceremonias y reservas" onClick={props.onOpenCalendar} />
        <QuickAction icon="✦" title="Ver notificaciones" detail={`${unread} avisos pendientes`} onClick={props.onOpenNotifications} badge={unread > 0 ? String(unread) : undefined} />
        <QuickAction icon="◎" title="Portal de insinuados" detail="Plazos y publicaciones vigentes" onClick={props.onOpenCandidates} />
        {props.onOpenSecretariat && <QuickAction icon="▤" title="Gran Secretaría" detail="Reservas y autorizaciones" onClick={props.onOpenSecretariat} />}
        {props.onOpenLodge && <QuickAction icon="□" title="Gestión Logial" detail="Tenidas, asistencia y actas" onClick={props.onOpenLodge} />}
      </div>
    </section>

    <section className="dashboard-grid enriched-dashboard-grid">
      <article className="panel command-panel">
        <div className="panel-heading"><div><p className="eyebrow">Agenda institucional</p><h2>Próximos hitos</h2></div><button className="text-button" type="button" onClick={props.onOpenCalendar}>Ver calendario</button></div>
        {pulseLoading ? <LoadingRows /> : upcoming.length === 0 ? <EmptyState title="Sin eventos próximos" detail="No existen hitos dentro de la ventana consultada." /> : <div className="upcoming-list">{upcoming.map(event => <UpcomingEvent key={event.id} event={event} />)}</div>}
      </article>

      <article className="panel command-panel">
        <div className="panel-heading"><div><p className="eyebrow">Comunicaciones</p><h2>Alertas pendientes</h2></div><button className="text-button" type="button" onClick={props.onOpenNotifications}>Abrir bandeja</button></div>
        {pulseLoading ? <LoadingRows /> : pending.length === 0 ? <EmptyState title="Bandeja al día" detail="No hay notificaciones pendientes." /> : <div className="alert-list">{pending.map(item => <DashboardAlert key={item.id} item={item} />)}</div>}
      </article>
    </section>

    <section className="dashboard-grid platform-grid">
      <article className="panel platform-health">
        <p className="eyebrow">Gobierno y seguridad</p>
        <h2>Controles incorporados al núcleo</h2>
        <div className="control-grid">
          <ControlState title="Protección de datos" detail="Clasificación, minimización y trazabilidad Ley 21.719" />
          <ControlState title="Auditoría" detail="Eventos críticos persistidos con sujeto y resultado" />
          <ControlState title="Seguridad de acceso" detail="OIDC, RBAC y alcance Orden/Taller" />
          <ControlState title="Documentos" detail="Versionado, integridad, S3 privado y análisis malware" />
        </div>
      </article>

      <article className="panel maturity-panel">
        <p className="eyebrow">Cobertura funcional QA</p>
        <h2>Flujos demostrables</h2>
        <div className="maturity-meter"><span style={{ width: '86%' }} /></div>
        <strong className="maturity-value">86%</strong>
        <p>El QA ya permite recorrer los procesos institucionales principales y demostrar integración transversal.</p>
        <div className="maturity-tags"><span>Miembros</span><span>Ceremonias</span><span>Secretaría</span><span>Gestión Logial</span><span>Calendario</span><span>Notificaciones</span><span>Documentos</span><span>Biblioteca</span></div>
      </article>
    </section>
  </>
}

function MetricCard({ label, value, detail, tone }: { label: string; value: string; detail: string; tone?: 'attention' | 'success' }) {
  return <article className={`metric-card${tone ? ` metric-${tone}` : ''}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>
}

function QuickAction({ icon, title, detail, onClick, badge }: { icon: string; title: string; detail: string; onClick: () => void; badge?: string }) {
  return <button className="quick-action" type="button" onClick={onClick}><span className="quick-action-icon" aria-hidden="true">{icon}</span><span><strong>{title}</strong><small>{detail}</small></span>{badge && <em>{badge}</em>}</button>
}

function UpcomingEvent({ event }: { event: CalendarEvent }) {
  const date = new Date(event.startsAtUtc)
  const day = new Intl.DateTimeFormat('es-CL', { day: '2-digit', timeZone: 'America/Santiago' }).format(date)
  const month = new Intl.DateTimeFormat('es-CL', { month: 'short', timeZone: 'America/Santiago' }).format(date).replace('.', '')
  return <div className={`upcoming-event${event.isMasked ? ' masked' : ''}`}><div className="date-block"><strong>{day}</strong><span>{month}</span></div><div><strong>{event.isMasked ? 'Ocupado' : event.title}</strong><small>{event.isMasked ? 'Detalle protegido por permisos' : event.locationDisplay ?? eventTypeLabel(event.eventType)}</small></div><span className={`status-pill ${event.status === 'confirmed' ? 'complete' : 'active'}`}>{event.status === 'confirmed' ? 'Confirmado' : statusLabel(event.status)}</span></div>
}

function DashboardAlert({ item }: { item: NotificationInboxItem }) {
  return <div className={`dashboard-alert${item.mandatory ? ' mandatory' : ''}`}><span aria-hidden="true">{item.mandatory ? '!' : '•'}</span><div><strong>{item.subject}</strong><small>{truncate(item.body, 112)}</small></div><time dateTime={item.createdAtUtc}>{new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', timeZone: 'America/Santiago' }).format(new Date(item.createdAtUtc))}</time></div>
}

function ControlState({ title, detail }: { title: string; detail: string }) {
  return <div className="control-state"><span aria-hidden="true">✓</span><div><strong>{title}</strong><small>{detail}</small></div></div>
}

function EmptyState({ title, detail }: { title: string; detail: string }) { return <div className="empty-state compact"><strong>{title}</strong><span>{detail}</span></div> }
function LoadingRows() { return <div className="loading-rows"><span /><span /><span /></div> }
function truncate(value: string, length: number) { return value.length <= length ? value : `${value.slice(0, length - 1)}…` }
function eventTypeLabel(value: string) { return value.includes('ceremony') ? 'Ceremonia' : value.includes('instruction') ? 'Docencia' : value.includes('meeting') ? 'Tenida' : value.includes('reservation') ? 'Reserva institucional' : 'Actividad institucional' }
function statusLabel(value: string) { return value === 'tentative' ? 'Tentativo' : value === 'completed' ? 'Realizado' : value === 'draft' ? 'Borrador' : value }
function accessScopeLabel(scope?: SessionProfile['accessScope']) { return scope === 'order' ? 'Orden' : scope === 'organization' ? 'Taller' : scope === 'authenticated' ? 'Autenticado' : '—' }
