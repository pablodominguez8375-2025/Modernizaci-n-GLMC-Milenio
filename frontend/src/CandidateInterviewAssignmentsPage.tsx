import { useCallback, useEffect, useState } from 'react'
import {
  type CandidateIntakeApiClient,
  type CandidateInterviewAssignment,
  type CandidateInterviewCase,
  type CandidateEligibleInterviewer,
  type CandidateAssignInterviewers,
} from './api/candidateIntakeApi'
import './CandidateWorkflowPanel.css'

interface Props { api: CandidateIntakeApiClient; canDesignate: boolean }
type ReportDraft = { interviewDate: string; summary: string; result: 'favorable' | 'desfavorable'; file: File | null }

const today = () => new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date())

export default function CandidateInterviewAssignmentsPage({ api, canDesignate }: Props) {
  const [cases, setCases] = useState<CandidateInterviewCase[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [masters, setMasters] = useState<CandidateEligibleInterviewer[]>([])
  const [designations, setDesignations] = useState<CandidateInterviewAssignment[]>([])
  const [mine, setMine] = useState<CandidateInterviewAssignment[]>([])
  const [memberIds, setMemberIds] = useState(['', '', ''])
  const [scheduled, setScheduled] = useState(['', '', ''])
  const [councilBody, setCouncilBody] = useState<CandidateAssignInterviewers['councilBody']>('administration_council')
  const [councilDate, setCouncilDate] = useState(today)
  const [minuteRef, setMinuteRef] = useState('')
  const [replacementReason, setReplacementReason] = useState('')
  const [reports, setReports] = useState<Record<string, ReportDraft>>({})
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const reloadMine = useCallback(async () => {
    try { setMine((await api.getMyInterviewAssignments()).items) }
    catch { setMine([]) }
  }, [api])

  useEffect(() => {
    let active = true
    if (canDesignate) void api.getInterviewCases().then(value => {
      if (!active) return
      setCases(value.items)
    }).catch(reason => {
      if (active) setError(reason instanceof Error ? reason.message : 'No fue posible consultar expedientes.')
    })
    void reloadMine()
    return () => { active = false }
  }, [api, canDesignate, reloadMine])

  const selectCase = async (id: string) => {
    setSelectedId(id); setError(''); setMessage('')
    if (!id) { setMasters([]); setDesignations([]); return }
    setLoading(true)
    try {
      const [eligible, current] = await Promise.all([
        api.getEligibleInterviewers(id), api.getAssignedInterviewers(id)
      ])
      setMasters(eligible.items)
      setDesignations(current.items)
      const active = current.items.filter(x => x.status === 'assigned' || x.status === 'completed')
        .sort((first, second) => first.position - second.position)
      if (active.length >= 3 && active.every(x => x.interviewerMemberId)) {
        setMemberIds(active.map(x => x.interviewerMemberId!))
        setScheduled(active.map(x => x.scheduledDate ?? ''))
        const first = active[0]
        if (first.councilBody === 'administration_council' || first.councilBody === 'masters_chamber')
          setCouncilBody(first.councilBody)
        if (first.councilDecisionDate) setCouncilDate(first.councilDecisionDate)
        if (first.councilMinuteReference) setMinuteRef(first.councilMinuteReference)
      } else {
        setMemberIds(['', '', ''])
        setScheduled(['', '', ''])
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible consultar Maestros del Taller.')
    } finally { setLoading(false) }
  }

  const assign = async () => {
    setError(''); setMessage('')
    if (new Set(memberIds).size !== memberIds.length || memberIds.some(x => !x)) {
      setError('Debe seleccionar al menos tres Maestros distintos del Taller.')
      return
    }
    if (minuteRef.trim().length < 5) { setError('Debe indicar la referencia del acta del acuerdo.'); return }
    setLoading(true)
    try {
      const outcome = await api.assignInterviewers(selectedId, {
        interviewerMemberIds: memberIds,
        councilBody, councilDecisionDate: councilDate,
        councilMinuteReference: minuteRef.trim(),
        scheduledDates: scheduled.map(x => x || null),
        replacementReason: replacementReason.trim() || null,
      })
      setMessage(outcome.notificationsPending.length
        ? 'Designación registrada. Hay avisos pendientes; utilice «Reintentar avisos».'
        : 'Designaciones y avisos privados registrados.')
      setDesignations((await api.getAssignedInterviewers(selectedId)).items)
      setReplacementReason('')
      await reloadMine()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No se pudo registrar la designación.')
    } finally { setLoading(false) }
  }

  const retry = async () => {
    setLoading(true); setError('')
    try {
      const result = await api.retryInterviewerNotifications(selectedId)
      setMessage(result.notificationsPending.length ? 'Algunos avisos siguen pendientes.' : 'Avisos privados corroborados.')
      setDesignations((await api.getAssignedInterviewers(selectedId)).items)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible reenviar avisos.') }
    finally { setLoading(false) }
  }

  const accept = async (assignment: CandidateInterviewAssignment) => {
    if (!assignment.ceremonyRequestId) return
    setLoading(true); setError(''); setMessage('')
    try {
      await api.acceptAssignedInterview(assignment.ceremonyRequestId, assignment.id)
      setMessage('Designación aceptada personalmente. Ya puede preparar su informe privado.')
      await reloadMine()
      if (canDesignate && selectedId === assignment.ceremonyRequestId)
        setDesignations((await api.getAssignedInterviewers(selectedId)).items)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible aceptar la designación.')
    } finally { setLoading(false) }
  }

  const updateReport = (id: string, patch: Partial<ReportDraft>) =>
    setReports(current => ({
      ...current,
      [id]: { ...(current[id] ?? { interviewDate: today(), summary: '', result: 'favorable', file: null }),
        ...patch },
    }))

  const deliver = async (assignment: CandidateInterviewAssignment) => {
    const report = reports[assignment.id]
    if (!report?.file || !report.summary.trim() || !assignment.ceremonyRequestId || !assignment.interviewerName) {
      setError('Seleccione un informe Word/PDF, indique un resumen y compruebe su identidad institucional.')
      return
    }
    setLoading(true); setError(''); setMessage('')
    try {
      const uploaded = await api.uploadInterviewDocument(
        assignment.ceremonyRequestId, assignment.id, report.file, {
          interviewDate: report.interviewDate,
          interviewerDisplayName: assignment.interviewerName,
          summary: report.summary.trim(),
          result: report.result,
        })
      await api.deliverAssignedInterview(assignment.ceremonyRequestId, assignment.id, uploaded.documentVersionId)
      setMessage('Informe privado entregado. Secretaría podrá revisar el expediente.')
      await reloadMine()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible entregar el informe.')
    } finally { setLoading(false) }
  }

  const active = designations.filter(x => x.status === 'assigned' || x.status === 'completed')
  return <div className="candidate-workflow-panel" style={{ maxWidth: 1080, margin: '0 auto' }}>
    <header className="candidate-workflow-heading">
      <h1>Entrevistas de insinuados</h1>
      <p>Designación conforme al acuerdo del Consejo de Administración o Cámara del Medio.
        Antecedentes reservados: nunca se muestran en el portal de insinuados publicados.</p>
    </header>
    {error && <div className="error-banner" role="alert">{error}</div>}
    {message && <div className="candidate-protected-notice" role="status">{message}</div>}

    {canDesignate && <section className="candidate-workflow-action" aria-label="Designar maestros entrevistadores">
      <h2>Designar entrevistadores · V:.M:.</h2>
      <p>Seleccione el expediente publicado, la fecha y el acta del acuerdo colegiado. Cada Maestro debe estar activo en su Taller y tener cuenta institucional.</p>
      <label>Expediente
        <select value={selectedId} onChange={event => void selectCase(event.target.value)} disabled={loading}>
          <option value="">Seleccione insinuado</option>
          {cases.map(item => <option key={item.id} value={item.id}>{item.displayName} · {item.workshop}</option>)}
        </select>
      </label>
      {selectedId && <>
        <div className="candidate-workflow-grid">
          <label>Órgano que acordó las entrevistas
            <select value={councilBody} onChange={event => setCouncilBody(event.target.value as CandidateAssignInterviewers['councilBody'])}>
              <option value="administration_council">Consejo de Administración</option>
              <option value="masters_chamber">Cámara del Medio</option>
            </select>
          </label>
          <label>Fecha del acuerdo
            <input type="date" max={today()} value={councilDate} onChange={event => setCouncilDate(event.target.value)} />
          </label>
          <label className="wide">Referencia del acta
            <input value={minuteRef} maxLength={500} onChange={event => setMinuteRef(event.target.value)}
              placeholder="Acta y folio del acuerdo" />
          </label>
        </div>
        {memberIds.map((id, index) => <div key={index} className="candidate-interview-card">
          <strong>Entrevista {index + 1}</strong>
          <div className="candidate-workflow-grid">
            <label>Maestro entrevistador
              <select value={id} disabled={active.some(x => x.position === index + 1 && x.status === 'completed')}
              onChange={event => {
                const next = [...memberIds]; next[index] = event.target.value; setMemberIds(next)
              }}>
                <option value="">Seleccione Maestro activo</option>
                {masters.map(m => <option key={m.id} value={m.id} disabled={memberIds.some((v, i) => i !== index && v === m.id)}>
                  {m.name}</option>)}
              </select>
            </label>
            <label>Fecha programada
              <input type="date" min={councilDate} value={scheduled[index]}
                onChange={event => { const next = [...scheduled]; next[index] = event.target.value; setScheduled(next) }} />
            </label>
          </div>
          {index > 2 && !active.some(x => x.position === index + 1 && x.status === 'completed') && <button type="button" className="candidate-secondary-button"
            onClick={() => { setMemberIds(memberIds.filter((_, i) => i !== index)); setScheduled(scheduled.filter((_, i) => i !== index)) }}>Quitar adicional</button>}
        </div>)}
        <button type="button" className="candidate-secondary-button" disabled={memberIds.length >= 6}
          onClick={() => { setMemberIds([...memberIds, '']); setScheduled([...scheduled, '']) }}>Agregar entrevista adicional</button>
        {active.length > 0 && <label>Motivo de reemplazo de designaciones
          <textarea value={replacementReason} maxLength={1000} rows={2}
            onChange={event => setReplacementReason(event.target.value)}
            placeholder="Motivo fundado y referencia del acuerdo actualizado" />
        </label>}
        <div className="candidate-workflow-grid">
          <button type="button" className="candidate-primary-button" disabled={loading || memberIds.some(x => !x)}
            onClick={() => void assign()}>{loading ? 'Registrando…' : 'Designar y notificar Maestros'}</button>
          <button type="button" className="candidate-secondary-button" disabled={loading}
            onClick={() => void retry()}>Reintentar avisos privados pendientes</button>
        </div>
        <h3>Seguimiento del expediente</h3>
        {active.length === 0 ? <p>Todavía no hay designaciones registradas.</p> :
          <ul>{active.map(row => <li key={row.id}>{row.interviewerName} · Entrevista {row.position} ·
            {row.status === 'completed' ? ' Informe entregado' : row.acceptedAtUtc ? ' Aceptada · informe pendiente' : ' Aceptación pendiente'} ·
            {row.notified ? ' Avisado' : ' Aviso pendiente'}</li>)}</ul>}
      </>}
    </section>}

    <section className="candidate-workflow-action" aria-label="Mis entrevistas asignadas">
      <h2>Mis entrevistas asignadas</h2>
      <p>Solo puede entregar informes para tareas vinculadas a su identidad institucional.
        Los informes son privados y se revisarán en Tenida de tercer grado.</p>
      {mine.length === 0 ? <p>No hay designaciones pendientes para esta identidad.</p> :
        mine.map(item => <article className="candidate-interview-card" key={item.id}>
          <h3>Entrevista {item.position} · {item.candidateName || 'Candidato asignado'} · {item.status === 'completed' ? 'Entregada' : item.acceptedAtUtc ? 'Aceptada' : 'Por aceptar'}</h3>
          <p>Fecha programada: {item.scheduledDate || 'Por coordinar'} · Expediente institucional identificado por código.</p>
          {item.status === 'assigned' && !item.acceptedAtUtc && <button type="button" className="candidate-primary-button" disabled={loading}
            onClick={() => void accept(item)}>Aceptar designación</button>}
          {item.status === 'assigned' && !!item.acceptedAtUtc && <>
            <div className="candidate-workflow-grid">
              <label>Fecha de entrevista
                <input type="date" max={today()} value={reports[item.id]?.interviewDate ?? today()}
                  onChange={event => updateReport(item.id, { interviewDate: event.target.value })} />
              </label>
              <label>Resultado
                <select value={reports[item.id]?.result ?? 'favorable'}
                  onChange={event => updateReport(item.id, { result: event.target.value as ReportDraft['result'] })}>
                  <option value="favorable">Favorable</option>
                  <option value="desfavorable">Desfavorable</option>
                </select>
              </label>
              <label className="wide">Resumen reservado (máximo 1.000 caracteres)
                <textarea rows={3} maxLength={1000} value={reports[item.id]?.summary ?? ''}
                  onChange={event => updateReport(item.id, { summary: event.target.value })} />
              </label>
              <label className="wide">Informe firmado Word/PDF
                <input type="file" accept=".pdf,.docx" onChange={event => updateReport(item.id, { file: event.target.files?.[0] ?? null })} />
              </label>
            </div>
            <button type="button" className="candidate-primary-button" disabled={loading}
              onClick={() => void deliver(item)}>Enviar informe privado</button>
          </>}
        </article>)}
    </section>
  </div>
}
