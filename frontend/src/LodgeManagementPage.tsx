import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import {
  type LodgeApiClient,
  type LodgeAttendanceCurrent,
  type LodgeAttendanceStatus,
  type LodgeGrade,
  type LodgeInstruction,
  type LodgeMeeting,
  type LodgeMeetingType,
  type LodgeMemberOption,
  type LodgeMinute,
} from './api/lodgeApi'
import './regularity.css'
import './lodgeManagement.css'
import './lodgeManagementAreas.css'
import LodgeBallotPanel from './LodgeBallotPanel'
import MinuteExtractEditor from './MinuteExtractEditor'

export const lodgeCockpitDemoData = {
  lodge: {
    name: 'Taller Demostrativo Nº 23',
    orient: 'Santiago · demo',
    rite: 'Rito institucional · demo',
    constitution: '12 de octubre de 2013',
    meetings: '2º y 4º sábado · 19:00 hrs.',
    temple: 'Templo Demostrativo',
    email: 'taller.demo@ejemplo.cl',
  },
  members: { active: 37, masters: 16, fellowcraft: 12, apprentices: 7, honorary: 2 },
  officers: [
    ['Venerable Maestro', 'H∴ Autoridad Demo'],
    ['Ex Venerable Maestro', 'H∴ Consejero Demo'],
    ['Primer Vigilante', 'H∴ Primer Vigilante Demo'],
    ['Segundo Vigilante', 'H∴ Segundo Vigilante Demo'],
    ['Secretaría', 'H∴ Secretaría Demo'],
    ['Tesorería', 'H∴ Tesorería Demo'],
    ['Hospitalaria', 'H∴ Hospitalaria Demo'],
  ],
  managementAreas: [
    ['Secretaría del Taller', 'Secretario/a', 'Tenidas, asistencia, actas, correspondencia y solicitudes.'],
    ['Tesorería del Taller', 'Tesorero/a', 'Cuotas, abonos, comprobantes, libro mayor y rendición a Gran Tesorería.'],
    ['Hospitalaria del Taller', 'Hospitalario/a', 'Bolso, ayudas, aportes, reposiciones y rendición a Gran Hospitalaria.'],
    ['Docencia e instrucción', 'Vigilantes y Ex Venerable Maestro', 'Plan por grado, sesiones, asistencia y seguimiento formativo.'],
  ],
  instruction: [
    ['Simbología y rito', 80],
    ['Historia de la Orden', 65],
    ['Ética y filosofía', 70],
    ['Trabajo en Taller', 50],
  ],
  tasks: { pending: 5, inProgress: 3, completed: 18 },
  notifications: [
    'Nueva circular demostrativa disponible',
    'Material de docencia autorizado',
    'Recordatorio de preparación de tenida',
  ],
} as const

export const instructionResponsibilityByGrade = {
  apprentice: 'Segundo Vigilante',
  fellowcraft: 'Primer Vigilante',
  master: 'Ex Venerable Maestro',
} as const

export default function LodgeManagementPage({ api, lodgeApi }: { api: PmgmApiClient; lodgeApi: LodgeApiClient }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [meetings, setMeetings] = useState<LodgeMeeting[]>([])
  const [selectedMeetingId, setSelectedMeetingId] = useState('')
  const [members, setMembers] = useState<LodgeMemberOption[]>([])
  const [attendance, setAttendance] = useState<LodgeAttendanceCurrent[]>([])
  const [minutes, setMinutes] = useState<LodgeMinute[]>([])
  const [instructions, setInstructions] = useState<LodgeInstruction[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showOperations, setShowOperations] = useState(false)
  const [instructionDate, setInstructionDate] = useState(todayInChile())
  const [instructionGrade, setInstructionGrade] = useState<Exclude<LodgeGrade, 'all'>>('apprentice')
  const [instructionTopic, setInstructionTopic] = useState('')
  const [instructionAttendance, setInstructionAttendance] = useState<Record<string, 'present' | 'absent'>>({})
  const [instructionConfirmation, setInstructionConfirmation] = useState<string | null>(null)
  const [selectedInstructionId, setSelectedInstructionId] = useState('')

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
  const nextMeeting = useMemo(() => {
    const today = todayInChile()
    return [...meetings].filter(item => item.meetingDate >= today && item.status !== 'cancelled').sort((a, b) => a.meetingDate.localeCompare(b.meetingDate))[0] ?? meetings[0] ?? null
  }, [meetings])
  const attendanceSummary = useMemo(() => {
    const present = attendance.filter(item => item.status === 'present').length
    const absent = attendance.filter(item => item.status === 'absent').length
    const excused = attendance.filter(item => item.status === 'excused').length
    const total = attendance.length
    return { present, absent, excused, total, percentage: total ? Math.round((present / total) * 100) : api.useMocks ? 89 : 0 }
  }, [attendance, api.useMocks])

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
    Promise.all([lodgeApi.getMeetings(organizationId), lodgeApi.getMemberOptions(organizationId), lodgeApi.getInstructions(organizationId)])
      .then(([meetingResponse, memberResponse, instructionResponse]) => {
        if (!active) return
        setMeetings(meetingResponse.items)
        setMembers(memberResponse.items)
        setInstructions(instructionResponse.items)
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

  const generateMinuteExtract = () => {
    if (!selectedMeetingId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.generateMinuteExtract(selectedMeetingId)
      .then(extract => { setMinuteContent(extract.content); setMessage(`Borrador generado desde ${extract.attendeeCount} asistentes y ${extract.ballotCount} escrutinios vigentes.`) })
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
        setMessage('Tenida realizada y cerrada. Ya puede registrar asistencia, escrutinios y acta.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const meetingFinalized = selectedMeeting?.status === 'closed' || selectedMeeting?.status === 'cancelled'
  const canRecordMeetingAttendance = selectedMeeting?.status === 'closed'
  const lodgeName = api.useMocks ? lodgeCockpitDemoData.lodge.name : selectedOrganization ? organizationLabel(selectedOrganization) : 'Taller autorizado'
  const memberCount = api.useMocks ? lodgeCockpitDemoData.members.active : members.length
  const instructionResponsible = instructionResponsibilityByGrade[instructionGrade]

  const saveInstruction = async (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId || !instructionTopic.trim() || members.length === 0) return
    setWorking(true); setError(null); setInstructionConfirmation(null)
    try {
      const instruction = await lodgeApi.createInstruction(organizationId, { instructionDate, grade: instructionGrade, topic: instructionTopic })
      const response = await lodgeApi.getInstructions(organizationId)
      setInstructions(response.items)
      setSelectedInstructionId(instruction.id)
      setInstructionConfirmation(`Instrucción programada · ${gradeLabel(instructionGrade)} · responsable: ${instructionResponsible}. Ya está disponible para calendario.`)
      setInstructionTopic('')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  const completeInstructionAndRecordAttendance = async () => {
    if (!selectedInstructionId) return
    setWorking(true); setError(null); setInstructionConfirmation(null)
    try {
      await lodgeApi.completeInstruction(selectedInstructionId)
      const items = members.map(member => ({ memberId: member.id, status: instructionAttendance[member.id] ?? 'present' as const }))
      await lodgeApi.recordInstructionAttendance(selectedInstructionId, items)
      const response = await lodgeApi.getInstructions(organizationId)
      setInstructions(response.items)
      setInstructionConfirmation(`Instrucción realizada · ${items.filter(item => item.status === 'present').length} asistentes registrados.`)
      setSelectedInstructionId('')
      setInstructionAttendance({})
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  return <div className="lodge-product-page">
    <section className="lodge-product-heading">
      <div>
        <p className="lodge-product-breadcrumb">Gestión Logial <span>›</span> Vista general</p>
        <h1>Gestión Logial</h1>
        <p>Herramientas para la administración y operación del Taller en un solo lugar.</p>
      </div>
      <div className="lodge-heading-actions">
        <label className="lodge-organization-select"><span>Taller</span><select value={organizationId} onChange={event => { setOrganizationId(event.target.value); setMessage(null) }}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>
        <button className="lodge-gold-button" type="button" onClick={() => { setShowOperations(true); document.getElementById('lodge-operations')?.scrollIntoView({ behavior: 'smooth' }) }}>Registrar tenida</button>
      </div>
    </section>

    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <section className="lodge-cockpit-grid">
      <article className="lodge-product-card lodge-profile-summary">
        <div className="lodge-card-heading"><div><p className="lodge-kicker">Ficha del Taller</p><h2>{lodgeName}</h2></div><span className="lodge-live-chip">{api.useMocks ? 'Datos ficticios' : 'Ámbito activo'}</span></div>
        <div className="lodge-profile-body"><span className="lodge-seal">M</span><div className="lodge-profile-fields"><LodgeDatum label="Oriente" value={api.useMocks ? lodgeCockpitDemoData.lodge.orient : 'Según ficha institucional'} /><LodgeDatum label="Rito" value={api.useMocks ? lodgeCockpitDemoData.lodge.rite : 'Según ficha institucional'} /><LodgeDatum label="Constitución" value={api.useMocks ? lodgeCockpitDemoData.lodge.constitution : 'Según ficha institucional'} /><LodgeDatum label="Reuniones" value={api.useMocks ? lodgeCockpitDemoData.lodge.meetings : 'Según calendario'} /><LodgeDatum label="Templo" value={api.useMocks ? lodgeCockpitDemoData.lodge.temple : 'Según reserva'} /><LodgeDatum label="Correo" value={api.useMocks ? lodgeCockpitDemoData.lodge.email : 'Según ficha institucional'} /></div></div>
      </article>

      <article className="lodge-product-card lodge-member-summary">
        <div className="lodge-card-heading"><div><p className="lodge-kicker">Miembros del Taller</p><h2>{memberCount} activos</h2></div><span className="lodge-link-label">Vista general</span></div>
        <div className="lodge-member-layout"><div className="lodge-member-ring"><div><strong>{memberCount}</strong><small>activos</small></div></div><div className="lodge-grade-list">{api.useMocks ? <><LodgeCount label="Maestros" value={lodgeCockpitDemoData.members.masters} tone="green" /><LodgeCount label="Compañeros" value={lodgeCockpitDemoData.members.fellowcraft} tone="blue" /><LodgeCount label="Aprendices" value={lodgeCockpitDemoData.members.apprentices} tone="gold" /><LodgeCount label="Honorarios" value={lodgeCockpitDemoData.members.honorary} tone="gray" /></> : <><LodgeCount label="Miembros disponibles" value={members.length} tone="green" /><LodgeCount label="Distribución por grado" value={0} tone="gray" /></>}</div></div>
      </article>

      <article className="lodge-product-card lodge-officers-card">
        <div className="lodge-card-heading"><div><p className="lodge-kicker">Cuadro de cargos</p><h2>Período vigente</h2></div></div>
        <div className="lodge-officer-list">{lodgeCockpitDemoData.officers.map(([role, name]) => <div key={role}><span className="lodge-officer-avatar">{role[0]}</span><div><strong>{role}</strong><small>{api.useMocks ? name : 'Disponible al integrar cuadro de cargos'}</small></div><em>Activo</em></div>)}</div>
      </article>

      <aside className="lodge-inspiration-card"><span>“</span><p>El verdadero Taller se construye cada día, con trabajo, estudio y fraternidad.</p><i /><strong>Proyecto Centenario</strong><small>Gestión institucional integrada</small></aside>
    </section>

    <section className="lodge-management-section">
      <div className="lodge-management-heading"><div><p className="lodge-kicker">Administración propia</p><h2>Gestión Logial de cada Taller</h2></div><p>Estas funciones pertenecen al Taller y se integran, sin confundirse, con los órganos correspondientes de la Gran Logia.</p></div>
      <div className="lodge-management-grid">{lodgeCockpitDemoData.managementAreas.map(([area, responsible, summary]) => <article className="lodge-management-area" key={area}><span>{area[0]}</span><div><h3>{area}</h3><strong>{responsible}</strong><p>{summary}</p></div></article>)}</div>
    </section>

    <section className="lodge-instruction-workspace">
      <div className="lodge-instruction-heading"><div><p className="lodge-kicker">Gestión Logial › Docencia</p><h2>Registrar instrucción y asistencia</h2><p>El grado determina automáticamente al responsable. La asistencia queda en el historial formativo individual.</p></div><span className="lodge-live-chip">Demostración con datos ficticios</span></div>
      {instructionConfirmation && <div className="regularity-success" role="status">{instructionConfirmation}</div>}
      <div className="lodge-instruction-workspace-grid">
        <form className="lodge-instruction-form" onSubmit={saveInstruction}>
          <label><span>Fecha</span><input type="date" value={instructionDate} onChange={event => setInstructionDate(event.target.value)} required /></label>
          <label><span>Grado</span><select value={instructionGrade} onChange={event => setInstructionGrade(event.target.value as Exclude<LodgeGrade, 'all'>)}><option value="apprentice">Aprendices</option><option value="fellowcraft">Compañeros</option><option value="master">Maestros</option></select></label>
          <label className="lodge-instruction-topic"><span>Tema de la instrucción</span><input value={instructionTopic} onChange={event => setInstructionTopic(event.target.value)} placeholder="Ej.: Simbología del grado" required /></label>
          <div className="lodge-instruction-responsible"><small>Responsable asignado</small><strong>{instructionResponsible}</strong><span>{instructionGrade === 'apprentice' ? 'Instrucción de Aprendices' : instructionGrade === 'fellowcraft' ? 'Instrucción de Compañeros' : 'Instrucción de Maestros'}</span></div>
          <button className="lodge-blue-button" type="submit" disabled={working}>{working ? 'Guardando…' : 'Programar instrucción'}</button>
        </form>
        <article className="lodge-instruction-history"><div className="lodge-card-heading"><div><p className="lodge-kicker">Agenda e historial</p><h2>Instrucciones del Taller</h2></div></div>{instructions.map(instruction => <div className="lodge-instruction-history-row" key={instruction.id}><div><strong>{instruction.topic}</strong><span>{formatDateOnly(instruction.instructionDate)} · {gradeLabel(instruction.grade)}</span></div><div><small>{instructionOfficeLabel(instruction.responsibleOffice)}</small><em>{instruction.status === 'scheduled' ? 'Programada' : instruction.status === 'held' ? 'Realizada' : 'Cancelada'}</em>{instruction.status === 'scheduled' && <button className="lodge-secondary-button" type="button" onClick={() => setSelectedInstructionId(instruction.id)}>Registrar ejecución</button>}</div></div>)}</article>
      </div>
      {selectedInstructionId && <section className="lodge-instruction-attendance"><div><p className="lodge-kicker">Después de la ejecución</p><h3>Registrar asistencia de la instrucción</h3></div>{members.map(member => <div className="lodge-instruction-member" key={member.id}><strong>{member.displayName}</strong><select aria-label={`Asistencia de ${member.displayName}`} value={instructionAttendance[member.id] ?? 'present'} onChange={event => setInstructionAttendance(current => ({ ...current, [member.id]: event.target.value as 'present' | 'absent' }))}><option value="present">Presente</option><option value="absent">Ausente</option></select></div>)}<button className="lodge-blue-button" type="button" disabled={working || members.length === 0} onClick={completeInstructionAndRecordAttendance}>Marcar realizada y guardar asistencia</button></section>}
    </section>

    <section className="lodge-insight-grid">
      <article className="lodge-product-card lodge-next-meeting-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Próxima tenida</p><h2>{nextMeeting ? nextMeeting.title || meetingTypeLabel(nextMeeting.meetingType) : api.useMocks ? 'Tenida Ordinaria · demo' : 'Sin tenida programada'}</h2></div></div><strong className="lodge-next-date">{nextMeeting ? formatDateOnly(nextMeeting.meetingDate) : api.useMocks ? '26 de septiembre de 2026' : '—'}</strong><p>{nextMeeting ? `${meetingTypeLabel(nextMeeting.meetingType)} · ${gradeLabel(nextMeeting.grade)}` : api.useMocks ? '19:00 hrs. · Todos los grados' : 'Registre una tenida para comenzar.'}</p><button type="button" className="lodge-blue-button" onClick={() => setShowOperations(true)}>Preparar tenida</button></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Asistencia última tenida</p><h2>{attendanceSummary.percentage}%</h2></div></div><div className="lodge-attendance-overview"><div className="lodge-attendance-ring" style={{ '--lodge-attendance': `${attendanceSummary.percentage}%` } as React.CSSProperties}><span>{attendanceSummary.percentage}%</span></div><div>{api.useMocks && attendanceSummary.total === 0 ? <><LodgeCount label="Presentes" value={33} tone="green" /><LodgeCount label="Ausentes" value={3} tone="red" /><LodgeCount label="Justificados" value={1} tone="blue" /></> : <><LodgeCount label="Presentes" value={attendanceSummary.present} tone="green" /><LodgeCount label="Ausentes" value={attendanceSummary.absent} tone="red" /><LodgeCount label="Justificados" value={attendanceSummary.excused} tone="blue" /></>}</div></div></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Docencia</p><h2>Plan de estudio del Taller</h2></div><span className="lodge-demo-only">{api.useMocks ? 'Datos ficticios' : 'Operativo'}</span></div><div className="lodge-instruction-list">{lodgeCockpitDemoData.instruction.map(([label, progress]) => <div key={label}><div><span>{label}</span><strong>{progress}%</strong></div><div className="lodge-progress-track"><span style={{ width: `${progress}%` }} /></div></div>)}</div></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Tareas y pendientes</p><h2>Seguimiento operativo</h2></div></div><div className="lodge-task-list"><LodgeCount label="Por completar" value={lodgeCockpitDemoData.tasks.pending} tone="red" /><LodgeCount label="En proceso" value={lodgeCockpitDemoData.tasks.inProgress} tone="gold" /><LodgeCount label="Completadas" value={lodgeCockpitDemoData.tasks.completed} tone="green" /></div></article>
    </section>

    <section className="lodge-activity-grid">
      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Agenda del Taller</p><h2>Próximas tenidas y actividades</h2></div><span>{loading ? 'Cargando…' : `${meetings.length} registradas`}</span></div><div className="lodge-activity-table"><div className="lodge-activity-header"><span>Fecha</span><span>Tipo / actividad</span><span>Grado</span><span>Estado</span></div>{meetings.length ? meetings.slice(0, 5).map(meeting => <button type="button" key={meeting.id} onClick={() => { setSelectedMeetingId(meeting.id); setShowOperations(true) }}><span>{formatDateOnly(meeting.meetingDate)}</span><strong>{meeting.title || meetingTypeLabel(meeting.meetingType)}</strong><span>{gradeLabel(meeting.grade)}</span><em className={meeting.status === 'closed' ? 'closed' : 'active'}>{meetingStatusLabel(meeting.status)}</em></button>) : api.useMocks ? demoMeetingRows.map(row => <div key={row[0]}><span>{row[0]}</span><strong>{row[1]}</strong><span>{row[2]}</span><em className="active">{row[3]}</em></div>) : <p className="lodge-empty-copy">No hay tenidas registradas para este Taller.</p>}</div></article>
      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Centro de avisos</p><h2>Últimas notificaciones</h2></div></div><div className="lodge-notification-list">{lodgeCockpitDemoData.notifications.map((notification, index) => <div key={notification}><span className={`lodge-notification-dot tone-${index}`} /><div><strong>{notification}</strong><small>{api.useMocks ? ['hace 2 días', 'hace 4 días', 'hace 6 días'][index] : 'Vista previa del panel transversal'}</small></div></div>)}</div></article>
    </section>

    <section id="lodge-operations" className={showOperations ? 'lodge-operations visible' : 'lodge-operations'}>
      <div className="lodge-operations-heading"><div><p className="lodge-kicker">Operación real</p><h2>Tenidas, asistencia y actas</h2><p>La capa visual nueva utiliza las mismas operaciones ya conectadas a la API.</p></div><button type="button" className="lodge-secondary-button" onClick={() => setShowOperations(value => !value)}>{showOperations ? 'Ocultar operación' : 'Abrir operación'}</button></div>

      {showOperations && <section className="lodge-layout">
        <article className="panel lodge-operation-panel">
          <p className="eyebrow">Nueva tenida</p><h2>Registrar en agenda</h2>
          <form className="regularity-form" onSubmit={createMeeting}>
            <label className="regularity-field"><span>Fecha · Chile</span><input type="date" required value={meetingDate} onChange={event => setMeetingDate(event.target.value)} /></label>
            <div className="lodge-form-row"><label className="regularity-field"><span>Tipo</span><select value={meetingType} onChange={event => setMeetingType(event.target.value as LodgeMeetingType)}>{meetingTypeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><label className="regularity-field"><span>Grado</span><select value={grade} onChange={event => setGrade(event.target.value as LodgeGrade)}>{gradeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label></div>
            <label className="regularity-field"><span>Título opcional</span><input maxLength={500} value={title} onChange={event => setTitle(event.target.value)} /></label>
            <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Crear Tenida</button>
          </form>
          <div className="lodge-meeting-list">{meetings.length === 0 ? <div className="empty-state"><strong>No hay Tenidas registradas.</strong></div> : meetings.map(meeting => <button key={meeting.id} className={meeting.id === selectedMeetingId ? 'lodge-meeting selected' : 'lodge-meeting'} type="button" onClick={() => setSelectedMeetingId(meeting.id)}><div><strong>{meeting.title || meetingTypeLabel(meeting.meetingType)}</strong><small>{formatDateOnly(meeting.meetingDate)} · {gradeLabel(meeting.grade)}</small></div><span className={meeting.status === 'closed' ? 'regularity-status blocked' : 'regularity-status good'}>{meetingStatusLabel(meeting.status)}</span></button>)}</div>
        </article>

        <article className="panel lodge-detail lodge-operation-panel">
          {!selectedMeeting ? <div className="empty-state"><strong>Seleccione o cree una Tenida para operar asistencia y actas.</strong></div> : <><div className="panel-heading"><div><p className="eyebrow">Tenida seleccionada</p><h2>{selectedMeeting.title || meetingTypeLabel(selectedMeeting.meetingType)}</h2><p>{formatDateOnly(selectedMeeting.meetingDate)} · {meetingTypeLabel(selectedMeeting.meetingType)} · {gradeLabel(selectedMeeting.grade)}</p></div><button className="regularity-secondary" type="button" disabled={working || meetingFinalized} onClick={closeMeeting}>Marcar realizada y cerrar</button></div>
            <section className="lodge-section"><div><p className="eyebrow">Después de la ejecución</p><h3>Asistencia</h3><small>{canRecordMeetingAttendance ? 'La Tenida está realizada: puede registrar o corregir su asistencia.' : 'Primero marque la Tenida como realizada y cerrada.'}</small></div><form className="regularity-form" onSubmit={recordAttendance}><label className="regularity-field"><span>Hermano/a</span><select required value={memberId} disabled={!canRecordMeetingAttendance} onChange={event => setMemberId(event.target.value)}><option value="">Seleccione…</option>{members.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label><div className="lodge-form-row"><label className="regularity-field"><span>Estado</span><select value={attendanceStatus} disabled={!canRecordMeetingAttendance} onChange={event => setAttendanceStatus(event.target.value as LodgeAttendanceStatus)}>{attendanceOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>{attendanceStatus === 'excused' && <label className="regularity-field"><span>Justificación</span><input maxLength={1000} value={excuseReason} disabled={!canRecordMeetingAttendance} onChange={event => setExcuseReason(event.target.value)} /></label>}</div><button className="regularity-primary" type="submit" disabled={working || !canRecordMeetingAttendance || !memberId}>Registrar asistencia</button></form><div className="lodge-attendance-list">{attendance.length === 0 ? <small>Sin asistencia registrada.</small> : attendance.map(item => <div key={item.memberId}><div><strong>{item.displayName}</strong>{item.excuseReason && <small>{item.excuseReason}</small>}</div><span className={attendanceClass(item.status)}>{attendanceLabel(item.status)}</span></div>)}</div></section>
            <LodgeBallotPanel meetingId={selectedMeeting.id} attendeeCount={attendance.filter(item => item.status === 'present').length} enabled={canRecordMeetingAttendance} api={lodgeApi} onError={setError} />
            <section className="lodge-section"><div><p className="eyebrow">Documento histórico</p><h3>Actas versionadas</h3><small>Genere el borrador desde la Tenida, asistencia y escrutinios; complete únicamente los antecedentes narrativos pendientes.</small></div><button className="regularity-secondary" type="button" disabled={working || !canRecordMeetingAttendance} onClick={generateMinuteExtract}>Generar Extracto de Acta</button>{minuteContent && <MinuteExtractEditor baseContent={minuteContent} onApply={setMinuteContent} />}<form className="regularity-form" onSubmit={createMinute}><label className="regularity-field"><span>Nueva versión del acta</span><textarea rows={18} maxLength={20000} value={minuteContent} onChange={event => setMinuteContent(event.target.value)} placeholder="Genere el extracto automático o redacte una nueva versión…" /></label><button className="regularity-primary" type="submit" disabled={working || !minuteContent.trim()}>Crear nueva versión</button></form><div className="lodge-minute-list">{minutes.length === 0 ? <small>Sin versiones de acta.</small> : minutes.map(minute => <article key={minute.id}><div className="lodge-minute-heading"><strong>Versión {minute.version}</strong><span className={minute.status === 'approved' ? 'regularity-status good' : minute.status === 'superseded' ? 'regularity-status blocked' : 'regularity-status pending'}>{minuteStatusLabel(minute.status)}</span></div><p>{minute.content}</p><small>Creada {formatChile(minute.createdAtUtc)}{minute.approvedAtUtc ? ` · aprobada ${formatChile(minute.approvedAtUtc)}` : ''}</small>{minute.status === 'draft' && <button className="regularity-secondary" type="button" disabled={working} onClick={() => approveMinute(minute.id)}>Aprobar esta versión</button>}</article>)}</div></section>
          </>}
        </article>
      </section>}
    </section>
  </div>
}

const demoMeetingRows = [
  ['26 sep 2026', 'Tenida Ordinaria Nº 185', 'Todos', 'Confirmada'],
  ['10 oct 2026', 'Docencia: simbolismo y rito', 'Aprendiz', 'Planificada'],
  ['24 oct 2026', 'Tenida Ordinaria Nº 186', 'Todos', 'Planificada'],
  ['07 nov 2026', 'Taller práctico de instrucción', 'Compañero', 'Planificada'],
] as const

function LodgeDatum({ label, value }: { label: string; value: string }) { return <div className="lodge-datum"><small>{label}</small><strong>{value}</strong></div> }
function LodgeCount({ label, value, tone }: { label: string; value: number; tone: 'green' | 'blue' | 'gold' | 'gray' | 'red' }) { return <div className="lodge-count"><span className={`lodge-count-dot ${tone}`} /><span>{label}</span><strong>{value}</strong></div> }

const meetingTypeOptions = [['regular', 'Regular'], ['solemn', 'Solemne'], ['instruction', 'Instrucción'], ['anniversary', 'Aniversario'], ['funeral', 'Fúnebre'], ['special', 'Especial']] as const
const gradeOptions = [['all', 'Todos los grados'], ['apprentice', 'Aprendiz'], ['fellowcraft', 'Compañero'], ['master', 'Maestro']] as const
const attendanceOptions = [['present', 'Presente'], ['excused', 'Justificado'], ['absent', 'Ausente']] as const
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function meetingTypeLabel(value: LodgeMeetingType) { return meetingTypeOptions.find(([key]) => key === value)?.[1] ?? value }
function gradeLabel(value: LodgeGrade) { return gradeOptions.find(([key]) => key === value)?.[1] ?? value }
function instructionOfficeLabel(value: LodgeInstruction['responsibleOffice']) { return value === 'second_warden' ? 'Segundo Vigilante' : value === 'first_warden' ? 'Primer Vigilante' : 'Ex Venerable Maestro' }
function attendanceLabel(value: LodgeAttendanceStatus) { return attendanceOptions.find(([key]) => key === value)?.[1] ?? value }
function meetingStatusLabel(value: string) { return value === 'closed' ? 'Cerrada' : value === 'open' ? 'Abierta' : value === 'cancelled' ? 'Cancelada' : 'Programada' }
function minuteStatusLabel(value: string) { return value === 'approved' ? 'Aprobada' : value === 'superseded' ? 'Reemplazada' : 'Borrador' }
function attendanceClass(value: LodgeAttendanceStatus) { return value === 'present' ? 'regularity-status good' : value === 'excused' ? 'regularity-status pending' : 'regularity-status blocked' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
