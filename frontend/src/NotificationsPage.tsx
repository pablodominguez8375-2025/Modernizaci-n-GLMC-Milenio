import { useEffect, useMemo, useState } from 'react'
import InstitutionalIcon, { type InstitutionalIconName } from './InstitutionalIcon'
import { ConfirmAction, RowMenu } from './actionKit'
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
  const [markingAll, setMarkingAll] = useState(false)
  const markAllRead = async () => {
    setMarkingAll(true)
    setError(null)
    try {
      for (const item of items.filter(entry => entry.readAtUtc === null && !entry.actionRequired)) await notificationApi.markRead(item.id)
      load()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible marcar los avisos como leídos.')
    } finally { setMarkingAll(false) }
  }
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
        <h1>Avisos</h1>
        <p>Avisos trazables generados por ceremonias, calendario, documentos y gestión logial.</p>
      </div>
      <div className="notification-summary">
        <span><strong>{unread}</strong> pendientes</span>
        <span><strong>{mandatory}</strong> obligatorias</span>
      </div>
    </section>

    {error && <div className="error-banner" role="alert"><strong>No fue posible actualizar la bandeja.</strong><span>{error}</span></div>}


    <div className="workspace-split">
    <aside className="workspace-side" aria-label="Filtros de avisos">
    <section className="panel notification-toolbar">
      <div className="segmented-control" role="group" aria-label="Filtrar notificaciones">
        <button type="button" className={filter === 'all' ? 'active' : ''} onClick={() => setFilter('all')}>Todas</button>
        <button type="button" className={filter === 'unread' ? 'active' : ''} onClick={() => setFilter('unread')}>Pendientes{unread > 0 ? ` (${unread})` : ''}</button>
        <button type="button" className={filter === 'mandatory' ? 'active' : ''} onClick={() => setFilter('mandatory')}>Obligatorias{mandatory > 0 ? ` (${mandatory})` : ''}</button>
      </div>
      <div className="notification-toolbar-actions">
        {unread > 0 && <ConfirmAction label={markingAll ? 'Marcando…' : 'Marcar todos como leídos'} message="Se marcarán como leídos los avisos pendientes que no requieren una decisión. Los que requieren decisión quedan pendientes." confirmLabel="Sí, marcar" disabled={markingAll || loading} onConfirm={() => { void markAllRead() }} />}
        <button className="secondary-button" type="button" onClick={load} disabled={loading}>Actualizar</button>
      </div>
    </section>
    </aside>
    <div className="workspace-main">
    <section className="notification-list" aria-live="polite">
      {loading ? <div className="panel"><div className="loading-rows"><span /><span /><span /></div></div> : filtered.length === 0 ? <div className="panel empty-state"><strong>No hay avisos para este filtro.</strong><span>La bandeja está al día.</span></div> : filtered.map(item => <NotificationCard key={item.id} item={item} busy={busyId === item.id} onRead={() => { void markRead(item) }} onAction={item.actionUrl && onAction ? () => { void openAction(item) } : undefined} />)}
    </section>
    </div>
    </div>
  </>
}

function NotificationCard({ item, busy, onRead, onAction }: { item: NotificationInboxItem; busy: boolean; onRead: () => void; onAction?: () => void }) {
  const unread = item.readAtUtc === null
  return <article className={`panel notification-card${unread ? ' unread' : ''}${item.mandatory ? ' mandatory' : ''}`}>
    <div className="notification-icon" aria-hidden="true"><InstitutionalIcon name={notificationIcon(item.typeCode)} size={20} /></div>
    <div className="notification-body">
      <div className="notification-title-row">
        <div>
          <div className="notification-badges">
            {item.actionRequired ? <span className="status-pill attention">Requiere decisión</span> : item.mandatory ? <span className="status-pill attention">Obligatoria</span> : unread ? <span className="status-pill active">Sin leer</span> : null}
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
          {onAction
            ? <button type="button" onClick={onAction} disabled={busy}>{busy ? 'Abriendo…' : actionLabel(item.actionUrl, item.actionRequired)}</button>
            : unread ? <button type="button" onClick={onRead} disabled={busy}>{busy ? 'Actualizando…' : 'Marcar como leído'}</button> : null}
          {onAction && unread && <RowMenu items={[{ label: 'Marcar como leído', onSelect: onRead, disabled: busy }]} />}
          {!unread && <span className="read-confirmation"><InstitutionalIcon name="check" size={16} /> Leído</span>}
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

function notificationIcon(typeCode: string): InstitutionalIconName {
  if (typeCode.includes('ceremony')) return 'ceremony'
  if (typeCode.includes('candidate')) return 'candidate'
  if (typeCode.includes('calendar')) return 'calendar'
  if (typeCode.includes('privacy')) return 'shield'
  if (typeCode.includes('lodge')) return 'lodge'
  return 'bell'
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
