import { useEffect, useState } from 'react'
import { type CandidateIntakeApiClient, type CandidateIntakeProfile, type CandidateReviewDecision, type CandidateReviewQueueItem } from './api/candidateIntakeApi'

export const candidateCoreFields = [
  'passportPhoto',
  'names',
  'paternalSurname',
  'maternalSurname',
  'rut',
  'birthDate',
  'presentingLodge',
  'orient',
  'presenters',
  'insinuationDate',
  'processStatus',
] as const

export const candidateDemoData = {
  names: 'Tomás Ignacio',
  paternalSurname: 'Valdés',
  maternalSurname: 'Riquelme',
  rut: 'DEMO-16.543.219-X',
  birthDate: '14 de agosto de 1990',
  age: '36 años',
  nationality: 'Chilena · demo',
  civilStatus: 'Soltero · demo',
  occupation: 'Profesional · dato ficticio',
  phone: '+56 9 0000 4321',
  email: 'insinuado.demo@ejemplo.cl',
  address: 'Dirección ficticia 2345, Depto. 702',
  city: 'Santiago · demo',
  presentingLodge: 'Taller Demostrativo Nº 23',
  orient: 'Santiago',
  presenters: ['H∴ Presentante Uno · demo', 'H∴ Presentante Dos · demo'],
  insinuationDate: '12 de agosto de 2026',
  targetDegree: 'Iniciación (Aprendiz)',
  processStatus: 'En evaluación',
  processStage: 'En comisión de estudio',
  documents: [
    ['Documento de identidad', 'Completo'],
    ['Certificado de antecedentes', 'Completo'],
    ['Carta de motivación', 'Completo'],
    ['Entrevista inicial', 'Completo'],
    ['Informe de la comisión', 'En revisión'],
    ['Patrocinio de la Logia', 'Completo'],
  ],
  history: [
    ['12 ago 2026', 'Recepción de antecedentes', 'Completado'],
    ['18 ago 2026', 'Asignación a comisión', 'Completado'],
    ['25 ago 2026', 'Entrevista preliminar', 'Completado'],
    ['05 sep 2026', 'Revisión documental', 'En curso'],
    ['Por definir', 'Presentación a Logia', 'Pendiente'],
  ],
} as const

interface CandidateProfilePageProps {
  api: CandidateIntakeApiClient
  canReview: boolean
  onBack?: () => void
}

export default function CandidateProfilePage({ api, canReview, onBack }: CandidateProfilePageProps) {
  const [queue, setQueue] = useState<CandidateReviewQueueItem[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [profile, setProfile] = useState<CandidateIntakeProfile | null>(null)
  const [photoSrc, setPhotoSrc] = useState<string | null>(null)
  const [loading, setLoading] = useState(api.useMocks || canReview)
  const [busy, setBusy] = useState(false)
  const [notes, setNotes] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    if (!api.useMocks && !canReview) {
      setLoading(false)
      return () => { active = false }
    }

    setLoading(true)
    api.getGrandSecretariatQueue()
      .then(result => {
        if (!active) return
        setQueue(result.items)
        setSelectedId(current => current ?? result.items[0]?.ceremonyRequestId ?? null)
      })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar la bandeja de insinuados.') })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [api, canReview])

  useEffect(() => {
    let active = true
    if (!selectedId) {
      setProfile(null)
      return () => { active = false }
    }

    setLoading(true)
    setError(null)
    api.getProfile(selectedId)
      .then(value => { if (active) setProfile(value) })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'No fue posible cargar la ficha del insinuado.') })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [api, selectedId])

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    setPhotoSrc(null)

    if (!profile) return () => { active = false }
    if (api.useMocks) {
      setPhotoSrc(`${import.meta.env.BASE_URL}demo-candidate-passport.svg`)
      return () => { active = false }
    }
    if (!profile.photoAvailable) return () => { active = false }

    api.getPrivatePhoto(profile.ceremonyRequestId)
      .then(blob => {
        if (!active || !blob) return
        objectUrl = URL.createObjectURL(blob)
        setPhotoSrc(objectUrl)
      })
      .catch(() => { if (active) setPhotoSrc(null) })

    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [api, profile])

  async function refresh(requestId: string) {
    const [queueResult, profileResult] = await Promise.all([
      api.getGrandSecretariatQueue(),
      api.getProfile(requestId),
    ])
    setQueue(queueResult.items)
    setProfile(profileResult)
    setSelectedId(requestId)
  }

  async function runReview(decision: CandidateReviewDecision) {
    if (!profile || busy) return
    if (!notes.trim()) {
      setError(decision === 'observed' ? 'Para observar una ficha debe registrar el motivo.' : 'Para rechazar una ficha debe registrar el motivo.')
      return
    }
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      await api.review(profile.ceremonyRequestId, decision, notes)
      await refresh(profile.ceremonyRequestId)
      setMessage(decision === 'observed' ? 'La ficha quedó observada y auditada.' : 'La ficha quedó rechazada y auditada.')
      setNotes('')
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible registrar la revisión.')
    } finally {
      setBusy(false)
    }
  }

  async function approveAndPublish() {
    if (!profile || busy) return
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      await api.approveAndPublish(profile.ceremonyRequestId)
      await refresh(profile.ceremonyRequestId)
      setMessage('Ficha aprobada por Gran Secretaría. La publicación institucional quedó iniciada y las notificaciones fueron encoladas por backend.')
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No fue posible aprobar la publicación.')
    } finally {
      setBusy(false)
    }
  }

  if (!api.useMocks && !canReview) {
    return <div className="candidate-profile-page">
      <section className="candidate-profile-heading"><div><p className="candidate-profile-breadcrumb">Secretaría Logial <span>›</span> Insinuados</p><h1>Ficha de Insinuado</h1><p>El expediente privado se abre desde la solicitud correspondiente del Taller.</p></div>{onBack && <button className="candidate-secondary-button" type="button" onClick={onBack}>Volver</button>}</section>
      <div className="candidate-protected-notice">La bandeja de revisión y aprobación de publicaciones es exclusiva de Gran Secretaría. Los datos sensibles no se muestran fuera del ámbito autorizado.</div>
    </div>
  }

  return <div className="candidate-profile-page">
    <section className="candidate-profile-heading">
      <div>
        <p className="candidate-profile-breadcrumb">Gran Secretaría <span>›</span> Insinuados <span>›</span> Revisión previa a publicación</p>
        <h1>Ficha de Insinuado</h1>
        <p>Revisión del expediente privado antes de iniciar la publicación institucional configurada.</p>
      </div>
      <div className="candidate-heading-actions">
        {onBack && <button className="candidate-secondary-button" type="button" onClick={onBack}>Insinuados publicados</button>}
        <button className="candidate-secondary-button" type="button" onClick={() => window.print()} disabled={!profile}>Imprimir ficha</button>
      </div>
    </section>

    {api.useMocks && <div className="candidate-protected-notice">Datos 100% ficticios para QA. Esta pantalla usa el mismo flujo de componentes y acciones que la instalación operativa.</div>}
    {error && <div className="error-banner" role="alert"><strong>Revisión de insinuados</strong><span>{error}</span></div>}
    {message && <div className="candidate-protected-notice" role="status">{message}</div>}

    <section className="candidate-product-card" aria-label="Bandeja de revisión de Gran Secretaría">
      <div className="candidate-section-title"><span>▤</span><h2>Bandeja de Gran Secretaría</h2><em>{queue.length} expediente{queue.length === 1 ? '' : 's'}</em></div>
      {loading && queue.length === 0 ? <p>Cargando expedientes…</p> : queue.length === 0 ? <p>No existen fichas pendientes o revisadas en esta bandeja.</p> : <div className="candidate-heading-actions" style={{ justifyContent: 'flex-start', flexWrap: 'wrap' }}>{queue.map(item => <button key={item.ceremonyRequestId} className={item.ceremonyRequestId === selectedId ? 'candidate-primary-button' : 'candidate-secondary-button'} type="button" onClick={() => setSelectedId(item.ceremonyRequestId)}><span>{item.displayName}</span> · {reviewStatusLabel(item.reviewStatus)}</button>)}</div>}
    </section>

    {!profile ? <section className="candidate-product-card"><p>{loading ? 'Cargando ficha…' : 'Seleccione un expediente para revisar.'}</p></section> : <>
      <section className="candidate-profile-grid">
        <article className="candidate-product-card candidate-person-card">
          <div className="candidate-section-title"><span>▣</span><h2>Datos personales del insinuado</h2><em>Expediente privado</em></div>
          <div className="candidate-person-layout">
            <div className="candidate-passport-photo">{photoSrc ? <img src={photoSrc} alt="Foto tipo pasaporte del expediente" /> : <div className="candidate-lodge-seal" aria-label="Fotografía no disponible">{initials(profile.firstNames, profile.paternalSurname)}</div>}<small>Foto tipo pasaporte</small></div>
            <div>
              <h3>{fullName(profile)}</h3>
              <div className="candidate-fields-grid">
                <CandidateField label="Nombres" value={profile.firstNames} />
                <CandidateField label="Apellido paterno" value={profile.paternalSurname} />
                <CandidateField label="Apellido materno" value={profile.maternalSurname} />
                <CandidateField label="RUT / ID" value={profile.rutOrInstitutionalId} />
                <CandidateField label="Fecha de nacimiento" value={formatDateOnly(profile.birthDate)} />
                <CandidateField label="Nacionalidad" value={profile.nationality} />
                <CandidateField label="Estado civil" value={profile.civilStatus} />
                <CandidateField label="Profesión u oficio" value={profile.occupation} />
                <CandidateField label="Empleador" value={profile.employerName} />
                <CandidateField label="Cargo o función" value={profile.workPosition} />
                <CandidateField label="Teléfono laboral" value={profile.workPhone} />
                <CandidateField label="Dirección laboral" value={profile.workAddress} />
                <CandidateField label="Teléfono" value={profile.phone} />
                <CandidateField label="Correo" value={profile.email} />
                <CandidateField label="Dirección" value={profile.address} />
                <CandidateField label="Ciudad" value={profile.city} />
              </div>
            </div>
          </div>
        </article>

        <article className="candidate-product-card candidate-lodge-card">
          <div className="candidate-section-title"><span>⌂</span><h2>Datos logiales y de presentación</h2></div>
          <div className="candidate-lodge-hero"><span className="candidate-lodge-seal">C</span><div><small>Logia que presenta al insinuado</small><strong>{profile.workshopName}{profile.workshopNumber ? ` · Nº ${profile.workshopNumber}` : ''}</strong><span>{profile.orient ?? 'Oriente no informado'}</span></div></div>
          <div className="candidate-lodge-fields">
            <CandidateField label="Oriente" value={profile.orient} />
            <CandidateField label="Fecha de insinuación" value={formatDateOnly(profile.insinuationDate)} />
            <CandidateField label="Presentación en 1.er grado" value={formatDateOnly(profile.firstDegreePresentationDate)} />
            <CandidateField label="Secretario responsable" value={profile.responsibleSecretaryName} />
            <CandidateField label="Grado objetivo" value="Iniciación (Aprendiz)" />
            <CandidateField label="Estado de revisión" value={reviewStatusLabel(profile.reviewStatus)} status />
          </div>
          <div className="candidate-presenters"><small>Patrocinantes / Presentantes</small>{profile.presenters.length ? profile.presenters.map(value => <strong key={value}>{value}</strong>) : <strong>No informados</strong>}</div>
          <div className="candidate-process-banner"><span className="candidate-blue-dot" /><div><strong>{reviewStatusLabel(profile.reviewStatus)}</strong><small>Gran Secretaría · revisión previa a publicación</small></div></div>
        </article>

        <article className="candidate-product-card candidate-doc-card">
          <div className="candidate-section-title"><span>▤</span><h2>Documentación y fotografía</h2></div>
          <div className="candidate-doc-list">
            <div><span className="candidate-doc-icon">▧</span><strong>Expediente documental seguro</strong><em className="complete">Gestionado</em></div>
            <div><span className="candidate-doc-icon">▧</span><strong>Foto tipo pasaporte</strong><em className={profile.photoAvailable ? 'complete' : 'review'}>{profile.photoAvailable ? 'Disponible' : 'Pendiente'}</em></div>
            <div><span className="candidate-doc-icon">▧</span><strong>Protección de acceso</strong><em className="complete">Privado</em></div>
          </div>
          <p style={{ marginTop: '1rem' }}>Los archivos permanecen en el gestor documental privado y se sirven sólo mediante autorización institucional. La publicación nunca expone la ubicación del objeto almacenado.</p>
        </article>

        <aside className="candidate-quote-panel"><span>“</span><p>La verdadera iniciación comienza cuando el ser humano decide trabajar sobre sí mismo.</p><i /><strong>Proyecto Centenario</strong><small>Libertad · Igualdad · Fraternidad</small></aside>
      </section>

      <section className="candidate-middle-grid">
        <article className="candidate-product-card candidate-observation-card">
          <div className="candidate-section-title"><span>●</span><h2>Resumen de entrevista y observaciones internas</h2></div>
          <p>{profile.interviewSummary || 'No se ha registrado resumen de entrevista.'}</p>
          {profile.internalObservations && <p><strong>Observaciones internas:</strong> {profile.internalObservations}</p>}
          <small>Última actualización: {formatDateTime(profile.updatedAtUtc)}</small>
        </article>
        <aside className="candidate-product-card candidate-summary-card">
          <div className="candidate-section-title"><span>▦</span><h2>Control de expediente</h2></div>
          <CandidateSummary label="Estado" value={reviewStatusLabel(profile.reviewStatus)} status />
          <CandidateSummary label="Foto" value={profile.photoAvailable ? 'Disponible' : 'Pendiente'} />
          <CandidateSummary label="Presentantes" value={String(profile.presenters.length)} />
          <CandidateSummary label="Ingresado" value={formatDateTime(profile.submittedAtUtc)} />
          <CandidateSummary label="Actualizado" value={formatDateTime(profile.updatedAtUtc)} />
        </aside>
      </section>

      {canReview && profile.reviewStatus !== 'approved' && <section className="candidate-product-card candidate-history-card">
        <div className="candidate-section-title"><span>✓</span><h2>Resolución de Gran Secretaría</h2><em>Auditable</em></div>
        <p>Registre fundamento cuando corresponda. Aprobar inicia la publicación institucional y activa el aviso a Hermanos con identidad vigente.</p>
        <label className="search-field" style={{ maxWidth: '100%' }}><span>Fundamento / observación</span><textarea value={notes} onChange={event => setNotes(event.target.value)} rows={4} maxLength={4000} placeholder="Motivo de observación o rechazo. Para aprobar puede quedar en blanco." style={{ width: '100%', boxSizing: 'border-box' }} /></label>
        <div className="candidate-heading-actions" style={{ justifyContent: 'flex-start', marginTop: '1rem' }}>
          <button className="candidate-secondary-button" type="button" disabled={busy} onClick={() => { void runReview('observed') }}>Observar</button>
          <button className="candidate-secondary-button" type="button" disabled={busy} onClick={() => { void runReview('rejected') }}>Rechazar</button>
          <button className="candidate-primary-button" type="button" disabled={busy} onClick={() => { void approveAndPublish() }}>{busy ? 'Procesando…' : 'Aprobar y publicar'}</button>
        </div>
      </section>}

      {profile.reviewStatus === 'approved' && <div className="candidate-protected-notice"><strong>Publicación aprobada.</strong> El expediente privado permanece protegido; en el portal de Hermanos sólo aparece la información autorizada para publicación.</div>}
    </>}
  </div>
}

function CandidateField({ label, value, status = false }: { label: string; value: string | null | undefined; status?: boolean }) {
  return <div className="candidate-field"><small>{label}</small><strong className={status ? 'candidate-status-text' : undefined}>{value || 'No informado'}</strong></div>
}

function CandidateSummary({ label, value, status = false }: { label: string; value: string; status?: boolean }) {
  return <div className="candidate-summary-row"><small>{label}</small><strong className={status ? 'candidate-status-text' : undefined}>{value}</strong></div>
}

function fullName(profile: CandidateIntakeProfile) {
  return [profile.firstNames, profile.paternalSurname, profile.maternalSurname].filter(Boolean).join(' ')
}

function initials(firstNames: string, surname: string | null) {
  return `${firstNames.trim()[0] ?? ''}${surname?.trim()[0] ?? ''}`.toUpperCase()
}

function reviewStatusLabel(status: string) {
  if (status === 'approved') return 'Aprobado y publicado'
  if (status === 'observed') return 'Observado'
  if (status === 'rejected') return 'Rechazado'
  return 'Pendiente de Gran Secretaría'
}

function formatDateOnly(value: string | null) {
  if (!value) return 'No informado'
  const date = new Date(`${value}T12:00:00Z`)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-CL', { dateStyle: 'long', timeZone: 'America/Santiago' }).format(date)
}

function formatDateTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(date)
}
