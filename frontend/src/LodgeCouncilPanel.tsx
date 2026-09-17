import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { useLodgeCouncilApi } from './LodgeCouncilApiContext'
import {
  type CouncilControlArea,
  type CouncilDecisionCategory,
  type CouncilDecisionOutcome,
  type LodgeCouncilAttendance,
  type LodgeCouncilDecision,
  type LodgeCouncilFinancialReview,
  type LodgeCouncilSession,
} from './api/lodgeCouncilApi'
import { type LodgeMemberOption } from './api/lodgeApi'

const roleOptions = [
  ['lodge_venerable', 'Venerable Maestro'],
  ['lodge_past_master', 'Inmediato Ex-Venerable Maestro'],
  ['lodge_first_warden', 'Primer Vigilante'],
  ['lodge_second_warden', 'Segundo Vigilante'],
  ['lodge_orator', 'Orador/a'],
  ['lodge_secretariat', 'Secretario/a'],
  ['lodge_treasury', 'Tesorero/a'],
  ['lodge_hospitalaria', 'Hospitalario/a'],
] as const

const decisionOptions: Array<[CouncilDecisionCategory, string, boolean]> = [
  ['handover', 'Recepción/entrega de bienes y documentación', false],
  ['financial_control', 'Control de cuentas / marcha del Taller', false],
  ['dues_relief', 'Condonación o disminución de cuotas', false],
  ['benevolence_aid_proposal', 'Ayuda de beneficencia', false],
  ['budget_proposal', 'Presupuesto o modificación presupuestaria', true],
  ['instruction_program_proposal', 'Programa anual de trabajos/instrucción', true],
  ['officer_change_proposal', 'Cambio de oficial electivo por negligencia', true],
  ['forced_withdrawal_proposal', 'Propuesta de Carta de Retiro Forzoso', true],
  ['other', 'Otra materia dentro de competencia', false],
]

export default function LodgeCouncilPanel({ organizationId, members }: { organizationId: string; members: LodgeMemberOption[] }) {
  const api = useLodgeCouncilApi()
  const [sessions, setSessions] = useState<LodgeCouncilSession[]>([])
  const [selectedSessionId, setSelectedSessionId] = useState('')
  const [attendance, setAttendance] = useState<LodgeCouncilAttendance[]>([])
  const [decisions, setDecisions] = useState<LodgeCouncilDecision[]>([])
  const [reviews, setReviews] = useState<LodgeCouncilFinancialReview[]>([])
  const [sessionDate, setSessionDate] = useState(todayInChile())
  const [sessionTitle, setSessionTitle] = useState('Consejo de Administración')
  const [memberId, setMemberId] = useState('')
  const [institutionalRole, setInstitutionalRole] = useState<string>(roleOptions[0][0])
  const [decisionCategory, setDecisionCategory] = useState<CouncilDecisionCategory>('financial_control')
  const [decisionSubject, setDecisionSubject] = useState('')
  const [decisionResolution, setDecisionResolution] = useState('')
  const [reviewArea, setReviewArea] = useState<CouncilControlArea>('treasury')
  const [reviewPeriod, setReviewPeriod] = useState('')
  const [reviewConclusion, setReviewConclusion] = useState('')
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const selectedSession = useMemo(() => sessions.find(item => item.id === selectedSessionId) ?? null, [sessions, selectedSessionId])
  const presentVotingMembers = attendance.filter(item => item.status === 'present' && item.hasVote).length
  const categoryRequiresChamber = decisionOptions.find(([value]) => value === decisionCategory)?.[2] ?? false

  const loadSessions = async (preferId?: string) => {
    if (!organizationId) { setSessions([]); setSelectedSessionId(''); return }
    const response = await api.getSessions(organizationId)
    setSessions(response.items)
    setSelectedSessionId(current => preferId && response.items.some(item => item.id === preferId) ? preferId : response.items.some(item => item.id === current) ? current : response.items[0]?.id ?? '')
  }

  const loadSessionDetails = async (sessionId: string) => {
    if (!sessionId) { setAttendance([]); setDecisions([]); setReviews([]); return }
    const [attendanceResponse, decisionResponse, reviewResponse] = await Promise.all([
      api.getAttendance(sessionId), api.getDecisions(sessionId), api.getReviews(sessionId),
    ])
    setAttendance(attendanceResponse.items)
    setDecisions(decisionResponse.items)
    setReviews(reviewResponse.items)
  }

  useEffect(() => {
    setError(null); setMessage(null)
    void loadSessions().catch(reason => setError(toMessage(reason)))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organizationId, api])

  useEffect(() => {
    void loadSessionDetails(selectedSessionId).catch(reason => setError(toMessage(reason)))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedSessionId, api])

  useEffect(() => {
    if (!memberId && members[0]) setMemberId(members[0].id)
  }, [members, memberId])

  const createSession = async (event: FormEvent) => {
    event.preventDefault(); if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    try {
      const session = await api.createSession(organizationId, { sessionDate, title: sessionTitle })
      await loadSessions(session.id)
      setMessage('Sesión del Consejo registrada con trazabilidad institucional.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  const recordMember = async (event: FormEvent) => {
    event.preventDefault(); if (!selectedSessionId || !memberId) return
    setWorking(true); setError(null); setMessage(null)
    try {
      await api.recordAttendance(selectedSessionId, { memberId, institutionalRole, participationType: 'member', status: 'present' })
      await loadSessionDetails(selectedSessionId)
      setMessage('Integrante registrado. El cargo define voz y voto según la composición reglamentaria.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  const confirmQuorum = async () => {
    if (!selectedSessionId) return
    setWorking(true); setError(null); setMessage(null)
    try {
      const response = await api.confirmQuorum(selectedSessionId)
      await loadSessions(selectedSessionId)
      setMessage(`Quórum calificado confirmado institucionalmente. Integrantes presentes con voto registrados: ${response.presentVotingMembers}. No se aplica un umbral numérico inventado.`)
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  const recordDecision = async (event: FormEvent) => {
    event.preventDefault(); if (!selectedSessionId) return
    setWorking(true); setError(null); setMessage(null)
    const outcome: CouncilDecisionOutcome = categoryRequiresChamber ? 'approved_for_referral' : 'approved'
    try {
      const decision = await api.recordDecision(selectedSessionId, { category: decisionCategory, subject: decisionSubject, resolution: decisionResolution, outcome })
      await loadSessionDetails(selectedSessionId)
      setDecisionSubject(''); setDecisionResolution('')
      setMessage(decision.requiresChamberReview ? 'Propuesta registrada para remisión a Cámara del Medio; no se marca como aprobación final.' : 'Acuerdo del Consejo registrado y auditado.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  const recordReview = async (event: FormEvent) => {
    event.preventDefault(); if (!selectedSessionId) return
    setWorking(true); setError(null); setMessage(null)
    try {
      await api.recordReview(selectedSessionId, { controlArea: reviewArea, periodLabel: reviewPeriod, conclusion: reviewConclusion })
      await loadSessionDetails(selectedSessionId)
      setReviewConclusion('')
      setMessage('Revisión del Consejo registrada en la bitácora institucional.')
    } catch (reason) { setError(toMessage(reason)) } finally { setWorking(false) }
  }

  return <section className="lodge-management-section" aria-labelledby="lodge-council-title">
    <div className="lodge-management-heading">
      <div><p className="lodge-kicker">Gestión Logial › Gobierno del Taller</p><h2 id="lodge-council-title">Consejo de Administración</h2></div>
      <p>Órgano distinto de una Tenida. Art. 10.1/10.2: ocho cargos, reunión mensual, control administrativo y propuestas trazables a Cámara del Medio.</p>
    </div>
    {error && <div className="error-banner" role="alert"><strong>Consejo no disponible.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <div className="lodge-layout">
      <article className="panel lodge-operation-panel">
        <p className="eyebrow">Sesiones</p><h3>Registro mensual</h3>
        <form className="regularity-form" onSubmit={createSession}>
          <label className="regularity-field"><span>Fecha</span><input type="date" value={sessionDate} onChange={event => setSessionDate(event.target.value)} required /></label>
          <label className="regularity-field"><span>Título</span><input value={sessionTitle} maxLength={300} onChange={event => setSessionTitle(event.target.value)} /></label>
          <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Crear sesión</button>
        </form>
        <div className="lodge-meeting-list">{sessions.map(session => <button key={session.id} type="button" className={session.id === selectedSessionId ? 'lodge-meeting selected' : 'lodge-meeting'} onClick={() => setSelectedSessionId(session.id)}><div><strong>{session.title || 'Consejo de Administración'}</strong><small>{formatDateOnly(session.sessionDate)}</small></div><span className={session.qualifiedQuorumConfirmed ? 'regularity-status good' : 'regularity-status pending'}>{session.qualifiedQuorumConfirmed ? 'Quórum confirmado' : 'Quórum pendiente'}</span></button>)}</div>
      </article>

      <article className="panel lodge-operation-panel">
        {!selectedSession ? <div className="empty-state"><strong>Seleccione o cree una sesión del Consejo.</strong></div> : <>
          <div className="panel-heading"><div><p className="eyebrow">Sesión seleccionada</p><h3>{selectedSession.title || 'Consejo de Administración'}</h3><p>{formatDateOnly(selectedSession.sessionDate)} · {selectedSession.status === 'closed' ? 'Cerrada' : 'En registro'}</p></div><span className={selectedSession.qualifiedQuorumConfirmed ? 'regularity-status good' : 'regularity-status pending'}>{selectedSession.qualifiedQuorumConfirmed ? 'Quórum calificado confirmado' : 'Pendiente de confirmación'}</span></div>
          <p><strong>{presentVotingMembers}</strong> integrantes presentes con voto registrados. El Reglamento vigente consultado exige quórum calificado, pero este módulo no inventa un número: la confirmación institucional queda auditada.</p>
          {!selectedSession.qualifiedQuorumConfirmed && <button className="regularity-primary" type="button" disabled={working || presentVotingMembers === 0} onClick={confirmQuorum}>Confirmar quórum calificado</button>}

          <form className="regularity-form" onSubmit={recordMember}>
            <h4>Integrantes del Consejo</h4>
            <label className="regularity-field"><span>Hermano/a</span><select value={memberId} onChange={event => setMemberId(event.target.value)}><option value="">Seleccione…</option>{members.map(member => <option key={member.id} value={member.id}>{member.displayName}</option>)}</select></label>
            <label className="regularity-field"><span>Cargo</span><select value={institutionalRole} onChange={event => setInstitutionalRole(event.target.value)}>{roleOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
            <button className="regularity-secondary" type="submit" disabled={working || !memberId}>Registrar presente</button>
          </form>
          <div className="lodge-attendance-list">{attendance.map(item => <div key={item.id}><div><strong>{item.displayName}</strong><small>{roleLabel(item.institutionalRole)} · {item.hasVote ? 'voz y voto' : item.hasVoice ? 'solo voz' : 'invitado'}</small></div><span className={item.status === 'present' ? 'regularity-status good' : 'regularity-status pending'}>{item.status === 'present' ? 'Presente' : item.status === 'excused' ? 'Excusado' : 'Ausente'}</span></div>)}</div>
        </>}
      </article>
    </div>

    {selectedSession && <div className="lodge-layout">
      <article className="panel lodge-operation-panel">
        <p className="eyebrow">Acuerdos y propuestas</p><h3>Competencia normativa</h3>
        <form className="regularity-form" onSubmit={recordDecision}>
          <label className="regularity-field"><span>Materia</span><select value={decisionCategory} onChange={event => setDecisionCategory(event.target.value as CouncilDecisionCategory)}>{decisionOptions.map(([value, label, chamber]) => <option key={value} value={value}>{label}{chamber ? ' · Cámara del Medio' : ''}</option>)}</select></label>
          <label className="regularity-field"><span>Asunto</span><input value={decisionSubject} onChange={event => setDecisionSubject(event.target.value)} required /></label>
          <label className="regularity-field"><span>Resolución / propuesta</span><textarea rows={4} value={decisionResolution} onChange={event => setDecisionResolution(event.target.value)} required /></label>
          {categoryRequiresChamber && <div className="regularity-warning"><strong>Requiere Cámara del Medio.</strong><span>El Consejo sólo registra la propuesta aprobada para remisión; no se presenta como resolución final.</span></div>}
          <button className="regularity-primary" type="submit" disabled={working || !selectedSession.qualifiedQuorumConfirmed}>Registrar {categoryRequiresChamber ? 'propuesta' : 'acuerdo'}</button>
        </form>
        <div className="lodge-minute-list">{decisions.map(item => <article key={item.id}><div className="lodge-minute-heading"><strong>{decisionLabel(item.category)}</strong><span className={item.requiresChamberReview ? 'regularity-status pending' : 'regularity-status good'}>{item.requiresChamberReview ? 'A Cámara del Medio' : 'Consejo'}</span></div><p><strong>{item.subject}</strong></p><p>{item.resolution}</p></article>)}</div>
      </article>

      <article className="panel lodge-operation-panel">
        <p className="eyebrow">Control Art. 10.2</p><h3>Revisiones del Consejo</h3>
        <form className="regularity-form" onSubmit={recordReview}>
          <label className="regularity-field"><span>Área</span><select value={reviewArea} onChange={event => setReviewArea(event.target.value as CouncilControlArea)}><option value="treasury">Tesorería</option><option value="hospitalaria">Hospitalaria</option><option value="instruction_columns">Columnas / instrucción</option></select></label>
          <label className="regularity-field"><span>Período</span><input value={reviewPeriod} onChange={event => setReviewPeriod(event.target.value)} placeholder="Ej.: Agosto 2026" required /></label>
          <label className="regularity-field"><span>Conclusión</span><textarea rows={4} value={reviewConclusion} onChange={event => setReviewConclusion(event.target.value)} required /></label>
          <button className="regularity-secondary" type="submit" disabled={working}>Registrar revisión</button>
        </form>
        <div className="lodge-minute-list">{reviews.map(item => <article key={item.id}><div className="lodge-minute-heading"><strong>{controlAreaLabel(item.controlArea)}</strong><span>{item.periodLabel}</span></div><p>{item.conclusion}</p>{item.observations && <small>{item.observations}</small>}</article>)}</div>
      </article>
    </div>}
  </section>
}

function roleLabel(value: string | null) { return roleOptions.find(([key]) => key === value)?.[1] ?? 'Invitado/a' }
function decisionLabel(value: CouncilDecisionCategory) { return decisionOptions.find(([key]) => key === value)?.[1] ?? value }
function controlAreaLabel(value: CouncilControlArea) { return value === 'treasury' ? 'Tesorería' : value === 'hospitalaria' ? 'Hospitalaria' : 'Columnas / instrucción' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
