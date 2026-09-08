import { type FormEvent, useEffect, useMemo, useState } from 'react'
import {
  type CeremonyInternalAffairsValidationRequest,
  type CeremonyReviewQueueItem,
  type PmgmApiClient,
} from './api/pmgmApi'
import './ceremonies.css'

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

  const execute = async (action: () => Promise<unknown>, success: string) => {
    setWorking(true); setError(null); setMessage(null)
    try { await action(); await refresh(); setMessage(success) }
    catch (reason) { setError(toMessage(reason)) }
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
        <p>Revisión de solicitudes y requisitos habilitantes, con acciones definidas por el rol institucional.</p>
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
  execute: (action: () => Promise<unknown>, success: string) => Promise<void>
}) {
  const completed = item.eligibility.requirements.filter(requirement => requirement.status === 'approved').length
  const total = item.eligibility.requirements.length
  const final = item.status === 'authorized' || item.status === 'rejected'

  return <article className="panel ceremony-card">
    <div className="ceremony-card-heading">
      <div>
        <p className="eyebrow">{ceremonyTypeLabel(item.ceremonyType)}</p>
        <h2>{item.subjectDisplayName}</h2>
        <p>{item.organizationName}{item.organizationNumber ? ` · Nº ${item.organizationNumber}` : ''}</p>
      </div>
      <div className="ceremony-state-stack">
        <span className={requestStatusClass(item.status)}>{requestStatusLabel(item.status)}</span>
        <span className={item.eligibility.canAuthorize ? 'status-pill complete' : 'status-pill blocked'}>{item.eligibility.canAuthorize ? 'Requisitos cumplidos' : 'Requisitos pendientes'}</span>
      </div>
    </div>

    <dl className="ceremony-meta">
      <div><dt>Fecha propuesta</dt><dd>{item.proposedDate ? formatDateOnly(item.proposedDate) : 'Sin fecha definida'}</dd></div>
      <div><dt>Cumplimiento</dt><dd>{completed}/{total} requisitos</dd></div>
      <div><dt>Solicitud</dt><dd>{formatDateTime(item.createdAtUtc)}</dd></div>
    </dl>

    <div className="requirement-grid">
      {item.eligibility.requirements.map(requirement => <div className="requirement-card" key={requirement.code}>
        <div><strong>{requirement.name}</strong><span className={requirement.status === 'approved' ? 'requirement-ok' : requirement.status === 'observed' ? 'requirement-observed' : 'requirement-blocked'}>{requirementStatusLabel(requirement.status)}</span></div>
        <p>{requirement.reason}</p>
      </div>)}
    </div>

    {item.eligibility.publication && <PublicationProgress item={item} />}

    {!final && (item.actions.canValidateInternalAffairs || item.actions.canPublishCandidate || item.actions.canAuthorize) && <div className="ceremony-actions">
      {item.actions.canValidateInternalAffairs && <InternalAffairsForm item={item} api={api} working={working} execute={execute} />}
      {item.actions.canPublishCandidate && <button className="secondary-action" type="button" disabled={working} onClick={() => void execute(() => api.publishCeremonyCandidate(item.id), 'Publicación del insinuado iniciada y auditada.')}>Publicar insinuado</button>}
      {item.actions.canAuthorize && <button className="primary-action" type="button" disabled={working || !item.eligibility.canAuthorize} title={item.eligibility.canAuthorize ? 'Autorizar ceremonia' : 'Todos los requisitos deben estar cumplidos antes de autorizar.'} onClick={() => void execute(() => api.authorizeCeremony(item.id), 'Ceremonia autorizada. Gran Secretaría ya puede continuar con la reserva y el documento formal.')}>Autorizar ceremonia</button>}
    </div>}
  </article>
}

function InternalAffairsForm({ item, api, working, execute }: {
  item: CeremonyReviewQueueItem
  api: PmgmApiClient
  working: boolean
  execute: (action: () => Promise<unknown>, success: string) => Promise<void>
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

  return <details className="validation-details">
    <summary>Validar Régimen Interior</summary>
    <form className="validation-form" onSubmit={submit}>
      <label className="field"><span>Decisión</span><select value={status} onChange={event => setStatus(event.target.value as CeremonyInternalAffairsValidationRequest['status'])}><option value="approved">Aprobar</option><option value="observed">Observar</option><option value="rejected">Rechazar</option><option value="exception_approved">Aprobar por excepción</option></select></label>
      <label className="field"><span>Referencia</span><input maxLength={160} value={sourceReference} onChange={event => setSourceReference(event.target.value)} placeholder="Acta, acuerdo o antecedente" /></label>
      <label className="field validation-notes"><span>Observaciones internas</span><textarea rows={2} maxLength={1000} value={notes} onChange={event => setNotes(event.target.value)} /></label>
      <button className="secondary-action" disabled={working}>Registrar validación</button>
    </form>
  </details>
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

function ceremonyTypeLabel(type: CeremonyReviewQueueItem['ceremonyType']) { return type === 'initiation' ? 'Iniciación' : type === 'wage_increase' ? 'Aumento de salario' : 'Exaltación' }
function requestStatusLabel(status: string) { return status === 'authorized' ? 'Autorizada' : status === 'rejected' ? 'Rechazada' : status === 'observed' ? 'Observada' : status === 'eligible' ? 'Elegible' : status === 'draft' ? 'Borrador' : 'En revisión' }
function requestStatusClass(status: string) { return status === 'authorized' ? 'status-pill complete' : status === 'rejected' ? 'status-pill blocked' : 'status-pill active' }
function requirementStatusLabel(status: string) { return status === 'approved' ? 'Cumplido' : status === 'observed' ? 'Observado' : 'Pendiente' }
function normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim() }
function formatDateOnly(value: string) { const [year, month, day] = value.split('-').map(Number); return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(year, month - 1, day, 12))) }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
