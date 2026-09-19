import { type FormEvent, type ReactNode, useEffect, useMemo, useState } from 'react'
import type { AdmissionApiClient, AdmissionCaseListItem, AdmissionCaseDetail, AdmissionEligibilityResponse, AdmissionPersonOption, AdmissionType, AffiliationMode, AffiliationProcedure } from './api/admissionApi'
import type { LodgeApiClient, LodgeMeeting, LodgeMemberOption } from './api/lodgeApi'
import type { OrganizationOption, PmgmApiClient } from './api/pmgmApi'
import './admissions.css'

export default function AdmissionsPage({
  api,
  pmgmApi,
  lodgeApi,
  canManageSecretariat,
  canAppointCommission,
  canValidateInternalAffairs,
}: {
  api: AdmissionApiClient
  pmgmApi: PmgmApiClient
  lodgeApi: LodgeApiClient
  canManageSecretariat: boolean
  canAppointCommission: boolean
  canValidateInternalAffairs: boolean
}) {
  const [organizations,setOrganizations]=useState<OrganizationOption[]>([])
  const [organizationId,setOrganizationId]=useState('')
  const [rows,setRows]=useState<AdmissionCaseListItem[]>([])
  const [selectedId,setSelectedId]=useState('')
  const [detail,setDetail]=useState<AdmissionCaseDetail|null>(null)
  const [eligibility,setEligibility]=useState<AdmissionEligibilityResponse|null>(null)
  const [members,setMembers]=useState<LodgeMemberOption[]>([])
  const [meetings,setMeetings]=useState<LodgeMeeting[]>([])
  const [loading,setLoading]=useState(true)
  const [working,setWorking]=useState(false)
  const [error,setError]=useState<string|null>(null)
  const [notice,setNotice]=useState<string|null>(null)

  useEffect(()=>{
    let active=true
    pmgmApi.getOrganizationOptions()
      .then(result=>{
        if(!active)return
        const workshops=result.items.filter(x=>x.type.toLowerCase()!=='order')
        setOrganizations(workshops)
        setOrganizationId(current=>current||workshops[0]?.id||'')
      })
      .catch(reason=>{if(active)setError(toMessage(reason))})
      .finally(()=>{if(active)setLoading(false)})
    return()=>{active=false}
  },[pmgmApi])

  useEffect(()=>{
    if(!organizationId){setRows([]);setSelectedId('');return}
    let active=true
    setLoading(true);setError(null)
    api.listCases(organizationId)
      .then(result=>{
        if(!active)return
        setRows(result.items)
        setSelectedId(current=>result.items.some(x=>x.admissionCase.id===current)?current:result.items[0]?.admissionCase.id??'')
      })
      .catch(reason=>{if(active)setError(toMessage(reason))})
      .finally(()=>{if(active)setLoading(false)})
    return()=>{active=false}
  },[api,organizationId])

  const refreshSelected=async(caseId=selectedId)=>{
    if(!caseId){setDetail(null);setEligibility(null);return}
    setError(null)
    const [caseDetail,caseEligibility]=await Promise.all([api.getCase(caseId),api.getEligibility(caseId)])
    setDetail(caseDetail);setEligibility(caseEligibility)
  }

  useEffect(()=>{
    if(!selectedId){setDetail(null);setEligibility(null);return}
    let active=true
    Promise.all([api.getCase(selectedId),api.getEligibility(selectedId)])
      .then(([caseDetail,caseEligibility])=>{if(active){setDetail(caseDetail);setEligibility(caseEligibility)}})
      .catch(reason=>{if(active)setError(toMessage(reason))})
    return()=>{active=false}
  },[api,selectedId])

  useEffect(()=>{
    if(!organizationId){setMembers([]);setMeetings([]);return}
    let active=true
    if(canManageSecretariat||canAppointCommission){
      lodgeApi.getMemberOptions(organizationId)
        .then(result=>{if(active)setMembers(result.items)})
        .catch(()=>{if(active)setMembers([])})
    } else setMembers([])
    if(canManageSecretariat){
      lodgeApi.getMeetings(organizationId)
        .then(result=>{if(active)setMeetings(result.items)})
        .catch(()=>{if(active)setMeetings([])})
    } else setMeetings([])
    return()=>{active=false}
  },[canAppointCommission,canManageSecretariat,lodgeApi,organizationId])

  const reload=async(caseId?:string)=>{
    const result=await api.listCases(organizationId)
    setRows(result.items)
    const id=caseId??selectedId
    if(id){setSelectedId(id);await refreshSelected(id)}
  }

  const act=async(label:string,fn:()=>Promise<unknown>)=>{
    setWorking(true);setError(null);setNotice(null)
    try{await fn();await reload();setNotice(label)}
    catch(reason){setError(toMessage(reason))}
    finally{setWorking(false)}
  }

  return <>
    <section className="page-heading admissions-heading">
      <div><p className="eyebrow">Afiliación e Incorporación · Reglamento General arts. 2.1–2.6</p><h1>Expedientes de admisión</h1><p>Flujo trazable del Taller: clasificación, controles normativos, comisión, 3.er grado, balotaje, autorización y materialización documental.</p></div>
      <label className="admissions-org"><span>Taller</span><select value={organizationId} onChange={e=>setOrganizationId(e.target.value)}>{organizations.map(o=><option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></label>
    </section>

    {error&&<div className="error-banner" role="alert"><strong>No fue posible completar la operación.</strong><span>{error}</span></div>}
    {notice&&<div className="admissions-notice" role="status">{notice}</div>}

    {canManageSecretariat&&<CreateCasePanel api={api} organizationId={organizationId} organizations={organizations} disabled={working||!organizationId} onCreated={id=>void reload(id)} />}

    <section className="admissions-layout">
      <article className="panel admissions-list">
        <div className="panel-heading"><div><p className="eyebrow">Taller seleccionado</p><h2>Expedientes</h2></div><span className="count-badge">{rows.length}</span></div>
        {loading?<Loading/>:rows.length===0?<div className="empty-state"><strong>No hay expedientes.</strong></div>:<div className="admissions-case-list">{rows.map(row=><button key={row.admissionCase.id} type="button" className={row.admissionCase.id===selectedId?'admission-case active':'admission-case'} onClick={()=>setSelectedId(row.admissionCase.id)}><span><strong>{row.personDisplayName}</strong><small>{typeLabel(row.admissionCase.admissionType)} · {procedureLabel(row.admissionCase.affiliationProcedure)}</small></span><span className={statusClass(row.admissionCase.status)}>{statusLabel(row.admissionCase.status)}</span></button>)}</div>}
      </article>

      <article className="panel admissions-detail">
        {!detail||!eligibility?<div className="empty-state"><strong>Seleccione un expediente.</strong></div>:<CaseDetail
          detail={detail}
          eligibility={eligibility}
          members={members}
          meetings={meetings}
          disabled={working}
          onAction={act}
          api={api}
          canManageSecretariat={canManageSecretariat}
          canAppointCommission={canAppointCommission}
          canValidateInternalAffairs={canValidateInternalAffairs}
        />}
      </article>
    </section>
  </>
}

function CreateCasePanel({api,organizationId,organizations,disabled,onCreated}:{api:AdmissionApiClient;organizationId:string;organizations:OrganizationOption[];disabled:boolean;onCreated:(id:string)=>void}){
  const [open,setOpen]=useState(false)
  const [type,setType]=useState<AdmissionType>('affiliation')
  const [mode,setMode]=useState<AffiliationMode>('simple')
  const [procedure,setProcedure]=useState<AffiliationProcedure>('standard')
  const [query,setQuery]=useState('')
  const [people,setPeople]=useState<AdmissionPersonOption[]>([])
  const [personId,setPersonId]=useState('')
  const [originOrganizationId,setOriginOrganizationId]=useState('')
  const [originObedience,setOriginObedience]=useState('')
  const [originLodge,setOriginLodge]=useState('')
  const [degree,setDegree]=useState('master')
  const [localError,setLocalError]=useState<string|null>(null)
  const [busy,setBusy]=useState(false)
  const selected=people.find(x=>x.personId===personId)

  const search=async()=>{
    setLocalError(null)
    try{const result=await api.searchPeople(organizationId,query);setPeople(result.items);setPersonId(result.items[0]?.personId??'')}
    catch(reason){setLocalError(toMessage(reason))}
  }
  const submit=async(event:FormEvent)=>{
    event.preventDefault();setLocalError(null)
    if(!selected){setLocalError('Seleccione una persona desde la búsqueda.');return}
    if(type==='affiliation'&&!selected.memberId){setLocalError('La Afiliación requiere que la persona ya tenga ficha de Hermano en la Orden.');return}
    if(type==='affiliation'&&procedure==='transfer'&&!originOrganizationId){setLocalError('El traslado debe identificar el Taller de origen.');return}
    setBusy(true)
    try{
      const created=await api.createCase({
        organizationId,admissionType:type,personId:selected.personId,memberId:type==='affiliation'?selected.memberId:null,
        affiliationMode:type==='affiliation'?mode:null,affiliationProcedure:type==='affiliation'?procedure:null,
        originOrganizationId:type==='affiliation'&&procedure==='transfer'?originOrganizationId:null,
        originLodgeName:originLodge||null,originObedience:type==='incorporation'?originObedience:null,degree:type==='incorporation'?degree:null,
        wageIncreaseEvidenceApplies:type==='incorporation',exaltationEvidenceApplies:type==='incorporation',
        hasPeaceAndFriendshipPact:type==='incorporation'?false:null,originObedienceRecognizedAsRegular:type==='incorporation'?true:null,
      })
      setOpen(false);onCreated(created.id)
    }catch(reason){setLocalError(toMessage(reason))}
    finally{setBusy(false)}
  }

  return <section className="panel admissions-create">
    <div className="panel-heading"><div><p className="eyebrow">Secretaría / revisión institucional</p><h2>Nuevo expediente</h2></div><button type="button" className="secondary-action" onClick={()=>setOpen(v=>!v)}>{open?'Cerrar formulario':'Crear expediente'}</button></div>
    {open&&<form className="admissions-create-form" onSubmit={submit}>
      <label><span>Tipo</span><select value={type} onChange={e=>setType(e.target.value as AdmissionType)}><option value="affiliation">Afiliación</option><option value="incorporation">Incorporación</option></select></label>
      {type==='affiliation'&&<><label><span>Modalidad</span><select value={mode} onChange={e=>setMode(e.target.value as AffiliationMode)}><option value="simple">Simple</option><option value="activation">Con activación</option></select></label><label><span>Procedimiento</span><select value={procedure} onChange={e=>setProcedure(e.target.value as AffiliationProcedure)}><option value="standard">Estándar</option><option value="reentry">Reintegro</option><option value="transfer">Traslado</option></select></label>{procedure==='transfer'&&<label><span>Taller de origen</span><select required value={originOrganizationId} onChange={e=>setOriginOrganizationId(e.target.value)}><option value="">Seleccione…</option>{organizations.filter(o=>o.id!==organizationId).map(o=><option key={o.id} value={o.id}>{organizationLabel(o)}</option>)}</select></label>}</>}
      <label className="admissions-person-search"><span>Buscar persona</span><div><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Nombre o apellido" minLength={2}/><button type="button" onClick={()=>void search()} disabled={query.trim().length<2}>Buscar</button></div></label>
      <label><span>Persona</span><select value={personId} onChange={e=>setPersonId(e.target.value)}><option value="">Seleccione…</option>{people.map(p=><option key={p.personId} value={p.personId}>{p.displayName}{p.memberId?' · Hermano registrado':' · sin membresía GLMCh'}</option>)}</select></label>
      <label><span>Logia/Taller de origen</span><input value={originLodge} onChange={e=>setOriginLodge(e.target.value)} placeholder="Opcional"/></label>
      {type==='incorporation'&&<><label><span>Obediencia de origen</span><input required value={originObedience} onChange={e=>setOriginObedience(e.target.value)}/></label><label><span>Grado acreditado</span><select value={degree} onChange={e=>setDegree(e.target.value)}><option value="apprentice">Aprendiz</option><option value="fellowcraft">Compañero/a</option><option value="master">Maestro/a</option></select></label></>}
      {localError&&<p className="form-error">{localError}</p>}
      <button className="primary-action" disabled={disabled||busy||!personId}>{busy?'Creando…':'Crear expediente'}</button>
    </form>}
  </section>
}

function CaseDetail({detail,eligibility,members,meetings,disabled,onAction,api,canManageSecretariat,canAppointCommission,canValidateInternalAffairs}:{detail:AdmissionCaseDetail;eligibility:AdmissionEligibilityResponse;members:LodgeMemberOption[];meetings:LodgeMeeting[];disabled:boolean;onAction:(label:string,fn:()=>Promise<unknown>)=>Promise<void>;api:AdmissionApiClient;canManageSecretariat:boolean;canAppointCommission:boolean;canValidateInternalAffairs:boolean}){
  const c=detail.admissionCase
  const [date,setDate]=useState(todaySantiago())
  const [source,setSource]=useState('ACTA-DEMO-2026')
  const [selectedMembers,setSelectedMembers]=useState<string[]>([])
  const [present,setPresent]=useState(6)
  const [favor,setFavor]=useState(4)
  const [against,setAgainst]=useState(2)
  const [abstentions,setAbstentions]=useState(0)
  const [eligible,setEligible]=useState(6)
  const [white,setWhite]=useState(6)
  const [black,setBlack]=useState(0)
  const [proposedDate,setProposedDate]=useState(date)
  const ceremonyRequestId=detail.decisions.find(x=>x.decisionType==='ceremony_request_created')?.sourceReference??null
  const completed=detail.decisions.some(x=>x.decisionType==='ceremony_completed')
  const matchingMeetings=useMemo(()=>meetings.filter(x=>x.status==='closed'&&x.ceremonyType===c.admissionType),[meetings,c.admissionType])
  const [meetingId,setMeetingId]=useState(matchingMeetings[0]?.id??'')
  useEffect(()=>{setMeetingId(matchingMeetings[0]?.id??'')},[c.id,matchingMeetings])

  const toggleMember=(id:string)=>setSelectedMembers(current=>current.includes(id)?current.filter(x=>x!==id):current.length<3?[...current,id]:current)
  const call=(label:string,fn:()=>Promise<unknown>)=>void onAction(label,fn)

  return <>
    <div className="admission-hero"><div><p className="eyebrow">{typeLabel(c.admissionType)}</p><h2>{procedureLabel(c.affiliationProcedure)}</h2><p>{modeLabel(c.affiliationMode)} · creado {formatDateTime(c.createdAtUtc)}</p></div><span className={eligibility.eligibility.canProceed?'status-pill complete':'status-pill attention'}>{eligibility.eligibility.canProceed?'Habilitado para solicitar ceremonia':'Requisitos pendientes'}</span></div>

    <section className="admission-summary-grid">
      <Summary label="Tipo" value={typeLabel(c.admissionType)} />
      <Summary label="Modalidad" value={modeLabel(c.affiliationMode)} />
      <Summary label="Procedimiento" value={procedureLabel(c.affiliationProcedure)} />
      <Summary label="Estado expediente" value={statusLabel(c.status)} />
    </section>

    <section className="admission-block"><div className="panel-heading"><div><p className="eyebrow">Matriz calculada</p><h3>Habilitación</h3></div><span className="count-badge">{eligibility.eligibility.requirements.filter(x=>x.status==='approved').length}/{eligibility.eligibility.requirements.length}</span></div>
      <div className="admission-requirements">{eligibility.eligibility.requirements.map(r=><div key={r.code}><span className={r.status==='approved'?'req-dot ok':'req-dot'} /><div><strong>{requirementLabel(r.code)}</strong><small>{r.reason}</small></div></div>)}</div>
    </section>

    <section className="admission-actions">
      <div className="admission-action-context"><label><span>Fecha actuación</span><input type="date" value={date} onChange={e=>setDate(e.target.value)}/></label><label><span>Referencia de acta/fuente</span><input value={source} onChange={e=>setSource(e.target.value)}/></label></div>

      <ActionCard title="1. Controles previos" detail="Régimen Interior registra el art. 2.3; Secretaría registra la presentación en Cámara de Primer Grado.">
        {canValidateInternalAffairs&&<button disabled={disabled} onClick={()=>call('Revisión art. 2.3 registrada.',()=>api.recordArticle23(c.id,{hasRayamiento:false,hasTribunalForcedWithdrawal:false,asOfDate:date,sourceReference:source}))}>Art. 2.3 sin impedimento</button>}
        {canManageSecretariat&&<button disabled={disabled} onClick={()=>call('Presentación de 1.er grado registrada.',()=>api.recordFirstDegreePresentation(c.id,{presentationDate:date,sourceReference:source}))}>Registrar presentación 1G</button>}
        {!canValidateInternalAffairs&&!canManageSecretariat&&<ReadOnlyAction/>}
      </ActionCard>

      {(c.admissionType==='incorporation'||c.affiliationProcedure==='reentry'||c.affiliationProcedure==='transfer')&&<ActionCard title="2. Comisión art. 2.5" detail={c.affiliationProcedure==='transfer'?'Traslado: comisión o dispensa expresa de Cámara del Medio.':'Reintegro/Incorporación: comisión de tres Maestros obligatoria.'}>
        {canAppointCommission&&<><div className="commission-members">{members.map(m=><label key={m.id}><input type="checkbox" checked={selectedMembers.includes(m.id)} onChange={()=>toggleMember(m.id)}/><span>{m.displayName}</span></label>)}</div><button disabled={disabled||selectedMembers.length!==3} onClick={()=>call('Comisión de tres Maestros nombrada.',()=>api.appointCommission(c.id,{memberIds:selectedMembers,appointmentDate:date,sourceReference:source}))}>Nombrar comisión ({selectedMembers.length}/3)</button></>}
        {c.affiliationProcedure==='transfer'&&(canManageSecretariat||canAppointCommission)&&<button disabled={disabled} onClick={()=>call('Dispensa de Cámara del Medio registrada.',()=>api.waiveTransferCommission(c.id,{asOfDate:date,sourceReference:source}))}>Registrar dispensa de traslado</button>}
        {(canManageSecretariat||canAppointCommission)&&<button disabled={disabled||detail.commission.length===0} onClick={()=>call('Conclusión de comisión registrada.',()=>api.completeCommission(c.id,{completed:true,asOfDate:date,sourceReference:source}))}>Concluir comisión vigente</button>}
        {!canManageSecretariat&&!canAppointCommission&&<ReadOnlyAction/>}
      </ActionCard>}

      <ActionCard title="3. Cámara del Medio" detail="Decisión de 3.er grado con mayoría reglamentaria; sólo se conservan totales.">
        <div className="vote-grid"><NumberField label="Presentes" value={present} set={setPresent}/><NumberField label="A favor" value={favor} set={setFavor}/><NumberField label="En contra" value={against} set={setAgainst}/><NumberField label="Abstenciones" value={abstentions} set={setAbstentions}/></div>
        {canManageSecretariat?<button disabled={disabled} onClick={()=>call('Decisión de 3.er grado registrada.',()=>api.recordThirdDegree(c.id,{asOfDate:date,sourceReference:source,presentMasters:present,votesInFavor:favor,votesAgainst:against,abstentions}))}>Registrar decisión 3G</button>:<ReadOnlyAction/>}
      </ActionCard>

      <ActionCard title="4. Balotaje posterior en 1.er grado" detail="Voto secreto: el sistema conserva sólo el recuento agregado de balotas.">
        <div className="vote-grid"><NumberField label="Habilitados" value={eligible} set={setEligible}/><NumberField label="Blancas" value={white} set={setWhite}/><NumberField label="Negras" value={black} set={setBlack}/></div>
        {canManageSecretariat?<button disabled={disabled} onClick={()=>call('Balotaje de 1.er grado registrado.',()=>api.recordFirstDegreeBallot(c.id,{asOfDate:date,sourceReference:source,ballots:[{procedureNumber:1,eligibleVoters:eligible,whiteBallots:white,blackBallots:black}],ballotApproved:white>black}))}>Registrar balotaje agregado</button>:<ReadOnlyAction/>}
      </ActionCard>

      <ActionCard title="5. Ceremonia y cierre documental" detail="Gran Secretaría emite la Plancha. La Tenida debe cerrar con Plancha + Extracto antes de materializar.">
        {!ceremonyRequestId?(canManageSecretariat?<div className="admission-inline-form"><label><span>Fecha propuesta</span><input type="date" value={proposedDate} onChange={e=>setProposedDate(e.target.value)}/></label><button disabled={disabled||!eligibility.eligibility.canProceed} onClick={()=>call('Solicitud de ceremonia creada; continúa por aprobaciones institucionales y Gran Secretaría.',()=>api.createCeremonyRequest(c.id,{proposedDate}))}>Crear solicitud de ceremonia</button></div>:<ReadOnlyAction/>):<p className="admission-reference">Solicitud vinculada: <code>{ceremonyRequestId}</code></p>}
        {ceremonyRequestId&&!completed&&canManageSecretariat&&<div className="admission-inline-form"><label><span>Tenida cerrada</span><select value={meetingId} onChange={e=>setMeetingId(e.target.value)}><option value="">Seleccione…</option>{matchingMeetings.map(m=><option key={m.id} value={m.id}>{formatDate(m.meetingDate)} · {m.title??typeLabel(c.admissionType)}</option>)}</select></label><button disabled={disabled||!meetingId} onClick={()=>{const meeting=matchingMeetings.find(x=>x.id===meetingId);if(meeting)call('Admisión materializada desde Tenida cerrada con evidencia documental.',()=>api.completeCeremony(ceremonyRequestId,{meetingId:meeting.id,ceremonyDate:meeting.meetingDate}))}}>Materializar admisión</button></div>}
        {completed&&<p className="admission-completed"><strong>Materialización completada.</strong> El reintento es idempotente y no crea una segunda pertenencia.</p>}
        {ceremonyRequestId&&matchingMeetings.length===0&&!completed&&<p className="admission-warning">No existe una Tenida cerrada del mismo tipo. Secretaría debe realizarla y cerrarla con Plancha de Gran Secretaría + Extracto de Acta.</p>}
      </ActionCard>
    </section>
  </>
}

function ReadOnlyAction(){return <span className="admission-readonly">Vista de consulta: esta actuación corresponde a otro perfil institucional.</span>}
function ActionCard({title,detail,children}:{title:string;detail:string;children:ReactNode}){return <section className="admission-action-card"><div><h4>{title}</h4><p>{detail}</p></div><div className="admission-action-controls">{children}</div></section>}
function Summary({label,value}:{label:string;value:string}){return <div><span>{label}</span><strong>{value}</strong></div>}
function NumberField({label,value,set}:{label:string;value:number;set:(value:number)=>void}){return <label><span>{label}</span><input type="number" min={0} value={value} onChange={e=>set(Math.max(0,Number(e.target.value)||0))}/></label>}
function Loading(){return <div className="loading-rows"><span/><span/><span/></div>}
function organizationLabel(o:OrganizationOption){return `${o.name}${o.number?` · Nº ${o.number}`:''}`}
function typeLabel(v:AdmissionType){return v==='affiliation'?'Afiliación':'Incorporación'}
function modeLabel(v:string|null){return v==='simple'?'Simple':v==='activation'?'Con activación':'No aplica'}
function procedureLabel(v:string|null){return v==='standard'?'Estándar':v==='reentry'?'Reintegro':v==='transfer'?'Traslado':'Desde otra Obediencia'}
function statusLabel(v:string){return v==='resolved'?'Resuelto':v==='eligible'?'Habilitado':v==='rejected'?'Rechazado':v==='observed'?'Observado':v==='under_review'?'En revisión':v.replaceAll('_',' ')}
function statusClass(v:string){return v==='resolved'||v==='eligible'?'status-pill complete':v==='rejected'?'status-pill attention':'status-pill pending'}
function requirementLabel(code:string){return code.replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase())}
function formatDate(v:string){return new Intl.DateTimeFormat('es-CL',{dateStyle:'medium',timeZone:'UTC'}).format(new Date(`${v}T12:00:00Z`))}
function formatDateTime(v:string){return new Intl.DateTimeFormat('es-CL',{dateStyle:'medium',timeZone:'America/Santiago'}).format(new Date(v))}
function todaySantiago(){const p=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santiago',year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date());const m=Object.fromEntries(p.map(x=>[x.type,x.value]));return `${m.year}-${m.month}-${m.day}`}
function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible completar la operación.'}
