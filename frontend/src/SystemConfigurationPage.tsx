import { useEffect, useMemo, useState } from 'react'
import { type PmgmApiClient, type SystemSetting } from './api/pmgmApi'
import './systemConfiguration.css'

export default function SystemConfigurationPage({ api }: { api: PmgmApiClient }) {
  const [items,setItems]=useState<SystemSetting[]>([])
  const [category,setCategory]=useState('Todos')
  const [drafts,setDrafts]=useState<Record<string,{value:string;effectiveFrom:string;sourceReference:string}>>({})
  const [busy,setBusy]=useState<string|null>(null)
  const [message,setMessage]=useState<string|null>(null)
  const [error,setError]=useState<string|null>(null)
  const categories=useMemo(()=>['Todos',...Array.from(new Set(items.map(item=>item.category)))],[items])
  const visible=category==='Todos'?items:items.filter(item=>item.category===category)
  const profiles=items.filter(item=>item.category==='Perfiles y permisos')

  useEffect(()=>{ let active=true; api.getSystemSettings().then(response=>{if(!active)return;setItems(response.items);setDrafts(Object.fromEntries(response.items.map(item=>[item.code,{value:item.value,effectiveFrom:item.effectiveFrom,sourceReference:item.sourceReference}])))}).catch(reason=>active&&setError(toMessage(reason)));return()=>{active=false}},[api])

  const update=(code:string,field:'value'|'effectiveFrom'|'sourceReference',value:string)=>setDrafts(current=>({...current,[code]:{...current[code], [field]:value}}))
  const save=async(item:SystemSetting)=>{const draft=drafts[item.code];if(!draft)return;setBusy(item.code);setError(null);setMessage(null);try{const saved=await api.createSystemSettingVersion(item.code,draft);setItems(current=>current.map(value=>value.code===saved.code?saved:value));setMessage(`Nueva versión guardada: ${item.label}.`)}catch(reason){setError(toMessage(reason))}finally{setBusy(null)}}

  return <>
    <section className="page-heading"><div><p className="eyebrow">Administración · parámetros versionados</p><h1>Sistema</h1><p>Control central de reglas, flujos, catálogos, plazos, documentos y seguridad institucional.</p></div><span className="count-badge">{items.length} parámetros</span></section>
    <section className="system-warning"><strong>Cambios con vigencia controlada</strong><span>Cada modificación crea una nueva versión auditada. No altera silenciosamente expedientes iniciados bajo una regla anterior.</span></section>
    {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
    {(category==='Todos'||category==='Perfiles y permisos')&&<section className="panel system-profile-matrix"><header><div><p className="eyebrow">Control de acceso</p><h2>Matriz vigente de perfiles</h2></div><span className="count-badge">{profiles.length} perfiles</span></header><div className="system-profile-table"><table><thead><tr><th>Perfil</th><th>Vistas y acciones habilitadas</th><th>Vigencia</th></tr></thead><tbody>{profiles.map(profile=><tr key={profile.code}><th>{profile.label}</th><td><div className="system-permission-chips">{profile.value.split('|').filter(Boolean).map(permission=><span key={permission}>{permission}</span>)}</div></td><td>{profile.effectiveFrom}</td></tr>)}</tbody></table></div><p className="system-profile-note">La matriz permite revisar el alcance completo antes de versionar cambios en las tarjetas inferiores. Las autorizaciones críticas continúan sujetas a segregación de funciones y auditoría.</p></section>}
    <section className="panel system-filter"><label><span>Categoría</span><select value={category} onChange={event=>setCategory(event.target.value)}>{categories.map(value=><option key={value}>{value}</option>)}</select></label><p>{visible.length} parámetros visibles</p></section>
    <section className="system-settings-grid">{visible.map(item=>{const draft=drafts[item.code]??{value:item.value,effectiveFrom:item.effectiveFrom,sourceReference:item.sourceReference};return <article className="panel system-setting-card" key={item.code}><header><div><p className="eyebrow">{item.category}</p><h2>{item.label}</h2></div><span className={`system-setting-state ${item.status}`}>{item.status==='default'?'Valor base':'Vigente'}</span></header><code>{item.code}</code><label><span>Valor</span>{item.valueType==='text'?<textarea rows={3} value={draft.value} onChange={event=>update(item.code,'value',event.target.value)}/>:<input type={item.valueType==='integer'?'number':'text'} value={draft.value} onChange={event=>update(item.code,'value',event.target.value)}/>} {item.valueType==='list'&&<small>Separe los valores con el carácter |</small>}</label><div className="system-setting-row"><label><span>Vigente desde</span><input type="date" value={draft.effectiveFrom} onChange={event=>update(item.code,'effectiveFrom',event.target.value)}/></label><label><span>Fundamento / referencia</span><input value={draft.sourceReference} onChange={event=>update(item.code,'sourceReference',event.target.value)}/></label></div><button type="button" disabled={busy===item.code} onClick={()=>void save(item)}>{busy===item.code?'Guardando…':'Guardar nueva versión'}</button></article>})}</section>
  </>
}

function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible guardar la configuración.'}
