import { useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import LodgeTreasuryPanel from './LodgeTreasuryPanel'
import TreasuryRoleNavigation from './TreasuryRoleNavigation'
import TreasuryStatementPage from './TreasuryStatementPage'

type LocalSection = 'summary' | 'collection' | 'expenses' | 'statement'

export default function LodgeTreasuryPage({api,canManage,canApproveExpenses}:{api:PmgmApiClient;canManage:boolean;canApproveExpenses:boolean}){
 const [organizations,setOrganizations]=useState<OrganizationOption[]>([]);const [organizationId,setOrganizationId]=useState('')
 const [section,setSection]=useState<LocalSection>(canManage?'summary':'expenses')
 useEffect(()=>{void api.getOrganizationOptions().then(response=>{setOrganizations(response.items);setOrganizationId(current=>current||response.items[0]?.id||'')})},[api])
 const sections = canManage
  ? [
    {id:'summary',label:'Resumen',description:'Estado del mes y cuotas vigentes'},
    {id:'collection',label:'Cuotas y cobranza',description:'Cargos, pagos y comprobantes'},
    {id:'expenses',label:'Egresos',description:'Registro y seguimiento de autorizaciones'},
    {id:'statement',label:'Cuadro mensual',description:'Pago y envío a Gran Tesorería'},
   ]
  : [{id:'expenses',label:'Egresos por autorizar',description:'Revisión exclusiva del Venerable Maestro'}]
 return <div className="lodge-product-page"><section className="lodge-product-heading"><div><p className="lodge-product-breadcrumb">Taller <span>›</span> Tesorería</p><h1>{canManage?'Tesorería del Taller':'Revisión de egresos de Tesorería'}</h1><p>{canManage?'Todo lo necesario para recaudar, registrar y rendir la Tesorería del Taller.':'El Venerable Maestro revisa y autoriza egresos, sin modificar cuotas ni pagos.'}</p></div><label className="lodge-organization-select"><span>Taller</span><select value={organizationId} onChange={e=>setOrganizationId(e.target.value)}><option value="">Seleccione…</option>{organizations.map(x=><option key={x.id} value={x.id}>{x.name}{x.number?` · Nº ${x.number}`:''}</option>)}</select></label></section>
 <TreasuryRoleNavigation title={canManage?'Tesorería del Taller':'Autorizaciones del Venerable'} sections={sections} active={section} onChange={id=>setSection(id as LocalSection)}/>
 {organizationId&&section!=='statement'&&<LodgeTreasuryPanel api={api} organizationId={organizationId} treasuryTerritory={organizations.find(x=>x.id===organizationId)?.treasuryTerritory??null} canManage={canManage} canApproveExpenses={canApproveExpenses} section={section}/>}
 {canManage&&section==='statement'&&<TreasuryStatementPage api={api} canPrepare canReview={false} organizationId={organizationId}/>}</div>
}
