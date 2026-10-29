import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ActionDrawer } from './actionKit'
import { officeLabel } from './officeLabels'
import { historyMilestones, statusLabel, type HistoryItem, type HistoryProposal, type MemberHistorySnapshot } from './api/memberHistory'
import type { MemberOffice, MemberProfile, MembershipApiClient } from './api/membershipApi'
import './memberHistory.css'

interface DraftOffice { key: number; officeType: string; period: string; startDate: string; endDate: string }

const formatDate = (value: string) => new Intl.DateTimeFormat('es-CL', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`))
const toMessage = (reason: unknown) => reason instanceof Error ? reason.message : 'No fue posible completar la operación.'

function describe(item: HistoryItem, options: { code: string; name: string }[]) {
  if (item.kind === 'degree') {
    const milestone = historyMilestones.find(x => x.type === item.eventType)
    return `${milestone?.label ?? item.eventType}: ${item.eventDate ? formatDate(item.eventDate) : '—'}`
  }
  const name = options.find(x => x.code === item.officeType)?.name ?? officeLabel(item.officeType ?? '')
  return `${item.action === 'replace' ? 'Corrección de cargo' : 'Cargo agregado'}: ${name} · ${item.period ?? ''}`
}

/** Secretaría del Taller propone agregar o corregir cargos y fechas masónicas; Régimen Interior aprueba. */
export default function MemberHistoryPanel({ profile, organizationId, membershipApi, onApproved }: { profile: MemberProfile; organizationId: string; membershipApi: MembershipApiClient; onApproved?: () => void }) {
  const memberId = profile.member.id
  const [snapshot, setSnapshot] = useState<MemberHistorySnapshot | null>(null)
  const [message, setMessage] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  const [dates, setDates] = useState<Record<string, string>>({})
  const [offices, setOffices] = useState<Record<string, DraftOffice>>({})
  const [added, setAdded] = useState<DraftOffice[]>([])
  const [evidence, setEvidence] = useState('')
  const [reason, setReason] = useState('')

  const load = useCallback(() => {
    membershipApi.getMemberHistory(organizationId, memberId).then(setSnapshot).catch(r => setMessage({ tone: 'error', text: toMessage(r) }))
  }, [membershipApi, organizationId, memberId])
  useEffect(() => { setSnapshot(null); load() }, [load])

  const options = snapshot?.officeOptions ?? []
  const pending = snapshot?.proposals.find(x => x.status === 'submitted')
  const dateOf = (type: string) => profile.degreeEvents.find(x => x.eventType === type)?.effectiveDate ?? ''
  const editable = profile.offices.filter((x: MemberOffice) => x.organizationId === organizationId)

  function reset() {
    setDates(Object.fromEntries(historyMilestones.map(x => [x.type, dateOf(x.type)])))
    setOffices(Object.fromEntries(editable.map(x => [x.id, { key: 0, officeType: x.officeType, period: x.period, startDate: x.startDate ?? '', endDate: x.endDate ?? '' }])))
    setAdded([]); setEvidence(''); setReason('')
  }
  useEffect(reset, [profile.member.id, profile.degreeEvents, profile.offices]) // eslint-disable-line react-hooks/exhaustive-deps

  function buildItems(): HistoryItem[] {
    const items: HistoryItem[] = []
    for (const m of historyMilestones) if (dates[m.type] && dates[m.type] !== dateOf(m.type)) items.push({ kind: 'degree', action: 'add', eventType: m.type, eventDate: dates[m.type] })
    for (const o of editable) {
      const d = offices[o.id]; if (!d) continue
      if (d.officeType !== o.officeType || d.period !== o.period || d.startDate !== (o.startDate ?? '') || d.endDate !== (o.endDate ?? '')) {
        items.push({ kind: 'office', action: 'replace', targetRecordId: o.id, officeType: d.officeType, period: d.period, startDate: d.startDate || null, endDate: d.endDate || null })
      }
    }
    for (const a of added) if (a.officeType && a.period) items.push({ kind: 'office', action: 'add', officeType: a.officeType, period: a.period, startDate: a.startDate || null, endDate: a.endDate || null })
    return items
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const items = buildItems()
    if (!items.length) { setMessage({ tone: 'error', text: 'No hay cambios que enviar. Modifique una fecha o un cargo, o agregue un cargo.' }); return }
    setBusy(true); setMessage(null)
    try {
      await membershipApi.proposeMemberHistory(organizationId, memberId, { evidenceReference: evidence, reason, items })
      setMessage({ tone: 'ok', text: 'Propuesta enviada a Régimen Interior. La ficha se actualizará cuando sea aprobada.' })
      setOpen(false); load(); onApproved?.()
    } catch (r) { setMessage({ tone: 'error', text: toMessage(r) }) } finally { setBusy(false) }
  }

  const patchOffice = (id: string, patch: Partial<DraftOffice>) => setOffices(c => ({ ...c, [id]: { ...c[id], ...patch } }))
  const patchAdded = (key: number, patch: Partial<DraftOffice>) => setAdded(c => c.map(x => x.key === key ? { ...x, ...patch } : x))
  const officeSelect = (value: string, onChange: (v: string) => void, label: string) => <select aria-label={label} value={value} onChange={e => onChange(e.target.value)}>
    {!options.some(x => x.code === value) && <option value={value}>{officeLabel(value)}</option>}
    {options.map(x => <option key={x.code} value={x.code}>{x.name}</option>)}
  </select>

  return <section className="member-history" aria-label="Historia del hermano">
    <div className="profile-block-heading"><div><p className="eyebrow">Secretaría del Taller</p><h3>Historia del hermano</h3></div>
      <ActionDrawer label="Completar o corregir historia" title="Completar o corregir la historia" description="Los cambios los aprueba Régimen Interior antes de reflejarse en la ficha." open={open} onOpenChange={v => { setOpen(v); if (v) reset() }} wide disabled={!!pending || !snapshot}>
        <form className="member-history-form" onSubmit={submit}>
          <fieldset><legend>Fechas masónicas</legend>
            <div className="member-history-dates">{historyMilestones.map(m => <label key={m.type}><span>{m.label}</span><input type="date" value={dates[m.type] ?? ''} onChange={e => setDates(c => ({ ...c, [m.type]: e.target.value }))} /></label>)}</div>
          </fieldset>
          <fieldset><legend>Cargos en este Taller</legend>
            {(editable.length > 0 || added.length > 0) && <div className="member-history-row member-history-head" aria-hidden="true"><span>Cargo</span><span>Período</span><span>Desde</span><span>Hasta</span></div>}
            {editable.length === 0 && <p className="form-note">Aún no hay cargos registrados en este Taller.</p>}
            {editable.map(o => { const d = offices[o.id]; if (!d) return null; return <div key={o.id} className="member-history-row">
              {officeSelect(d.officeType, v => patchOffice(o.id, { officeType: v }), 'Cargo')}
              <input aria-label="Período" value={d.period} maxLength={40} onChange={e => patchOffice(o.id, { period: e.target.value })} />
              <input aria-label="Desde" type="date" value={d.startDate} onChange={e => patchOffice(o.id, { startDate: e.target.value })} />
              <input aria-label="Hasta" type="date" value={d.endDate} onChange={e => patchOffice(o.id, { endDate: e.target.value })} />
            </div> })}
            {added.map(a => <div key={a.key} className="member-history-row is-new">
              {officeSelect(a.officeType, v => patchAdded(a.key, { officeType: v }), 'Cargo nuevo')}
              <input aria-label="Período del cargo nuevo" placeholder="Ej.: 2018" value={a.period} maxLength={40} onChange={e => patchAdded(a.key, { period: e.target.value })} />
              <input aria-label="Desde" type="date" value={a.startDate} onChange={e => patchAdded(a.key, { startDate: e.target.value })} />
              <input aria-label="Hasta" type="date" value={a.endDate} onChange={e => patchAdded(a.key, { endDate: e.target.value })} />
              <button type="button" className="action-link" onClick={() => setAdded(c => c.filter(x => x.key !== a.key))}>Quitar</button>
            </div>)}
            <button type="button" className="action-trigger secondary" onClick={() => setAdded(c => [...c, { key: Date.now() + c.length, officeType: options[0]?.code ?? 'secretary', period: '', startDate: '', endDate: '' }])}>+ Agregar cargo</button>
          </fieldset>
          <label><span>Fuente o respaldo</span><input required maxLength={200} value={evidence} onChange={e => setEvidence(e.target.value)} placeholder="Acta, plancha o certificado" /></label>
          <label><span>Motivo</span><textarea required minLength={10} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Por qué se completa o corrige" /></label>
          {message?.tone === 'error' && <p role="alert" className="member-history-error">{message.text}</p>}
          <button type="submit" className="action-trigger primary" disabled={busy}>Enviar a Régimen Interior</button>
        </form>
      </ActionDrawer>
    </div>
    {message?.tone === 'ok' && <p role="status" className="member-history-ok">{message.text}</p>}
    {message?.tone === 'error' && !open && <p role="alert" className="member-history-error">{message.text}</p>}
    {pending && <p className="form-note">Hay una propuesta pendiente en Régimen Interior; no se pueden enviar otros cambios hasta que se resuelva.</p>}
    <div className="member-history-list">
      {snapshot?.proposals.map((p: HistoryProposal) => <article key={p.id} className={`member-history-card is-${p.status}`}>
        <div><strong>{statusLabel(p.status)}</strong><small>{formatDate(p.createdAtUtc.slice(0, 10))} · {p.evidenceReference}</small></div>
        <ul>{p.items.map((item, i) => <li key={i}>{describe(item, options)}</li>)}</ul>
        {p.reviewNotes && <small>Régimen Interior: {p.reviewNotes}</small>}
      </article>)}
      {snapshot && snapshot.proposals.length === 0 && <p className="form-note">Sin propuestas de cambio. Use el botón para completar fechas o cargos históricos.</p>}
    </div>
  </section>
}
