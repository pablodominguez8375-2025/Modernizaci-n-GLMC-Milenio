import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import {
  type LodgeApiClient,
  type LodgeAttendanceCurrent,
  type LodgeAttendanceStatus,
  type LodgeGrade,
  type LodgeMeeting,
  type LodgeMeetingType,
  type LodgeMemberOption,
  type LodgeMinute,
} from './api/lodgeApi'
import './regularity.css'
import './lodgeManagement.css'

export default function LodgeManagementPage({ api, lodgeApi }: { api: PmgmApiClient; lodgeApi: LodgeApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [meetings, setMeetings] = useState<LodgeMeeting[]>([])
  const [selectedMeetingId, setSelectedMeetingId] = useState('')
  const [members, setMembers] = useState<LodgeMemberOption[]>([])
  const [attendance, setAttendance] = useState<LodgeAttendanceCurrent[]>([])
  const [minutes, setMinutes] = useState<LodgeMinute[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [meetingDate, setMeetingDate] = useState(todayInChile())
  const [meetingType, setMeetingType] = useState<LodgeMeetingType>('regular')
  const [grade, setGrade] = useState<LodgeGrade>('all')
  const [title, setTitle] = useState('')
  const [memberId, setMemberId] = useState('')
  const [attendanceStatus, setAttendanceStatus] = useState<LodgeAttendanceStatus>('present')
  const [excuseReason, setExcuseReason] = useState('')
  const [minuteContent, setMinuteContent] = useState('')

  const selectedMeeting = useMemo(() => meetings.find(item => item.id === selectedMeetingId) ?? null, [meetings, selectedMeetingId])
  const selectedOrganization = organizations.find(item => item.id === organizationId)

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
    if (!organizationId) { setMeetings([]); setMembers([]); setSelectedMeetingId(''); return }
    let active = true
    setWorking(true); setError(null)
    Promise.all([lodgeApi.getMeetings(organizationId), lodgeApi.getMemberOptions(organizationId)])
      .then(([meetingResponse, memberResponse]) => {
        if (!active) return
        setMeetings(meetingResponse.items)
        setMembers(memberResponse.items)
        setMemberId(memberResponse.items[0]?.id ?? '')
        setSelectedMeetingId(current => meetingResponse.items.some(item => item.id === current) ? current : meetingResponse.items[0]?.id ?? '')
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setWorking(false) })
    return () => { active = false }
  }, [lodgeApi, organizationId])

  useEffect(() => {
    if (!selectedMeetingId) { setAttendance([]); setMinutes([]); return }
    let active = true
    Promise.all([lodgeApi.getAttendance(selectedMeetingId), lodgeApi.getMinutes(selectedMeetingId)])
      .then(([attendanceResponse, minuteResponse]) => {
        if (!active) return
        setAttendance(attendanceResponse.items)
        setMinutes(minuteResponse.items)
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [lodgeApi, selectedMeetingId])

  const refreshMeetings = async (preferId?: string) => {
    if (!organizationId) return
    const response = await lodgeApi.getMeetings(organizationId)
    setMeetings(response.items)
    const nextId = preferId && response.items.some(item => item.id === preferId) ? preferId : response.items[0]?.id ?? ''
    setSelectedMeetingId(nextId)
  }

  const refreshSelected = async () => {
    if (!selectedMeetingId) return
    const [attendanceResponse, minuteResponse] = await Promise.all([
      lodgeApi.getAttendance(selectedMeetingId),
      lodgeApi.getMinutes(selectedMeetingId),
    ])
    setAttendance(attendanceResponse.items)
    setMinutes(minuteResponse.items)
  }

  const createMeeting = (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.createMeeting(organizationId, { meetingDate, meetingType, grade, title: title.trim() || null })
      .then(async meeting => {
        await refreshMeetings(meeting.id)
        setTitle('')
        setMessage('Tenida creada y registrada en auditoría institucional.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const recordAttendance = (event: FormEvent) => {
    event.preventDefault()
    if (!selectedMeetingId || !memberId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.recordAttendance(selectedMeetingId, {
      memberId,
      status: attendanceStatus,
      excuseReason: attendanceStatus === 'excused' ? excuseReason.trim() || null : null,
    })
      .then(async () => {
        const response = await lodgeApi.getAttendance(selectedMeetingId)
        setAttendance(response.items)
        setExcuseReason('')
        setMessage('Asistencia registrada. Las rectificaciones conservan el historial anterior.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const createMinute = (event: FormEvent) => {
    event.preventDefault()
    if (!selectedMeetingId || !minuteContent.trim()) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.createMinute(selectedMeetingId, minuteContent)
      .then(async minute => {
        const response = await lodgeApi.getMinutes(selectedMeetingId)
        setMinutes(response.items)
        setMinuteContent('')
        setMessage(`Versión ${minute.version} del acta creada sin reemplazar versiones anteriores.`)
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const approveMinute = (minuteId: string) => {
    if (!selectedMeetingId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.approveMinute(selectedMeetingId, minuteId)
      .then(async minute => {
        const response = await lodgeApi.getMinutes(selectedMeetingId)
        setMinutes(response.items)
        setMessage(`Versión ${minute.version} aprobada. La aprobación anterior, si existía, quedó conservada como reemplazada.`)
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const closeMeeting = () => {
    if (!selectedMeetingId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.closeMeeting(selectedMeetingId)
      .then(async () => {
        await refreshMeetings(selectedMeetingId)
        await refreshSelected()
        setMessage('Tenida cerrada. La API ya no admite nuevas asistencias para esta Tenida.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const meetingClosed = selectedMeeting?.status === 'closed' || selectedMeeting?.status === 'cancelled'

  return <>
    <section className="page-heading">
      <div><p className="eyebrow">Secretaría de Taller</p><h1>Gestión Logial</h1><p>Tenidas, asistencia y actas versionadas dentro del ámbito autorizado del Taller.</p></div>
      <span className="count-badge">{loading ? 'cargando…' : `${meetings.length} Tenidas`}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <section className="lodge-toolbar panel">
      <label className="regularity-field"><span>Taller</span><select value={organizationId} onChange={event => { setOrganizationId(event.target.value); setMessage(null) }}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
      <div className="lodge-context"><strong>{selectedOrganization ? organizationLabel(selectedOrganization) : 'Sin Taller seleccionado'}</strong><small>Actas y asistencia no se proyectan automáticamente a otros órganos de la Orden.</small></div>
    </section>

    <section className="lodge-layout">
      <article className="panel">
        <p className="eyebrow">Agenda del Taller</p><h2>Tenidas</h2>
        <form className="regularity-form" onSubmit={createMeeting}>
          <label className="regularity-field"><span>Fecha · Chile</span><input type="date" required value={meetingDate} onChange={event => setMeetingDate(event.target.value)} /></label>
          <div className="lodge-form-row">
            <label className="regularity-field"><span>Tipo</span><select value={meetingType} onChange={event => setMeetingType(event.target.value as LodgeMeetingType)}>{meetingTypeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
            <label className="regularity-field"><span>Grado</span><select value={grade} onChange={event => setGrade(event.target.value as LodgeGrade)}>{gradeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          </div>
          <label className="regularity-field"><span>Título opcional</span><input maxLength={500} value={title} onChange={event => setTitle(event.target.value)} /></label>
          <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Crear Tenida</button>
        </form>

        <div className="lodge-meeting-list">
          {meetings.length === 0 ? <div className="empty-state"><strong>No hay Tenidas registradas.</strong></div> : meetings.map(meeting => <button key={meeting.id} className={meeting.id === selectedMeetingId ? 'lodge-meeting selected' : 'lodge-meeting'} type="button" onClick={() => setSelectedMeetingId(meeting.id)}><div><strong>{meeting.title || meetingTypeLabel(meeting.meetingType)}</strong><small>{formatDateOnly(meeting.meetingDate)} · {gradeLabel(meeting.grade)}</small></div><span className={meeting.status === 'closed' ? 'regularity-status blocked' : 'regularity-status good'}>{meetingStatusLabel(meeting.status)}</span></button>)}
        </div>
      </article>

      <article className="panel lodge-detail">
        {!selectedMeeting ? <div className="empty-state"><strong>Seleccione una Tenida.</strong></div> : <>
          <div className="panel-heading"><div><p className="eyebrow">Tenida seleccionada</p><h2>{selectedMeeting.title || meetingTypeLabel(selectedMeeting.meetingType)}</h2><p>{formatDateOnly(selectedMeeting.meetingDate)} · {meetingTypeLabel(selectedMeeting.meetingType)} · {gradeLabel(selectedMeeting.grade)}</p></div><button className="regularity-secondary" type="button" disabled={working || meetingClosed} onClick={closeMeeting}>Cerrar Tenida</button></div>

          <section className="lodge-section">
            <div><p className="eyebrow">Registro vigente</p><h3>Asistencia</h3></div>
            <form className="regularity-form" onSubmit={recordAttendance}>
              <label className="regularity-field"><span>Hermano/a</span><select required value={memberId} disabled={meetingClosed} onChange={event => setMemberId(event.target.value)}><option value="">Seleccione…</option>{members.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>
              <div className="lodge-form-row">
                <label className="regularity-field"><span>Estado</span><select value={attendanceStatus} disabled={meetingClosed} onChange={event => setAttendanceStatus(event.target.value as LodgeAttendanceStatus)}>{attendanceOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                {attendanceStatus === 'excused' && <label className="regularity-field"><span>Justificación</span><input maxLength={1000} value={excuseReason} disabled={meetingClosed} onChange={event => setExcuseReason(event.target.value)} /></label>}
              </div>
              <button className="regularity-primary" type="submit" disabled={working || meetingClosed || !memberId}>Registrar asistencia</button>
            </form>
            <div className="lodge-attendance-list">{attendance.length === 0 ? <small>Sin asistencia registrada.</small> : attendance.map(item => <div key={item.memberId}><div><strong>{item.displayName}</strong>{item.excuseReason && <small>{item.excuseReason}</small>}</div><span className={attendanceClass(item.status)}>{attendanceLabel(item.status)}</span></div>)}</div>
          </section>

          <section className="lodge-section">
            <div><p className="eyebrow">Documento histórico</p><h3>Actas versionadas</h3></div>
            <form className="regularity-form" onSubmit={createMinute}>
              <label className="regularity-field"><span>Nueva versión del acta</span><textarea rows={7} maxLength={20000} value={minuteContent} onChange={event => setMinuteContent(event.target.value)} placeholder="Redacte el contenido de la nueva versión…" /></label>
              <button className="regularity-primary" type="submit" disabled={working || !minuteContent.trim()}>Crear nueva versión</button>
            </form>
            <div className="lodge-minute-list">{minutes.length === 0 ? <small>Sin versiones de acta.</small> : minutes.map(minute => <article key={minute.id}><div className="lodge-minute-heading"><strong>Versión {minute.version}</strong><span className={minute.status === 'approved' ? 'regularity-status good' : minute.status === 'superseded' ? 'regularity-status blocked' : 'regularity-status pending'}>{minuteStatusLabel(minute.status)}</span></div><p>{minute.content}</p><small>Creada {formatChile(minute.createdAtUtc)}{minute.approvedAtUtc ? ` · aprobada ${formatChile(minute.approvedAtUtc)}` : ''}</small>{minute.status === 'draft' && <button className="regularity-secondary" type="button" disabled={working} onClick={() => approveMinute(minute.id)}>Aprobar esta versión</button>}</article>)}</div>
          </section>
        </>}
      </article>
    </section>
  </>
}

const meetingTypeOptions = [['regular', 'Regular'], ['solemn', 'Solemne'], ['instruction', 'Instrucción'], ['anniversary', 'Aniversario'], ['funeral', 'Fúnebre'], ['special', 'Especial']] as const
const gradeOptions = [['all', 'Todos los grados'], ['apprentice', 'Aprendiz'], ['fellowcraft', 'Compañero'], ['master', 'Maestro']] as const
const attendanceOptions = [['present', 'Presente'], ['excused', 'Justificado'], ['absent', 'Ausente']] as const
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function meetingTypeLabel(value: LodgeMeetingType) { return meetingTypeOptions.find(([key]) => key === value)?.[1] ?? value }
function gradeLabel(value: LodgeGrade) { return gradeOptions.find(([key]) => key === value)?.[1] ?? value }
function attendanceLabel(value: LodgeAttendanceStatus) { return attendanceOptions.find(([key]) => key === value)?.[1] ?? value }
function meetingStatusLabel(value: string) { return value === 'closed' ? 'Cerrada' : value === 'open' ? 'Abierta' : value === 'cancelled' ? 'Cancelada' : 'Programada' }
function minuteStatusLabel(value: string) { return value === 'approved' ? 'Aprobada' : value === 'superseded' ? 'Reemplazada' : 'Borrador' }
function attendanceClass(value: LodgeAttendanceStatus) { return value === 'present' ? 'regularity-status good' : value === 'excused' ? 'regularity-status pending' : 'regularity-status blocked' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
