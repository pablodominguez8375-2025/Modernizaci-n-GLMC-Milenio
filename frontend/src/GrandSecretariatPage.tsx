import { type FormEvent, useEffect, useMemo, useState } from 'react'
import {
  type InstitutionalSpace,
  type OrganizationOption,
  type PmgmApiClient,
  type SecretariatDocument,
  type SpaceAvailabilityResponse,
} from './api/pmgmApi'
import './secretariat.css'

const SANTIAGO = 'America/Santiago'

export default function GrandSecretariatPage({ api }: { api: PmgmApiClient }) {
  const initial = useMemo(() => defaultWindow(), [])
  const [fromLocal, setFromLocal] = useState(initial.from)
  const [toLocal, setToLocal] = useState(initial.to)
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [availability, setAvailability] = useState<SpaceAvailabilityResponse | null>(null)
  const [documents, setDocuments] = useState<SecretariatDocument[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const refreshAvailability = async () => {
    const fromUtc = santiagoLocalToIso(fromLocal)
    const toUtc = santiagoLocalToIso(toLocal)
    if (new Date(toUtc) <= new Date(fromUtc)) throw new Error('El término debe ser posterior al inicio.')
    setAvailability(await api.getSecretariatAvailability(fromUtc, toUtc))
  }

  const refreshDocuments = async () => {
    const response = await api.getSecretariatDocuments()
    setDocuments(response.items)
  }

  useEffect(() => {
    let active = true
    Promise.all([
      api.getOrganizationOptions(),
      api.getSecretariatDocuments(),
      api.getSecretariatAvailability(santiagoLocalToIso(initial.from), santiagoLocalToIso(initial.to)),
    ]).then(([orgs, docs, spaces]) => {
      if (!active) return
      setOrganizations(orgs.items)
      setDocuments(docs.items)
      setAvailability(spaces)
    }).catch(reason => {
      if (active) setError(toMessage(reason))
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [api, initial])

  const execute = async (action: () => Promise<void>, success: string) => {
    setWorking(true); setError(null); setMessage(null)
    try { await action(); setMessage(success) }
    catch (reason) { setError(toMessage(reason)) }
    finally { setWorking(false) }
  }

  return (
    <>
      <section className="page-heading">
        <div>
          <p className="eyebrow">Operación institucional</p>
          <h1>Gran Secretaría</h1>
          <p>Espacios, reservas y documentos oficiales, con trazabilidad y horario institucional de Chile.</p>
        </div>
        <span className="count-badge">{loading ? 'cargando…' : `${documents.length} documentos`}</span>
      </section>

      {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
      {message && <div className="success-banner" role="status">{message}</div>}

      <section className="secretariat-grid">
        <article className="panel secretariat-wide">
          <div className="panel-heading">
            <div><p className="eyebrow">Calendario</p><h2>Disponibilidad de templos y salas</h2></div>
            <span className="count-badge">{availability ? `${availability.available}/${availability.total} disponibles` : '—'}</span>
          </div>
          <form className="inline-form" onSubmit={(event) => { event.preventDefault(); void execute(refreshAvailability, 'Disponibilidad actualizada.') }}>
            <Field label="Desde · hora Chile"><input type="datetime-local" required value={fromLocal} onChange={e => setFromLocal(e.target.value)} /></Field>
            <Field label="Hasta · hora Chile"><input type="datetime-local" required value={toLocal} onChange={e => setToLocal(e.target.value)} /></Field>
            <button className="primary-action" type="submit" disabled={working}>Consultar</button>
          </form>
          <div className="space-list">
            {loading ? <p>Cargando espacios…</p> : availability?.items.length ? availability.items.map(space => <SpaceRow key={space.id} space={space} />) : <p className="muted">No hay espacios institucionales activos.</p>}
          </div>
        </article>

        <ReservationPanel api={api} organizations={organizations} availability={availability} working={working} execute={execute} refreshAvailability={refreshAvailability} fromLocal={fromLocal} toLocal={toLocal} />
        <SpacePanel api={api} working={working} execute={execute} refreshAvailability={refreshAvailability} />
        <DocumentPanel api={api} organizations={organizations} working={working} execute={execute} refreshDocuments={refreshDocuments} />

        <article className="panel secretariat-wide">
          <div className="panel-heading"><div><p className="eyebrow">Registro oficial</p><h2>Documentos recientes</h2></div></div>
          {documents.length === 0 ? <p className="muted">Aún no hay decretos o comunicados emitidos.</p> : (
            <div className="document-list">{documents.slice(0, 12).map(document => <div key={document.id}><strong>{document.documentCode}</strong><span>{document.title}</span><small>{documentTypeLabel(document.documentType)} · {formatChile(document.issuedAtUtc)}</small></div>)}</div>
          )}
        </article>
      </section>
    </>
  )
}

function ReservationPanel({ api, organizations, availability, working, execute, refreshAvailability, fromLocal, toLocal }: {
  api: PmgmApiClient; organizations: OrganizationOption[]; availability: SpaceAvailabilityResponse | null; working: boolean
  execute: (action: () => Promise<void>, success: string) => Promise<void>; refreshAvailability: () => Promise<void>; fromLocal: string; toLocal: string
}) {
  const availableSpaces = availability?.items.filter(x => x.isAvailable) ?? []
  const [organizationId, setOrganizationId] = useState('')
  const [spaceId, setSpaceId] = useState('')
  const [purpose, setPurpose] = useState('')
  useEffect(() => { if (!organizationId && organizations[0]) setOrganizationId(organizations[0].id) }, [organizations, organizationId])
  useEffect(() => { if (!availableSpaces.some(x => x.id === spaceId)) setSpaceId(availableSpaces[0]?.id ?? '') }, [availableSpaces, spaceId])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    void execute(async () => {
      await api.createSecretariatReservation({
        spaceId, organizationId, purpose,
        startsAtUtc: santiagoLocalToIso(fromLocal), endsAtUtc: santiagoLocalToIso(toLocal),
      })
      setPurpose('')
      await refreshAvailability()
    }, 'Reserva institucional registrada y auditada.')
  }

  return <article className="panel"><p className="eyebrow">Reserva</p><h2>Asignar espacio</h2><form className="stack-form" onSubmit={submit}>
    <Field label="Taller / organización"><select required value={organizationId} onChange={e => setOrganizationId(e.target.value)}><option value="">Seleccione…</option>{organizations.map(o => <option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></Field>
    <Field label="Templo o sala disponible"><select required value={spaceId} onChange={e => setSpaceId(e.target.value)}><option value="">Seleccione…</option>{availableSpaces.map(s => <option key={s.id} value={s.id}>{s.name} · {spaceTypeLabel(s.spaceType)}</option>)}</select></Field>
    <Field label="Propósito"><input required maxLength={300} value={purpose} onChange={e => setPurpose(e.target.value)} placeholder="Ej.: Tenida especial" /></Field>
    <small className="form-note">Usa el período consultado arriba. Las reservas vinculadas a una ceremonia se incorporarán desde el flujo de Ceremonias.</small>
    <button className="primary-action" disabled={working || !spaceId || !organizationId}>Reservar</button>
  </form></article>
}

function SpacePanel({ api, working, execute, refreshAvailability }: { api: PmgmApiClient; working: boolean; execute: (action: () => Promise<void>, success: string) => Promise<void>; refreshAvailability: () => Promise<void> }) {
  const [code, setCode] = useState(''); const [name, setName] = useState(''); const [type, setType] = useState<'temple' | 'secretariat_room'>('temple'); const [location, setLocation] = useState(''); const [capacity, setCapacity] = useState('')
  const submit = (event: FormEvent) => { event.preventDefault(); void execute(async () => {
    await api.createSecretariatSpace({ code, name, spaceType: type, location: location || null, capacity: capacity ? Number(capacity) : null })
    setCode(''); setName(''); setLocation(''); setCapacity(''); await refreshAvailability()
  }, 'Espacio institucional creado.') }
  return <article className="panel"><p className="eyebrow">Catálogo</p><h2>Nuevo templo o sala</h2><form className="stack-form" onSubmit={submit}>
    <div className="form-grid"><Field label="Código"><input required maxLength={40} value={code} onChange={e => setCode(e.target.value)} placeholder="TEMP-02" /></Field><Field label="Tipo"><select value={type} onChange={e => setType(e.target.value as typeof type)}><option value="temple">Templo</option><option value="secretariat_room">Sala de Secretaría</option></select></Field></div>
    <Field label="Nombre"><input required maxLength={200} value={name} onChange={e => setName(e.target.value)} /></Field>
    <div className="form-grid"><Field label="Ubicación"><input value={location} onChange={e => setLocation(e.target.value)} /></Field><Field label="Capacidad"><input type="number" min="1" value={capacity} onChange={e => setCapacity(e.target.value)} /></Field></div>
    <button className="primary-action" disabled={working}>Crear espacio</button>
  </form></article>
}

function DocumentPanel({ api, organizations, working, execute, refreshDocuments }: { api: PmgmApiClient; organizations: OrganizationOption[]; working: boolean; execute: (action: () => Promise<void>, success: string) => Promise<void>; refreshDocuments: () => Promise<void> }) {
  const [type, setType] = useState<'decree' | 'communication'>('communication'); const [title, setTitle] = useState(''); const [content, setContent] = useState(''); const [organizationId, setOrganizationId] = useState('')
  const submit = (event: FormEvent) => { event.preventDefault(); void execute(async () => {
    await api.issueSecretariatDocument({ documentType: type, title, content, organizationId: organizationId || null })
    setTitle(''); setContent(''); await refreshDocuments()
  }, type === 'decree' ? 'Decreto emitido y auditado.' : 'Comunicado emitido y auditado.') }
  return <article className="panel"><p className="eyebrow">Documentos</p><h2>Emitir documento oficial</h2><form className="stack-form" onSubmit={submit}>
    <div className="form-grid"><Field label="Tipo"><select value={type} onChange={e => setType(e.target.value as typeof type)}><option value="communication">Comunicado</option><option value="decree">Decreto</option></select></Field><Field label="Destinatario institucional"><select value={organizationId} onChange={e => setOrganizationId(e.target.value)}><option value="">Toda la Orden / general</option>{organizations.map(o => <option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></Field></div>
    <Field label="Título"><input required maxLength={240} value={title} onChange={e => setTitle(e.target.value)} /></Field>
    <Field label="Contenido"><textarea required rows={5} maxLength={4000} value={content} onChange={e => setContent(e.target.value)} /></Field>
    <button className="primary-action" disabled={working}>Emitir</button>
  </form></article>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="field"><span>{label}</span>{children}</label> }
function SpaceRow({ space }: { space: InstitutionalSpace }) { return <div className="space-row"><div><strong>{space.name}</strong><small>{space.code} · {spaceTypeLabel(space.spaceType)}{space.capacity ? ` · ${space.capacity} personas` : ''}</small></div><span className={space.isAvailable ? 'status-pill complete' : 'status-pill active'}>{space.isAvailable ? 'Disponible' : 'Ocupado'}</span></div> }
function organizationLabel(o: OrganizationOption) { return `${o.name}${o.number ? ` · Nº ${o.number}` : ''}` }
function spaceTypeLabel(type: InstitutionalSpace['spaceType']) { return type === 'temple' ? 'Templo' : 'Sala de Secretaría' }
function documentTypeLabel(type: SecretariatDocument['documentType']) { return type === 'decree' ? 'Decreto' : type === 'communication' ? 'Comunicado' : 'Autorización de ceremonia' }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: SANTIAGO }).format(new Date(value)) }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }

function defaultWindow() {
  const now = new Date(); const from = new Date(now.getTime() + 60 * 60 * 1000); from.setUTCMinutes(0, 0, 0); const to = new Date(from.getTime() + 2 * 60 * 60 * 1000)
  return { from: toSantiagoInput(from), to: toSantiagoInput(to) }
}
function toSantiagoInput(date: Date) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: SANTIAGO, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(date)
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === type)?.value ?? ''
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`
}
function santiagoLocalToIso(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value)
  if (!match) throw new Error('Fecha u hora inválida.')
  const [, y, m, d, hh, mm] = match
  const wall = Date.UTC(Number(y), Number(m) - 1, Number(d), Number(hh), Number(mm), 0)
  let guess = new Date(wall)
  for (let i = 0; i < 2; i++) guess = new Date(wall - zoneOffsetMs(guess, SANTIAGO))
  return guess.toISOString()
}
function zoneOffsetMs(date: Date, timeZone: string) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' }).formatToParts(date)
  const values = Object.fromEntries(parts.filter(p => p.type !== 'literal').map(p => [p.type, Number(p.value)]))
  return Date.UTC(values.year, values.month - 1, values.day, values.hour, values.minute, values.second) - date.getTime()
}
