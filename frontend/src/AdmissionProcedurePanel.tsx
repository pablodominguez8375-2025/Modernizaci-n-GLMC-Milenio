import { useEffect, useState, type FormEvent } from 'react'
import { type AdmissionCaseDetail, type AdmissionProcedureAction, type AdmissionProcedureResponse, type PmgmApiClient } from './api/pmgmApi'
import { type MemberDirectoryItem, type MembershipApiClient } from './api/membershipApi'
import { chileCivilDate } from './admissionDates'

export default function AdmissionProcedurePanel({ api, membershipApi, admission, onRefresh }: { api: PmgmApiClient; membershipApi: MembershipApiClient; admission: AdmissionCaseDetail; onRefresh: () => Promise<void> }) {
  const [procedure, setProcedure] = useState<AdmissionProcedureResponse | null>(null)
  const [members, setMembers] = useState<MemberDirectoryItem[]>([])
  const [selectedMembers, setSelectedMembers] = useState<string[]>([])
  const [action, setAction] = useState<AdmissionProcedureAction | 'signature'>('decisiones/presentacion-primer-grado')
  const [date, setDate] = useState(chileCivilDate())
  const [source, setSource] = useState('')
  const [notes, setNotes] = useState('')
  const [approved, setApproved] = useState(true)
  const [rayamiento, setRayamiento] = useState(false)
  const [forced, setForced] = useState(false)
  const [ceremonyDate, setCeremonyDate] = useState('')
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    setProcedure(null); setError(null)
    api.getAdmissionProcedure(admission.id).then(async value => {
      if (!active) return
      setProcedure(value)
      if (value.actions.canManageLodge) {
        const directory = await membershipApi.getMembers(admission.organizationId, { status: 'active', limit: 200 })
        if (active) setMembers(directory.items.filter(x => x.currentDegree === 'master' && x.membershipStatus === 'active'))
      }
    }).catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible consultar los requisitos.') })
    return () => { active = false }
  }, [api, membershipApi, admission])
  const actions: Array<{ value: AdmissionProcedureAction | 'signature'; label: string }> = []
  if (procedure?.actions.canManageLodge) actions.push(
    { value: 'decisiones/presentacion-primer-grado', label: 'Lectura de solicitud en primer grado' },
    { value: 'comision-informacion', label: 'Nombrar comisión de tres Maestros' },
    { value: 'comision-informacion/conclusion', label: 'Registrar conclusión de comisión' },
    { value: 'decisiones/tercer-grado', label: 'Decisión de tercer grado (dos tercios)' },
    { value: 'decisiones/balotaje-primer-grado', label: 'Balotaje secreto de primer grado' })
  if (procedure?.actions.canReviewSignature) actions.push({ value: 'signature', label: 'Verificar firma manuscrita del original CRV' })
  if (procedure?.actions.canReviewArticle23) actions.push({ value: 'revision-articulo-2-3', label: 'Revisión de impedimentos del art. 2.3' })
  if (procedure?.actions.canProvideGrandMasterDecision) actions.push(
    { value: 'decisiones/indulto-gran-maestria', label: 'Indulto de Gran Maestría' },
    ...(admission.admissionType === 'incorporation' ? [
      { value: 'decisiones/reconocimiento-regularidad' as const, label: 'Reconocimiento de regularidad' },
      { value: 'decisiones/gran-maestria-aceptacion-especial' as const, label: 'Aceptación especial de Gran Maestría' }] : []))
  const currentAction = actions.some(x => x.value === action) ? action : actions[0]?.value
  const latestLetter = [...admission.evidence].filter(x => x.evidenceType === 'withdrawal_letter').sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc) || b.id.localeCompare(a.id))[0]
  const submit = async (event: FormEvent) => {
    event.preventDefault(); if (!currentAction) return
    setWorking(true); setError(null); setMessage(null)
    try {
      if (currentAction === 'signature') {
        if (!latestLetter) throw new Error('Debe cargar la Carta de Retiro Voluntario vigente.')
        await api.reviewWithdrawalLetterSignature(admission.id, { evidenceId: latestLetter.id, status: approved ? 'approved' : 'rejected', asOfDate: date, sourceReference: source.trim(), notes: notes.trim() || null })
      } else await api.recordAdmissionProcedure(admission.id, currentAction, { asOfDate: date, appointmentDate: date, sourceReference: source.trim(), notes: notes.trim() || null, status: approved ? 'approved' : 'rejected', approved, completed: approved, memberIds: selectedMembers, hasRayamiento: rayamiento, hasTribunalForcedWithdrawal: forced })
      await onRefresh(); setSource(''); setNotes(''); setMessage('Registro institucional guardado. Requisitos actualizados.')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible registrar la decisión.') }
    finally { setWorking(false) }
  }
  const requestCeremony = async (event: FormEvent) => {
    event.preventDefault(); setWorking(true); setError(null); setMessage(null)
    try {
      const result = await api.createAdmissionCeremony(admission.id, { proposedDate: ceremonyDate, notes: notes.trim() || null })
      await onRefresh(); setMessage(result.alreadyCreated ? 'La solicitud de ceremonia ya estaba registrada.' : 'Solicitud enviada a revisión institucional. Gran Secretaría emitirá la autorización cuando corresponda.')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No fue posible solicitar la ceremonia.') }
    finally { setWorking(false) }
  }
  return <section className="panel" aria-labelledby="admission-procedure-title"><h3 id="admission-procedure-title">Tramitación y decisiones</h3>
    <p>La lectura de primer grado precede a la decisión de tercer grado. El balotaje debe ser en una fecha posterior. Registre actas y resultados; nunca votos individuales.</p>
    {error && <div role="alert" className="error-banner">{error}</div>}{message && <div role="status" className="success-banner">{message}</div>}
    {procedure && <ul>{procedure.requirements.map(item => <li key={item.code}><strong>{item.status === 'approved' ? 'Cumple' : item.status === 'rejected' ? 'No cumple' : 'Pendiente'}</strong> · {item.reason}</li>)}</ul>}
    {admission.status !== 'resolved' && currentAction && <form onSubmit={event => void submit(event)} className="form-grid">
      <label>Actuación<select value={currentAction} disabled={working} onChange={event => setAction(event.target.value as typeof action)}>{actions.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label>Fecha de actuación<input required type="date" max={chileCivilDate()} value={date} onChange={event => setDate(event.target.value)} /></label>
      <label>Acta o resolución<input required maxLength={500} value={source} onChange={event => setSource(event.target.value)} /></label>
      <label>Observaciones<input maxLength={2000} value={notes} onChange={event => setNotes(event.target.value)} /></label>
      {currentAction === 'comision-informacion' ? <fieldset><legend>Tres Maestros del Taller</legend><p>El servidor comprobará su pertenencia y grado en la fecha del nombramiento.</p>{members.map(member => <label key={member.memberId}><input type="checkbox" checked={selectedMembers.includes(member.memberId)} onChange={event => setSelectedMembers(event.target.checked ? [...selectedMembers, member.memberId] : selectedMembers.filter(id => id !== member.memberId))} />{member.displayName}</label>)}<span>{selectedMembers.length} de 3 seleccionados</span></fieldset> : currentAction === 'revision-articulo-2-3' ? <fieldset><legend>Impedimentos comprobados</legend><label><input type="checkbox" checked={rayamiento} onChange={event => setRayamiento(event.target.checked)} />Pena de rayamiento</label><label><input type="checkbox" checked={forced} onChange={event => setForced(event.target.checked)} />Retiro forzoso impuesto por Tribunal</label></fieldset> : <label>Resultado<select value={String(approved)} onChange={event => setApproved(event.target.value === 'true')}><option value="true">Aprobado / verificado</option><option value="false">Rechazado / no verificado</option></select></label>}
      <button type="submit" className="primary-button" disabled={working || !source.trim() || (currentAction === 'comision-informacion' && selectedMembers.length !== 3)}>Registrar actuación</button>
    </form>}
    {admission.status !== 'resolved' && procedure?.actions.canManageLodge && <form onSubmit={event => void requestCeremony(event)} className="form-grid"><label>Fecha propuesta de ceremonia<input required type="date" value={ceremonyDate} onChange={event => setCeremonyDate(event.target.value)} /></label><button className="primary-button" type="submit" disabled={working || !ceremonyDate || !procedure.canProceedToCeremonyRequest}>Solicitar ceremonia</button><small>Se habilita al cumplir los requisitos. La materialización requiere autorización, Plancha y Tenida cerrada con acta y extracto.</small></form>}
    <details><summary>Historial de actuaciones</summary><ul>{admission.decisions.map(item => <li key={item.id}>{item.asOfDate} · {item.decisionType} · {item.status} · {item.sourceReference ?? 'Sin referencia'}</li>)}</ul></details>
  </section>
}
