import { useEffect, useMemo, useState } from 'react'
import {
  type CandidateIntakeApiClient,
  type CandidateIntakeProfile,
  type CandidateInterviewUploadMetadata,
  type CandidateWorkflowResponse,
  type FinalBallotRoundPayload,
} from './api/candidateIntakeApi'
import './CandidateWorkflowPanel.css'

interface CandidateWorkflowPanelProps {
  api: CandidateIntakeApiClient
  requestId: string
  profile: CandidateIntakeProfile
  requestStatus: string
}

interface InterviewDraft extends CandidateInterviewUploadMetadata {
  id: string
  file: File | null
}

const emptyInterview = (): InterviewDraft => ({
  id: crypto.randomUUID(),
  interviewDate: chileToday(),
  interviewerDisplayName: '',
  summary: '',
  result: 'favorable',
  file: null,
})

export default function CandidateWorkflowPanel({ api, requestId, profile, requestStatus }: CandidateWorkflowPanelProps) {
  const [workflow, setWorkflow] = useState<CandidateWorkflowResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const presentationDate = profile.firstDegreePresentationDate
  const defaultDeliberationDate = presentationDate ? addDays(presentationDate, 7) : chileToday()
  const [deliberationDate, setDeliberationDate] = useState(defaultDeliberationDate)
  const [presentVoters, setPresentVoters] = useState(0)
  const [votesInFavor, setVotesInFavor] = useState(0)
  const [deliberationReference, setDeliberationReference] = useState('')

  const [interviews, setInterviews] = useState<InterviewDraft[]>(() => [emptyInterview(), emptyInterview(), emptyInterview()])
  const [questionnaireReference, setQuestionnaireReference] = useState('')
  const [autobiographyReference, setAutobiographyReference] = useState('')

  const [thirdDate, setThirdDate] = useState(chileToday())
  const [thirdPresent, setThirdPresent] = useState(0)
  const [thirdFor, setThirdFor] = useState(0)
  const [thirdAgainst, setThirdAgainst] = useState(0)
  const [thirdAbstentions, setThirdAbstentions] = useState(0)
  const [thirdApproved, setThirdApproved] = useState(true)
  const [thirdReference, setThirdReference] = useState('')

  const [ballotDate, setBallotDate] = useState(chileToday())
  const [ballots, setBallots] = useState<FinalBallotRoundPayload[]>([
    { procedureNumber: 1, eligibleVoters: 0, whiteBallots: 0, blackBallots: 0 },
  ])
  const [ballotApproved, setBallotApproved] = useState(true)
  const [ballotReference, setBallotReference] = useState('')

  const [proposedDate, setProposedDate] = useState(addDays(chileToday(), 14))
  const [secretaryName, setSecretaryName] = useState(profile.responsibleSecretaryName ?? '')
  const [initiationReference, setInitiationReference] = useState('')
  const [venerableApproval, setVenerableApproval] = useState(false)

  const publicationElapsed = useMemo(() => {
    if (!workflow?.publication) return null
    return Math.max(0, daysBetween(workflow.publication.publishedFromUtc.slice(0, 10), chileToday()))
  }, [workflow])

  async function reload() {
    setLoading(true)
    try {
      setWorkflow(await api.getWorkflow(requestId))
    } catch (reason) {
      setError(errorMessage(reason, 'No fue posible cargar el flujo reglamentario.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    setWorkflow(null)
    setError(null)
    setMessage(null)
    setDeliberationDate(profile.firstDegreePresentationDate ? addDays(profile.firstDegreePresentationDate, 7) : chileToday())
    setSecretaryName(profile.responsibleSecretaryName ?? '')
    void reload()
    // reload is intentionally scoped to the selected expediente.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, requestId, profile.firstDegreePresentationDate, profile.responsibleSecretaryName])

  async function runAction(action: () => Promise<void>, success: string) {
    if (busy) return
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      await action()
      await reload()
      setMessage(success)
    } catch (reason) {
      setError(errorMessage(reason, 'No fue posible registrar la etapa.'))
    } finally {
      setBusy(false)
    }
  }

  function updateInterview(id: string, patch: Partial<InterviewDraft>) {
    setInterviews(current => current.map(item => item.id === id ? { ...item, ...patch } : item))
  }

  async function submitInterviews() {
    if (interviews.length < 3 || interviews.some(item => !item.file || !item.interviewerDisplayName.trim() || !item.summary.trim())) {
      setError('Debe completar al menos tres entrevistas con responsable, resumen, resultado y archivo PDF/DOCX.')
      return
    }
    if (!questionnaireReference.trim() || !autobiographyReference.trim()) {
      setError('Debe registrar las referencias privadas del Cuestionario Confidencial y la autobiografía.')
      return
    }

    await runAction(async () => {
      const uploaded = []
      for (const item of interviews) {
        const file = item.file
        if (!file) throw new Error('Falta un archivo de entrevista.')
        const result = await api.uploadInterviewDocument(requestId, item.id, file, {
          interviewDate: item.interviewDate,
          interviewerDisplayName: item.interviewerDisplayName.trim(),
          summary: item.summary.trim(),
          result: item.result,
        })
        uploaded.push({
          interviewDate: item.interviewDate,
          interviewerDisplayName: item.interviewerDisplayName.trim(),
          summary: item.summary.trim(),
          result: item.result,
          documentVersionId: result.documentVersionId,
        })
      }

      await api.recordInterviewPackage(requestId, {
        asOfDate: chileToday(),
        interviews: uploaded,
        confidentialQuestionnaireAvailable: true,
        confidentialQuestionnaireReference: questionnaireReference.trim(),
        autobiographyAvailable: true,
        autobiographyReference: autobiographyReference.trim(),
      })
    }, 'Entrevistas y antecedentes privados registrados para revisión de 3.er grado.')
  }

  const rejected = requestStatus === 'rejected' || workflow?.status === 'rejected'

  return <section className="candidate-product-card candidate-workflow-panel">
    <div className="candidate-section-title">
      <span>⇄</span>
      <h2>Flujo reglamentario de la insinuación</h2>
      <em>Protocolo 2026 · expediente único</em>
    </div>

    <p className="candidate-workflow-intro">
      Las etapas se registran sobre el mismo expediente. El plazo de deliberación se calcula en servidor desde la presentación en 1.er grado; la publicación y el balotaje conservan su evidencia histórica.
    </p>

    {loading && !workflow && <p>Cargando trazabilidad del expediente…</p>}
    {error && <div className="error-banner" role="alert"><strong>Flujo de insinuación</strong><span>{error}</span></div>}
    {message && <div className="candidate-protected-notice" role="status">{message}</div>}
    {rejected && <div className="error-banner" role="alert"><strong>Expediente rechazado</strong><span>Este rechazo queda como antecedente transversal de la Orden. Una nueva presentación debe respetar el plazo reglamentario y acreditar subsanación de las causas.</span></div>}

    {workflow && <>
      <div className="candidate-workflow-timeline">
        <Stage title="1. Deliberación inicial" stage={workflow.initialDeliberation} pending="Pendiente · mínimo 7 días desde presentación en 1.er grado" />
        <Stage title="2. Publicación institucional" stage={workflow.publication ? publicationAsStage(workflow) : null} pending="Pendiente de aprobación y publicación por Gran Secretaría" />
        <Stage title="3. Entrevistas y antecedentes" stage={workflow.interviewPackage} pending="Pendiente · mínimo 3 entrevistas + cuestionario + autobiografía" />
        <Stage title="4. Revisión de 3.er grado" stage={workflow.thirdDegreeReview} pending="Pendiente de revisión y votación abierta" />
        <Stage title="5. Balotaje de 1.er grado" stage={workflow.finalBallot} pending="Pendiente · requiere plazo de publicación cumplido" />
        <Stage title="6. Solicitud de Iniciación" stage={workflow.initiationRequest} pending="Pendiente de balotaje favorable" />
      </div>

      {!workflow.initialDeliberation && !rejected && <div className="candidate-workflow-action">
        <h3>Registrar deliberación inicial</h3>
        {!presentationDate && <div className="error-banner"><span>Primero registre en la ficha la fecha de presentación en 1.er grado.</span></div>}
        {presentationDate && <p>Presentación en 1.er grado: <strong>{formatDateOnly(presentationDate)}</strong>. Primera fecha reglamentaria posible: <strong>{formatDateOnly(addDays(presentationDate, 7))}</strong>.</p>}
        <div className="candidate-workflow-grid">
          <label><span>Fecha deliberación</span><input type="date" min={presentationDate ? addDays(presentationDate, 7) : undefined} value={deliberationDate} onChange={event => setDeliberationDate(event.target.value)} /></label>
          <label><span>Presentes habilitados</span><input type="number" min="1" value={presentVoters} onChange={event => setPresentVoters(Number(event.target.value))} /></label>
          <label><span>Votos favorables</span><input type="number" min="0" max={presentVoters || undefined} value={votesInFavor} onChange={event => setVotesInFavor(Number(event.target.value))} /></label>
          <label className="wide"><span>Referencia acta / extracto</span><input value={deliberationReference} onChange={event => setDeliberationReference(event.target.value)} placeholder="ACTA-..." /></label>
        </div>
        <button className="candidate-primary-button" type="button" disabled={busy || !presentationDate || !deliberationReference.trim() || presentVoters <= 0} onClick={() => void runAction(
          () => api.recordInitialDeliberation(requestId, {
            deliberationDate,
            presentVoters,
            votesInFavor,
            sourceReference: deliberationReference.trim(),
          }),
          'Deliberación inicial registrada.',
        )}>Registrar deliberación</button>
      </div>}

      {workflow.initialDeliberation?.status === 'approved' && !workflow.publication && <div className="candidate-workflow-action">
        <h3>Esperando publicación institucional</h3>
        <p>La aprobación inicial ya está registrada. Gran Secretaría debe revisar la ficha y ejecutar «Aprobar y publicar». Sólo desde esa publicación comienzan los 20 días corridos.</p>
      </div>}

      {workflow.publication && !workflow.interviewPackage && !rejected && <div className="candidate-workflow-action">
        <h3>Entrevistas y antecedentes privados</h3>
        <p>Publicación iniciada el {formatDateOnly(workflow.publication.publishedFromUtc.slice(0, 10))}. Han transcurrido {publicationElapsed ?? 0} de {workflow.publication.requiredDays} días. Las entrevistas pueden registrarse durante este período.</p>
        <div className="candidate-interview-list">
          {interviews.map((item, index) => <div className="candidate-interview-card" key={item.id}>
            <div className="candidate-interview-heading"><strong>Entrevista {index + 1}</strong>{interviews.length > 3 && <button type="button" className="candidate-link-button" onClick={() => setInterviews(current => current.filter(value => value.id !== item.id))}>Quitar</button>}</div>
            <div className="candidate-workflow-grid">
              <label><span>Fecha</span><input type="date" value={item.interviewDate} onChange={event => updateInterview(item.id, { interviewDate: event.target.value })} /></label>
              <label><span>Maestro entrevistador</span><input value={item.interviewerDisplayName} onChange={event => updateInterview(item.id, { interviewerDisplayName: event.target.value })} /></label>
              <label><span>Resultado</span><select value={item.result} onChange={event => updateInterview(item.id, { result: event.target.value as InterviewDraft['result'] })}><option value="favorable">Favorable</option><option value="desfavorable">Desfavorable</option></select></label>
              <label><span>Informe Word/PDF</span><input type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" onChange={event => updateInterview(item.id, { file: event.target.files?.[0] ?? null })} /></label>
              <label className="wide"><span>Resumen en sistema</span><textarea rows={3} maxLength={1000} value={item.summary} onChange={event => updateInterview(item.id, { summary: event.target.value })} /></label>
            </div>
          </div>)}
        </div>
        <button className="candidate-secondary-button" type="button" disabled={busy} onClick={() => setInterviews(current => [...current, emptyInterview()])}>Agregar entrevista adicional</button>
        <div className="candidate-workflow-grid candidate-workflow-private-refs">
          <label><span>Referencia privada Cuestionario Confidencial</span><input value={questionnaireReference} onChange={event => setQuestionnaireReference(event.target.value)} placeholder="DOC-PRIVADO-..." /></label>
          <label><span>Referencia privada autobiografía</span><input value={autobiographyReference} onChange={event => setAutobiographyReference(event.target.value)} placeholder="DOC-PRIVADO-..." /></label>
        </div>
        <button className="candidate-primary-button" type="button" disabled={busy} onClick={() => void submitInterviews()}>Guardar entrevistas y antecedentes</button>
      </div>}

      {workflow.interviewPackage?.status === 'approved' && !workflow.thirdDegreeReview && !rejected && <div className="candidate-workflow-action">
        <h3>Revisión y votación abierta de 3.er grado</h3>
        <div className="candidate-workflow-grid">
          <label><span>Fecha revisión</span><input type="date" value={thirdDate} onChange={event => setThirdDate(event.target.value)} /></label>
          <label><span>Presentes</span><input type="number" min="1" value={thirdPresent} onChange={event => setThirdPresent(Number(event.target.value))} /></label>
          <label><span>Favorables</span><input type="number" min="0" value={thirdFor} onChange={event => setThirdFor(Number(event.target.value))} /></label>
          <label><span>Desfavorables</span><input type="number" min="0" value={thirdAgainst} onChange={event => setThirdAgainst(Number(event.target.value))} /></label>
          <label><span>Abstenciones</span><input type="number" min="0" value={thirdAbstentions} onChange={event => setThirdAbstentions(Number(event.target.value))} /></label>
          <label><span>Resultado institucional</span><select value={thirdApproved ? 'approved' : 'rejected'} onChange={event => setThirdApproved(event.target.value === 'approved')}><option value="approved">Favorable</option><option value="rejected">Desfavorable</option></select></label>
          <label className="wide"><span>Referencia extracto de acta</span><input value={thirdReference} onChange={event => setThirdReference(event.target.value)} /></label>
        </div>
        <button className="candidate-primary-button" type="button" disabled={busy || !thirdReference.trim() || thirdPresent <= 0} onClick={() => void runAction(
          () => api.recordThirdDegreeReview(requestId, {
            reviewDate: thirdDate,
            presentVoters: thirdPresent,
            votesInFavor: thirdFor,
            votesAgainst: thirdAgainst,
            abstentions: thirdAbstentions,
            openVoteApproved: thirdApproved,
            sourceReference: thirdReference.trim(),
          }),
          'Revisión de 3.er grado registrada.',
        )}>Registrar revisión de 3.er grado</button>
      </div>}

      {workflow.thirdDegreeReview?.status === 'approved' && !workflow.finalBallot && !rejected && <div className="candidate-workflow-action">
        <h3>Balotaje definitivo de 1.er grado</h3>
        {workflow.publication && <p>Publicación: {publicationElapsed ?? 0}/{workflow.publication.requiredDays} días corridos. El backend bloqueará el balotaje mientras el plazo no esté cumplido.</p>}
        <div className="candidate-workflow-grid">
          <label><span>Fecha balotaje</span><input type="date" value={ballotDate} onChange={event => setBallotDate(event.target.value)} /></label>
          <label><span>Resultado definitivo</span><select value={ballotApproved ? 'approved' : 'rejected'} onChange={event => setBallotApproved(event.target.value === 'approved')}><option value="approved">Aprobado</option><option value="rejected">Rechazado</option></select></label>
          <label className="wide"><span>Referencia extracto de acta</span><input value={ballotReference} onChange={event => setBallotReference(event.target.value)} /></label>
        </div>
        <div className="candidate-ballot-rounds">
          {ballots.map((round, index) => <div className="candidate-ballot-round" key={round.procedureNumber}>
            <strong>Trámite {round.procedureNumber}</strong>
            <label><span>Habilitados</span><input type="number" min="1" value={round.eligibleVoters} onChange={event => setBallots(current => current.map((value, row) => row === index ? { ...value, eligibleVoters: Number(event.target.value) } : value))} /></label>
            <label><span>Balotas blancas</span><input type="number" min="0" value={round.whiteBallots} onChange={event => setBallots(current => current.map((value, row) => row === index ? { ...value, whiteBallots: Number(event.target.value) } : value))} /></label>
            <label><span>Balotas negras</span><input type="number" min="0" value={round.blackBallots} onChange={event => setBallots(current => current.map((value, row) => row === index ? { ...value, blackBallots: Number(event.target.value) } : value))} /></label>
          </div>)}
        </div>
        {ballots.length < 3 && <button className="candidate-secondary-button" type="button" disabled={busy} onClick={() => setBallots(current => [...current, { procedureNumber: current.length + 1, eligibleVoters: 0, whiteBallots: 0, blackBallots: 0 }])}>Agregar trámite</button>}
        <button className="candidate-primary-button" type="button" disabled={busy || !ballotReference.trim() || ballots.some(row => row.eligibleVoters <= 0)} onClick={() => void runAction(
          () => api.recordFinalBallot(requestId, {
            ballotDate,
            ballots,
            ballotApproved,
            sourceReference: ballotReference.trim(),
          }),
          'Balotaje definitivo registrado.',
        )}>Registrar balotaje</button>
      </div>}

      {workflow.finalBallot?.status === 'approved' && !workflow.initiationRequest && !rejected && <div className="candidate-workflow-action">
        <h3>Enviar solicitud formal de Iniciación</h3>
        <div className="candidate-workflow-grid">
          <label><span>Fecha envío</span><input type="date" value={chileToday()} disabled /></label>
          <label><span>Fecha referencial solicitada (no programación)</span><input type="date" min={chileToday()} value={proposedDate} onChange={event => setProposedDate(event.target.value)} /></label>
          <label><span>Secretaría responsable</span><input value={secretaryName} onChange={event => setSecretaryName(event.target.value)} /></label>
          <label className="candidate-check"><input type="checkbox" checked={venerableApproval} onChange={event => setVenerableApproval(event.target.checked)} /><span>Confirmación del Venerable Maestro</span></label>
          <label className="wide"><span>Referencia documental</span><input value={initiationReference} onChange={event => setInitiationReference(event.target.value)} /></label>\n          <p className="candidate-form-note">La fecha indicada es sólo referencial. La ceremonia no se puede establecer ni programar hasta que Gran Secretaría emita la Plancha de Autorización.</p>
        </div>
        <button className="candidate-primary-button" type="button" disabled={busy || !venerableApproval || !secretaryName.trim() || !initiationReference.trim()} onClick={() => void runAction(
          () => api.submitInitiationRequest(requestId, {
            submissionDate: chileToday(),
            proposedCeremonyDate: proposedDate,
            venerableApproval,
            secretaryDisplayName: secretaryName.trim(),
            sourceReference: initiationReference.trim(),
          }),
          'Solicitud formal de Iniciación enviada al flujo de validaciones superiores.',
        )}>Enviar solicitud de Iniciación</button>
      </div>}

      {workflow.initiationRequest?.status === 'approved' && <div className="candidate-protected-notice">
        <strong>Flujo de insinuación completado.</strong> La solicitud de Iniciación continúa con Régimen Interior, Gran Tesorería, Gran Hospitalaria, Gran Maestría y Gran Secretaría según las validaciones vigentes.
      </div>}
    </>}
  </section>
}

function Stage({ title, stage, pending }: { title: string; stage: CandidateWorkflowStageLike | null; pending: string }) {
  const status = stage?.status ?? 'pending'
  return <div className={`candidate-workflow-stage ${status}`}>
    <span className="candidate-workflow-dot" />
    <div>
      <strong>{title}</strong>
      <small>{stage ? `${statusLabel(status)} · ${formatDateOnly(stage.asOfDate)}` : pending}</small>
      {stage?.notes && <p>{stage.notes}</p>}
    </div>
  </div>
}

interface CandidateWorkflowStageLike {
  status: string
  asOfDate: string
  notes: string | null
}

function publicationAsStage(workflow: CandidateWorkflowResponse): CandidateWorkflowStageLike | null {
  if (!workflow.publication) return null
  return {
    status: workflow.publication.status === 'published' || workflow.publication.status === 'completed' ? 'approved' : workflow.publication.status,
    asOfDate: workflow.publication.publishedFromUtc.slice(0, 10),
    notes: `Plazo aplicado: ${workflow.publication.requiredDays} días corridos.`,
  }
}

function statusLabel(status: string) {
  if (status === 'approved' || status === 'published' || status === 'completed') return 'Aprobado'
  if (status === 'rejected') return 'Rechazado'
  if (status === 'observed') return 'Observado'
  return 'Pendiente'
}

function chileToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]))
  return `${values.year}-${values.month}-${values.day}`
}

function addDays(value: string, days: number) {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day + days, 12))
  return date.toISOString().slice(0, 10)
}

function daysBetween(fromDate: string, toDate: string) {
  const from = Date.parse(`${fromDate}T12:00:00Z`)
  const to = Date.parse(`${toDate}T12:00:00Z`)
  return Math.floor((to - from) / 86_400_000)
}

function formatDateOnly(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return value
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(year, month - 1, day, 12)))
}

function errorMessage(reason: unknown, fallback: string) {
  return reason instanceof Error ? reason.message : fallback
}
