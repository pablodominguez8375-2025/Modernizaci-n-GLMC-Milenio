import { type FormEvent, useEffect, useMemo, useState } from 'react'
import {
  type AdvancementLiveEligibility,
  type AdvancementPaperAttestation,
  type AdvancementPaperAttestationCandidate,
  type CeremonyInternalAffairsValidationRequest,
  type CeremonyReviewQueueItem,
  type PmgmApiClient,
} from './api/pmgmApi'
import './ceremonies.css'
import { ActionDrawer } from './actionKit'
import { ceremonyTypeLabel } from './ceremonyTypes'
import { organizationDisplayName } from './displayFormat'

export default function CeremoniesPage({ api }: { api: PmgmApiClient }) {
  const [items, setItems] = useState<CeremonyReviewQueueItem[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('open')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const refresh = async () => {
    const response = await api.getCeremonyReviewQueue()
    setItems(response.items)
  }

  useEffect(() => {
    let active = true
    api.getCeremonyReviewQueue()
      .then(response => { if (active) setItems(response.items) })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  const execute = async (action: () => Promise<unknown>, success: string): Promise<unknown | null> => {
    setWorking(true); setError(null); setMessage(null)
    try { const result = await action(); await refresh(); setMessage(success); return result }
    catch (reason) { setError(toMessage(reason)); return null }
    finally { setWorking(false) }
  }

  const filtered = useMemo(() => {
    const normalized = normalize(query)
    return items.filter(item => {
      const statusMatches = status === 'all' || (status === 'open' ? item.status !== 'authorized' && item.status !== 'rejected' : item.status === status)
      const textMatches = !normalized || normalize(`${item.subjectDisplayName} ${item.organizationName} ${item.organizationNumber ?? ''} ${ceremonyTypeLabel(item.ceremonyType)}`).includes(normalized)
      return statusMatches && textMatches
    })
  }, [items, query, status])

  const ready = items.filter(item => item.eligibility.canAuthorize && item.status !== 'authorized').length
  const blocked = items.filter(item => !item.eligibility.canAuthorize && item.status !== 'authorized' && item.status !== 'rejected').length

  return <>
    <section className="page-heading">
      <div>
        <p className="eyebrow">Flujo institucional</p>
        <h1>Ceremonias</h1>
        <p>Revisión de solicitudes, fichas de insinuados y requisitos habilitantes, con acciones definidas por el rol institucional.</p>
      </div>
      <div className="ceremony-heading-metrics"><span className="count-badge">{ready} listas</span><span className="count-badge">{blocked} con pendientes</span></div>
    </section>

    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}

    <section className="panel ceremony-filters">
      <label className="search-field"><span>Buscar persona o Taller</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Ej.: Libertad 23" /></label>
      <label className="field"><span>Estado</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="open">En gestión</option><option value="all">Todos</option><option value="under_review">En revisión</option><option value="observed">Observada</option><option value="authorized">Autorizada</option><option value="rejected">Rechazada</option></select></label>
    </section>

    {loading ? <section className="panel"><div className="loading-rows"><span /><span /><span /></div></section> : filtered.length === 0 ? <section className="panel empty-state"><strong>No hay ceremonias para los filtros seleccionados.</strong></section> : (
      <section className="ceremony-list">
        {filtered.map(item => <CeremonyCard key={item.id} item={item} api={api} working={working} execute={execute} />)}
      </section>
    )}
  </>
}

function CeremonyCard({ item, api, working, execute }: {
  item: CeremonyReviewQueueItem
  api: PmgmApiClient
  working: boolean
  execute: (action: () => Promise<unknown>, success: string) => Promise<unknown | null>
}) {
  const [liveEligibility, setLiveEligibility] = useState<AdvancementLiveEligibility | null>(null)
  const [reviewing, setReviewing] = useState(false)
  const [reviewError, setReviewError] = useState<string | null>(null)
  useEffect(() => { setLiveEligibility(null); setReviewError(null) }, [item.eligibility])
  const eligibility = liveEligibility ?? item.eligibility
  const completed = eligibility.requirements.filter(requirement => requirement.status === 'approved').length
  const total = eligibility.requirements.length
  const final = item.status === 'authorized' || item.status === 'rejected'
  const isAdvancement = item.ceremonyType === 'wage_increase' || item.ceremonyType === 'exaltation'
  const canAuthorize = item.actions.canAuthorize ||
    (isAdvancement && Boolean(item.actions.canAuthorizeAfterLiveReview) && Boolean(liveEligibility?.canAuthorize))
  const reviewAdvancement = async () => {
    setReviewing(true); setReviewError(null)
    try { setLiveEligibility(await api.getCeremonyEligibility(item.id)) }
    catch (reason) { setReviewError(toMessage(reason)) }
    finally { setReviewing(false) }
  }

  return <article className="panel ceremony-card">
    <div className="ceremony-card-heading">
      <div>
        <p className="eyebrow">{ceremonyTypeLabel(item.ceremonyType)}</p>
        <h2>{item.subjectDisplayName}</h2>
        <p>{organizationDisplayName(item.organizationName, item.organizationNumber)}</p>
      </div>
      <div className="ceremony-state-stack">
        <span className={requestStatusClass(item.status)}>{requestStatusLabel(item.status)}</span>
        <span className={eligibility.canAuthorize ? 'status-pill complete' : 'status-pill blocked'}>{eligibility.canAuthorize ? 'Requisitos cumplidos' : 'Requisitos pendientes'}</span>
      </div>
    </div>

    <dl className="ceremony-meta">
      <div><dt>Fecha propuesta</dt><dd>{item.proposedDate ? formatDateOnly(item.proposedDate) : 'Sin fecha definida'}</dd></div>
      <div><dt>Cumplimiento</dt><dd>{completed}/{total} requisitos</dd></div>
      <div><dt>Solicitud</dt><dd>{formatDateTime(item.createdAtUtc)}</dd></div>
    </dl>

    <div className="requirement-grid">
      {eligibility.requirements.map(requirement => <div className="requirement-card" key={requirement.code}>
        <div><strong>{requirement.name}</strong><span className={requirement.status === 'approved' ? 'requirement-ok' : requirement.status === 'observed' ? 'requirement-observed' : 'requirement-blocked'}>{requirementStatusLabel(requirement.status)}</span></div>
        <p>{requirement.reason}</p>
      </div>)}
    </div>

    {isAdvancement && !final && <section className="panel" aria-label="Constancias de ascenso">
      <div className="ceremony-actions">
        <button className="secondary-action" type="button" disabled={working || reviewing}
          onClick={() => { void reviewAdvancement() }}>
          {reviewing ? 'Comprobando fuentes institucionales…' : 'Revisar elegibilidad y constancias'}
        </button>
      </div>
      {reviewError && <p role="alert">{reviewError}</p>}
      {liveEligibility && <div className="requirement-grid">
        {(liveEligibility.advancement?.decision.requirements ?? []).map(requirement =>
          <div className="requirement-card" key={requirement.code}>
            <div>
              <strong>{requirement.name}</strong>
              <span className={requirement.complies ? 'requirement-ok' : 'requirement-blocked'}>
                {requirement.complies ? 'Cumple' : 'No cumple'}
              </span>
            </div>
            <p>Alcanzado: {requirement.achieved} · Mínimo requerido: {requirement.minimum}</p>
          </div>)}
        {!liveEligibility.advancement && <div className="requirement-card">
          <strong>Tenidas, instrucciones y planchas</strong>
          <p>Pendiente de regla vigente y corroboración de las fuentes institucionales.</p>
        </div>}
        <div className="requirement-card"><strong>Planchas institucionales de dos clases</strong><p>
          {liveEligibility.advancement?.twoDifferentKindsCertified ? 'Ambas categorías certificadas' : 'Pendiente de certificar simbolismo y cultura general masónica'}
        </p></div>
        <div className="requirement-card"><strong>Continuidad validada</strong><p>
          {liveEligibility.advancement?.institutionalContinuityCertified ? 'Sí' : 'Pendiente'}
        </p></div>
        <div className="requirement-card"><strong>Regla aplicada</strong><p>{liveEligibility.advancement?.ruleVersion ?? 'No consta regla vigente'}</p></div>
      </div>}
    </section>}

    {isAdvancement && !final && (item.actions.canSubmitAdvancementEvidence || item.actions.canReviewAdvancementEvidence) &&
      <AdvancementEvidencePanel item={item} api={api} working={working} onUpdated={() => setLiveEligibility(null)} />}

    {item.eligibility.publication && <PublicationProgress item={item} />}

    {item.eligibility.ceremonyRight && <section className="ceremony-right-summary" aria-label="Derecho de ceremonia">
      <div><strong>Derecho de ceremonia</strong><span>Fuente: {item.eligibility.ceremonyRight.source}</span></div>
      <dl><div><dt>Exigido</dt><dd>{formatMoney(item.eligibility.ceremonyRight.amount, item.eligibility.ceremonyRight.currency)}</dd></div><div><dt>Pagado</dt><dd>{formatMoney(item.eligibility.ceremonyRight.paid, item.eligibility.ceremonyRight.currency)}</dd></div><div><dt>Saldo</dt><dd>{formatMoney(item.eligibility.ceremonyRight.balance, item.eligibility.ceremonyRight.currency)}</dd></div></dl>
    </section>}

    {!final && (item.actions.canValidateInternalAffairs || item.actions.canPublishCandidate || canAuthorize) && <div className="ceremony-actions">
      {item.actions.canValidateInternalAffairs && <InternalAffairsForm item={item} api={api} working={working} execute={execute} />}
      {item.actions.canPublishCandidate && <button className="secondary-action" type="button" disabled={working} title="Gran Secretaría aprueba la ficha, la hace visible y notifica a los Hermanos." onClick={() => void execute(() => api.publishCeremonyCandidate(item.id), 'Ficha aprobada por Gran Secretaría. La insinuación quedó publicada y se generaron las notificaciones institucionales.')}>Aprobar ficha y publicar</button>}
      {canAuthorize && <button className="primary-action" type="button" disabled={working || !eligibility.canAuthorize} title={eligibility.canAuthorize ? 'Autorizar ceremonia' : 'Todos los requisitos deben estar cumplidos antes de autorizar.'} onClick={() => void execute(() => api.authorizeCeremony(item.id), 'Ceremonia autorizada. Gran Secretaría ya puede continuar con la reserva y el documento formal.')}>Autorizar ceremonia</button>}
    </div>}
  </article>
}

function AdvancementEvidencePanel({ item, api, working, onUpdated }: {
  item: CeremonyReviewQueueItem
  api: PmgmApiClient
  working: boolean
  onUpdated: () => void
}) {
  const [expanded, setExpanded] = useState(false)
  const [busy, setBusy] = useState(false)
  const [candidates, setCandidates] = useState<AdvancementPaperAttestationCandidate[]>([])
  const [records, setRecords] = useState<AdvancementPaperAttestation[]>([])
  const [choice, setChoice] = useState('')
  const [kind, setKind] = useState<'degree_symbolism' | 'masonic_general_culture'>('degree_symbolism')
  const [reviewId, setReviewId] = useState('')
  const [councilReference, setCouncilReference] = useState('')
  const [continuityReference, setContinuityReference] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const reload = async () => {
    const [workPapers, attestations] = await Promise.all([
      api.getAdvancementPaperCandidates(item.id),
      api.getAdvancementAttestations(item.id),
    ])
    setCandidates(workPapers.attestationCandidates)
    setRecords(attestations.records)
  }
  const run = async (fn: () => Promise<unknown>, ok: string) => {
    setBusy(true); setError(null); setSuccess(null)
    try { await fn(); await reload(); onUpdated(); setSuccess(ok) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setBusy(false) }
  }
  const load = async () => {
    setBusy(true); setError(null)
    try { await reload(); setExpanded(true) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setBusy(false) }
  }
  const chosenCandidate = choice === '' ? undefined : candidates[Number(choice)]
  const pending = records.filter(record => record.status === 'pending')

  return <section className="panel" aria-label="Constancias institucionales para ascenso">
    <div className="ceremony-actions">
      <strong>Constancias de planchas y continuidad</strong>
      <button className="secondary-action" type="button" disabled={busy || working}
        onClick={() => { void load() }}>
        {busy ? 'Consultando…' : expanded ? 'Actualizar constancias' : 'Gestionar constancias'}
      </button>
    </div>
    <p>La Secretaría registra referencias de documentos ya cotejados. Régimen Interior resuelve la presentación y la continuidad mediante constancias auditadas; ninguna referencia aprueba por sí sola una ceremonia.</p>
    {error && <p role="alert">{error}</p>}
    {success && <p role="status">{success}</p>}
    {expanded && <>
      <div className="requirement-grid">
        <div className="requirement-card"><strong>Constancias registradas</strong><p>{records.length}</p></div>
        <div className="requirement-card"><strong>Con revisión pendiente</strong><p>{pending.length}</p></div>
      </div>
      {item.actions.canSubmitAdvancementEvidence && <div className="ceremony-filters">
        <h3>Registrar presentación de plancha</h3>
        <label className="field"><span>Plancha y Tenida con documentos cotejados</span>
          <select value={choice} onChange={event => setChoice(event.target.value)}>
            <option value="">Seleccione evidencia</option>
            {candidates.map((candidate, index) =>
              <option key={candidate.workPaperVersionId + candidate.meetingId} value={String(index)}>
                {candidate.title} — {candidate.presentationDate}
              </option>)}
          </select>
        </label>
        <label className="field"><span>Tipo de trabajo</span>
          <select value={kind} onChange={event => setKind(event.target.value as typeof kind)}>
            <option value="degree_symbolism">Simbolismo del grado</option>
            <option value="masonic_general_culture">Cultura general masónica</option>
          </select>
        </label>
        <div className="ceremony-actions">
          <button className="secondary-action" type="button" disabled={busy || working || !chosenCandidate}
            onClick={() => { if (!chosenCandidate) return; void run(() =>
              api.submitAdvancementPresentation(item.id, {
                workPaperDocumentId: chosenCandidate.workPaperDocumentId,
                workPaperVersionId: chosenCandidate.workPaperVersionId,
                meetingId: chosenCandidate.meetingId,
                extractVersionId: chosenCandidate.extractVersionId,
                fullMinuteVersionId: chosenCandidate.fullMinuteVersionId,
                presentationDate: chosenCandidate.presentationDate,
                workKind: kind,
              }), 'Presentación registrada para revisión de Régimen Interior.') }}>
            Registrar para revisión
          </button>
        </div>
        {candidates.length === 0 && <p>No hay paquetes completos. Registre primero la plancha, la Tenida celebrada, el extracto y el acta completa en Secretaría.</p>}
      </div>}
      {item.actions.canReviewAdvancementEvidence && <div className="ceremony-filters">
        <h3>Resolver constancias institucionales</h3>
        <label className="field"><span>Plancha pendiente</span>
          <select value={reviewId} onChange={event => setReviewId(event.target.value)}>
            <option value="">Seleccione constancia pendiente</option>
            {pending.map(row => <option key={row.id} value={row.id}>
              {row.workKind === 'degree_symbolism' ? 'Simbolismo' : 'Cultura general'} — {row.presentationDate}
            </option>)}
          </select>
        </label>
        <label className="field"><span>Referencia del acuerdo de Cámara del Medio</span>
          <input value={councilReference} maxLength={500} onChange={e => setCouncilReference(e.target.value)}
            placeholder="Acta y folio de aprobación" />
        </label>
        <label className="field"><span>Observaciones de revisión</span>
          <input value={note} maxLength={2000} onChange={e => setNote(e.target.value)} />
        </label>
        <div className="ceremony-actions">
          <button className="secondary-action" type="button"
            disabled={busy || working || !reviewId || !councilReference.trim()}
            onClick={() => { void run(() => api.resolveAdvancementAttestation(item.id, reviewId, {
              approved: true, councilApprovalReference: councilReference, reviewNotes: note || null,
            }), 'Presentación y aprobación documentada por Régimen Interior.') }}>
            Aprobar constancia
          </button>
          <button className="secondary-action" type="button" disabled={busy || working || !reviewId}
            onClick={() => { void run(() => api.resolveAdvancementAttestation(item.id, reviewId, {
              approved: false, councilApprovalReference: null, reviewNotes: note || null,
            }), 'Constancia rechazada de forma auditada.') }}>
            Rechazar constancia
          </button>
        </div>
        <h3>Validación de antigüedad continuada</h3>
        <label className="field"><span>Referencia formal de la verificación</span>
          <input value={continuityReference} maxLength={500} onChange={e => setContinuityReference(e.target.value)}
            placeholder="Acta, certificado o resolución" />
        </label>
        <div className="ceremony-actions">
          <button className="secondary-action" type="button"
            disabled={busy || working || !continuityReference.trim()}
            onClick={() => { void run(() => api.validateAdvancementContinuity(item.id, {
              approved: true, reference: continuityReference,
            }), 'Continuidad institucional validada con regla vigente.') }}>
            Validar continuidad
          </button>
          <button className="secondary-action" type="button"
            disabled={busy || working || !continuityReference.trim()}
            onClick={() => { void run(() => api.validateAdvancementContinuity(item.id, {
              approved: false, reference: continuityReference,
            }), 'Continuidad observada/rechazada en el expediente.') }}>
            Rechazar continuidad
          </button>
        </div>
      </div>}
    </>}
  </section>
}

function InternalAffairsForm({ item, api, working, execute }: {
  item: CeremonyReviewQueueItem
  api: PmgmApiClient
  working: boolean
  execute: (action: () => Promise<unknown>, success: string) => Promise<unknown | null>
}) {
  const [status, setStatus] = useState<CeremonyInternalAffairsValidationRequest['status']>('approved')
  const [sourceReference, setSourceReference] = useState('')
  const [notes, setNotes] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const label = status === 'approved' ? 'aprobada' : status === 'exception_approved' ? 'aprobada por excepción' : status === 'observed' ? 'observada' : 'rechazada'
    void execute(
      () => api.setCeremonyInternalAffairsValidation(item.id, { status, sourceReference: sourceReference || null, notes: notes || null }),
      `Validación de Régimen Interior ${label} y auditada.`)
  }

  return <ActionDrawer label="Validar Régimen Interior" tone="secondary" title={`Validación de Régimen Interior — ${item.subjectDisplayName}`} description="Decisión, referencia del acta o acuerdo y observaciones internas." confirmMessage="Se registrará la validación de Régimen Interior y quedará auditada.">
    <form className="validation-form" onSubmit={submit}>
      <label className="field"><span>Decisión</span><select value={status} onChange={event => setStatus(event.target.value as CeremonyInternalAffairsValidationRequest['status'])}><option value="approved">Aprobar</option><option value="observed">Observar</option><option value="rejected">Rechazar</option><option value="exception_approved">Aprobar por excepción</option></select></label>
      <label className="field"><span>Referencia</span><input maxLength={160} value={sourceReference} onChange={event => setSourceReference(event.target.value)} placeholder="Acta, acuerdo o antecedente" /></label>
      <label className="field validation-notes"><span>Observaciones internas</span><textarea rows={2} maxLength={1000} value={notes} onChange={event => setNotes(event.target.value)} /></label>
      <button className="secondary-action" disabled={working}>Registrar validación</button>
    </form>
  </ActionDrawer>
}

function PublicationProgress({ item }: { item: CeremonyReviewQueueItem }) {
  const publication = item.eligibility.publication
  if (!publication) return null
  const percentage = Math.min(100, Math.round(publication.completedDays / Math.max(1, publication.requiredDays) * 100))
  return <div className="publication-progress">
    <div><strong>Publicación del insinuado</strong><span>{publication.completedDays}/{publication.requiredDays} días</span></div>
    <div className="progress-track" role="progressbar" aria-valuenow={percentage} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${percentage}%` }} /></div>
  </div>
}

function requestStatusLabel(status: string) { return status === 'authorized' ? 'Autorizada' : status === 'rejected' ? 'Rechazada' : status === 'observed' ? 'Observada' : status === 'eligible' ? 'Elegible' : status === 'draft' ? 'Borrador' : 'En revisión' }
function requestStatusClass(status: string) { return status === 'authorized' ? 'status-pill complete' : status === 'rejected' ? 'status-pill blocked' : 'status-pill active' }
function requirementStatusLabel(status: string) { return status === 'approved' ? 'Cumplido' : status === 'observed' ? 'Observado' : 'Pendiente' }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function formatDateOnly(value: string) { const [year, month, day] = value.split('-').map(Number); return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(year, month - 1, day, 12))) }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
function formatMoney(value: number, currency: string) { return new Intl.NumberFormat('es-CL', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
