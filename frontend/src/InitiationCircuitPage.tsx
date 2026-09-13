import { useMemo, useState } from 'react'
import type { CandidateInterviewResult, CandidatePublicationWorkflowResponse, FinalBallotRound, PmgmApiClient } from './api/pmgmApi'
import type { DemoProfileKey } from './demoProfiles'
import './initiationCircuit.css'
import './initiationDeliberation.css'

const stages = [
  { title: 'Ingreso del insinuado', owner: 'Secretaría del Taller', profile: 'lodge', evidence: 'Ficha 2026, fotografía, fecha de ingreso y secretario responsable' },
  { title: 'Presentación y deliberación inicial', owner: 'Taller · 1.er grado', profile: 'lodge', evidence: 'Acta de presentación, espera mínima de 7 días y acuerdo unánime' },
  { title: 'Publicación institucional', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Publicación en intranet por 20 días corridos' },
  { title: 'Entrevistas y antecedentes', owner: 'Maestros entrevistadores', profile: 'lodge', evidence: 'Mínimo tres entrevistas con resumen, resultado, Word/PDF privado, cuestionario confidencial y autobiografía' },
  { title: 'Revisión en 3.er grado', owner: 'Maestros del Taller', profile: 'lodge', evidence: 'Votación abierta y extracto de acta' },
  { title: 'Balotaje definitivo', owner: 'Taller · 1.er grado', profile: 'lodge', evidence: 'Balotaje anónimo, balotas blancas/negras y extracto de acta' },
  { title: 'Solicitud de Iniciación', owner: 'Venerable Maestro y Secretaría', profile: 'lodge', evidence: 'Solicitud vinculada al mismo expediente, sin redigitación' },
  { title: 'Aprobación de Régimen Interior', owner: 'Régimen Interior', profile: 'regimen', evidence: 'Procedimiento, documentos, tenidas, asistencia y balotaje válidos' },
  { title: 'Aprobación de Gran Tesorería', owner: 'Gran Tesorero', profile: 'treasury', evidence: 'Regularidad del Taller, derecho de Iniciación y conciliación' },
  { title: 'Aprobación de Gran Hospitalaria', owner: 'Gran Hospitalaria', profile: 'hospitalaria', evidence: 'Reposiciones, Fondo de Defunción y aportes aplicables' },
  { title: 'Control administrativo', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Expediente íntegro, aprobaciones vigentes, fecha y lugar' },
  { title: 'Visto bueno institucional', owner: 'Gran Maestra', profile: 'grandMaster', evidence: 'Decisión final sobre el expediente completo y trazable' },
  { title: 'Plancha y programación', owner: 'Gran Secretaría', profile: 'secretariat', evidence: 'Plancha de autorización, fecha, Taller y reserva si corresponde' },
  { title: 'Ceremonia y activación', owner: 'Taller', profile: 'lodge', evidence: 'Acta de Iniciación; alta como miembro activo y Aprendiz' },
] as const

interface InterviewDraft { id: string; interviewDate: string; interviewerDisplayName: string; summary: string; result: CandidateInterviewResult; documentVersionId: string | null; fileName: string | null; uploading: boolean }
const demoRequestId = 'eeeeeeee-2222-2222-2222-222222222222'

export default function InitiationCircuitPage({ api, demoProfileKey }: { api: PmgmApiClient; demoProfileKey?: DemoProfileKey }) {
  const initialCompleted = readDemoProgress(api.useMocks)
  const [completed, setCompleted] = useState(initialCompleted)
  const [selected, setSelected] = useState(Math.min(initialCompleted, stages.length - 1))
  const [history, setHistory] = useState<string[]>([])
  const [decision, setDecision] = useState<'approved' | 'rejected' | 'observed' | null>(null)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [deliberationDate, setDeliberationDate] = useState('2026-09-20')
  const [presentVoters, setPresentVoters] = useState(12)
  const [votesInFavor, setVotesInFavor] = useState(12)
  const [deliberationSource, setDeliberationSource] = useState('ACTA-1G-DEMO-2026-001')
  const [publication, setPublication] = useState<CandidatePublicationWorkflowResponse | null>(null)
  const [publicationControlDate, setPublicationControlDate] = useState('2026-10-11')
  const [interviews, setInterviews] = useState<InterviewDraft[]>(initialInterviews)
  const [questionnaireAvailable, setQuestionnaireAvailable] = useState(true)
  const [questionnaireReference, setQuestionnaireReference] = useState('CUEST-CONF-DEMO-2026-001')
  const [autobiographyAvailable, setAutobiographyAvailable] = useState(true)
  const [autobiographyReference, setAutobiographyReference] = useState('AUTOBIO-DEMO-2026-001')
  const [thirdDegreeDate, setThirdDegreeDate] = useState('2026-10-12')
  const [thirdDegreePresent, setThirdDegreePresent] = useState(12)
  const [thirdDegreeFavor, setThirdDegreeFavor] = useState(10)
  const [thirdDegreeAgainst, setThirdDegreeAgainst] = useState(2)
  const [thirdDegreeAbstentions, setThirdDegreeAbstentions] = useState(0)
  const [thirdDegreeApproved, setThirdDegreeApproved] = useState(true)
  const [thirdDegreeSource, setThirdDegreeSource] = useState('R∴L∴ DEMO Nº 23 · TENIDA 18 · 3.er GRADO · EXTRACTO 2026-010')
  const [ballotDate, setBallotDate] = useState('2026-10-12')
  const [ballotRounds, setBallotRounds] = useState<FinalBallotRound[]>([{ procedureNumber: 1, eligibleVoters: 12, whiteBallots: 11, blackBallots: 1 }])
  const [ballotApproved, setBallotApproved] = useState(true)
  const [ballotSource, setBallotSource] = useState('R∴L∴ DEMO Nº 23 · TENIDA 18 · 1.er GRADO · EXTRACTO 2026-010')
  const [requestSubmissionDate, setRequestSubmissionDate] = useState('2026-10-13')
  const [proposedCeremonyDate, setProposedCeremonyDate] = useState('2026-11-07')
  const [venerableApproval, setVenerableApproval] = useState(true)
  const [secretaryDisplayName, setSecretaryDisplayName] = useState('Secretaria Demostrativa del Taller')
  const [initiationRequestSource, setInitiationRequestSource] = useState('SOL-INI-DEMO-2026-001')
  const [regimenSource, setRegimenSource] = useState('ACTA-RI-DEMO-2026-023')
  const [regimenNotes, setRegimenNotes] = useState('Procedimiento, documentos, tenidas, asistencia y balotaje revisados conforme.')
  const current = stages[selected]
  const canDecideCurrent = !demoProfileKey || demoProfileKey === 'grandLodge' || demoProfileKey === current.profile
  const finished = completed === stages.length
  const progress = Math.round((completed / stages.length) * 100)
  const status = useMemo(() => finished ? 'Hermano activo · Aprendiz' : completed >= 10 ? 'Ceremonia autorizada' : completed >= 6 ? 'Candidato aprobado' : 'Insinuado en tramitación', [completed, finished])

  const advance = async () => {
    if (finished) return
    setWorking(true); setError(null)
    try {
      if (!canDecideCurrent) throw new Error(`Esta etapa corresponde a ${current.owner}. Cambie al perfil aprobante indicado.`)
      if (completed === 1) {
        const result = await api.recordInitialDeliberation(demoRequestId, { deliberationDate, presentVoters, votesInFavor, minimumWaitingDays: 7, sourceReference: deliberationSource })
        if (result.validationStatus !== 'approved') {
          setDecision(result.validationStatus)
          setHistory(items => [`${stages[completed].title} — ${result.reason} · respaldo ${deliberationSource || 'sin referencia'}`, ...items])
          return
        }
      }
      if (completed === 2) {
        const result = publication ?? await api.publishCeremonyCandidate(demoRequestId)
        setPublication(result)
        const controlDate = api.useMocks ? publicationControlDate : new Date().toISOString().slice(0, 10)
        const elapsedDays = Math.max(0, dateOnlyDayNumber(controlDate) - dateOnlyDayNumber(result.publishedFromUtc.slice(0, 10)))
        if (elapsedDays < result.requiredDays) {
          setDecision('observed')
          setHistory(items => [`${stages[completed].title} — ${elapsedDays}/${result.requiredDays} días cumplidos; disponible desde ${formatDateOnly(addDays(result.publishedFromUtc.slice(0, 10), result.requiredDays))}`, ...items])
          return
        }
      }
      if (completed === 3) {
        const completedEvidence = interviews.filter(item => item.documentVersionId)
        if (completedEvidence.length < 3) throw new Error('Debe cargar el archivo Word o PDF de al menos tres entrevistas.')
        const result = await api.recordInterviewPackage(demoRequestId, { asOfDate: completedEvidence.map(item => item.interviewDate).sort().at(-1) ?? '2026-09-25', interviews: completedEvidence.map(item => ({ interviewDate: item.interviewDate, interviewerDisplayName: item.interviewerDisplayName, summary: item.summary, result: item.result, documentVersionId: item.documentVersionId! })), confidentialQuestionnaireAvailable: questionnaireAvailable, confidentialQuestionnaireReference: questionnaireReference || null, autobiographyAvailable, autobiographyReference: autobiographyReference || null })
        if (result.validationStatus !== 'approved') { setDecision(result.validationStatus); setHistory(items => [`${stages[completed].title} — ${result.reason}`, ...items]); return }
      }
      if (completed === 4) {
        const result = await api.recordThirdDegreeReview(demoRequestId, { reviewDate: thirdDegreeDate, presentVoters: thirdDegreePresent, votesInFavor: thirdDegreeFavor, votesAgainst: thirdDegreeAgainst, abstentions: thirdDegreeAbstentions, openVoteApproved: thirdDegreeApproved, sourceReference: thirdDegreeSource })
        if (result.thirdDegree.status !== 'approved') { setDecision(result.thirdDegree.status); setHistory(items => [`${stages[completed].title} — ${result.thirdDegree.reason} · ${thirdDegreeFavor}/${thirdDegreePresent} favorables`, ...items]); return }
      }
      if (completed === 5) {
        const result = await api.recordFinalBallot(demoRequestId, { ballotDate, ballots: ballotRounds, ballotApproved, sourceReference: ballotSource })
        if (result.validationStatus !== 'approved') { setDecision(result.validationStatus); setHistory(items => [`${stages[completed].title} — ${result.reason}`, ...items]); return }
      }
      if (completed === 6) {
        const result = await api.submitInitiationRequest(demoRequestId, { submissionDate: requestSubmissionDate, proposedCeremonyDate, venerableApproval, secretaryDisplayName, sourceReference: initiationRequestSource })
        setHistory(items => [`${stages[completed].title} — ${result.alreadySubmitted ? 'envío formal ya existente' : 'enviada'} · ceremonia propuesta ${formatDateOnly(result.proposedDate)}`, ...items])
      }
      if (completed === 7) {
        await api.setCeremonyInternalAffairsValidation(demoRequestId, { status: 'approved', sourceReference: regimenSource, notes: regimenNotes })
      }
      if (completed === stages.length - 1) await api.registerInitiation(demoRequestId, '2026-09-12', 'ACTA-INI-DEMO-2026-001')
    const stage = stages[completed]
    setDecision('approved')
    setHistory(items => [`${stage.title} — evidencia registrada`, ...items])
    const next = completed + 1
    setCompleted(next)
    if (api.useMocks && typeof window !== 'undefined') window.localStorage.setItem('centenario.demo.initiation.completed', String(next))
    setSelected(Math.min(completed + 1, stages.length - 1))
    setDecision(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible registrar la etapa.') } finally { setWorking(false) }
  }

  const updateInterview = (id: string, patch: Partial<InterviewDraft>, preserveDocument = false) => setInterviews(items => items.map(item => item.id === id ? { ...item, ...patch, documentVersionId: preserveDocument ? (patch.documentVersionId ?? item.documentVersionId) : null, fileName: preserveDocument ? (patch.fileName ?? item.fileName) : null } : item))
  const uploadInterview = async (interview: InterviewDraft, file?: File) => {
    if (!file) return
    setError(null); updateInterview(interview.id, { uploading: true }, true)
    try {
      const uploaded = await api.uploadInterviewDocument(demoRequestId, interview.id, file, { interviewDate: interview.interviewDate, interviewerDisplayName: interview.interviewerDisplayName, summary: interview.summary, result: interview.result })
      updateInterview(interview.id, { uploading: false, documentVersionId: uploaded.documentVersionId, fileName: uploaded.fileName }, true)
    } catch (reason) { updateInterview(interview.id, { uploading: false }, true); setError(reason instanceof Error ? reason.message : 'No fue posible cargar la entrevista.') }
  }
  const addInterview = () => setInterviews(items => [...items, { id: crypto.randomUUID(), interviewDate: '2026-09-26', interviewerDisplayName: `Entrevistador Demostrativo ${items.length + 1}`, summary: '', result: 'favorable', documentVersionId: null, fileName: null, uploading: false }])
  const updateBallotRound = (procedureNumber: number, patch: Partial<FinalBallotRound>) => setBallotRounds(items => items.map(item => item.procedureNumber === procedureNumber ? { ...item, ...patch } : item))
  const addBallotRound = () => setBallotRounds(items => { const procedureNumber = ([1, 2, 3] as const).find(number => !items.some(item => item.procedureNumber === number)); return procedureNumber ? [...items, { procedureNumber, eligibleVoters: 12, whiteBallots: 11, blackBallots: 1 }] : items })

  const reject = () => {
    if (selected !== completed || finished) return
    const stage = stages[selected]
    setDecision('rejected')
    setHistory(items => [`${stage.title} — rechazado por el aprobante; el expediente queda detenido`, ...items])
  }

  const observe = () => {
    if (selected !== completed || finished) return
    const stage = stages[selected]
    setDecision('observed')
    setHistory(items => [`${stage.title} — observado; se solicita subsanar antecedentes`, ...items])
  }

  const restart = () => { setCompleted(0); setSelected(0); setHistory([]); setDecision(null); if (api.useMocks && typeof window !== 'undefined') window.localStorage.removeItem('centenario.demo.initiation.completed') }

  return <div className="initiation-circuit">
    <header className="initiation-hero">
      <div><p className="eyebrow">Expediente único · datos ficticios</p><h1>Circuito completo de Iniciación</h1><p>Desde el ingreso por el Taller hasta la ceremonia efectiva y el alta como Aprendiz.</p></div>
      <div className="initiation-case"><small>EXPEDIENTE</small><strong>INI-DEMO-2026-001</strong><span>{status}</span></div>
    </header>

    <section className="initiation-progress" aria-label="Avance del expediente"><div><strong>{progress}% completado</strong><span>{completed}/{stages.length} etapas cerradas</span></div><div className="initiation-track"><span style={{ width: `${progress}%` }} /></div></section>

    <div className="initiation-layout">
      <ol className="initiation-steps">
        {stages.map((stage, index) => <li key={stage.title}><button type="button" className={index < completed ? 'done' : index === completed ? 'current' : ''} onClick={() => setSelected(index)}><span>{index < completed ? '✓' : index + 1}</span><div><strong>{stage.title}</strong><small>{stage.owner}</small></div></button></li>)}
      </ol>

      <section className="initiation-detail">
        {error && <div className="error-banner" role="alert">{error}</div>}
        <p className="eyebrow">Etapa {selected + 1}</p><h2>{current.title}</h2>
        <dl><div><dt>Responsable / aprobante</dt><dd>{current.owner}</dd></div><div><dt>Evidencia exigida</dt><dd>{current.evidence}</dd></div><div><dt>Estado</dt><dd>{selected < completed ? 'Completada' : selected === completed ? (decision === 'rejected' ? 'Rechazada · requiere corrección' : decision === 'observed' ? 'Observada · pendiente de subsanar' : 'Pendiente de decisión') : 'Bloqueada por etapa anterior'}</dd></div></dl>
        {selected === completed && !finished && <div className="initiation-approval-note"><strong>Esta etapa requiere aprobación</strong><span>El aprobante revisa el expediente completo, actividad, tenidas y documentos antes de resolver.</span></div>}
        {selected === completed && !finished && !canDecideCurrent && <div className="error-banner" role="status"><strong>Cambio de perfil requerido.</strong><span>Seleccione “{current.owner}” en Perfil QA para resolver esta tarea.</span></div>}
        {selected === 0 && <div className="initiation-fields"><label>Insinuado<input value="Persona Demostrativa Centenario" readOnly /></label><label>Taller<input value="Taller Demostrativo Nº 23" readOnly /></label><label>Fecha de ingreso<input value="12-09-2026" readOnly /></label><label>Secretario responsable<input value="Secretario Demostrativo" readOnly /></label></div>}
        {selected === 1 && <div className="initiation-deliberation"><div className="initiation-rule-check"><strong>Reglas automáticas</strong><span>Presentación: 12-09-2026 · espera mínima: 7 días · aprobación: unanimidad</span></div><div className="initiation-fields"><label>Fecha de deliberación<input type="date" value={deliberationDate} onChange={event => setDeliberationDate(event.target.value)} /></label><label>Asambleístas presentes<input type="number" min="1" value={presentVoters} onChange={event => setPresentVoters(Number(event.target.value))} /></label><label>Votos favorables<input type="number" min="0" max={presentVoters} value={votesInFavor} onChange={event => setVotesInFavor(Number(event.target.value))} /></label><label>Acta o extracto de respaldo<input value={deliberationSource} required maxLength={240} onChange={event => setDeliberationSource(event.target.value)} /></label></div><p className="initiation-vote-summary"><strong>{votesInFavor === presentVoters && presentVoters > 0 ? 'Unanimidad registrada' : 'La votación no es unánime'}</strong><span>{votesInFavor} de {presentVoters} votos favorables</span></p></div>}
        {selected === 2 && <div className="initiation-deliberation"><div className="initiation-rule-check"><strong>Publicación institucional protegida</strong><span>Gran Secretaría publica sólo con ficha completa y deliberación inicial aprobada.</span></div><div className="initiation-fields"><label>Período exigido<input value={`${publication?.requiredDays ?? 20} días corridos`} readOnly /></label>{api.useMocks && <label>Fecha de control QA<input type="date" value={publicationControlDate} onChange={event => setPublicationControlDate(event.target.value)} /></label>}<label>Inicio de publicación<input value={publication ? formatDateOnly(publication.publishedFromUtc.slice(0, 10)) : 'Se asigna al publicar'} readOnly /></label><label>Notificaciones internas<input value={publication ? `${publication.notificationsCreated} generadas` : 'Pendientes'} readOnly /></label></div>{publication && <p className="initiation-vote-summary"><strong>{publication.status === 'published' ? 'Visible en portal institucional' : publication.status}</strong><span>Regla: {publication.ruleCode}</span></p>}</div>}
        {selected === 3 && <div className="interview-package"><div className="initiation-rule-check"><strong>Mínimo tres; adicionales por decisión del Venerable Maestro</strong><span>El sistema muestra sólo el resumen y el resultado. En QA el Word o PDF se conserva temporalmente durante la sesión; en producción queda como antecedente privado.</span></div><div className="interview-list">{interviews.map((interview, index) => <article className="interview-card" key={interview.id}><header><strong>Entrevista {index + 1}</strong><button type="button" disabled={interviews.length <= 3 || interview.uploading} onClick={() => setInterviews(items => items.filter(item => item.id !== interview.id))}>Quitar</button></header><div className="initiation-fields"><label>Fecha<input type="date" value={interview.interviewDate} onChange={event => updateInterview(interview.id, { interviewDate: event.target.value })} /></label><label>Responsable<input value={interview.interviewerDisplayName} onChange={event => updateInterview(interview.id, { interviewerDisplayName: event.target.value })} /></label></div><label className="interview-summary">Resumen<textarea maxLength={1000} value={interview.summary} onChange={event => updateInterview(interview.id, { summary: event.target.value })} /></label><div className="interview-result"><label>Resultado<select value={interview.result} onChange={event => updateInterview(interview.id, { result: event.target.value as CandidateInterviewResult })}><option value="favorable">Favorable</option><option value="desfavorable">Desfavorable</option></select></label><label className="interview-upload">Antecedente Word o PDF<input type="file" accept=".docx,.pdf,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" disabled={interview.uploading} onChange={event => void uploadInterview(interview, event.target.files?.[0])} /><span>{interview.uploading ? 'Analizando archivo…' : interview.fileName ?? 'Sin archivo cargado'}</span></label></div></article>)}</div><button type="button" className="regularity-secondary" onClick={addInterview}>Agregar entrevista adicional</button><div className="interview-attachments"><label><input type="checkbox" checked={questionnaireAvailable} onChange={event => setQuestionnaireAvailable(event.target.checked)} /> Cuestionario confidencial disponible</label><input aria-label="Referencia cuestionario confidencial" value={questionnaireReference} onChange={event => setQuestionnaireReference(event.target.value)} /><label><input type="checkbox" checked={autobiographyAvailable} onChange={event => setAutobiographyAvailable(event.target.checked)} /> Autobiografía disponible</label><input aria-label="Referencia autobiografía" value={autobiographyReference} onChange={event => setAutobiographyReference(event.target.value)} /></div></div>}
        {selected === 4 && <div className="initiation-deliberation"><div className="initiation-rule-check"><strong>Revisión en Cámara de tercer grado</strong><span>Requiere antecedentes previamente validados. Se conserva el extracto de acta y el cómputo agregado; no se registra el voto individual.</span></div><div className="initiation-fields"><label>Fecha de la tenida<input type="date" value={thirdDegreeDate} onChange={event => setThirdDegreeDate(event.target.value)} /></label><label>Asambleístas presentes<input type="number" min="1" value={thirdDegreePresent} onChange={event => setThirdDegreePresent(Number(event.target.value))} /></label><label>Votos favorables<input type="number" min="0" value={thirdDegreeFavor} onChange={event => setThirdDegreeFavor(Number(event.target.value))} /></label><label>Votos desfavorables<input type="number" min="0" value={thirdDegreeAgainst} onChange={event => setThirdDegreeAgainst(Number(event.target.value))} /></label><label>Abstenciones<input type="number" min="0" value={thirdDegreeAbstentions} onChange={event => setThirdDegreeAbstentions(Number(event.target.value))} /></label><label>Acuerdo consignado<select value={thirdDegreeApproved ? 'favorable' : 'desfavorable'} onChange={event => setThirdDegreeApproved(event.target.value === 'favorable')}><option value="favorable">Favorable</option><option value="desfavorable">Desfavorable</option></select></label><label className="interview-summary">Referencia del extracto de acta<input required maxLength={240} value={thirdDegreeSource} onChange={event => setThirdDegreeSource(event.target.value)} /></label></div><p className="initiation-vote-summary"><strong>{thirdDegreeFavor + thirdDegreeAgainst + thirdDegreeAbstentions === thirdDegreePresent && thirdDegreePresent > 0 ? 'Cómputo consistente' : 'Revise el cómputo de la votación'}</strong><span>{thirdDegreeFavor} favorables · {thirdDegreeAgainst} desfavorables · {thirdDegreeAbstentions} abstenciones · {thirdDegreePresent} presentes</span></p></div>}
        {selected === 5 && <div className="interview-package"><div className="initiation-rule-check"><strong>Balotaje anónimo con escrutinio agregado</strong><span>El extracto 2026 admite primer, segundo y tercer trámite. El sistema nunca vincula una balota con una persona.</span></div><div className="initiation-fields"><label>Fecha del balotaje<input type="date" value={ballotDate} onChange={event => setBallotDate(event.target.value)} /></label><label>Resultado consignado<select value={ballotApproved ? 'favorable' : 'desfavorable'} onChange={event => setBallotApproved(event.target.value === 'favorable')}><option value="favorable">Favorable</option><option value="desfavorable">Desfavorable</option></select></label><label className="interview-summary">Referencia del extracto de acta<input required maxLength={240} value={ballotSource} onChange={event => setBallotSource(event.target.value)} /></label></div><div className="interview-list">{ballotRounds.map(round => <article className="interview-card" key={round.procedureNumber}><header><strong>{procedureLabel(round.procedureNumber)} trámite</strong><button type="button" disabled={ballotRounds.length === 1} onClick={() => setBallotRounds(items => items.filter(item => item.procedureNumber !== round.procedureNumber))}>Quitar</button></header><div className="initiation-fields"><label>Personas habilitadas<input type="number" min="1" value={round.eligibleVoters} onChange={event => updateBallotRound(round.procedureNumber, { eligibleVoters: Number(event.target.value) })} /></label><label>Balotas blancas<input type="number" min="0" value={round.whiteBallots} onChange={event => updateBallotRound(round.procedureNumber, { whiteBallots: Number(event.target.value) })} /></label><label>Balotas negras<input type="number" min="0" value={round.blackBallots} onChange={event => updateBallotRound(round.procedureNumber, { blackBallots: Number(event.target.value) })} /></label></div><p className="initiation-vote-summary"><strong>{round.whiteBallots + round.blackBallots === round.eligibleVoters && round.eligibleVoters > 0 ? 'Escrutinio consistente' : 'Revise el escrutinio'}</strong><span>{round.whiteBallots} blancas · {round.blackBallots} negras · {round.eligibleVoters} habilitadas</span></p></article>)}</div><button type="button" className="regularity-secondary" disabled={ballotRounds.length >= 3} onClick={addBallotRound}>Agregar trámite</button></div>}
        {selected === 6 && <div className="initiation-deliberation"><div className="initiation-rule-check"><strong>Mismo expediente, sin redigitación</strong><span>La identidad del candidato, Taller, entrevistas, publicación y votaciones se reutilizan desde INI-DEMO-2026-001.</span></div><div className="initiation-fields"><label>Candidato<input value="Persona Demostrativa Centenario" readOnly /></label><label>Taller solicitante<input value="Taller Demostrativo Nº 23" readOnly /></label><label>Fecha de solicitud<input type="date" value={requestSubmissionDate} onChange={event => setRequestSubmissionDate(event.target.value)} /></label><label>Fecha propuesta de ceremonia<input type="date" min={requestSubmissionDate} value={proposedCeremonyDate} onChange={event => setProposedCeremonyDate(event.target.value)} /></label><label>Secretaría responsable<input required maxLength={240} value={secretaryDisplayName} onChange={event => setSecretaryDisplayName(event.target.value)} /></label><label>Referencia documental<input required maxLength={240} value={initiationRequestSource} onChange={event => setInitiationRequestSource(event.target.value)} /></label></div><label className="interview-summary"><input type="checkbox" checked={venerableApproval} onChange={event => setVenerableApproval(event.target.checked)} /> Confirmación del Venerable Maestro para enviar la solicitud formal</label><p className="initiation-vote-summary"><strong>{venerableApproval && secretaryDisplayName.trim() && initiationRequestSource.trim() ? 'Solicitud completa para envío' : 'Faltan confirmaciones obligatorias'}</strong><span>El envío no crea otra ficha ni otro candidato.</span></p></div>}
        {selected === 7 && <div className="initiation-deliberation"><div className="initiation-rule-check"><strong>Control de Régimen Interior</strong><span>El Taller declara conformes el procedimiento, documentos, tenidas, asistencia y balotaje antes de continuar con Tesorería.</span></div><div className="initiation-fields"><label>Referencia del acuerdo<input required maxLength={240} value={regimenSource} onChange={event => setRegimenSource(event.target.value)} /></label><label className="interview-summary">Observaciones de revisión<textarea maxLength={1000} rows={3} value={regimenNotes} onChange={event => setRegimenNotes(event.target.value)} /></label></div><p className="initiation-vote-summary"><strong>{regimenSource.trim() ? 'Antecedentes listos para aprobar' : 'Falta referencia documental'}</strong><span>Resultado: aprobación habilitante de Régimen Interior.</span></p></div>}
        {selected === 12 && <div className="initiation-document"><span>PLANCHA</span><strong>{completed > 12 ? 'AUT-CER-DEMO-2026-001' : 'Se genera únicamente tras el visto bueno'}</strong><small>Permanece vinculada al expediente y a las validaciones congeladas.</small></div>}
        {selected === 13 && <div className="initiation-member"><strong>{finished ? 'Aprendiz activado' : 'Activación todavía bloqueada'}</strong><span>{finished ? 'Persona Demostrativa Centenario · Miembro activo · 1.er grado' : 'La autorización no convierte por sí sola al candidato en hermano.'}</span></div>}
        <div className="initiation-actions"><button type="button" className="regularity-primary" disabled={working || finished || selected !== completed || decision === 'rejected' || !canDecideCurrent} onClick={advance}>{working ? 'Registrando…' : completed === 1 ? 'Registrar deliberación inicial' : completed === 2 ? (publication ? 'Verificar plazo y continuar' : 'Aprobar y publicar insinuado') : completed === 3 ? 'Validar entrevistas y antecedentes' : completed === 4 ? 'Registrar revisión de tercer grado' : completed === 5 ? 'Registrar balotaje definitivo' : completed === 6 ? 'Enviar solicitud formal de Iniciación' : completed === 7 ? 'Aprobar Régimen Interior' : completed === 12 ? 'Aprobar y emitir Plancha' : completed === 13 ? 'Registrar ceremonia y activar Aprendiz' : 'Registrar etapa y continuar'}</button>{selected === completed && !finished && completed < 13 && <><button type="button" className="regularity-secondary" disabled={working || !canDecideCurrent} onClick={observe}>Observar</button><button type="button" className="regularity-secondary" disabled={working || !canDecideCurrent} onClick={reject}>Rechazar</button></>}<button type="button" className="regularity-secondary" disabled={working} onClick={restart}>Reiniciar caso de prueba</button></div>
      </section>
    </div>

    <section className="initiation-audit"><div><p className="eyebrow">Trazabilidad</p><h2>Bitácora del expediente</h2></div>{history.length === 0 ? <p>Aún no hay etapas registradas en esta ejecución de prueba.</p> : <ul>{history.map((item, index) => <li key={`${item}-${index}`}><span>✓</span>{item}</li>)}</ul>}</section>
    <p className="initiation-rule"><strong>Regla de integridad:</strong> candidato aprobado ≠ ceremonia autorizada ≠ hermano iniciado. La membresía y el grado Aprendiz sólo nacen al registrar la ceremonia efectivamente realizada.</p>
  </div>
}

function readDemoProgress(useMocks: boolean) {
  if (!useMocks || typeof window === 'undefined') return 0
  const stored = Number(window.localStorage.getItem('centenario.demo.initiation.completed') ?? '0')
  return Number.isInteger(stored) ? Math.max(0, Math.min(stored, stages.length)) : 0
}

function initialInterviews(): InterviewDraft[] { return [
  { id: '10000000-0000-4000-8000-000000000001', interviewDate: '2026-09-23', interviewerDisplayName: 'Entrevistador Demostrativo Uno', summary: 'Conversa con claridad sobre sus motivaciones y disposición al trabajo personal.', result: 'favorable', documentVersionId: null, fileName: null, uploading: false },
  { id: '10000000-0000-4000-8000-000000000002', interviewDate: '2026-09-24', interviewerDisplayName: 'Entrevistador Demostrativo Dos', summary: 'Se verifican antecedentes generales y compatibilidad con los principios institucionales.', result: 'favorable', documentVersionId: null, fileName: null, uploading: false },
  { id: '10000000-0000-4000-8000-000000000003', interviewDate: '2026-09-25', interviewerDisplayName: 'Entrevistador Demostrativo Tres', summary: 'Manifiesta disponibilidad, comprensión del proceso y apoyo de su entorno inmediato.', result: 'favorable', documentVersionId: null, fileName: null, uploading: false },
] }

function dateOnlyDayNumber(value: string) { const [year, month, day] = value.split('-').map(Number); return Math.floor(Date.UTC(year, month - 1, day) / 86_400_000) }
function addDays(value: string, days: number) { const date = new Date(`${value}T12:00:00Z`); date.setUTCDate(date.getUTCDate() + days); return date.toISOString().slice(0, 10) }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`)) }
function procedureLabel(value: number) { return value === 1 ? 'Primer' : value === 2 ? 'Segundo' : 'Tercer' }
