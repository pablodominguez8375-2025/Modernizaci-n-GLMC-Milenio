import { type FormEvent, useEffect, useState } from 'react'
import { type LodgeApiClient, type LodgeMemberOption, type LodgeWithdrawal, type LodgeWithdrawalSignatureRole, type LodgeWithdrawalType } from './api/lodgeApi'

export default function LodgeWithdrawalsPanel({ lodgeApi, organizationId, members, signatureRole }: { lodgeApi: LodgeApiClient; organizationId: string; members: LodgeMemberOption[]; signatureRole?: LodgeWithdrawalSignatureRole }) {
  const [items, setItems] = useState<LodgeWithdrawal[]>([])
  const [memberId, setMemberId] = useState('')
  const [type, setType] = useState<LodgeWithdrawalType>('voluntary')
  const [date, setDate] = useState(today())
  const [reason, setReason] = useState('')
  const [evidence, setEvidence] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { if (!memberId && members.length) setMemberId(members[0].id) }, [memberId, members])
  useEffect(() => {
    if (!organizationId) { setItems([]); return }
    void lodgeApi.getWithdrawals(organizationId).then(result => setItems(result.items)).catch(cause => setError(toMessage(cause)))
  }, [lodgeApi, organizationId])

  const submit = (event: FormEvent) => {
    event.preventDefault(); if (!memberId || !organizationId) return
    setBusy(true); setError(null); setMessage(null)
    void lodgeApi.createWithdrawal({ memberId, organizationId, withdrawalType: type, requestedEffectiveDate: date, reason, evidenceReference: evidence })
      .then(async () => { const result = await lodgeApi.getWithdrawals(organizationId); setItems(result.items); setReason(''); setEvidence(''); setMessage('Solicitud registrada y enviada para resolución institucional.') })
      .catch(cause => setError(toMessage(cause)))
      .finally(() => setBusy(false))
  }

  const sign = (item: LodgeWithdrawal) => {
    if (!signatureRole) return
    setBusy(true); setError(null); setMessage(null)
    void lodgeApi.signWithdrawal(item.id, signatureRole)
      .then(async result => {
        const refreshed = await lodgeApi.getWithdrawals(organizationId)
        setItems(refreshed.items)
        setMessage(result.executedAtUtc
          ? 'Carta completada con las cuatro firmas. El retiro quedó materializado y registrado en el historial.'
          : `Firma de ${signatureLabel(signatureRole)} registrada. La carta continúa pendiente de los demás cargos.`)
      })
      .catch(cause => setError(toMessage(cause)))
      .finally(() => setBusy(false))
  }

  return <section className="lodge-withdrawals">
    <div className="lodge-instruction-heading"><div><p className="lodge-kicker">Gestión Logial › Secretaría</p><h2>Cartas de retiro</h2><p>El retiro voluntario deja al hermano en sueño. El retiro forzoso requiere causal documentada e inhabilita transversalmente cuando sea aprobado.</p></div><span className="lodge-live-chip">Historial protegido</span></div>
    {message && <div className="regularity-success" role="status">{message}</div>}{error && <div className="regularity-error" role="alert">{error}</div>}
    <div className="lodge-withdrawal-grid">
      <form className="regularity-form lodge-withdrawal-form" onSubmit={submit}>
        <label className="regularity-field"><span>Hermano</span><select value={memberId} onChange={event => setMemberId(event.target.value)} required>{members.map(member => <option key={member.id} value={member.id}>{member.displayName}</option>)}</select></label>
        <label className="regularity-field"><span>Tipo de carta</span><select value={type} onChange={event => setType(event.target.value as LodgeWithdrawalType)}><option value="voluntary">Retiro voluntario · sueño</option><option value="forced">Retiro forzoso · inhabilitación</option></select></label>
        <label className="regularity-field"><span>Fecha efectiva propuesta</span><input type="date" value={date} onChange={event => setDate(event.target.value)} required /></label>
        <label className="regularity-field"><span>Referencia documental</span><input value={evidence} onChange={event => setEvidence(event.target.value)} maxLength={500} placeholder="Ej.: CARTA-RET-2026-001" required /></label>
        <label className="regularity-field"><span>Causal o fundamento</span><textarea value={reason} onChange={event => setReason(event.target.value)} minLength={10} maxLength={1000} rows={4} required /></label>
        <button className="regularity-primary" type="submit" disabled={busy || !memberId}>{busy ? 'Registrando…' : 'Enviar solicitud'}</button>
      </form>
      <div className="lodge-withdrawal-list"><h3>Trámites del Taller</h3>{items.length === 0 ? <p className="muted">No hay retiros registrados.</p> : items.map(item => <article key={item.id}>
        <strong>{item.withdrawalType === 'voluntary' ? 'Retiro voluntario' : 'Retiro forzoso'}</strong>
        <span>Fecha efectiva: {item.requestedEffectiveDate}</span>
        <small>{item.evidenceReference}</small>
        <em className={`regularity-status ${item.executedAtUtc ? 'good' : item.status === 'rejected' ? 'blocked' : 'pending'}`}>{item.executedAtUtc ? 'Completado y materializado' : item.status === 'pending' ? 'Pendiente de resolución' : item.status === 'approved' ? 'Aprobado · firmas pendientes' : 'Rechazado'}</em>
        <div className="withdrawal-signatures" aria-label="Firmas de la carta de retiro">
          {(Object.keys(item.signatures) as LodgeWithdrawalSignatureRole[]).map(role => <span key={role} className={item.signatures[role].signed ? 'signed' : ''}>{item.signatures[role].signed ? 'Firmado' : 'Pendiente'} · {signatureLabel(role)}</span>)}
        </div>
        {signatureRole && item.status === 'approved' && !item.executedAtUtc && !item.signatures[signatureRole].signed && <button type="button" className="regularity-secondary" disabled={busy} onClick={() => sign(item)}>Firmar como {signatureLabel(signatureRole)}</button>}
      </article>)}</div>
    </div>
  </section>
}

function today() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()) }
function toMessage(value: unknown) { return value instanceof Error ? value.message : 'No fue posible completar la operación.' }
function signatureLabel(role: LodgeWithdrawalSignatureRole) { return role === 'venerable' ? 'Venerable Maestro' : role === 'treasurer' ? 'Tesorero/a' : role === 'orator' ? 'Orador/a' : 'Secretario/a' }
