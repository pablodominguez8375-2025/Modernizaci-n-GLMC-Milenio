import { type FormEvent, useEffect, useState } from 'react'
import {
  type OrganizationOption,
  type PmgmApiClient,
  type WorkshopRegularitySnapshot,
} from './api/pmgmApi'
import './regularity.css'

type RegularityKind = 'treasury' | 'hospitalaria'

const configs = {
  treasury: {
    eyebrow: 'Control financiero institucional',
    title: 'Gran Tesorería',
    description: 'Registra y consulta la regularidad financiera de cada Taller que participa en los flujos institucionales.',
    sourceLabel: 'Referencia de Tesorería',
    statusOptions: [
      ['up_to_date', 'Al día'],
      ['delinquent', 'Moroso'],
      ['pending', 'Pendiente de revisión'],
      ['exempt', 'Exento'],
    ] as const,
    ceremonyNote: 'Ceremonias consume únicamente el estado y la fecha de corte necesarios para determinar si el Taller se encuentra al día.',
  },
  hospitalaria: {
    eyebrow: 'Control de reposiciones',
    title: 'Gran Hospitalaria',
    description: 'Registra y consulta el estado de reposiciones de cada Taller para los controles previos a ceremonias.',
    sourceLabel: 'Referencia de Hospitalaria',
    statusOptions: [
      ['up_to_date', 'Al día'],
      ['overdue', 'Reposiciones pendientes'],
      ['pending', 'Pendiente de revisión'],
      ['exempt', 'Exento'],
    ] as const,
    ceremonyNote: 'Ceremonias consume únicamente el estado y la fecha de corte necesarios para comprobar que las reposiciones estén regularizadas.',
  },
} as const

export default function RegularityPage({ api, kind }: { api: PmgmApiClient; kind: RegularityKind }) {
  const config = configs[kind]
  const [organizations, setOrganizations] = useState<OrganizationOption[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [asOfDate, setAsOfDate] = useState(todayInChile())
  const [status, setStatus] = useState<string>('up_to_date')
  const [sourceReference, setSourceReference] = useState('')
  const [notes, setNotes] = useState('')
  const [current, setCurrent] = useState<WorkshopRegularitySnapshot | null>(null)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationOptions()
      .then(response => {
        if (!active) return
        const workshopOptions = response.items.filter(item => item.type.toLowerCase() !== 'order')
        setOrganizations(workshopOptions)
        setOrganizationId(workshopOptions[0]?.id ?? '')
      })
      .catch(reason => { if (active) setError(toMessage(reason)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [api])

  const readCurrent = async () => {
    if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    try {
      const snapshot = kind === 'treasury'
        ? await api.getTreasuryWorkshopRegularity(organizationId, asOfDate)
        : await api.getHospitalariaWorkshopRegularity(organizationId, asOfDate)
      setCurrent(snapshot)
      if (snapshot) {
        setStatus(snapshot.status)
        setSourceReference(snapshot.sourceReference ?? '')
        setNotes(snapshot.notes ?? '')
        setMessage('Estado institucional consultado.')
      } else {
        setMessage('No existe un estado registrado para el Taller en esa fecha de corte.')
      }
    } catch (reason) { setError(toMessage(reason)) }
    finally { setWorking(false) }
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!organizationId) return
    setWorking(true); setError(null); setMessage(null)
    const payload = { status, asOfDate, sourceReference: sourceReference.trim() || null, notes: notes.trim() || null }
    const request = kind === 'treasury'
      ? api.setTreasuryWorkshopRegularity(organizationId, payload)
      : api.setHospitalariaWorkshopRegularity(organizationId, payload)
    void request
      .then(snapshot => {
        setCurrent(snapshot)
        setMessage(kind === 'treasury' ? 'Regularidad de Gran Tesorería registrada y auditada.' : 'Regularidad de Gran Hospitalaria registrada y auditada.')
      })
      .catch(reason => setError(toMessage(reason)))
      .finally(() => setWorking(false))
  }

  const selectedOrganization = organizations.find(item => item.id === organizationId)

  return <>
    <section className="page-heading">
      <div><p className="eyebrow">{config.eyebrow}</p><h1>{config.title}</h1><p>{config.description}</p></div>
      <span className="count-badge">{loading ? 'cargando…' : `${organizations.length} Talleres visibles`}</span>
    </section>

    {error && <div className="error-banner" role="alert"><strong>Operación no completada.</strong><span>{error}</span></div>}
    {message && <div className="regularity-success" role="status">{message}</div>}

    <section className="regularity-grid">
      <article className="panel">
        <p className="eyebrow">Estado vigente</p><h2>Consultar Taller</h2>
        <div className="regularity-form">
          <Field label="Taller / organización"><select required value={organizationId} onChange={event => { setOrganizationId(event.target.value); setCurrent(null); setMessage(null) }}><option value="">Seleccione…</option>{organizations.map(item => <option key={item.id} value={item.id}>{organizationLabel(item)}</option>)}</select></Field>
          <Field label="Fecha de corte · Chile"><input type="date" required value={asOfDate} onChange={event => { setAsOfDate(event.target.value); setCurrent(null); setMessage(null) }} /></Field>
          <button className="regularity-secondary" type="button" disabled={working || !organizationId} onClick={() => { void readCurrent() }}>Consultar estado</button>
        </div>

        <div className="regularity-current">
          {current ? <>
            <span className={statusClass(current.status)}>{statusLabel(kind, current.status)}</span>
            <strong>{selectedOrganization ? organizationLabel(selectedOrganization) : 'Taller seleccionado'}</strong>
            <small>Vigente al {formatDateOnly(current.asOfDate)}</small>
            {current.sourceReference && <small>Referencia: {current.sourceReference}</small>}
            {current.recordedAtUtc && <small>Registrado: {formatChile(current.recordedAtUtc)}</small>}
          </> : <><strong>Sin estado cargado</strong><small>Consulte el Taller para revisar el último registro vigente a la fecha indicada.</small></>}
        </div>
      </article>

      <article className="panel">
        <p className="eyebrow">Nuevo registro</p><h2>Actualizar regularidad</h2>
        <form className="regularity-form" onSubmit={submit}>
          <Field label="Estado"><select value={status} onChange={event => setStatus(event.target.value)}>{config.statusOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></Field>
          <Field label="Fecha efectiva · Chile"><input type="date" required value={asOfDate} onChange={event => setAsOfDate(event.target.value)} /></Field>
          <Field label={config.sourceLabel}><input maxLength={500} value={sourceReference} onChange={event => setSourceReference(event.target.value)} placeholder="Documento, comprobante o referencia interna" /></Field>
          <Field label="Observaciones administrativas"><textarea rows={4} maxLength={2000} value={notes} onChange={event => setNotes(event.target.value)} /></Field>
          <button className="regularity-primary" type="submit" disabled={working || !organizationId}>Registrar estado</button>
        </form>
      </article>

      <article className="panel regularity-wide">
        <p className="eyebrow">Integración institucional</p><h2>Uso en autorización de ceremonias</h2>
        <p className="regularity-note">{config.ceremonyNote} La referencia y las observaciones quedan restringidas a la administración de esta área y no se proyectan al Portal de Insinuados.</p>
      </article>
    </section>
  </>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="regularity-field"><span>{label}</span>{children}</label> }
function organizationLabel(item: OrganizationOption) { return `${item.name}${item.number ? ` · Nº ${item.number}` : ''}` }
function statusLabel(kind: RegularityKind, status: string) { const match = configs[kind].statusOptions.find(([value]) => value === status); return match?.[1] ?? status }
function statusClass(status: string) { return status === 'up_to_date' || status === 'exempt' ? 'regularity-status good' : status === 'pending' ? 'regularity-status pending' : 'regularity-status blocked' }
function toMessage(reason: unknown) { return reason instanceof Error ? reason.message : 'No fue posible completar la operación.' }
function todayInChile() { const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()); const get = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''; return `${get('year')}-${get('month')}-${get('day')}` }
function formatDateOnly(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)) }
function formatChile(value: string) { return new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Santiago' }).format(new Date(value)) }
