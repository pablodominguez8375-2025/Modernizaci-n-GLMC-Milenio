import { type FormEvent, type ReactNode, useCallback, useEffect, useState } from 'react'
import { ActionBar, ActionDrawer, ConfirmAction, HelpNote } from './actionKit'
import { type LodgeApiClient } from './api/lodgeApi'
import { type PmgmApiClient, type OrganizationOption } from './api/pmgmApi'
import {
  type SpaceOption, type SpaceRequest, type SpaceRequestPurpose, type SpaceRequestType,
  purposeLabel, requestStatusLabel, spaceTypeText,
} from './api/spaceRequests'
import { organizationDisplayName } from './displayFormat'
import './spaceRequests.css'

const SANTIAGO = 'America/Santiago'
const range = (a: string, b: string) => {
  const date = new Intl.DateTimeFormat('es-CL', { dateStyle: 'full', timeZone: SANTIAGO }).format(new Date(a))
  const time = new Intl.DateTimeFormat('es-CL', { timeStyle: 'short', timeZone: SANTIAGO })
  return `${date} · ${time.format(new Date(a))} a ${time.format(new Date(b))}`
}
function offsetMs(date: Date) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: SANTIAGO, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }).formatToParts(date)
  const v = (t: string) => Number(parts.find(p => p.type === t)?.value)
  return Date.UTC(v('year'), v('month') - 1, v('day'), v('hour'), v('minute'), v('second')) - date.getTime()
}
function santiagoToIso(value: string) {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value)
  if (!m) throw new Error('Indique fecha y hora.')
  const wall = Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], 0)
  let guess = new Date(wall)
  for (let i = 0; i < 2; i++) guess = new Date(wall - offsetMs(guess))
  return guess.toISOString()
}
const messageOf = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.'
function Field({ label, children }: { label: string; children: ReactNode }) { return <label className="field"><span>{label}</span>{children}</label> }
function StatusPill({ status }: { status: SpaceRequest['status'] }) {
  return <span className={`status-pill space-request-${status}`}>{requestStatusLabel(status)}</span>
}

export type CeremonyChoice = { ceremonyRequestId: string; label: string }

/** Secretaría del Taller: solicita templo o sala y sigue el estado. Solo Gran Secretaría confirma. */
export function LodgeSpaceRequests({ organizationId, api, ceremonies, canManage }: {
  organizationId: string; api: LodgeApiClient; ceremonies: CeremonyChoice[]; canManage: boolean
}) {
  const [items, setItems] = useState<SpaceRequest[]>([])
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const refresh = useCallback(async () => { setItems((await api.listSpaceRequests(organizationId)).items) }, [api, organizationId])
  useEffect(() => {
    let active = true
    api.listSpaceRequests(organizationId).then(r => { if (active) setItems(r.items) })
      .catch(e => { if (active) setError(messageOf(e)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api, organizationId])
  const run = async (action: () => Promise<void>, ok: string) => {
    setWorking(true); setError(null); setMessage(null)
    try { await action(); setMessage(ok) } catch (e) { setError(messageOf(e)) } finally { setWorking(false) }
  }
  const pending = items.filter(x => x.status === 'pending').length

  return <article className="panel space-requests">
    <div className="panel-heading">
      <div><p className="eyebrow">Gran Secretaría</p><h2>Solicitudes de templo o sala</h2></div>
      <span className="count-badge">{loading ? '…' : `${pending} en revisión`}</span>
    </div>
    <HelpNote><p>Aquí pide un templo o sala para su Taller. Gran Secretaría revisa la solicitud y responde. La reserva solo queda confirmada cuando la solicitud aparece como «Aprobada».</p></HelpNote>
    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}
    {canManage && <ActionBar>
      <ActionDrawer label="Solicitar templo o sala" description="Indique cuándo y para qué necesita el espacio." confirmMessage="La solicitud se enviará a Gran Secretaría para su revisión.">
        <LodgeRequestForm organizationId={organizationId} api={api} ceremonies={ceremonies} working={working}
          submit={payload => run(async () => { await api.createSpaceRequest(organizationId, payload); await refresh() }, 'Solicitud enviada a Gran Secretaría.')} />
      </ActionDrawer>
    </ActionBar>}
    {loading ? <p>Cargando solicitudes…</p> : items.length === 0
      ? <p className="muted">Su Taller aún no ha solicitado templos ni salas.</p>
      : <ul className="space-request-list">{items.map(item => <li key={item.id} className="space-request-row">
        <div>
          <div className="space-request-title"><strong>{spaceTypeText(item.preferredSpaceType)} · {purposeLabel(item.purpose)}</strong><StatusPill status={item.status} /></div>
          <span>{range(item.startsAtUtc, item.endsAtUtc)}</span>
          {item.status === 'rejected' && item.responseReason && <small className="space-request-reason"><b>Motivo:</b> {item.responseReason}</small>}
          {item.status === 'approved' && <small className="space-request-ok">Reserva confirmada por Gran Secretaría.</small>}
        </div>
        {canManage && item.status === 'pending' && <ConfirmAction label="Cancelar solicitud" tone="secondary" disabled={working}
          message="¿Cancelar esta solicitud? Gran Secretaría ya no la revisará." confirmLabel="Sí, cancelar"
          onConfirm={() => void run(async () => { await api.cancelSpaceRequest(item.id); await refresh() }, 'Solicitud cancelada.')} />}
      </li>)}</ul>}
  </article>
}

function LodgeRequestForm({ organizationId, api, ceremonies, working, submit }: {
  organizationId: string; api: LodgeApiClient; ceremonies: CeremonyChoice[]; working: boolean
  submit: (payload: Parameters<LodgeApiClient['createSpaceRequest']>[1]) => Promise<void>
}) {
  const [spaceType, setSpaceType] = useState<SpaceRequestType>('secretariat_room')
  const [purpose, setPurpose] = useState<SpaceRequestPurpose>('tenida')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [spaceId, setSpaceId] = useState('')
  const [ceremonyId, setCeremonyId] = useState('')
  const [attendees, setAttendees] = useState('')
  const [notes, setNotes] = useState('')
  const [equipment, setEquipment] = useState('')
  const [responsible, setResponsible] = useState('')
  const [localError, setLocalError] = useState<string | null>(null)
  const [options, setOptions] = useState<SpaceOption[]>([])
  useEffect(() => {
    if (!start || !end) return
    let active = true
    try { api.getSpaceOptions(santiagoToIso(start), santiagoToIso(end)).then(r => { if (active) setOptions(r) }).catch(() => { if (active) setOptions([]) }) }
    catch { /* fechas incompletas */ }
    return () => { active = false }
  }, [api, start, end])
  const choices = options.filter(x => x.spaceType === spaceType)
  const onSubmit = (event: FormEvent) => {
    event.preventDefault(); setLocalError(null)
    try {
      const startsAtUtc = santiagoToIso(start); const endsAtUtc = santiagoToIso(end)
      if (new Date(endsAtUtc) <= new Date(startsAtUtc)) return setLocalError('La hora de término debe ser posterior al inicio.')
      if (purpose === 'ceremonia' && !ceremonyId) return setLocalError('Para una ceremonia, elija la ceremonia autorizada.')
      void submit({
        spaceType, preferredSpaceId: choices.some(x => x.id === spaceId) ? spaceId : null, startsAtUtc, endsAtUtc, purpose,
        ceremonyRequestId: purpose === 'ceremonia' ? ceremonyId : null, notes: notes.trim() || null,
        expectedAttendees: attendees ? Number(attendees) : null, equipmentRequest: equipment.trim() || null,
        equipmentResponsible: equipment.trim() ? responsible.trim() || null : null, vmApprovalReference: null,
      })
    } catch (e) { setLocalError(messageOf(e)) }
  }
  void organizationId
  return <form className="stack-form" onSubmit={onSubmit}>
    {localError && <div className="error-banner" role="alert">{localError}</div>}
    <Field label="¿Qué necesita?"><select value={spaceType} onChange={e => { setSpaceType(e.target.value as SpaceRequestType); setSpaceId('') }}>
      <option value="secretariat_room">Sala de Secretaría</option><option value="temple">Templo</option></select></Field>
    <Field label="¿Para qué actividad?"><select value={purpose} onChange={e => setPurpose(e.target.value as SpaceRequestPurpose)}>
      <option value="tenida">Tenida</option><option value="docencia">Docencia</option><option value="ceremonia">Ceremonia</option><option value="otro">Otra actividad</option></select></Field>
    {purpose === 'ceremonia' && <Field label="Ceremonia autorizada"><select required value={ceremonyId} onChange={e => setCeremonyId(e.target.value)}>
      <option value="">Elija…</option>{ceremonies.map(c => <option key={c.ceremonyRequestId} value={c.ceremonyRequestId}>{c.label}</option>)}</select></Field>}
    <Field label="Inicio · hora Chile"><input type="datetime-local" required value={start} onChange={e => setStart(e.target.value)} /></Field>
    <Field label="Término · hora Chile"><input type="datetime-local" required value={end} onChange={e => setEnd(e.target.value)} /></Field>
    {choices.length > 0 && <Field label="Espacio preferido · opcional"><select value={spaceId} onChange={e => setSpaceId(e.target.value)}>
      <option value="">Sin preferencia</option>{choices.map(x => <option key={x.id} value={x.id}>{x.name}{x.isAvailable ? '' : ' (ocupado en ese horario)'}</option>)}</select></Field>}
    <Field label="Personas esperadas · opcional"><input type="number" min={1} inputMode="numeric" value={attendees} onChange={e => setAttendees(e.target.value)} /></Field>
    <Field label="Equipos que necesita · opcional"><input maxLength={500} value={equipment} onChange={e => setEquipment(e.target.value)} placeholder="Ej.: proyector" /></Field>
    {equipment.trim() && <Field label="Responsable de los equipos"><input maxLength={240} value={responsible} onChange={e => setResponsible(e.target.value)} /></Field>}
    <Field label="Comentario · opcional"><textarea maxLength={500} rows={3} value={notes} onChange={e => setNotes(e.target.value)} /></Field>
    <button className="primary-action" type="submit" disabled={working}>Enviar solicitud</button>
  </form>
}

/** Gran Secretaría: bandeja de solicitudes de los Talleres. */
export function GrandSpaceRequests({ api, organizations, onChanged }: {
  api: PmgmApiClient; organizations: OrganizationOption[]; onChanged?: () => Promise<void> | void
}) {
  const [items, setItems] = useState<SpaceRequest[]>([])
  const [showResolved, setShowResolved] = useState(false)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const refresh = useCallback(async () => { setItems((await api.listAllSpaceRequests()).items) }, [api])
  useEffect(() => {
    let active = true
    api.listAllSpaceRequests().then(r => { if (active) setItems(r.items) })
      .catch(e => { if (active) setError(messageOf(e)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])
  const name = (id: string) => { const o = organizations.find(x => x.id === id); return o ? organizationDisplayName(o.name, o.number) : 'Taller' }
  const run = async (action: () => Promise<void>, ok: string) => {
    setWorking(true); setError(null); setMessage(null)
    try { await action(); await refresh(); await onChanged?.(); setMessage(ok) } catch (e) { setError(messageOf(e)) } finally { setWorking(false) }
  }
  const pending = items.filter(x => x.status === 'pending')
  const resolved = items.filter(x => x.status !== 'pending')

  return <article className="panel space-requests secretariat-wide">
    <div className="panel-heading">
      <div><p className="eyebrow">Peticiones de los Talleres</p><h2>Solicitudes de espacio</h2></div>
      <span className="count-badge">{loading ? '…' : `${pending.length} por responder`}</span>
    </div>
    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="success-banner" role="status">{message}</div>}
    {loading ? <p>Cargando solicitudes…</p> : pending.length === 0
      ? <p className="muted">No hay solicitudes por responder.</p>
      : <ul className="space-request-list">{pending.map(item => <li key={item.id} className="space-request-row">
        <div>
          <div className="space-request-title"><strong>{name(item.organizationId)}</strong><StatusPill status={item.status} /></div>
          <span>{spaceTypeText(item.preferredSpaceType)} · {purposeLabel(item.purpose)}</span>
          <span>{range(item.startsAtUtc, item.endsAtUtc)}</span>
          {(item.expectedAttendees || item.equipmentRequest || item.notes) && <small>
            {item.expectedAttendees ? `${item.expectedAttendees} personas. ` : ''}{item.equipmentRequest ? `Equipos: ${item.equipmentRequest}. ` : ''}{item.notes ?? ''}</small>}
        </div>
        <ActionBar>
          <ActionDrawer label="Aprobar" description="Elija el espacio que se reservará." confirmMessage="Se confirmará la reserva para el Taller.">
            <ApproveForm api={api} request={item} working={working} approve={spaceId => run(() => api.approveSpaceRequest(item.id, spaceId).then(() => undefined), 'Solicitud aprobada y reserva confirmada.')} />
          </ActionDrawer>
          <ActionDrawer label="Rechazar" tone="secondary" description="El Taller verá el motivo que escriba.">
            <RejectForm working={working} reject={reason => run(() => api.rejectSpaceRequest(item.id, reason).then(() => undefined), 'Solicitud rechazada. El Taller verá el motivo.')} />
          </ActionDrawer>
        </ActionBar>
      </li>)}</ul>}
    {resolved.length > 0 && <>
      <button type="button" className="action-trigger secondary" aria-expanded={showResolved} onClick={() => setShowResolved(v => !v)}>
        {showResolved ? 'Ocultar solicitudes resueltas' : `Ver solicitudes resueltas (${resolved.length})`}</button>
      {showResolved && <ul className="space-request-list">{resolved.map(item => <li key={item.id} className="space-request-row"><div>
        <div className="space-request-title"><strong>{name(item.organizationId)}</strong><StatusPill status={item.status} /></div>
        <span>{spaceTypeText(item.preferredSpaceType)} · {purposeLabel(item.purpose)} · {range(item.startsAtUtc, item.endsAtUtc)}</span>
        {item.responseReason && <small className="space-request-reason"><b>Motivo:</b> {item.responseReason}</small>}
      </div></li>)}</ul>}
    </>}
  </article>
}

function ApproveForm({ api, request, working, approve }: { api: PmgmApiClient; request: SpaceRequest; working: boolean; approve: (spaceId: string) => Promise<void> }) {
  const [spaces, setSpaces] = useState<Array<{ id: string; name: string; isAvailable?: boolean }>>([])
  const [spaceId, setSpaceId] = useState(request.preferredSpaceId ?? '')
  useEffect(() => {
    let active = true
    api.getSecretariatAvailability(request.startsAtUtc, request.endsAtUtc).then(r => {
      if (!active) return
      const list = r.items.filter(x => x.spaceType === request.preferredSpaceType)
      setSpaces(list); setSpaceId(prev => list.some(x => x.id === prev && x.isAvailable) ? prev : list.find(x => x.isAvailable)?.id ?? '')
    }).catch(() => { if (active) setSpaces([]) })
    return () => { active = false }
  }, [api, request])
  const free = spaces.filter(x => x.isAvailable)
  return <form className="stack-form" onSubmit={(e: FormEvent) => { e.preventDefault(); void approve(spaceId) }}>
    {request.preferredSpaceType === 'temple' && <div className="space-request-note" role="note">Los templos requieren validar antes el protocolo de uso y la firma del Venerable Maestro. Mientras tanto el sistema no permite aprobarlos.</div>}
    <p>{range(request.startsAtUtc, request.endsAtUtc)}</p>
    <Field label={`${spaceTypeText(request.preferredSpaceType)} disponible`}>
      <select required value={spaceId} onChange={e => setSpaceId(e.target.value)}>
        <option value="">{free.length ? 'Elija…' : 'No hay espacios libres en ese horario'}</option>
        {free.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
    <button className="primary-action" type="submit" disabled={working || !spaceId}>Aprobar y reservar</button>
  </form>
}

function RejectForm({ working, reject }: { working: boolean; reject: (reason: string) => Promise<void> }) {
  const [reason, setReason] = useState('')
  return <form className="stack-form" onSubmit={(e: FormEvent) => { e.preventDefault(); void reject(reason.trim()) }}>
    <Field label="Motivo del rechazo · obligatorio"><textarea required maxLength={500} rows={4} value={reason} onChange={e => setReason(e.target.value)} placeholder="Ej.: el espacio no está disponible; sugiera otra fecha." /></Field>
    <button className="primary-action" type="submit" disabled={working || !reason.trim()}>Rechazar solicitud</button>
  </form>
}
