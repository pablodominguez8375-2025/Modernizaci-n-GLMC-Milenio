import { useEffect, useState } from 'react'
import { type OrganizationOption, type PmgmApiClient } from './api/pmgmApi'
import LodgeTreasuryPanel from './LodgeTreasuryPanel'

export default function LodgeTreasuryPage({api,canManage,canApproveExpenses}:{api:PmgmApiClient;canManage:boolean;canApproveExpenses:boolean}){
 const [organizations,setOrganizations]=useState<OrganizationOption[]>([]);const [organizationId,setOrganizationId]=useState('')
 useEffect(()=>{void api.getOrganizationOptions().then(response=>{setOrganizations(response.items);setOrganizationId(current=>current||response.items[0]?.id||'')})},[api])
 return <div className="lodge-product-page"><section className="lodge-product-heading"><div><p className="lodge-product-breadcrumb">Taller <span>›</span> Tesorería</p><h1>Tesorería</h1><p>Cuotas, cobranza, comprobantes, egresos y autorizaciones del Taller.</p></div><label className="lodge-organization-select"><span>Taller</span><select value={organizationId} onChange={e=>setOrganizationId(e.target.value)}><option value="">Seleccione…</option>{organizations.map(x=><option key={x.id} value={x.id}>{x.name}{x.number?` · Nº ${x.number}`:''}</option>)}</select></label></section>{organizationId&&<LodgeTreasuryPanel api={api} organizationId={organizationId} canManage={canManage} canApproveExpenses={canApproveExpenses}/>}</div>
}
