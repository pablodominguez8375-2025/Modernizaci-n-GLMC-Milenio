import { useEffect, useMemo, useState } from 'react'
import { type NotificationApiClient, type NotificationInboxItem } from './api/notificationApi'

interface NotificationsPageProps {
  notificationApi: NotificationApiClient
  onAction?: (actionUrl: string) => void
}

export default function NotificationsPage({ notificationApi, onAction }: NotificationsPageProps) {
  const [items, setItems] = useState<NotificationInboxItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState<'all' | 'unread' | 'mandatory'>('all')
  const [busyId, setBusyId] = useState<string | null>(null)

  const load = () => {
    setLoading(true)
    setError(null)
    notificationApi.getMine({ unreadOnly: false, limit: 50 })
      .then(setItems)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'No fue posible cargar las notificaciones.'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [notificationApi])

  const unread = items.filter(item => item.readAtUtc === null).length
  const mandatory = items.filter(item => item.mandatory).length
  const filtered = useMemo(() => items.filter(item => {
    if (filter === 'unread') return item.readAtUtc === null
    if (filter === 'mandatory') return item.mandatory
    return true
  }), [filter, items])

  const markRead = async (item: NotificationInboxItem) => {
    if (item.readAtUtc) return true
    setBusyId(item.id)
    try {
      await notificationApi.markRead(item.id)
      setItems(current => current.map(value => value.id === item.id ? { ...value, readAtUtc: new Date().toISOString() } : value))
      return true
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible actualizar la notificación.')
      return false
    } finally {
      setBusyId(null)
    }
  }

  const openAction = async (item: NotificationInboxItem) => {
    if (!item.actionUrl || !onAction) return
    const updated = await markRead(item)
    if (updated) onAction(item.actionUrl)
  }

  return <>
    <section className="page-heading notification-heading">
      <div>
        <p className="eyebrow">Centro de comunicaciones</p>
        <h1>Notificaciones institucionales</h1>
        <p>Avisos trazables generados por ceremonias, calendario, documentos y gestión logial.</p>
      </div>
      <div className="notification-summary">
        <span><strong>{unread}</strong> pendientes</span>
        <span><strong>{mandatory}</strong> obligatorias</span>
      </div>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible actualizar la bandeja.</strong><span>{error}</span></div>}

    <section className="panel notification-toolbar">
      <div className="segmented-control" role="group" aria-label="Filtrar notificaciones">
        <button type="button" className={filter === 'all' ? 'active' : ''} onClick={() => setFilter('all')}>Todas</button>
        <button type="button" className={filter === 'unread' ? 'active' : ''} onClick={() => setFilter('unread')}>Pendientes</button>
        <button type="button" className={filter === 'mandatory' ? 'active' : ''} onClick={() => setFilter('mandatory')}>Obligatorias</button>
      </div>
      <button className="secondary-button" type="button" onClick={load} disabled={loading}>Actualizar</button>
    </section>

    <section className="notification-list" aria-live="polite">
      {loading ? <div className="panel"><div className="loading-rows"><span /><span /><span /></div></div> : filtered.length === 0 ? <div className="panel empty-state"><strong>No hay avisos para este filtro.</strong><span>La bandeja está al día.</span></div> : filtered.map(item => <NotificationCard key={item.id} item={item} busy={busyId === item.id} onRead={() => { void markRead(item) }} onAction={item.actionUrl && onAction ? () => { void openAction(item) } : undefined} />)}
    </section>
  </>
}

function NotificationCard({ item, busy, onRead, onAction }: { item: NotificationInboxItem; busy: boolean; onRead: () => void; onAction?: () => void }) {
  const unread = item.readAtUtc === null
  return <article className={`panel notification-card${unread ? ' unread' : ''}${item.mandatory ? ' mandatory' : ''}`}>
    <div className="notification-icon" aria-hidden="true">{notificationIcon(item.typeCode)}</div>
    <div className="notification-body">
      <div className="notification-title-row">
        <div>
          <div className="notification-badges">
            {unread && <span className="status-pill active">Pendiente</span>}
            {item.mandatory && <span className="status-pill attention">Obligatoria</span>}
            {item.actionRequired && <span className="status-pill attention">Requiere decisión</span>}
            <span className="classification-badge">{classificationLabel(item.classification)}</span>
          </div>
          <h2>{item.subject}</h2>
        </div>
        <time dateTime={item.createdAtUtc}>{formatRelativeDate(item.createdAtUtc)}</time>
      </div>
      <p>{item.body}</p>
      <div className="notification-footer">
        <span>{typeLabel(item.typeCode)}</span>
        <div className="notification-actions">
          {onAction && <button className="secondary-button" type="button" onClick={onAction} disabled={busy}>{busy ? 'Abriendo…' : actionLabel(item.actionUrl, item.actionRequired)}</button>}
          {unread ? <button type="button" onClick={onRead} disabled={busy}>{busy ? 'Actualizando…' : 'Marcar como leído'}</button> : <span className="read-confirmation">✓ Leído</span>}
        </div>
      </div>
    </div>
  </article>
}

function actionLabel(actionUrl: string | null, actionRequired = false) {
  if (actionRequired) return 'Revisar y decidir'
  if (actionUrl === '/candidates') return 'Ver insinuados'
  if (actionUrl === '/calendar') return 'Ver calendario'
  if (actionUrl === '/ceremonies') return 'Ver ceremonias'
  if (actionUrl === '/lodge') return 'Ir a Gestión Logial'
  if (actionUrl === '/documents') return 'Ver documentos'
  return 'Abrir'
}

function notificationIcon(typeCode: string) {
  if (typeCode.includes('ceremony')) return '◉'
  if (typeCode.includes('candidate')) return '◎'
  if (typeCode.includes('calendar')) return '▣'
  if (typeCode.includes('privacy')) return '◇'
  if (typeCode.includes('lodge')) return '□'
  return '✦'
}

function typeLabel(typeCode: string) {
  if (typeCode.includes('candidate')) return 'Insinuados'
  if (typeCode.includes('ceremony')) return 'Ceremonias'
  if (typeCode.includes('calendar')) return 'Calendario'
  if (typeCode.includes('privacy')) return 'Privacidad'
  if (typeCode.includes('lodge')) return 'Gestión Logial'
  return 'Institucional'
}

function classificationLabel(value: string) {
  return value === 'confidential' ? 'Confidencial' : value === 'restricted' ? 'Restringida' : 'Interna'
}

function formatRelativeDate(value: string) {
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value))
}
