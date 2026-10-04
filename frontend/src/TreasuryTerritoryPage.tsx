import { useEffect, useState } from 'react'
import type { PmgmApiClient, TreasuryTerritory, TreasuryTerritoryOption } from './api/pmgmApi'
import { organizationDisplayName } from './displayFormat'
const labels:Record<TreasuryTerritory,string>={santiago:'Santiago',other_oriente:'Regiones de Chile',peru:'Perú'}
export default function TreasuryTerritoryPage({api}:{api:PmgmApiClient}){
  const [items,setItems]=useState<TreasuryTerritoryOption[]>([])
  const [error,setError]=useState('')
  useEffect(()=>{void api.getTreasuryTerritories().then(result=>setItems(result.items.filter(x=>x.type!=='order'))).catch(reason=>setError(reason instanceof Error?reason.message:'No se pudieron cargar las Fichas.'))},[api])
  return <section className="panel treasury-territory-panel"><h2>Orientes desde la Ficha del Taller</h2><p>Consulta de solo lectura. El Oriente, la ciudad y el país se administran en la Ficha del Taller.</p>{error&&<p role="alert">{error}</p>}<div className="table-scroll"><table className="treasury-table"><thead><tr><th>Taller</th><th>Zona del tarifario</th><th>Ciudad</th><th>País</th></tr></thead><tbody>{items.map(x=><tr key={x.id}><td>{organizationDisplayName(x.name,x.number)}</td><td>{x.treasuryTerritory?labels[x.treasuryTerritory]:'Ficha incompleta'}</td><td>{x.city??'Sin registrar'}</td><td>{x.country??'Sin registrar'}</td></tr>)}</tbody></table></div></section>
}
