import { useEffect, useMemo, useState } from 'react'
import { type CandidateIntakeApiClient, type CandidateIntakeProfile, type CandidateIntakeUpsertPayload, type CandidateWorkshopQueueItem } from './api/candidateIntakeApi'
import './CandidateWorkshopIntakePage.css'

interface CandidateWorkshopIntakePageProps {
  api: CandidateIntakeApiClient
  onBack?: () => void
}

const emptyForm = (item?: CandidateWorkshopQueueItem): CandidateIntakeUpsertPayload => ({
  firstNames: item?.firstNames ?? '',
  paternalSurname: '',
  maternalSurname: null,
  rutOrInstitutionalId: null,
  birthDate: null,
  nationality: null,
  civilStatus: null,
  occupation: null,
  phone: null,
  email: null,
  address: null,
  city: null,
  orient: null,
  presenters: [],
  insinuationDate: chileToday(),
  interviewSummary: null,
  internalObservations: null,
})

export default function CandidateWorkshopIntakePage({ api, onBack }: CandidateWorkshopIntakePageProps) {
  const [queue, setQueue] = useState<CandidateWorkshopQueueItem[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [profile, setProfile] = useState<CandidateIntakeProfile | null>(null)
  const [form, setForm] = useState<CandidateIntakeUpsertPayload>(() => emptyForm())
  const [presentersText, setPresentersText] = useState('')
  const [photoVersionId, setPhotoVersionId] = useState('')
  const [photoSrc, setPhotoSrc] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const selected = useMemo(() => queue.find(item => item.ceremonyRequestId === selectedId) ?? null, [queue, selectedId])
  const locked = selected?.reviewStatus === 'approved' || selected?.reviewStatus === 'rejected'

  useEffect(() => {
    let active = true
    setLoading(true)
    api.getWorkshopQueue()
      .then(result => {
        if (!active) return
        setQueue(result.items)
        setSelectedId(current => current ?? result.items[0]?.ceremonyRequestId ?? null)
      })
      .catch(reason => { if (active) setError(errorMessage(reason, 'No fue posible cargar las solicitudes de iniciación del Taller.')) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  useEffect(() => {
    let active = true
    setMessage(null)
    setError(null)
    setPhotoVersionId('')
    if (!selected) {
      setProfile(null)
      setForm(emptyForm())
      setPresentersText('')
      return () => { active = false }
    }

    if (!selected.profileAvailable) {
      setProfile(null)
      setForm(emptyForm(selected))
      setPresentersText('')
      return () => { active = false }
    }

    setLoading(true)
    api.getProfile(selected.ceremonyRequestId)
      .then(value => {
        if (!active) return
        setProfile(value)
        setForm(profileToPayload(value))
        setPresentersText(value.presenters.join('\n'))
      })
      .catch(reason => { if (active) setError(errorMessage(reason, 'No fue posible cargar la ficha del insinuado.')) })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [api, selected])

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    setPhotoSrc(null)
    if (!profile?.photoAvailable) return () => { active = false }

    if (api.useMocks) {
      setPhotoSrc(`${import.meta.env.BASE_URL}demo-candidate-passport.svg`)
      return () => { active = false }
    }

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
    const queueResult = await api.getWorkshopQueue()
    setQueue(queueResult.items)
    const current = queueResult.items.find(item => item.ceremonyRequestId === requestId)
    if (current?.profileAvailable) {
      const updated = await api.getProfile(requestId)
      setProfile(updated)
      setForm(profileToPayload(updated))
      setPresentersText(updated.presenters.join('\n'))
    }
  }

  async function save() {
    if (!selected || busy || locked) return
    const presenters = parsePresenters(presentersText)
    if (!form.firstNames.trim() || !form.paternalSurname.trim()) {
      setError('Nombres y apellido paterno son obligatorios.')
      return
    }
    if (!form.insinuationDate) {
      setError('Debe registrar la fecha de insinuación.')
      return
    }
    if (presenters.length === 0) {
      setError('Debe registrar al menos un patrocinante o presentante.')
      return
    }

    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      const saved = await api.saveProfile(selected.ceremonyRequestId, { ...form, presenters })
      setProfile(saved)
      setForm(profileToPayload(saved))
      setPresentersText(saved.presenters.join('\n'))
      await refresh(selected.ceremonyRequestId)
      setMessage('Ficha guardada y enviada a revisión de Gran Secretaría. Aún no está publicada para los Hermanos.')
    } catch (reason) {
      setError(errorMessage(reason, 'No fue posible guardar la ficha.'))
    } finally {
      setBusy(false)
    }
  }

  async function linkPhoto() {
    if (!selected || !profile || busy || locked) return
    const value = photoVersionId.trim()
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value)) {
      setError('Ingrese un ID válido de versión documental procesada para la fotografía.')
      return
    }
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      await api.attachPhotoVersion(selected.ceremonyRequestId, value)
      await refresh(selected.ceremonyRequestId)
      setPhotoVersionId('')
      setMessage('Fotografía tipo pasaporte vinculada. La ficha volvió a quedar pendiente de revisión de Gran Secretaría.')
    } catch (reason) {
      setError(errorMessage(reason, 'No fue posible vincular la fotografía.'))
    } finally {
      setBusy(false)
    }
  }

  return <div className="workshop-intake-page">
    <section className="candidate-profile-heading">
      <div>
        <p className="candidate-profile-breadcrumb">Secretaría Logial <span>›</span> Insinuados <span>›</span> Carga de ficha</p>
        <h1>Ingreso de Insinuado</h1>
        <p>Secretaría del Taller completa el expediente privado. La publicación sólo comienza después de la aprobación de Gran Secretaría.</p>
      </div>
      <div className="candidate-heading-actions">
        {onBack && <button className="candidate-secondary-button" type="button" onClick={onBack}>Insinuados publicados</button>}
        <span className="workshop-intake-scope">Ámbito: Taller autorizado</span>
      </div>
    </section>

    {api.useMocks && <div className="candidate-protected-notice">Datos ficticios para QA. Esta vista representa la operación real de Secretaría Logial con control de ámbito y auditoría.</div>}
    {error && <div className="error-banner" role="alert"><strong>Ficha de insinuado</strong><span>{error}</span></div>}
    {message && <div className="candidate-protected-notice" role="status">{message}</div>}

    <section className="workshop-intake-layout">
      <aside className="candidate-product-card workshop-intake-queue">
        <div className="candidate-section-title"><span>▤</span><h2>Solicitudes de iniciación</h2><em>{queue.length}</em></div>
        {loading && queue.length === 0 ? <p>Cargando solicitudes…</p> : queue.length === 0 ? <p>No existen solicitudes de iniciación disponibles para este Taller.</p> : <div className="workshop-intake-list">{queue.map(item => <button key={item.ceremonyRequestId} type="button" className={item.ceremonyRequestId === selectedId ? 'active' : ''} onClick={() => setSelectedId(item.ceremonyRequestId)}><strong>{item.displayName || 'Insinuado sin nombre'}</strong><span>{item.workshopName}{item.workshopNumber ? ` · Nº ${item.workshopNumber}` : ''}</span><small>{statusLabel(item.reviewStatus)} · {item.profileAvailable ? 'Ficha registrada' : 'Ficha pendiente'}</small></button>)}</div>}
      </aside>

      <main className="workshop-intake-main">
        {!selected ? <section className="candidate-product-card"><p>Seleccione una solicitud para comenzar.</p></section> : <>
          <section className="candidate-product-card workshop-intake-summary">
            <div><small>Insinuado base</small><strong>{selected.displayName}</strong></div>
            <div><small>Logia/Taller presentante</small><strong>{selected.workshopName}{selected.workshopNumber ? ` · Nº ${selected.workshopNumber}` : ''}</strong></div>
            <div><small>Estado de revisión</small><strong className={`workshop-status ${selected.reviewStatus}`}>{statusLabel(selected.reviewStatus)}</strong></div>
            <div><small>Fecha propuesta de ceremonia</small><strong>{selected.proposedDate ? formatDateOnly(selected.proposedDate) : 'Por definir'}</strong></div>
          </section>

          {locked && <div className="candidate-protected-notice">Esta ficha está {selected.reviewStatus === 'approved' ? 'aprobada/publicada' : 'rechazada'} y se muestra en modo de sólo lectura. Cualquier reapertura deberá quedar trazada mediante un flujo institucional específico.</div>}

          <section className="candidate-product-card">
            <div className="candidate-section-title"><span>▣</span><h2>Datos personales y de contacto</h2><em>Núcleo institucional protegido</em></div>
            <div className="workshop-form-grid">
              <Field label="Nombres *"><input value={form.firstNames} disabled={locked} onChange={event => setForm({ ...form, firstNames: event.target.value })} /></Field>
              <Field label="Apellido paterno *"><input value={form.paternalSurname} disabled={locked} onChange={event => setForm({ ...form, paternalSurname: event.target.value })} /></Field>
              <Field label="Apellido materno"><input value={form.maternalSurname ?? ''} disabled={locked} onChange={event => setForm({ ...form, maternalSurname: event.target.value || null })} /></Field>
              <Field label="RUT / ID"><input value={form.rutOrInstitutionalId ?? ''} disabled={locked} onChange={event => setForm({ ...form, rutOrInstitutionalId: event.target.value || null })} /></Field>
              <Field label="Fecha de nacimiento"><input type="date" value={form.birthDate ?? ''} disabled={locked} onChange={event => setForm({ ...form, birthDate: event.target.value || null })} /></Field>
              <Field label="Nacionalidad"><input value={form.nationality ?? ''} disabled={locked} onChange={event => setForm({ ...form, nationality: event.target.value || null })} /></Field>
              <Field label="Estado civil"><input value={form.civilStatus ?? ''} disabled={locked} onChange={event => setForm({ ...form, civilStatus: event.target.value || null })} /></Field>
              <Field label="Profesión u oficio"><input value={form.occupation ?? ''} disabled={locked} onChange={event => setForm({ ...form, occupation: event.target.value || null })} /></Field>
              <Field label="Teléfono"><input type="tel" value={form.phone ?? ''} disabled={locked} onChange={event => setForm({ ...form, phone: event.target.value || null })} /></Field>
              <Field label="Correo"><input type="email" value={form.email ?? ''} disabled={locked} onChange={event => setForm({ ...form, email: event.target.value || null })} /></Field>
              <Field label="Dirección" wide><input value={form.address ?? ''} disabled={locked} onChange={event => setForm({ ...form, address: event.target.value || null })} /></Field>
              <Field label="Ciudad"><input value={form.city ?? ''} disabled={locked} onChange={event => setForm({ ...form, city: event.target.value || null })} /></Field>
            </div>
          </section>

          <section className="candidate-product-card">
            <div className="candidate-section-title"><span>⌂</span><h2>Presentación logial y antecedentes</h2><em>Formulario auditable</em></div>
            <div className="workshop-form-grid">
              <Field label="Logia/Taller que presenta"><input value={`${selected.workshopName}${selected.workshopNumber ? ` · Nº ${selected.workshopNumber}` : ''}`} disabled /></Field>
              <Field label="Oriente"><input value={form.orient ?? ''} disabled={locked} onChange={event => setForm({ ...form, orient: event.target.value || null })} /></Field>
              <Field label="Fecha de insinuación *"><input type="date" value={form.insinuationDate} disabled={locked} onChange={event => setForm({ ...form, insinuationDate: event.target.value })} /></Field>
              <Field label="Patrocinantes / Presentantes *" wide><textarea rows={3} value={presentersText} disabled={locked} onChange={event => setPresentersText(event.target.value)} placeholder="Un nombre por línea" /></Field>
              <Field label="Resumen de entrevista" wide><textarea rows={5} value={form.interviewSummary ?? ''} disabled={locked} onChange={event => setForm({ ...form, interviewSummary: event.target.value || null })} /></Field>
              <Field label="Observaciones internas" wide><textarea rows={4} value={form.internalObservations ?? ''} disabled={locked} onChange={event => setForm({ ...form, internalObservations: event.target.value || null })} /></Field>
            </div>
            <div className="workshop-form-actions">
              <small>Al guardar, la ficha queda pendiente de Gran Secretaría. Guardar no publica ni inicia el cómputo de días.</small>
              <button className="candidate-primary-button" type="button" disabled={busy || locked} onClick={() => void save()}>{busy ? 'Guardando…' : profile ? 'Guardar cambios y reenviar' : 'Guardar y enviar a revisión'}</button>
            </div>
          </section>

          <section className="candidate-product-card workshop-photo-section">
            <div className="candidate-section-title"><span>◫</span><h2>Fotografía tipo pasaporte</h2><em>Almacenamiento privado</em></div>
            <div className="workshop-photo-layout">
              <div className="candidate-passport-photo">{photoSrc ? <img src={photoSrc} alt="Foto tipo pasaporte del expediente" /> : <div className="workshop-photo-placeholder">Sin foto</div>}<small>{profile?.photoAvailable ? 'Fotografía vinculada' : 'Pendiente'}</small></div>
              <div>
                <p>La foto se vincula desde una versión documental JPEG/PNG que ya haya completado el análisis antivirus. El objeto original permanece privado y nunca se publica con una URL permanente.</p>
                <label className="workshop-photo-version"><span>ID de versión documental procesada</span><input value={photoVersionId} disabled={!profile || busy || locked} onChange={event => setPhotoVersionId(event.target.value)} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" /></label>
                <button className="candidate-secondary-button" type="button" disabled={!profile || busy || locked} onClick={() => void linkPhoto()}>Vincular fotografía</button>
                {!profile && <small className="workshop-help">Primero guarde la ficha antes de vincular la fotografía.</small>}
              </div>
            </div>
          </section>

          <section className="candidate-product-card workshop-config-note">
            <div className="candidate-section-title"><span>⚙</span><h2>Campos configurables</h2><em>PMGM-BLG-074</em></div>
            <p>Los campos esenciales de identidad, Logia/Taller, estado del proceso, fechas y trazabilidad permanecen protegidos. La siguiente capa incorporará campos no esenciales configurables, listas institucionales, obligatoriedad, orden, visibilidad por rol y versionado sin perder el significado histórico de las fichas.</p>
          </section>
        </>}
      </main>
    </section>
  </div>
}

function Field({ label, wide = false, children }: { label: string; wide?: boolean; children: React.ReactNode }) {
  return <label className={wide ? 'workshop-field wide' : 'workshop-field'}><span>{label}</span>{children}</label>
}

function profileToPayload(profile: CandidateIntakeProfile): CandidateIntakeUpsertPayload {
  return {
    firstNames: profile.firstNames,
    paternalSurname: profile.paternalSurname ?? '',
    maternalSurname: profile.maternalSurname,
    rutOrInstitutionalId: profile.rutOrInstitutionalId,
    birthDate: profile.birthDate,
    nationality: profile.nationality,
    civilStatus: profile.civilStatus,
    occupation: profile.occupation,
    phone: profile.phone,
    email: profile.email,
    address: profile.address,
    city: profile.city,
    orient: profile.orient,
    presenters: [...profile.presenters],
    insinuationDate: profile.insinuationDate,
    interviewSummary: profile.interviewSummary,
    internalObservations: profile.internalObservations,
  }
}

function parsePresenters(value: string) {
  return [...new Set(value.split(/[\n,;]+/).map(item => item.trim()).filter(Boolean))]
}

function statusLabel(value: string) {
  if (value === 'approved') return 'Aprobada / publicada'
  if (value === 'observed') return 'Observada por Gran Secretaría'
  if (value === 'rejected') return 'Rechazada'
  return 'Pendiente de Gran Secretaría'
}

function errorMessage(reason: unknown, fallback: string) {
  return reason instanceof Error ? reason.message : fallback
}

function chileToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]))
  return `${values.year}-${values.month}-${values.day}`
}

function formatDateOnly(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return value
  return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'America/Santiago' }).format(new Date(Date.UTC(year, month - 1, day, 12)))
}
