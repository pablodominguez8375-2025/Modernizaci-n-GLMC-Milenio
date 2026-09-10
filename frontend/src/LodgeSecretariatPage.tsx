import { type FormEvent, useEffect, useMemo, useState } from 'react'
import type { OrganizationOption, PmgmApiClient } from './api/pmgmApi'
import type {
  LodgeApiClient,
  LodgeCorrespondenceChannel,
  LodgeCorrespondenceDirection,
  LodgeCorrespondenceRecord,
  LodgeMeeting,
  LodgeMeetingAgendaItem,
  LodgeSecretariatPriority,
  LodgeSecretariatTask,
} from './api/lodgeApi'
import './lodgeSecretariat.css'

export default function LodgeSecretariatPage({ api, lodgeApi }: { api: PmgmApiClient; lodgeApi: LodgeApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [correspondence, setCorrespondence] = useState<LodgeCorrespondenceRecord[]>([])
  const [tasks, setTasks] = useState<LodgeSecretariatTask[]>([])
  const [meetings, setMeetings] = useState<LodgeMeeting[]>([])
  const [meetingId, setMeetingId] = useState('')
  const [agenda, setAgenda] = useState<LodgeMeetingAgendaItem[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const [folio, setFolio] = useState('')
  const [direction, setDirection] = useState<LodgeCorrespondenceDirection>('incoming')
  const [correspondenceDate, setCorrespondenceDate] = useState(todayInChile())
  const [subject, setSubject] = useState('')
  const [counterparty, setCounterparty] = useState('')
  const [channel, setChannel] = useState<LodgeCorrespondenceChannel>('platform')
  const [externalReference, setExternalReference] = useState('')

  const [taskTitle, setTaskTitle] = useState('')
  const [taskDetail, setTaskDetail] = useState('')
  const [taskDueDate, setTaskDueDate] = useState('')
  const [taskPriority, setTaskPriority] = useState<LodgeSecretariatPriority>('normal')
  const [taskResponsible, setTaskResponsible] = useState('Secretaría')

  const [agendaTitle, setAgendaTitle] = useState('')
  const [agendaDetail, setAgendaDetail] = useState('')

  const selectedOrganization = organizations.find(item => item.id === organizationId)
  const selectedMeeting = meetings.find(item => item.id === meetingId)
  const openTasks = useMemo(() => tasks.filter(item => item.status === 'open').length, [tasks])
  const pendingCorrespondence = useMemo(() => correspondence.filter(item => item.status === 'registered').length, [correspondence])
  const pendingAgenda = useMemo(() => agenda.filter(item => item.status === 'pending').length, [agenda])

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => {
        if (!active) return
        const workshops = response.items.filter(item => item.type.toLowerCase() !== 'order')
        setOrganizations(workshops)
        setOrganizationId(workshops[0]?.id ?? '')
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    if (!organizationId) {
      setCorrespondence([]); setTasks([]); setMeetings([]); setMeetingId(''); setAgenda([])
      return
    }
    let active = true
    setWorking(true); setError(null)
    Promise.all([
      lodgeApi.getCorrespondence(organizationId),
      lodgeApi.getSecretariatTasks(organizationId),
      lodgeApi.getMeetings(organizationId),
    ]).then(([correspondenceResponse, taskResponse, meetingResponse]) => {
      if (!active) return
      setCorrespondence(correspondenceResponse.items)
      setTasks(taskResponse.items)
      setMeetings(meetingResponse.items)
      setMeetingId(current => meetingResponse.items.some(item => item.id === current) ? current : meetingResponse.items.find(item => item.status !== 'closed' && item.status !== 'cancelled')?.id ?? meetingResponse.items[0]?.id ?? '')
    }).catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setWorking(false) })
    return () => { active = false }
  }, [organizationId, lodgeApi])

  useEffect(() => {
    if (!meetingId) { setAgenda([]); return }
    let active = true
    lodgeApi.getMeetingAgenda(meetingId)
      .then(response => { if (active) setAgenda(response.items) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [meetingId, lodgeApi])

  const reloadCorrespondence = async () => setCorrespondence((await lodgeApi.getCorrespondence(organizationId)).items)
  const reloadTasks = async () => setTasks((await lodgeApi.getSecretariatTasks(organizationId)).items)
  const reloadAgenda = async () => { if (meetingId) setAgenda((await lodgeApi.getMeetingAgenda(meetingId)).items) }

  const submitCorrespondence = (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.createCorrespondence(organizationId, {
      folio, direction, correspondenceDate, subject, counterparty, channel,
      externalReference: externalReference || null,
    }).then(async () => {
      await reloadCorrespondence()
      setFolio(''); setSubject(''); setCounterparty(''); setExternalReference('')
      setMessage('Correspondencia registrada con trazabilidad institucional.')
    }).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  const submitTask = (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.createSecretariatTask(organizationId, {
      title: taskTitle,
      detail: taskDetail || null,
      dueDate: taskDueDate || null,
      priority: taskPriority,
      responsibleLabel: taskResponsible || null,
    }).then(async () => {
      await reloadTasks()
      setTaskTitle(''); setTaskDetail(''); setTaskDueDate('')
      setMessage('Pendiente de Secretaría registrado.')
    }).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  const submitAgenda = (event: FormEvent) => {
    event.preventDefault()
    if (!meetingId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.createMeetingAgendaItem(meetingId, { title: agendaTitle, detail: agendaDetail || null })
      .then(async () => {
        await reloadAgenda()
        setAgendaTitle(''); setAgendaDetail('')
        setMessage('Punto agregado a la tabla de la Tenida.')
      }).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  const advanceCorrespondence = (item: LodgeCorrespondenceRecord) => {
    const next = item.status === 'registered' ? 'processed' : 'archived'
    setWorking(true); setError(null)
    void lodgeApi.updateCorrespondenceStatus(organizationId, item.id, next)
      .then(reloadCorrespondence).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  const closeTask = (item: LodgeSecretariatTask) => {
    setWorking(true); setError(null)
    void lodgeApi.updateSecretariatTaskStatus(organizationId, item.id, 'done')
      .then(reloadTasks).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  const resolveAgenda = (item: LodgeMeetingAgendaItem, status: 'addressed' | 'deferred') => {
    setWorking(true); setError(null)
    void lodgeApi.updateMeetingAgendaStatus(meetingId, item.id, status)
      .then(reloadAgenda).catch(reason => setError(toMessage(reason))).finally(() => setWorking(false))
  }

  return <>
    <section className="page-heading lodge-secretariat-heading">
      <div><p className="eyebrow">Administración del Taller</p><h1>Secretaría Logial</h1><p>Correspondencia, pendientes y tabla de Tenida dentro del ámbito autorizado del Taller.</p></div>
      <span className="count-badge">{loading ? 'cargando…' : selectedOrganization ? organizationLabel(selectedOrganization) : 'Sin Taller'}</span>
    </section>

    {api.useMocks && <div className="lodge-secretariat-demo-note"><strong>Showcase QA:</strong> todos los registros visibles en esta pantalla son ficticios.</div>}
    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <section className="panel lodge-secretariat-toolbar">
      <label className="regularity-field"><span>Taller</span><select value={organizationId} onChange={event => { setOrganizationId(event.target.value); setMessage(null) }}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <div><strong>{selectedOrganization ? organizationLabel(selectedOrganization) : 'Seleccione un Taller'}</strong><small>Secretaría Logial no emite decretos ni autorizaciones de Gran Logia.</small></div>
    </section>

    <section className="lodge-secretariat-kpis" aria-label="Resumen Secretaría Logial">
      <Kpi label="Correspondencia por tramitar" value={pendingCorrespondence} note={`${correspondence.length} registros visibles`} />
      <Kpi label="Pendientes abiertos" value={openTasks} note={`${tasks.length} tareas registradas`} />
      <Kpi label="Puntos de tabla pendientes" value={pendingAgenda} note={selectedMeeting ? selectedMeeting.title || 'Tenida seleccionada' : 'Sin Tenida'} />
    </section>

    <section className="lodge-secretariat-grid">
      <article className="panel lodge-secretariat-card">
        <div className="panel-heading"><div><p className="eyebrow">Libro de Secretaría</p><h2>Correspondencia</h2></div><span className="count-badge">{correspondence.length}</span></div>
        <form className="regularity-form" onSubmit={submitCorrespondence}>
          <div className="lodge-secretariat-form-row">
            <label className="regularity-field"><span>Folio</span><input required maxLength={80} value={folio} onChange={event => setFolio(event.target.value)} placeholder="SEC-2026-015" /></label>
            <label className="regularity-field"><span>Fecha</span><input required type="date" value={correspondenceDate} onChange={event => setCorrespondenceDate(event.target.value)} /></label>
          </div>
          <div className="lodge-secretariat-form-row">
            <label className="regularity-field"><span>Dirección</span><select value={direction} onChange={event => setDirection(event.target.value as LodgeCorrespondenceDirection)}><option value="incoming">Recibida</option><option value="outgoing">Enviada</option></select></label>
            <label className="regularity-field"><span>Canal</span><select value={channel} onChange={event => setChannel(event.target.value as LodgeCorrespondenceChannel)}><option value="platform">Plataforma</option><option value="email">Correo</option><option value="letter">Carta</option><option value="other">Otro</option></select></label>
          </div>
          <label className="regularity-field"><span>Asunto</span><input required maxLength={500} value={subject} onChange={event => setSubject(event.target.value)} /></label>
          <label className="regularity-field"><span>Contraparte</span><input required maxLength={320} value={counterparty} onChange={event => setCounterparty(event.target.value)} /></label>
          <label className="regularity-field"><span>Referencia externa opcional</span><input maxLength={240} value={externalReference} onChange={event => setExternalReference(event.target.value)} /></label>
          <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Registrar correspondencia</button>
        </form>
        <div className="lodge-secretariat-list">{correspondence.length === 0 ? <Empty text="Sin correspondencia registrada." /> : correspondence.map(item => <article key={item.id} className="lodge-secretariat-row"><div><div className="lodge-secretariat-row-title"><strong>{item.folio}</strong><Status text={correspondenceStatus(item.status)} state={item.status === 'archived' ? 'muted' : item.status === 'processed' ? 'good' : 'pending'} /></div><h3>{item.subject}</h3><p>{directionLabel(item.direction)} · {item.counterparty} · {formatDate(item.correspondenceDate)}</p></div>{item.status !== 'archived' && <button className="regularity-secondary" type="button" disabled={working} onClick={() => advanceCorrespondence(item)}>{item.status === 'registered' ? 'Marcar tramitada' : 'Archivar'}</button>}</article>)}</div>
      </article>

      <article className="panel lodge-secretariat-card">
        <div className="panel-heading"><div><p className="eyebrow">Seguimiento</p><h2>Pendientes</h2></div><span className="count-badge">{openTasks} abiertos</span></div>
        <form className="regularity-form" onSubmit={submitTask}>
          <label className="regularity-field"><span>Título</span><input required maxLength={500} value={taskTitle} onChange={event => setTaskTitle(event.target.value)} /></label>
          <label className="regularity-field"><span>Detalle opcional</span><textarea rows={3} maxLength={2000} value={taskDetail} onChange={event => setTaskDetail(event.target.value)} /></label>
          <div className="lodge-secretariat-form-row">
            <label className="regularity-field"><span>Vencimiento</span><input type="date" value={taskDueDate} onChange={event => setTaskDueDate(event.target.value)} /></label>
            <label className="regularity-field"><span>Prioridad</span><select value={taskPriority} onChange={event => setTaskPriority(event.target.value as LodgeSecretariatPriority)}><option value="low">Baja</option><option value="normal">Normal</option><option value="high">Alta</option></select></label>
          </div>
          <label className="regularity-field"><span>Responsable/cargo</span><input maxLength={160} value={taskResponsible} onChange={event => setTaskResponsible(event.target.value)} /></label>
          <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Agregar pendiente</button>
        </form>
        <div className="lodge-secretariat-list">{tasks.length === 0 ? <Empty text="Sin pendientes registrados." /> : tasks.map(item => <article key={item.id} className="lodge-secretariat-row"><div><div className="lodge-secretariat-row-title"><strong>{priorityLabel(item.priority)}</strong><Status text={taskStatus(item.status)} state={item.status === 'open' ? 'pending' : item.status === 'done' ? 'good' : 'muted'} /></div><h3>{item.title}</h3><p>{item.responsibleLabel || 'Sin responsable'}{item.dueDate ? ` · vence ${formatDate(item.dueDate)}` : ''}</p>{item.detail && <small>{item.detail}</small>}</div>{item.status === 'open' && <button className="regularity-secondary" type="button" disabled={working} onClick={() => closeTask(item)}>Completar</button>}</article>)}</div>
      </article>
    </section>

    <section className="panel lodge-secretariat-agenda">
      <div className="panel-heading"><div><p className="eyebrow">Orden de los trabajos</p><h2>Tabla de Tenida</h2><p>Los puntos se vinculan a una Tenida existente y conservan su estado.</p></div><label className="regularity-field lodge-secretariat-meeting-select"><span>Tenida</span><select value={meetingId} onChange={event => setMeetingId(event.target.value)}><option value="">Seleccione…</option>{meetings.map(item => <option key={item.id} value={item.id}>{formatDate(item.meetingDate)} · {item.title || item.meetingType}</option>)}</select></label></div>
      {selectedMeeting && <div className="lodge-secretariat-meeting-context"><strong>{selectedMeeting.title || 'Tenida'}</strong><span>{formatDate(selectedMeeting.meetingDate)} · {selectedMeeting.status === 'closed' ? 'Cerrada' : 'Programada'}</span></div>}
      <form className="lodge-secretariat-agenda-form" onSubmit={submitAgenda}>
        <label className="regularity-field"><span>Nuevo punto</span><input required maxLength={500} value={agendaTitle} onChange={event => setAgendaTitle(event.target.value)} /></label>
        <label className="regularity-field"><span>Detalle opcional</span><input maxLength={3000} value={agendaDetail} onChange={event => setAgendaDetail(event.target.value)} /></label>
        <button className="regularity-primary" type="submit" disabled={working || !meetingId || selectedMeeting?.status === 'closed' || selectedMeeting?.status === 'cancelled'}>Agregar a tabla</button>
      </form>
      <div className="lodge-secretariat-agenda-list">{agenda.length === 0 ? <Empty text="La Tenida no tiene puntos de tabla." /> : agenda.map(item => <article key={item.id}><span className="lodge-secretariat-position">{item.position}</span><div><div className="lodge-secretariat-row-title"><h3>{item.title}</h3><Status text={agendaStatus(item.status)} state={item.status === 'pending' ? 'pending' : item.status === 'addressed' ? 'good' : 'muted'} /></div>{item.detail && <p>{item.detail}</p>}</div>{item.status === 'pending' && <div className="lodge-secretariat-actions"><button type="button" className="regularity-secondary" disabled={working} onClick={() => resolveAgenda(item, 'addressed')}>Tratado</button><button type="button" className="regularity-secondary" disabled={working} onClick={() => resolveAgenda(item, 'deferred')}>Postergar</button></div>}</article>)}</div>
    </section>
  </>
}

function Kpi({ label, value, note }: { label: string; value: number; note: string }) { return <article><span>{label}</span><strong>{value}</strong><small>{note}</small></article> }
function Status({ text, state }: { text: string; state: 'good' | 'pending' | 'muted' }) { return <span className={`lodge-secretariat-status ${state}`}>{text}</span> }
function Empty({ text }: { text: string }) { return <div className="empty-state"><strong>{text}</strong></div> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number && !item.name.includes(item.number) ? ` · Nº ${item.number}` : ''}` }
function directionLabel(value: LodgeCorrespondenceDirection) { return value === 'incoming' ? 'Recibida' : 'Enviada' }
function correspondenceStatus(value: LodgeCorrespondenceRecord['status']) { return value === 'registered' ? 'Registrada' : value === 'processed' ? 'Tramitada' : 'Archivada' }
function taskStatus(value: LodgeSecretariatTask['status']) { return value === 'open' ? 'Abierto' : value === 'done' ? 'Completado' : 'Cancelado' }
function agendaStatus(value: LodgeMeetingAgendaItem['status']) { return value === 'pending' ? 'Pendiente' : value === 'addressed' ? 'Tratado' : 'Postergado' }
function priorityLabel(value: LodgeSecretariatPriority) { return value === 'high' ? 'Prioridad alta' : value === 'low' ? 'Prioridad baja' : 'Prioridad normal' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
