import { ActionDrawer, HelpNote, WorkspaceTabs, type WorkspaceTab } from './actionKit'
import { type FormEvent, type ReactNode, useEffect, useMemo, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import { type DocumentApiClient } from './api/documentApi'
import {
  type LodgeApiClient,
  type LodgeAttendanceCurrent,
  type LodgeAttendanceStatus,
  type LodgeCeremonyType,
  type LodgeGrade,
  type LodgeInstruction,
  type LodgeMeeting,
  type LodgeMeetingModality,
  type LodgeMeetingType,
  type LodgeMemberOption,
  type LodgeMinute,
} from './api/lodgeApi'
import './regularity.css'
import './lodgeManagement.css'
import './lodgeManagementAreas.css'
import LodgeBallotPanel from './LodgeBallotPanel'
import MinuteExtractEditor from './MinuteExtractEditor'
import LodgeWithdrawalsPanel from './LodgeWithdrawalsPanel'
import LodgeCouncilPanel from './LodgeCouncilPanel'
import LodgeSecretariatPanel, { type LodgeSecretariatSection } from './LodgeSecretariatPanel'
import { ceremonyTypeLabel } from './ceremonyTypes'
import { organizationDisplayName } from './displayFormat'

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
    ['Inmediato Ex-Venerable Maestro', 'H∴ Consejero Demo'],
    ['Primer Vigilante', 'H∴ Primer Vigilante Demo'],
    ['Segundo Vigilante', 'H∴ Segundo Vigilante Demo'],
    ['Orador/a', 'H∴ Orador Demo'],
    ['Secretaría', 'H∴ Secretaría Demo'],
    ['Tesorería', 'H∴ Tesorería Demo'],
    ['Hospitalaria', 'H∴ Hospitalaria Demo'],
  ],
  managementAreas: [
    ['Consejo de Administración', '8 cargos reglamentarios', 'Sesiones mensuales, quórum, acuerdos, controles y propuestas a Cámara del Medio.'],
    ['Secretaría del Taller', 'Secretario/a', 'Tenidas, asistencia, actas, correspondencia y solicitudes.'],
    ['Tesorería del Taller', 'Tesorero/a', 'Cuotas, abonos, comprobantes, libro mayor y rendición a Gran Tesorería.'],
    ['Hospitalaria del Taller', 'Hospitalario/a', 'Bolso, ayudas, aportes, reposiciones y rendición a Gran Hospitalaria.'],
    ['Docencia e instrucción', 'Vigilantes e Inmediato Ex-Venerable Maestro', 'Plan por grado, sesiones, asistencia y seguimiento formativo.'],
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
  master: 'Inmediato Ex-Venerable Maestro',
} as const

/* PMGM-UX-004 · Gestión Logial y Secretaría del Taller en pestañas: cada pestaña muestra su lista y abre sus formularios bajo demanda. */
type LodgeTab = 'resumen' | 'tenidas' | 'correspondencia' | 'cuadro' | 'archivo' | 'consejo' | 'docencia' | 'retiros' | 'ficha'

export default function LodgeManagementPage({ api, lodgeApi, documentApi, canReadSecretariat = false, canManageSecretariat = false, instructionGrades = [], focusInstructions = false, profileSlot, initialTab, pageTitle = 'Gestión Logial' }: { api: PmgmApiClient; lodgeApi: LodgeApiClient; documentApi: DocumentApiClient; canReadSecretariat?: boolean; canManageSecretariat?: boolean; instructionGrades?: Exclude<LodgeGrade, 'all'>[]; focusInstructions?: boolean; profileSlot?: ReactNode; initialTab?: LodgeTab; pageTitle?: string }) {
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [meetings, setMeetings] = useState<LodgeMeeting[]>([])
  const [selectedMeetingId, setSelectedMeetingId] = useState('')
  const [members, setMembers] = useState<LodgeMemberOption[]>([])
  const [instructionMembers, setInstructionMembers] = useState<LodgeMemberOption[]>([])
  const [attendance, setAttendance] = useState<LodgeAttendanceCurrent[]>([])
  const [minutes, setMinutes] = useState<LodgeMinute[]>([])
  const [instructions, setInstructions] = useState<LodgeInstruction[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showOperations] = useState(true)
  const [tab, setTab] = useState<LodgeTab>(initialTab ?? (focusInstructions ? 'docencia' : 'resumen'))
  const lodgeTabs: WorkspaceTab<LodgeTab>[] = [
    { id: 'resumen', label: 'Resumen', icon: 'report' },
    { id: 'tenidas', label: 'Tenidas y actas', icon: 'calendar' },
    ...(canReadSecretariat ? [{ id: 'correspondencia' as const, label: 'Correspondencia', icon: 'documents' as const }, { id: 'cuadro' as const, label: 'Cuadro y reuniones', icon: 'members' as const }, { id: 'archivo' as const, label: 'Archivo y cierre', icon: 'archive' as const }] : []),
    { id: 'consejo', label: 'Consejo', icon: 'shield' },
    { id: 'docencia', label: 'Docencia', icon: 'library' },
    { id: 'retiros', label: 'Cartas de retiro', icon: 'memberControl' },
    ...(profileSlot ? [{ id: 'ficha' as const, label: 'Ficha del Taller', icon: 'lodge' as const }] : []),
  ]
  const secretariatSection: LodgeSecretariatSection | null = tab === 'correspondencia' ? 'daily' : tab === 'cuadro' ? 'roster' : tab === 'archivo' ? 'documents' : null
  const openTenidas = () => { setTab('tenidas'); window.scrollTo({ top: 0, behavior: 'smooth' }) }
  const [instructionDate, setInstructionDate] = useState(todayInChile())
  const [instructionGrade, setInstructionGrade] = useState<Exclude<LodgeGrade, 'all'>>(instructionGrades[0] ?? 'apprentice')
  const [instructionTopic, setInstructionTopic] = useState('')
  const [instructionAttendance, setInstructionAttendance] = useState<Record<string, 'present' | 'absent'>>({})
  const [instructionConfirmation, setInstructionConfirmation] = useState<string | null>(null)
  const [selectedInstructionId, setSelectedInstructionId] = useState('')

  const [meetingDate, setMeetingDate] = useState(todayInChile())
  const [meetingType, setMeetingType] = useState<LodgeMeetingType>('regular')
  const [grade, setGrade] = useState<LodgeGrade>('all')
  const [ceremonyType, setCeremonyType] = useState<LodgeCeremonyType | ''>('')
  const [modality, setModality] = useState<LodgeMeetingModality>('in_person')
  const [locationReference, setLocationReference] = useState('Templo o sala del Taller')
  const [virtualAccessReference, setVirtualAccessReference] = useState('')
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
    if (!instructionGrades.includes(instructionGrade)) setInstructionGrade(instructionGrades[0] ?? 'apprentice')
  }, [instructionGrades, instructionGrade])

  useEffect(() => {
    if (!organizationId || instructionGrades.length === 0) { setInstructionMembers([]); return }
    let active = true
    lodgeApi.getMemberOptions(organizationId, instructionGrade, instructionDate)
      .then(response => { if (active) setInstructionMembers(response.items) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
    return () => { active = false }
  }, [lodgeApi, organizationId, instructionGrade, instructionDate, instructionGrades])

  useEffect(() => {
    if (focusInstructions) document.getElementById('lodge-instruction-workspace')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [focusInstructions, loading])

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
    void lodgeApi.createMeeting(organizationId, { meetingDate, meetingType, grade, ceremonyType: ceremonyType || null, modality, locationReference: modality === 'in_person' ? locationReference.trim() || null : null, virtualAccessReference: modality === 'virtual' ? virtualAccessReference.trim() || null : null, title: title.trim() || null })
      .then(async meeting => {
        await refreshMeetings(meeting.id)
        setTitle('')
        setVirtualAccessReference('')
        setMessage('Tenida creada como Programada y registrada en auditoría institucional.')
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

  const markMeetingHeld = () => {
    if (!selectedMeetingId) return
    setWorking(true); setError(null); setMessage(null)
    void lodgeApi.markMeetingHeld(selectedMeetingId)
      .then(async () => {
        await refreshMeetings(selectedMeetingId)
        await refreshSelected()
        setMessage('Tenida marcada como Realizada. Ya puede registrar asistencia, escrutinios y completar la documentación necesaria antes del cierre definitivo.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const meetingFinalized = selectedMeeting?.status === 'held' || selectedMeeting?.status === 'closed' || selectedMeeting?.status === 'cancelled'
  const canRecordMeetingAttendance = selectedMeeting?.status === 'held' || selectedMeeting?.status === 'closed'
  /* PMGM-UX-003 (P3-5): el sello muestra el número del Taller, igual que en Mi ficha. */
  const instructionResponsible = instructionResponsibilityByGrade[instructionGrade]

  const saveInstruction = async (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId || !instructionTopic.trim() || instructionGrades.length === 0 || !instructionGrades.includes(instructionGrade)) return
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
      const items = instructionMembers.map(member => ({ memberId: member.id, status: instructionAttendance[member.id] ?? 'present' as const }))
      await lodgeApi.recordInstructionAttendance(selectedInstructionId, items)
      const response = await lodgeApi.getInstructions(organizationId)
      setInstructions(response.items)
      setInstructionConfirmation(`Instrucción realizada · ${items.filter(item => item.status === 'present').length} asistentes registrados. El historial ya está disponible en Mi ficha del hermano.`)
      setSelectedInstructionId('')
      setInstructionAttendance({})
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  return <div className="lodge-product-page">
    <section className="lodge-product-heading">
      <div>
        <p className="lodge-product-breadcrumb">Gestión Logial <span>›</span> Vista general</p>
        <h1>{pageTitle}</h1>
        <p>Herramientas para la administración y operación del Taller en un solo lugar.</p>
      </div>
      <div className="lodge-heading-actions">
        {organizations.length === 1 && selectedOrganization ? <div className="lodge-organization-select single-organization"><span>Taller</span><strong>{organizationLabel(selectedOrganization)}</strong></div> : <label className="lodge-organization-select"><span>Taller</span><select value={organizationId} onChange={event => { setOrganizationId(event.target.value); setMessage(null) }}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></label>}
        <button className="lodge-gold-button" type="button" onClick={openTenidas}>Registrar tenida</button>
      </div>
    </section>

    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <WorkspaceTabs label="Secciones de Gestión Logial" tabs={lodgeTabs} active={tab} onChange={setTab} hub={{ title: 'Gestión Logial', startOpen: Boolean(initialTab) || focusInstructions, hints: { resumen: 'Cuadro de cargos, próxima tenida y pendientes', tenidas: 'Tenidas, asistencia y actas', docencia: 'Registrar instrucción y ver el historial' } }} />
    {tab === 'resumen' && <HelpNote><p>Aquí ve el estado del Taller. Para trabajar, elija una pestaña: <strong>Tenidas y actas</strong> para la agenda y asistencia{canReadSecretariat ? ', o las pestañas de Secretaría para correspondencia, Cuadro y archivo' : ''}.</p><p>Cada pestaña muestra primero la lista; los formularios se abren con su botón.</p></HelpNote>}

    <section hidden={tab !== 'resumen'} id="lodge-summary" className="lodge-cockpit-grid">
      <article className="lodge-product-card lodge-officers-card">
        <div className="lodge-card-heading"><div><p className="lodge-kicker">Cuadro de cargos</p><h2>Período vigente</h2></div></div>
        <div className="lodge-officer-list">{lodgeCockpitDemoData.officers.map(([role, name]) => <div key={role}><span className="lodge-officer-avatar">{role[0]}</span><div><strong>{role}</strong><small>{api.useMocks ? name : 'Disponible al integrar cuadro de cargos'}</small></div><em>Activo</em></div>)}</div>
      </article>

    </section>

    {profileSlot && <div className="workspace-panel" hidden={tab !== 'ficha'}>{profileSlot}</div>}
    <div className="workspace-panel" hidden={tab !== 'consejo'}><LodgeCouncilPanel organizationId={organizationId} members={members} /></div>

    {canReadSecretariat && <div className="workspace-panel" hidden={!secretariatSection}><LodgeSecretariatPanel organizationId={organizationId} lodgeApi={lodgeApi} documentApi={documentApi} meetings={meetings} members={members} canManage={canManageSecretariat} onMeetingChanged={meetingId => refreshMeetings(meetingId)} section={secretariatSection ?? 'daily'} /></div>}

    <section hidden={tab !== 'docencia'} id="lodge-instruction-workspace" className="lodge-instruction-workspace">
      <div className="lodge-instruction-heading"><div><p className="lodge-kicker">Gestión Logial › Docencia</p><h2>Registrar instrucción y asistencia</h2><p>El grado determina automáticamente al responsable. La asistencia queda en el historial formativo individual.</p></div><span className="lodge-live-chip">{api.useMocks ? 'Demostración con datos ficticios' : 'Operativo'}</span></div>
      {instructionConfirmation && <div className="regularity-success" role="status">{instructionConfirmation}</div>}
      <div className="lodge-instruction-workspace-grid">
        {instructionGrades.length > 0 ? <div className="action-bar lodge-instruction-actions"><ActionDrawer label="Programar instrucción" description="El grado define automáticamente al responsable de la instrucción."><form className="lodge-instruction-form" onSubmit={saveInstruction}>
          <label><span>Fecha</span><input type="date" value={instructionDate} onChange={event => setInstructionDate(event.target.value)} required /></label>
          <label><span>Grado</span><select value={instructionGrade} onChange={event => setInstructionGrade(event.target.value as Exclude<LodgeGrade, 'all'>)} disabled={instructionGrades.length === 1}>{instructionGrades.map(value => <option key={value} value={value}>{gradeLabel(value)}</option>)}</select></label>
          <label className="lodge-instruction-topic"><span>Tema de la instrucción</span><input value={instructionTopic} onChange={event => setInstructionTopic(event.target.value)} placeholder="Ej.: Simbología del grado" required /></label>
          <div className="lodge-instruction-responsible"><small>Responsable asignado</small><strong>{instructionResponsible}</strong><span>{instructionGrade === 'apprentice' ? 'Instrucción de Aprendices' : instructionGrade === 'fellowcraft' ? 'Instrucción de Compañeros' : 'Instrucción de Maestros'}</span></div>
          <button className="lodge-blue-button" type="submit" disabled={working || !organizationId}>{working ? 'Guardando…' : 'Registrar instrucción'}</button>
        </form></ActionDrawer></div> : <div className="lodge-instruction-form lodge-instruction-readonly"><p>La carga y asistencia de instrucciones está limitada al cargo responsable de cada grado.</p><small>Consulta las sesiones registradas en el historial del Taller y en Mi ficha.</small></div>}
        <article className="lodge-instruction-history"><div className="lodge-card-heading"><div><p className="lodge-kicker">Agenda e historial</p><h2>Instrucciones del Taller</h2></div></div>{instructions.map(instruction => <div className="lodge-instruction-history-row" key={instruction.id}><div><strong>{instruction.topic}</strong><span>{formatDateOnly(instruction.instructionDate)} · {gradeLabel(instruction.grade)}</span></div><div><small>{instructionOfficeLabel(instruction.responsibleOffice)}</small><em>{instruction.status === 'scheduled' ? 'Programada' : instruction.status === 'held' ? 'Realizada' : 'Cancelada'}</em>{instruction.status === 'scheduled' && instructionGrades.includes(instruction.grade) && <button className="lodge-secondary-button" type="button" onClick={() => setSelectedInstructionId(instruction.id)}>Registrar ejecución</button>}</div></div>)}</article>
      </div>
      {selectedInstructionId && instructions.some(item => item.id === selectedInstructionId && instructionGrades.includes(item.grade)) && <section className="lodge-instruction-attendance"><div><p className="lodge-kicker">Después de la ejecución</p><h3>Registrar asistencia de la instrucción</h3><p>Se muestran hermanos activos cuyo grado corresponde a esta sesión en la fecha indicada.</p></div>{instructionMembers.map(member => <div className="lodge-instruction-member" key={member.id}><strong>{member.displayName}</strong><select aria-label={`Asistencia de ${member.displayName}`} value={instructionAttendance[member.id] ?? 'present'} onChange={event => setInstructionAttendance(current => ({ ...current, [member.id]: event.target.value as 'present' | 'absent' }))}><option value="present">Presente</option><option value="absent">Ausente</option></select></div>)}<button className="lodge-blue-button" type="button" disabled={working || instructionMembers.length === 0} onClick={completeInstructionAndRecordAttendance}>Marcar realizada y guardar asistencia</button></section>}
    </section>

    <div className="workspace-panel" hidden={tab !== 'retiros'}><LodgeWithdrawalsPanel lodgeApi={lodgeApi} organizationId={organizationId} members={members} /></div>

    <section hidden={tab !== 'resumen'} id="lodge-insights" className="lodge-insight-grid">
      <article className="lodge-product-card lodge-next-meeting-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Próxima tenida</p><h2>{nextMeeting ? nextMeeting.title || meetingTypeLabel(nextMeeting.meetingType) : api.useMocks ? 'Tenida Ordinaria · demo' : 'Sin tenida programada'}</h2></div></div><strong className="lodge-next-date">{nextMeeting ? formatDateOnly(nextMeeting.meetingDate) : api.useMocks ? '26 de septiembre de 2026' : '—'}</strong><p>{nextMeeting ? `${meetingTypeLabel(nextMeeting.meetingType)} · ${gradeLabel(nextMeeting.grade)}` : api.useMocks ? '19:00 hrs. · Todos los grados' : 'Registre una tenida para comenzar.'}</p><button type="button" className="lodge-blue-button" onClick={() => openTenidas()}>Preparar tenida</button></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Asistencia última tenida</p><h2>{attendanceSummary.percentage}%</h2></div></div><div className="lodge-attendance-overview"><div className="lodge-attendance-ring" style={{ '--lodge-attendance': `${attendanceSummary.percentage}%` } as React.CSSProperties}><span>{attendanceSummary.percentage}%</span></div><div>{api.useMocks && attendanceSummary.total === 0 ? <><LodgeCount label="Presentes" value={33} tone="green" /><LodgeCount label="Ausentes" value={3} tone="red" /><LodgeCount label="Justificados" value={1} tone="blue" /></> : <><LodgeCount label="Presentes" value={attendanceSummary.present} tone="green" /><LodgeCount label="Ausentes" value={attendanceSummary.absent} tone="red" /><LodgeCount label="Justificados" value={attendanceSummary.excused} tone="blue" /></>}</div></div></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Docencia</p><h2>Plan de estudio del Taller</h2></div><span className="lodge-demo-only">{api.useMocks ? 'Datos ficticios' : 'Operativo'}</span></div><div className="lodge-instruction-list">{lodgeCockpitDemoData.instruction.map(([label, progress]) => <div key={label}><div><span>{label}</span><strong>{progress}%</strong></div><div className="lodge-progress-track"><span style={{ width: `${progress}%` }} /></div></div>)}</div></article>

      <article className="lodge-product-card"><div className="lodge-card-heading"><div><p className="lodge-kicker">Tareas y pendientes</p><h2>Seguimiento operativo</h2></div></div><div className="lodge-task-list"><LodgeCount label="Por completar" value={lodgeCockpitDemoData.tasks.pending} tone="red" /><LodgeCount label="En proceso" value={lodgeCockpitDemoData.tasks.inProgress} tone="gold" /><LodgeCount label="Completadas" value={lodgeCockpitDemoData.tasks.completed} tone="green" /></div></article>
    </section>

    <section hidden={tab !== 'tenidas'} id="lodge-operations" className={showOperations ? 'lodge-operations visible' : 'lodge-operations'}>
      <div className="lodge-operations-heading"><div><p className="lodge-kicker">Operación real</p><h2>Tenidas, asistencia y actas</h2><p>Registre la Tenida, márquela como realizada, registre la asistencia y genere el acta.</p></div></div>

      {showOperations && <section className="lodge-layout">
        <article className="panel lodge-operation-panel">
          <p className="eyebrow">Agenda</p><h2>Tenidas del Taller</h2>
          <ActionDrawer label="Registrar tenida" description="Nueva Tenida en la agenda del Taller."><form className="regularity-form" onSubmit={createMeeting}>
            <label className="regularity-field"><span>Fecha · Chile</span><input type="date" required value={meetingDate} onChange={event => setMeetingDate(event.target.value)} /></label>
            <div className="lodge-form-row"><label className="regularity-field"><span>Tipo</span><select value={meetingType} onChange={event => setMeetingType(event.target.value as LodgeMeetingType)}>{meetingTypeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><label className="regularity-field"><span>Grado</span><select value={grade} onChange={event => setGrade(event.target.value as LodgeGrade)}>{gradeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label></div>
            <div className="lodge-form-row"><label className="regularity-field"><span>Ceremonia</span><select value={ceremonyType} onChange={event => setCeremonyType(event.target.value as LodgeCeremonyType | '')}><option value="">No ceremonial</option><option value="initiation">Iniciación</option><option value="wage_increase">Aumento de salario</option><option value="exaltation">Exaltación</option><option value="affiliation">Afiliación</option><option value="incorporation">Incorporación</option></select></label><label className="regularity-field"><span>Modalidad</span><select value={modality} onChange={event => setModality(event.target.value as LodgeMeetingModality)}><option value="in_person">Presencial</option><option value="virtual">Virtual</option></select></label></div>
            {modality === 'in_person' ? <label className="regularity-field"><span>Templo, sala o lugar</span><input required value={locationReference} onChange={event => setLocationReference(event.target.value)} /></label> : <label className="regularity-field"><span>Referencia de acceso virtual</span><input required value={virtualAccessReference} onChange={event => setVirtualAccessReference(event.target.value)} placeholder="Enlace o referencia de conexión restringida" /></label>}
            <label className="regularity-field"><span>Título opcional</span><input maxLength={500} value={title} onChange={event => setTitle(event.target.value)} /></label>
            <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Crear Tenida</button>
          </form></ActionDrawer>
          <div className="lodge-meeting-list">{meetings.length === 0 ? <div className="empty-state"><strong>No hay Tenidas registradas.</strong></div> : meetings.map(meeting => <button key={meeting.id} className={meeting.id === selectedMeetingId ? 'lodge-meeting selected' : 'lodge-meeting'} type="button" onClick={() => setSelectedMeetingId(meeting.id)}><div><strong>{meeting.title || meetingTypeLabel(meeting.meetingType)}</strong><small>{formatDateOnly(meeting.meetingDate)} · {gradeLabel(meeting.grade)}</small></div><span className={meeting.status === 'held' || meeting.status === 'closed' ? 'regularity-status blocked' : 'regularity-status good'}>{meetingStatusLabel(meeting.status)}</span></button>)}</div>
        </article>

        <article className="panel lodge-detail lodge-operation-panel">
          {!selectedMeeting ? <div className="empty-state"><strong>Seleccione o cree una Tenida para operar asistencia y actas.</strong></div> : <><div className="panel-heading"><div><p className="eyebrow">Tenida seleccionada</p><h2>{selectedMeeting.title || meetingTypeLabel(selectedMeeting.meetingType)}</h2><p>{formatDateOnly(selectedMeeting.meetingDate)} · {meetingTypeLabel(selectedMeeting.meetingType)} · {gradeLabel(selectedMeeting.grade)} · {selectedMeeting.modality === 'virtual' ? 'Virtual' : 'Presencial'}{selectedMeeting.ceremonyType ? ` · ${ceremonyTypeLabel(selectedMeeting.ceremonyType)}` : ''}</p></div><button className="regularity-secondary" type="button" disabled={working || meetingFinalized} onClick={markMeetingHeld}>Marcar realizada</button></div>
            <section className="lodge-section"><div><p className="eyebrow">Después de la ejecución</p><h3>Asistencia</h3><small>{canRecordMeetingAttendance ? 'La Tenida está realizada: puede registrar o corregir su asistencia.' : 'Primero marque la Tenida como Realizada.'}</small></div><ActionDrawer keepOpen label="Registrar asistencia" description="Marque el estado de cada hermano en esta Tenida."><form className="regularity-form" onSubmit={recordAttendance}><label className="regularity-field"><span>Hermano/a</span><select required value={memberId} disabled={!canRecordMeetingAttendance} onChange={event => setMemberId(event.target.value)}><option value="">Seleccione…</option>{members.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label><div className="lodge-form-row"><label className="regularity-field"><span>Estado</span><select value={attendanceStatus} disabled={!canRecordMeetingAttendance} onChange={event => setAttendanceStatus(event.target.value as LodgeAttendanceStatus)}>{attendanceOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>{attendanceStatus === 'excused' && <label className="regularity-field"><span>Justificación</span><input maxLength={1000} value={excuseReason} disabled={!canRecordMeetingAttendance} onChange={event => setExcuseReason(event.target.value)} /></label>}</div><button className="regularity-primary" type="submit" disabled={working || !canRecordMeetingAttendance || !memberId}>Registrar asistencia</button></form></ActionDrawer><div className="lodge-attendance-list">{attendance.length === 0 ? <small>Sin asistencia registrada.</small> : attendance.map(item => <div key={item.memberId}><div><strong>{item.displayName}</strong>{item.excuseReason && <small>{item.excuseReason}</small>}</div><span className={attendanceClass(item.status)}>{attendanceLabel(item.status)}</span></div>)}</div></section>
            <LodgeBallotPanel meetingId={selectedMeeting.id} attendeeCount={attendance.filter(item => item.status === 'present').length} enabled={canRecordMeetingAttendance} api={lodgeApi} onError={setError} />
            <section className="lodge-section"><div><p className="eyebrow">Documento histórico</p><h3>Actas versionadas</h3><small>Genere el borrador desde la Tenida, asistencia y escrutinios; complete únicamente los antecedentes narrativos pendientes.</small></div><ActionDrawer label="Redactar acta" tone="secondary" description="Genere el Extracto de Acta desde la Tenida y complete los antecedentes narrativos. Cada guardado crea una nueva versión."><button className="regularity-secondary" type="button" disabled={working || !canRecordMeetingAttendance} onClick={generateMinuteExtract}>Generar Extracto de Acta</button>{minuteContent && <MinuteExtractEditor baseContent={minuteContent} onApply={setMinuteContent} />}<form className="regularity-form" onSubmit={createMinute}><label className="regularity-field"><span>Nueva versión del acta</span><textarea rows={18} maxLength={20000} value={minuteContent} onChange={event => setMinuteContent(event.target.value)} placeholder="Genere el extracto automático o redacte una nueva versión…" /></label><button className="regularity-primary" type="submit" disabled={working || !minuteContent.trim()}>Crear nueva versión</button></form></ActionDrawer><div className="lodge-minute-list">{minutes.length === 0 ? <small>Sin versiones de acta.</small> : minutes.map(minute => <article key={minute.id}><div className="lodge-minute-heading"><strong>Versión {minute.version}</strong><span className={minute.status === 'approved' ? 'regularity-status good' : minute.status === 'superseded' ? 'regularity-status blocked' : 'regularity-status pending'}>{minuteStatusLabel(minute.status)}</span></div><p>{minute.content}</p><small>Creada {formatChile(minute.createdAtUtc)}{minute.approvedAtUtc ? ` · aprobada ${formatChile(minute.approvedAtUtc)}` : ''}</small>{minute.status === 'draft' && <button className="regularity-secondary" type="button" disabled={working} onClick={() => approveMinute(minute.id)}>Aprobar esta versión</button>}</article>)}</div></section>
          </>}
        </article>
      </section>}
    </section>
  </div>
}

function LodgeCount({ label, value, tone }: { label: string; value: number; tone: 'green' | 'blue' | 'gold' | 'gray' | 'red' }) { return <div className="lodge-count"><span className={`lodge-count-dot ${tone}`} /><span>{label}</span><strong>{value}</strong></div> }

const meetingTypeOptions = [['regular', 'Regular'], ['solemn', 'Solemne'], ['instruction', 'Instrucción'], ['anniversary', 'Aniversario'], ['funeral', 'Fúnebre'], ['special', 'Especial']] as const
const gradeOptions = [['all', 'Todos los grados'], ['apprentice', 'Aprendiz'], ['fellowcraft', 'Compañero'], ['master', 'Maestro']] as const
const attendanceOptions = [['present', 'Presente'], ['excused', 'Justificado'], ['absent', 'Ausente']] as const
function organizationLabel(item: OrganizationOption) { return `${organizationDisplayName(item.name, item.number)}` }
function meetingTypeLabel(value: LodgeMeetingType) { return meetingTypeOptions.find(([key]) => key === value)?.[1] ?? value }
function gradeLabel(value: LodgeGrade) { return gradeOptions.find(([key]) => key === value)?.[1] ?? value }
function instructionOfficeLabel(value: LodgeInstruction['responsibleOffice']) { return value === 'second_warden' ? 'Segundo Vigilante' : value === 'first_warden' ? 'Primer Vigilante' : 'Inmediato Ex-Venerable Maestro' }
function attendanceLabel(value: LodgeAttendanceStatus) { return attendanceOptions.find(([key]) => key === value)?.[1] ?? value }
function meetingStatusLabel(value: string) { return value === 'closed' ? 'Cerrada' : value === 'held' ? 'Realizada' : value === 'open' ? 'Abierta' : value === 'cancelled' ? 'Cancelada' : 'Programada' }
function minuteStatusLabel(value: string) { return value === 'approved' ? 'Aprobada' : value === 'superseded' ? 'Reemplazada' : 'Borrador' }
function attendanceClass(value: LodgeAttendanceStatus) { return value === 'present' ? 'regularity-status good' : value === 'excused' ? 'regularity-status pending' : 'regularity-status blocked' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
