import { useEffect, useMemo, useState } from 'react'
import {
  type GrandHospitalariaSubmission,
  type HospitalariaCouncilAidDecision,
  type HospitalariaCouncilFinancialReview,
  type HospitalariaMonthlySubmission,
  type HospitalariaMovementCategory,
  type LodgeHospitalariaSummary,
  type OrganizationOption,
  type PmgmApiClient,
} from './api/pmgmApi'
import './regularity.css'
import './treasuryStatement.css'

const money = new Intl.NumberFormat('es-CL',{style:'currency',currency:'CLP',maximumFractionDigits:0})

interface Props {
  api:PmgmApiClient
  canReadLocal:boolean
  canManageLocal:boolean
  canApproveExpenses:boolean
  canManageGrand:boolean
}

export default function HospitalariaPage({api,canReadLocal,canManageLocal,canApproveExpenses,canManageGrand}:Props){
  const [organizations,setOrganizations]=useState<OrganizationOption[]>([])
  const [organizationId,setOrganizationId]=useState('')
  const [period,setPeriod]=useState(currentPeriodInChile())
  const [summary,setSummary]=useState<LodgeHospitalariaSummary|null>(null)
  const [submission,setSubmission]=useState<HospitalariaMonthlySubmission|null>(null)
  const [grandItems,setGrandItems]=useState<GrandHospitalariaSubmission[]>([])
  const [aidDecisions,setAidDecisions]=useState<HospitalariaCouncilAidDecision[]>([])
  const [councilReviews,setCouncilReviews]=useState<HospitalariaCouncilFinancialReview[]>([])
  const [busy,setBusy]=useState(false)
  const [error,setError]=useState<string|null>(null)
  const [message,setMessage]=useState<string|null>(null)

  const [movementType,setMovementType]=useState<'income'|'expense'>('income')
  const [category,setCategory]=useState<HospitalariaMovementCategory>('charity_bag')
  const [amount,setAmount]=useState(0)
  const [movementDate,setMovementDate]=useState(todayInChile())
  const [memberReference,setMemberReference]=useState('')
  const [destination,setDestination]=useState('')
  const [evidenceReference,setEvidenceReference]=useState('')
  const [observation,setObservation]=useState('')

  const [replenishmentDue,setReplenishmentDue]=useState(0)
  const [replenishmentPaid,setReplenishmentPaid]=useState(0)
  const [paymentReference,setPaymentReference]=useState('')
  const [councilReviewId,setCouncilReviewId]=useState('')
  const [reviewNotes,setReviewNotes]=useState('')

  const [year,month]=period.split('-').map(Number)
  const start=`${period}-01`
  const end=monthEnd(year,month)

  useEffect(()=>{
    let active=true
    api.getOrganizationOptions().then(result=>{
      if(!active)return
      const items=result.items.filter(x=>x.type!=='order')
      setOrganizations(items);setOrganizationId(current=>current||items[0]?.id||'')
    }).catch(reason=>{if(active)setError(toMessage(reason))})
    return()=>{active=false}
  },[api])

  const refreshLocal=async()=>{
    if(!organizationId||!canReadLocal)return
    const [s,subs,decisions,reviews]=await Promise.all([
      api.getLodgeHospitalariaSummary(organizationId,start,end),
      api.getHospitalariaMonthlySubmissions(organizationId,year,month),
      canManageLocal?api.getHospitalariaCouncilAidDecisions(organizationId):Promise.resolve({total:0,items:[]}),
      canManageLocal?api.getHospitalariaCouncilFinancialReviews(organizationId):Promise.resolve({total:0,items:[]}),
    ])
    setSummary(s);setSubmission(subs.items[0]??null);setAidDecisions(decisions.items);setCouncilReviews(reviews.items)
    const existing=subs.items[0]
    if(existing){setReplenishmentDue(existing.replenishmentDueAmount);setReplenishmentPaid(existing.replenishmentPaidAmount);setPaymentReference(existing.paymentReference??'');setCouncilReviewId(existing.councilFinancialReviewId??'')}
  }

  const refreshGrand=async()=>{
    if(!canManageGrand)return
    const result=await api.getGrandHospitalariaSubmissions({organizationId:organizationId||undefined,year,month})
    setGrandItems(result.items)
  }

  useEffect(()=>{
    if(!organizationId)return
    let active=true
    setError(null)
    Promise.all([canReadLocal?refreshLocal():Promise.resolve(),canManageGrand?refreshGrand():Promise.resolve()])
      .catch(reason=>{if(active)setError(toMessage(reason))})
    return()=>{active=false}
  },[organizationId,period,canReadLocal,canManageGrand])

  const execute=async(operation:()=>Promise<unknown>,success:string)=>{
    setBusy(true);setError(null);setMessage(null)
    try{await operation();await Promise.all([canReadLocal?refreshLocal():Promise.resolve(),canManageGrand?refreshGrand():Promise.resolve()]);setMessage(success)}
    catch(reason){setError(toMessage(reason))}
    finally{setBusy(false)}
  }

  const createMovement=()=>execute(async()=>{
    await api.createLodgeHospitalariaMovement(organizationId,{
      movementType,category,amount,movementDate,
      memberReference:memberReference.trim()||null,
      destination:destination.trim()||null,
      evidenceReference:evidenceReference.trim()||null,
      observation:observation.trim()||null,
    })
    setAmount(0);setMemberReference('');setDestination('');setEvidenceReference('');setObservation('')
  },movementType==='income'?'Aporte ingresado al Tronco de Beneficencia.':'Egreso registrado y enviado a autorización.')

  const saveSubmission=()=>execute(async()=>{
    const saved=await api.upsertHospitalariaMonthlySubmission(organizationId,year,month,{
      replenishmentDueAmount:replenishmentDue,
      replenishmentPaidAmount:replenishmentPaid,
      paymentReference:paymentReference.trim()||null,
      councilFinancialReviewId:councilReviewId||null,
      sourceReference:`Estado mensual Hospitalaria ${period}`,
    })
    setSubmission(saved)
  },'Rendición mensual guardada con cifras agregadas.')

  const submit=()=>submission&&execute(()=>api.submitHospitalariaMonthlySubmission(submission.id),'Rendición agregada enviada a Gran Hospitalaria.')

  const pendingExpenses=useMemo(()=>summary?.items.filter(x=>x.movementType==='expense'&&x.approvalStatus==='pending_approval')??[],[summary])

  if(canManageGrand&&!canReadLocal){
    return <>
      <section className="page-heading"><div><p className="eyebrow">Gran Hospitalaria · regularidad institucional</p><h1>Rendiciones de Hospitalaria</h1><p>La bandeja recibe sólo cifras agregadas, reposiciones y referencias institucionales. No expone beneficiarios, destinos ni observaciones privadas del Taller.</p></div><span className="count-badge">{grandItems.length} rendiciones</span></section>
      {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
      <section className="panel treasury-toolbar"><label className="regularity-field"><span>Taller</span><select value={organizationId} onChange={e=>setOrganizationId(e.target.value)}><option value="">Todos</option>{organizations.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label className="regularity-field"><span>Período</span><input type="month" value={period} onChange={e=>setPeriod(e.target.value)}/></label></section>
      <section className="panel">
        {grandItems.length===0?<div className="empty-state">No existen rendiciones enviadas para el filtro seleccionado.</div>:grandItems.map(item=><article className="hospitalaria-submission-card" key={item.id}>
          <div><p className="eyebrow">{item.organizationName}{item.organizationNumber?` · Nº ${item.organizationNumber}`:''}</p><h2>{String(item.periodMonth).padStart(2,'0')}/{item.periodYear}</h2><p>Ingresos {money.format(item.incomeAmount)} · Egresos aprobados {money.format(item.approvedExpenseAmount)} · Reposición pendiente {money.format(item.differenceAmount)}</p></div>
          <div><strong>{statusLabel(item.status)}</strong><small>Movimientos agregados: {item.movementCount} · Egresos pendientes: {item.pendingExpenseCount}</small><small>Revisión Consejo: {item.councilFinancialReviewId?'✅':'❌'} · Comprobante reposición: {item.paymentReference?'✅':'—'}</small></div>
          {item.status==='submitted'&&<div className="hospitalaria-review-actions"><input aria-label="Observación de Gran Hospitalaria" value={reviewNotes} onChange={e=>setReviewNotes(e.target.value)} placeholder="Observación institucional"/><button type="button" className="regularity-secondary" disabled={busy||!reviewNotes.trim()} onClick={()=>void execute(()=>api.reviewGrandHospitalariaSubmission(item.id,'observed',reviewNotes),'Rendición observada por Gran Hospitalaria.')}>Observar</button><button type="button" className="regularity-primary" disabled={busy||item.differenceAmount>0} onClick={()=>void execute(()=>api.reviewGrandHospitalariaSubmission(item.id,'reconciled',reviewNotes||null),'Rendición conciliada; regularidad institucional actualizada.')}>Conciliar</button></div>}
        </article>)}
      </section>
    </>
  }

  return <>
    <section className="page-heading"><div><p className="eyebrow">{canManageLocal?'Hospitalaria del Taller':'Venerable Maestro · inspección Hospitalaria'}</p><h1>Tronco de Beneficencia y estado mensual</h1><p>Fondo independiente de Tesorería. Los antecedentes personales de socorros permanecen restringidos al Taller; Gran Hospitalaria recibe sólo la rendición agregada.</p></div><span className="count-badge">{period}</span></section>
    {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
    <section className="panel treasury-toolbar"><label className="regularity-field"><span>Taller</span><select value={organizationId} disabled={busy} onChange={e=>setOrganizationId(e.target.value)}>{organizations.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label className="regularity-field"><span>Período</span><input type="month" value={period} disabled={busy} onChange={e=>setPeriod(e.target.value)}/></label></section>

    <section className="treasury-kpis">
      <article><span>Ingresos Tronco</span><strong>{money.format(summary?.income??0)}</strong></article>
      <article><span>Socorros/egresos aprobados</span><strong>{money.format(summary?.approvedExpenses??0)}</strong></article>
      <article><span>Saldo período</span><strong>{money.format(summary?.periodNet??0)}</strong></article>
      <article className={(summary?.pendingExpenses??0)>0?'difference':'balanced'}><span>Egresos pendientes</span><strong>{summary?.pendingExpenses??0}</strong></article>
    </section>

    {canManageLocal&&<section className="panel">
      <p className="eyebrow">Registro reservado del Taller</p><h2>Movimiento del Tronco de Beneficencia</h2>
      <div className="treasury-toolbar">
        <label className="regularity-field"><span>Tipo</span><select value={movementType} onChange={e=>{const next=e.target.value as 'income'|'expense';setMovementType(next);setCategory(next==='income'?'charity_bag':'charity_aid')}}><option value="income">Ingreso / aporte</option><option value="expense">Socorro / egreso</option></select></label>
        <label className="regularity-field"><span>Categoría</span><select value={category} onChange={e=>setCategory(e.target.value as HospitalariaMovementCategory)}>{movementType==='income'?<><option value="charity_bag">Tronco de Beneficencia</option><option value="voluntary_contribution">Aporte voluntario</option><option value="death_replenishment">Reposición por fallecimiento</option><option value="annual_replenishment_fund">Fondo anual/reposición</option><option value="initiation_fee">Aporte de iniciación</option></>:<><option value="charity_aid">Socorro / ayuda</option><option value="supplies">Insumos de beneficencia</option><option value="ceremony">Obra/actividad de beneficencia</option></>}</select></label>
        <label className="regularity-field"><span>Monto</span><input type="number" min="1" value={amount||''} onChange={e=>setAmount(Number(e.target.value))}/></label>
        <label className="regularity-field"><span>Fecha</span><input type="date" value={movementDate} onChange={e=>setMovementDate(e.target.value)}/></label>
        {movementType==='expense'&&<><label className="regularity-field"><span>Referencia reservada</span><input value={memberReference} onChange={e=>setMemberReference(e.target.value)} placeholder="ID interno o referencia; evitar diagnóstico"/></label><label className="regularity-field"><span>Destino resumido</span><input value={destination} onChange={e=>setDestination(e.target.value)} placeholder="Socorro / obra"/></label></>}
        <label className="regularity-field"><span>Respaldo</span><input value={evidenceReference} onChange={e=>setEvidenceReference(e.target.value)} placeholder="Acta, comprobante o referencia"/></label>
        <label className="regularity-field"><span>Observación reservada</span><input value={observation} onChange={e=>setObservation(e.target.value)}/></label>
        <button type="button" className="regularity-primary" disabled={busy||amount<=0||(movementType==='expense'&&!evidenceReference.trim())} onClick={()=>void createMovement()}>Registrar movimiento</button>
      </div>
    </section>}

    <section className="panel">
      <p className="eyebrow">Socorros por autorizar</p><h2>Venerable Maestro o Consejo de Administración</h2>
      {pendingExpenses.length===0?<p>No hay egresos pendientes.</p>:pendingExpenses.map(item=>{
        const matching=aidDecisions.filter(x=>x.amount===item.amount)
        return <div className="payment-row" key={item.id}><span>{formatDate(item.movementDate)} · {categoryLabel(item.category)}</span><strong>{money.format(item.amount)}</strong><small>{item.evidenceReference??'Sin respaldo'}</small>
          {canApproveExpenses&&<button type="button" className="regularity-primary" disabled={busy} onClick={()=>void execute(()=>api.approveLodgeHospitalariaExpense(item.id),'Socorro autorizado por el Venerable Maestro.')}>Aprobar como Venerable</button>}
          {canManageLocal&&item.category==='charity_aid'&&<select aria-label="Acuerdo del Consejo" defaultValue="" onChange={e=>{if(e.target.value)void execute(()=>api.approveLodgeHospitalariaExpenseByCouncil(item.id,e.target.value),'Socorro vinculado a acuerdo aprobado del Consejo.')}}><option value="">Vincular acuerdo del Consejo…</option>{matching.map(decision=><option key={decision.id} value={decision.id}>{formatDate(decision.sessionDate)} · {money.format(decision.amount??0)}</option>)}</select>}
        </div>
      })}
    </section>

    {canManageLocal&&<section className="panel">
      <p className="eyebrow">Estado mensual al Consejo / Gran Hospitalaria</p><h2>Rendición agregada {period}</h2>
      <p>El sistema recalcula ingresos y egresos aprobados. La rendición no contiene beneficiarios ni observaciones privadas.</p>
      <div className="treasury-toolbar">
        <label className="regularity-field"><span>Reposición/obligación del período</span><input type="number" min="0" value={replenishmentDue} onChange={e=>setReplenishmentDue(Number(e.target.value))}/></label>
        <label className="regularity-field"><span>Reposición pagada</span><input type="number" min="0" value={replenishmentPaid} onChange={e=>setReplenishmentPaid(Number(e.target.value))}/></label>
        <label className="regularity-field"><span>Referencia de pago</span><input value={paymentReference} onChange={e=>setPaymentReference(e.target.value)} placeholder="Comprobante / transferencia"/></label>
        <label className="regularity-field"><span>Revisión del Consejo</span><select value={councilReviewId} onChange={e=>setCouncilReviewId(e.target.value)}><option value="">Seleccione revisión mensual…</option>{councilReviews.map(x=><option key={x.id} value={x.id}>{formatDate(x.sessionDate)} · {x.periodLabel}</option>)}</select></label>
        <button type="button" className="regularity-secondary" disabled={busy||replenishmentPaid>0&&!paymentReference.trim()} onClick={()=>void saveSubmission()}>Guardar rendición</button>
        <button type="button" className="regularity-primary" disabled={busy||!submission||submission.status!=='draft'||submission.pendingExpenseCount>0||!submission.councilFinancialReviewId} onClick={()=>void submit()}>Enviar a Gran Hospitalaria</button>
      </div>
      {submission&&<div className="payment-row"><span>Estado: {statusLabel(submission.status)}</span><strong>Diferencia reposición: {money.format(submission.differenceAmount)}</strong><small>Ingresos {money.format(submission.incomeAmount)} · Egresos aprobados {money.format(submission.approvedExpenseAmount)} · {submission.movementCount} movimientos agregados</small></div>}
    </section>}

    <section className="panel">
      <p className="eyebrow">Libro reservado Hospitalaria</p><h2>Movimientos del período</h2>
      {summary?.items.length?<div className="table-scroll"><table className="treasury-table"><thead><tr><th>Fecha</th><th>Tipo</th><th>Categoría</th><th>Monto</th><th>Autorización</th><th>Respaldo</th></tr></thead><tbody>{summary.items.map(item=><tr key={item.id}><td>{formatDate(item.movementDate)}</td><td>{item.movementType==='income'?'Ingreso':'Egreso'}</td><td>{categoryLabel(item.category)}</td><td>{money.format(item.amount)}</td><td>{approvalLabel(item)}</td><td>{item.evidenceReference??'—'}</td></tr>)}</tbody></table></div>:<p>No existen movimientos para el período.</p>}
    </section>
  </>
}

function statusLabel(value:string){return value==='draft'?'Borrador':value==='submitted'?'Enviada':value==='observed'?'Observada':value==='reconciled'?'Conciliada':value}
function categoryLabel(value:string){return value==='charity_bag'?'Tronco de Beneficencia':value==='voluntary_contribution'?'Aporte voluntario':value==='death_replenishment'?'Reposición por fallecimiento':value==='annual_replenishment_fund'?'Fondo/reposición anual':value==='initiation_fee'?'Aporte iniciación':value==='charity_aid'?'Socorro/ayuda':value==='supplies'?'Insumos':'Obra/actividad'}
function approvalLabel(item:{approvalStatus:string;approvalSource:string|null}){if(item.approvalStatus==='not_required')return'No requerida';if(item.approvalStatus==='pending_approval')return'Pendiente';return item.approvalSource==='lodge_council'?'Consejo':'Venerable'}
function todayInChile(){return new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())}
function currentPeriodInChile(){return todayInChile().slice(0,7)}
function monthEnd(year:number,month:number){return `${year}-${String(month).padStart(2,'0')}-${String(new Date(Date.UTC(year,month,0)).getUTCDate()).padStart(2,'0')}`}
function formatDate(value:string){const [y,m,d]=value.split('-').map(Number);return y&&m&&d?new Intl.DateTimeFormat('es-CL',{dateStyle:'medium',timeZone:'America/Santiago'}).format(new Date(Date.UTC(y,m-1,d,12))):value}
function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible completar la operación de Hospitalaria.'}
