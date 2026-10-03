import { useEffect, useMemo, useState, type FormEvent } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { LodgeApiClient, LodgeGrade, LodgeInstruction, LodgeInstructionAttendanceStatus, LodgeMemberOption } from './api/lodgeApi'
import './lodgeManagement.css'
import { ActionDrawer } from './actionKit'

type TeachingGrade = Exclude<LodgeGrade, 'all'>

const gradeLabels: Record<TeachingGrade, string> = {
  apprentice: 'Aprendices',
  fellowcraft: 'Compañeros',
  master: 'Maestros',
}

const officeLabels: Record<LodgeInstruction['responsibleOffice'], string> = {
  second_warden: 'Segundo Vigilante',
  first_warden: 'Primer Vigilante',
  immediate_past_master: 'Inmediato Ex Venerable Maestro',
}

export default function LodgeInstructionPage({ api, lodgeApi, allowedGrades }: {
  api: PmgmApiClient
  lodgeApi: LodgeApiClient
  allowedGrades: TeachingGrade[]
}) {
  const [organizationId, setOrganizationId] = useState('')
  const [organizationName, setOrganizationName] = useState('')
  const [instructionDate, setInstructionDate] = useState(todayInChile())
  const [grade, setGrade] = useState<TeachingGrade>(allowedGrades[0] ?? 'apprentice')
  const [topic, setTopic] = useState('')
  const [members, setMembers] = useState<LodgeMemberOption[]>([])
  const [instructions, setInstructions] = useState<LodgeInstruction[]>([])
  const [attendance, setAttendance] = useState<Record<string, LodgeInstructionAttendanceStatus | ''>>({})
  const [selectedId, setSelectedId] = useState('')
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const allowed = useMemo(() => new Set(allowedGrades), [allowedGrades])
  const selectedInstruction = instructions.find(item => item.id === selectedId)
  const officeLabel = grade === 'apprentice' ? officeLabels.second_warden
    : grade === 'fellowcraft' ? officeLabels.first_warden : officeLabels.immediate_past_master

  const refreshInstructions = async (id: string) => {
    const result = await lodgeApi.getInstructions(id)
    setInstructions(result.items)
  }

  useEffect(() => {
    let active = true
    api.getOrganizationOptions().then(result => {
      if (!active) return
      const workshops = result.items.filter(item => item.type.toLowerCase() !== 'order')
      const workshop = workshops[0]
      if (workshop) {
        setOrganizationId(workshop.id)
        setOrganizationName(workshop.name)
      } else {
        setError('La sesión no tiene un Taller disponible para docencia.')
      }
    }).catch(reason => { if (active) setError(messageOf(reason)) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    if (!allowed.has(grade)) setGrade(allowedGrades[0] ?? 'apprentice')
  }, [allowedGrades, allowed, grade])

  useEffect(() => {
    if (!organizationId) return
    let active = true
    setError(null)
    Promise.all([
      lodgeApi.getInstructions(organizationId),
      lodgeApi.getMemberOptions(organizationId, grade, instructionDate),
    ]).then(([instructionResult, memberResult]) => {
      if (!active) return
      setInstructions(instructionResult.items)
      setMembers(memberResult.items)
    }).catch(reason => { if (active) setError(messageOf(reason)) })
    return () => { active = false }
  }, [lodgeApi, organizationId, grade, instructionDate])

  const register = async (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId || !allowed.has(grade) || !topic.trim()) return
    setWorking(true); setError(null); setNotice(null)
    try {
      const item = await lodgeApi.createInstruction(organizationId, { instructionDate, grade, topic: topic.trim() })
      await refreshInstructions(organizationId)
      setSelectedId(item.id)
      setAttendance({})
      setTopic('')
      setNotice('Instrucción registrada. Indica el estado de asistencia de cada hermano y marca la sesión como realizada para actualizar Mi ficha.')
    } catch (reason) { setError(messageOf(reason)) } finally { setWorking(false) }
  }

  const markHeld = async () => {
    if (!selectedInstruction || !allowed.has(selectedInstruction.grade)) return
    setWorking(true); setError(null); setNotice(null)
    try {
      if (!members.every(member => attendance[member.id])) {
        setError('Selecciona Presente, Ausente o Justificada para cada hermano.')
        return
      }
      const items = members.map(member => ({ memberId: member.id, status: attendance[member.id] as LodgeInstructionAttendanceStatus }))
      await lodgeApi.completeInstruction(selectedInstruction.id)
      await lodgeApi.recordInstructionAttendance(selectedInstruction.id, items)
      await refreshInstructions(organizationId)
      setSelectedId('')
      setAttendance({})
      setNotice('Instrucción realizada y asistencia guardada. Estos registros aparecen en Mi ficha de cada hermano.')
    } catch (reason) { setError(messageOf(reason)) } finally { setWorking(false) }
  }

  return <div className="lodge-product-page">
    <section className="lodge-product-heading">
      <div><p className="lodge-product-breadcrumb">Taller <span>›</span> Docencia</p><h1>Instrucciones por grado</h1><p>Registra la sesión realizada y su asistencia. El historial queda asociado a cada hermano.</p></div>
      <span className="lodge-organization-select"><span>Taller</span><strong>{organizationName || 'Cargando…'}</strong></span>
    </section>
    {error && <div className="error-banner" role="alert"><strong>No se pudo completar la operación.</strong><span>{error}</span></div>}
    {notice && <div className="regularity-success" role="status">{notice}</div>}
    <section className="lodge-instruction-workspace">
      <div className="lodge-instruction-heading"><div><p className="lodge-kicker">Docencia del Taller</p><h2>Registrar instrucción realizada</h2><p>Responsable del grado seleccionado: {officeLabel}. Los asistentes se determinan según su grado a la fecha registrada.</p></div><span className="lodge-live-chip">{api.useMocks ? 'Demostración con datos ficticios' : 'Operativo'}</span></div>
      <div className="lodge-instruction-workspace-grid">
        <div className="action-bar lodge-instruction-actions"><ActionDrawer label="Registrar sesión" title="Registrar instrucción realizada" description="Fecha, grado y tema de la instrucción. Luego podrás marcar la asistencia." confirmMessage="Se registrará la sesión de instrucción."><form className="lodge-instruction-form" onSubmit={register}>
          <label><span>Fecha de la instrucción</span><input type="date" value={instructionDate} onChange={event => setInstructionDate(event.target.value)} required /></label>
          <label><span>Grado</span><select value={grade} onChange={event => setGrade(event.target.value as TeachingGrade)} disabled={allowedGrades.length === 1}>{allowedGrades.map(item => <option key={item} value={item}>{gradeLabels[item]}</option>)}</select></label>
          <label className="lodge-instruction-topic"><span>Tema tratado</span><input maxLength={500} value={topic} onChange={event => setTopic(event.target.value)} placeholder="Tema de la instrucción" required /></label>
          <div className="lodge-instruction-responsible"><small>Cargo responsable</small><strong>{officeLabel}</strong><span>{members.length} hermanos del grado para registrar asistencia</span></div>
          <button className="lodge-blue-button" type="submit" disabled={working || !organizationId || !topic.trim()}>{working ? 'Guardando…' : 'Registrar sesión'}</button>
        </form></ActionDrawer></div>
        <article className="lodge-instruction-history"><div className="lodge-card-heading"><div><p className="lodge-kicker">Historial del Taller</p><h2>Sesiones de {gradeLabels[grade]}</h2></div></div>
          {instructions.filter(item => allowed.has(item.grade)).map(item => <div className="lodge-instruction-history-row" key={item.id}><div><strong>{item.topic}</strong><span>{formatDate(item.instructionDate)} · {gradeLabels[item.grade]}</span></div><div><small>{officeLabels[item.responsibleOffice]}</small><em>{item.status === 'scheduled' ? 'Pendiente de asistencia' : item.status === 'held' ? 'Realizada' : 'Cancelada'}</em>{item.status === 'scheduled' && <button className="lodge-secondary-button" type="button" onClick={() => { setSelectedId(item.id); setAttendance({}) }}>Registrar asistencia</button>}</div></div>)}
          {instructions.filter(item => allowed.has(item.grade)).length === 0 && <p className="lodge-empty-copy">No hay instrucciones registradas para este grado.</p>}
        </article>
      </div>
      {selectedInstruction && allowed.has(selectedInstruction.grade) && <section className="lodge-instruction-attendance"><div><p className="lodge-kicker">Asistencia · {formatDate(selectedInstruction.instructionDate)}</p><h3>{selectedInstruction.topic}</h3><p>Indica el estado real de cada hermano: Presente si asistió; Ausente si no asistió ni avisó; Justificada si avisó, tenía permiso o comunicó una razón.</p></div>{members.map(member => <div className="lodge-instruction-member" key={member.id}><strong>{member.displayName}</strong><select aria-label={`Asistencia de ${member.displayName}`} aria-required="true" value={attendance[member.id] ?? ''} onChange={event => setAttendance(current => ({ ...current, [member.id]: event.target.value as LodgeInstructionAttendanceStatus | '' }))}><option value="" disabled>Selecciona estado</option><option value="present">Presente</option><option value="absent">Ausente</option><option value="excused">Justificada</option></select></div>)}<button className="lodge-blue-button" type="button" disabled={working || members.length === 0} onClick={markHeld}>{working ? 'Guardando…' : 'Marcar realizada y guardar asistencia'}</button></section>}
    </section>
  </div>
}

function todayInChile() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
}
function formatDate(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function messageOf(reason: unknown) { return reason instanceof Error ? reason.message : 'Revise su conexión e inténtelo nuevamente.' }
