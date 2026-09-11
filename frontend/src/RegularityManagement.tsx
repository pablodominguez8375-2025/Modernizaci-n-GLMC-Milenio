import { type FormEvent, useState } from 'react'
import type { OrganizationOption, PmgmApiClient, WorkshopRegularitySnapshot } from './api/pmgmApi'
import { regularityStatusClass, regularityStatusLabel, type RegularityKind } from './regularityDashboardModel'

export default function RegularityManagement({ api, kind, organizations }: { api: PmgmApiClient; kind: RegularityKind; organizations: OrganizationOption[] }) {
  const [organizationId, setOrganizationId] = useState(organizations[0]?.id ?? '')
  const [date, setDate] = useState(today())
  const [status, setStatus] = useState('up_to_date')
  const [reference, setReference] = useState('')
  const [notes, setNotes] = useState('')
  const [current, setCurrent] = useState<WorkshopRegularitySnapshot | null>(null)
  const [message, setMessage] = useState('')
  const options = kind === 'treasury' ? [['up_to_date','Al día'],['delinquent','Moroso'],['pending','Pendiente de revisión'],['exempt','Exento']] : [['up_to_date','Al día'],['overdue','Reposiciones pendientes'],['pending','Pendiente de revisión'],['exempt','Exento']]

  const read = async () => {
    if (!organizationId) return
    const value = kind === 'treasury' ? await api.getTreasuryWorkshopRegularity(organizationId, date) : await api.getHospitalariaWorkshopRegularity(organizationId, date)
    setCurrent(value)
    if (value) { setStatus(value.status); setReference(value.sourceReference ?? ''); setNotes(value.notes ?? '') }
    setMessage(value ? 'Estado institucional consultado.' : 'No existe estado vigente para esa fecha.')
  }
  const save = (event: FormEvent) => {
    event.preventDefault(); if (!organizationId) return
    const payload = { status, asOfDate: date, sourceReference: reference || null, notes: notes || null }
    const request = kind === 'treasury' ? api.setTreasuryWorkshopRegularity(organizationId, payload) : api.setHospitalariaWorkshopRegularity(organizationId, payload)
    void request.then(value => { setCurrent(value); setMessage('Regularidad registrada y auditada.') })
  }

  return <section className="regularity-grid">
    {message && <div className="regularity-success regularity-wide">{message}</div>}
    <article className="panel regularity-consult-panel"><p className="eyebrow">Estado vigente</p><h2>Consultar Taller</h2><div className="regularity-form">
      <Field label="Taller / organización"><select value={organizationId} onChange={e => setOrganizationId(e.target.value)}><option value="">Seleccione…</option>{organizations.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></Field>
      <Field label="Fecha de corte · Chile"><input type="date" value={date} onChange={e => setDate(e.target.value)} /></Field><button className="regularity-secondary" type="button" onClick={() => void read()}>Consultar estado</button></div>
      <div className="regularity-current">{current ? <><span className={regularityStatusClass(current.status)}>{regularityStatusLabel(kind,current.status)}</span><strong>{organizations.find(x => x.id === organizationId)?.name}</strong><small>Vigente al {current.asOfDate}</small></> : <><strong>Sin estado cargado</strong><small>Consulta el Taller para revisar su registro vigente.</small></>}</div>
    </article>
    <article className="panel regularity-update-panel"><p className="eyebrow">Nuevo registro</p><h2>Actualizar regularidad</h2><form className="regularity-form" onSubmit={save}>
      <Field label="Estado"><select value={status} onChange={e => setStatus(e.target.value)}>{options.map(([value,label]) => <option value={value} key={value}>{label}</option>)}</select></Field>
      <Field label="Fecha efectiva · Chile"><input type="date" value={date} onChange={e => setDate(e.target.value)} /></Field>
      <Field label="Referencia"><input value={reference} onChange={e => setReference(e.target.value)} /></Field><Field label="Observaciones"><textarea rows={4} value={notes} onChange={e => setNotes(e.target.value)} /></Field><button className="regularity-primary">Registrar estado</button></form>
    </article>
  </section>
}
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="regularity-field"><span>{label}</span>{children}</label> }
function today() { return new Intl.DateTimeFormat('en-CA',{ timeZone:'America/Santiago' }).format(new Date()) }
