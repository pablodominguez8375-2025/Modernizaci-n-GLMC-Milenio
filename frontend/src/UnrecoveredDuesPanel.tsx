import { useEffect, useState, type FormEvent } from 'react'
import type { LodgeTreasuryReport, PmgmApiClient, UnrecoveredDueCandidate } from './api/pmgmApi'
import { ActionDrawer } from './actionKit'
import { formatDateOnlyCl } from './displayFormat'

export default function UnrecoveredDuesPanel({api,organizationId,currency,report,canWrite,onSaved}:{api:PmgmApiClient;organizationId:string;currency:'CLP'|'USD';report:LodgeTreasuryReport;canWrite:boolean;onSaved:()=>Promise<void>}) {
  const [candidates,setCandidates]=useState<UnrecoveredDueCandidate[]>([])
  const [selected,setSelected]=useState('');const [evidence,setEvidence]=useState('');const [confirmed,setConfirmed]=useState(false)
  const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [success,setSuccess]=useState('')
  const money=new Intl.NumberFormat('es-CL',{style:'currency',currency,maximumFractionDigits:currency==='USD'?2:0})
  useEffect(()=>{let active=true;setCandidates([]);setSelected('');setEvidence('');setConfirmed(false);setError('');setSuccess('');
    if(canWrite)void api.getUnrecoveredDueCandidates(organizationId,currency).then(r=>{if(active)setCandidates(r.items)}).catch(e=>{if(active)setError(e instanceof Error?e.message:String(e))})
    return()=>{active=false}
  },[api,organizationId,currency,canWrite,report.unrecoveredDuesTotal])
  async function save(event:FormEvent){event.preventDefault();setBusy(true);setError('');setSuccess('');try{
    const recognitionDate=new Intl.DateTimeFormat('sv-SE',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())
    await api.recognizeUnrecoveredDues(organizationId,{withdrawalRequestId:selected,recognitionDate,currency,nonPaymentConfirmed:confirmed,evidenceReference:evidence})
    await onSaved();setSelected('');setEvidence('');setConfirmed(false);setSuccess('Pérdida registrada con respaldo. La caja y el control de cuotas se conservan.')
  }catch(e){setError(e instanceof Error?e.message:String(e))}finally{setBusy(false)}}
  return <article className="panel treasury-expenses">
    <div className="panel-heading"><div><p className="lodge-kicker">Control separado de caja</p><h3>Pérdida por cuotas no recuperadas</h3></div><strong>{money.format(report.unrecoveredDuesTotal??0)}</strong></div>
    <p>Deuda no recuperada por expulsión por no pago. No es un egreso y no disminuye el saldo de caja. El registro conserva los períodos originales y los abonos recibidos; no da por pagadas las cuotas.</p>
    {canWrite&&<ActionDrawer label="Registrar pérdida por no pago" title="Reconocer cuotas no recuperadas" description="Vincula la pérdida al retiro forzoso formal y su respaldo." confirmMessage="Se conservará el saldo impago como pérdida informativa, sin modificar la caja ni dar por pagadas las cuotas.">
      <form className="regularity-form" onSubmit={save}>
        <label>Retiro forzoso aprobado<select required value={selected} onChange={e=>setSelected(e.target.value)}><option value="">Selecciona un retiro</option>{candidates.map(x=><option key={x.withdrawalRequestId} value={x.withdrawalRequestId}>{x.memberDisplayName} · {money.format(x.outstandingAmount)}</option>)}</select></label>
        <label>Respaldo de expulsión por no pago<input required maxLength={500} value={evidence} onChange={e=>setEvidence(e.target.value)} placeholder="Referencia del documento o resolución"/></label>
        <label><input type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)} required/>Confirmo que el retiro formal corresponde a no pago y que estas cuotas no fueron recuperadas.</label>
        <p>Se registra con la fecha actual en un ejercicio abierto. Si existe saldo a favor recibido, debe revisarse su imputación primero.</p>
        <button type="submit" className="regularity-primary" disabled={busy||!selected||!confirmed||!evidence.trim()}>Registrar pérdida</button>
      </form>
    </ActionDrawer>}
    {error&&<p role="alert">{error}</p>}{success&&<p role="status">{success}</p>}
    {(report.unrecoveredDues?.length??0)>0?<div className="table-scroll"><table className="treasury-table"><thead><tr><th>Hermano / período</th><th>Reconocida</th><th>Cuota</th><th>Abonado</th><th>No recuperado</th><th>Respaldo / registro</th></tr></thead><tbody>{report.unrecoveredDues?.map(x=><tr key={x.id}><td>{x.memberDisplayName}<small>{x.periodYear}-{String(x.periodMonth).padStart(2,'0')}</small></td><td>{formatDateOnlyCl(x.recognitionDate)}</td><td>{money.format(x.chargedAmount)}</td><td>{money.format(x.paidAmount)}</td><td>{money.format(x.amount)}</td><td>{x.evidenceReference}<small>{x.recordedBySubject} · {x.recordedAtUtc}</small></td></tr>)}</tbody></table></div>:<p>No hay pérdidas registradas en el período y moneda consultados.</p>}
    <p>Los importes son evidencia histórica al reconocer la pérdida. Cualquier recuperación posterior se registra como dinero recibido en su fecha efectiva.</p>
  </article>
}
