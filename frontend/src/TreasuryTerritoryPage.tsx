import { useEffect, useState } from 'react'
import type { PmgmApiClient, TreasuryTerritory, TreasuryTerritoryOption } from './api/pmgmApi'

const labels:Record<TreasuryTerritory,string>={santiago:'Santiago',other_oriente:'Otro Oriente de Chile',peru:'Perú'}

export default function TreasuryTerritoryPage({api}:{api:PmgmApiClient}){
  const [items,setItems]=useState<TreasuryTerritoryOption[]>([])
  const [values,setValues]=useState<Record<string,TreasuryTerritory|''>>({})
  const [message,setMessage]=useState('')
  const [error,setError]=useState('')
  const [busy,setBusy]=useState(false)
  useEffect(()=>{void api.getTreasuryTerritories().then(result=>{const workshops=result.items.filter(item=>item.type!=='order');setItems(workshops);setValues(Object.fromEntries(workshops.map(item=>[item.id,item.treasuryTerritory??'']))) }).catch(reason=>setError(messageOf(reason)))},[api])
  const save=async(item:TreasuryTerritoryOption)=>{const territory=values[item.id];if(!territory)return;setBusy(true);setError('');setMessage('');try{await api.setTreasuryTerritory(item.id,territory);setMessage(`Oriente registrado para ${item.name}.`);setItems(current=>current.map(value=>value.id===item.id?{...value,treasuryTerritory:territory}:value))}catch(reason){setError(messageOf(reason))}finally{setBusy(false)}}
  return <section className="panel treasury-territory-panel"><div className="section-title"><div><p className="eyebrow">Configuración institucional</p><h2>Oriente y tarifario por Taller</h2></div></div>
    <p>Gran Tesorería clasifica cada Taller. Esta ubicación determina el aporte institucional; la cuota que cobra el Taller se configura aparte por su Tesorero.</p>
    {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
    <div className="table-scroll"><table className="treasury-table"><thead><tr><th>Taller</th><th>Oriente para cuotas</th><th>Decreto vigente</th><th></th></tr></thead><tbody>{items.map(item=>{const selected=values[item.id]??'';return <tr key={item.id}><td>{item.name}{item.number?` · Nº ${item.number}`:''}</td><td><select aria-label={`Oriente de ${item.name}`} value={selected} onChange={event=>setValues(current=>({...current,[item.id]:event.target.value as TreasuryTerritory|''}))}><option value="">Clasificación pendiente</option>{Object.entries(labels).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></td><td>{selected==='santiago'?'Santiago: $21.000 / $13.000 / $10.000 / $8.000':selected==='other_oriente'?'Otros Orientes: $15.000 / $10.000 / $8.000 / $8.000':selected==='peru'?'Perú: ordinaria US$6; soporte USD pendiente':'Pendiente'}</td><td><button type="button" className="regularity-primary" disabled={busy||!selected||selected===item.treasuryTerritory} onClick={()=>void save(item)}>Guardar</button></td></tr>})}</tbody></table></div>
    <h3>Derechos únicos de ceremonia</h3><div className="table-scroll"><table className="treasury-table"><thead><tr><th>Ceremonia</th><th>Derecho a Gran Tesorería</th></tr></thead><tbody><tr><td>Iniciación</td><td>$41.000</td></tr><tr><td>Aumento de salario</td><td>$31.000</td></tr><tr><td>Exaltación</td><td>$41.000</td></tr><tr><td>Afiliación</td><td>$26.000</td></tr><tr><td>Incorporación</td><td>$31.000</td></tr></tbody></table></div>
    <small>Orden de importes mensuales: cuota normal / cónyuge / tercera edad / estudiante. Los derechos de ceremonia son cobros únicos y no se suman a la nómina mensual. Fuente: Decreto N.º 1.759, vigente desde 01-01-2026. Perú se muestra sin conversión porque los cuadros actuales sólo admiten CLP.</small>
  </section>
}

function messageOf(reason:unknown){return reason instanceof Error?reason.message:'No se pudo cargar o guardar la clasificación territorial.'}
