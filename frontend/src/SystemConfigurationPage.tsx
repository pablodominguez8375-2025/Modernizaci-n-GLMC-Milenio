import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { ActionDrawer, HelpNote, WorkspacePanel, WorkspaceTabs } from './actionKit'
import { type PmgmApiClient, type SystemSetting, type SystemSettingVersion } from './api/pmgmApi'
import './systemConfiguration.css'
import SystemOperationsPanel, { type Area } from './SystemOperationsPanel'

/* PMGM-UX administración ordenada (04-10-2026): un solo menú de secciones (antes había tres niveles de pestañas)
   y ningún formulario abierto: cada parámetro se cambia desde «Cambiar valor». */
type Section=Area|'profiles'|'settings'|'bootstrap'

export default function SystemConfigurationPage({ api, bootstrapSlot }: { api: PmgmApiClient; bootstrapSlot?: ReactNode }) {
  const [items,setItems]=useState<SystemSetting[]>([])
  const [category,setCategory]=useState('')
  const [drafts,setDrafts]=useState<Record<string,{value:string;effectiveFrom:string;sourceReference:string}>>({})
  const [busy,setBusy]=useState<string|null>(null)
  const [message,setMessage]=useState<string|null>(null)
  const [error,setError]=useState<string|null>(null)
  const [historyCode,setHistoryCode]=useState<string|null>(null)
  const [history,setHistory]=useState<SystemSettingVersion[]>([])
  const [tab,setTab]=useState<Section>('backup')
  const OPS:Area[]=['backup','users','mail','brand','audit']
  const isOps=OPS.includes(tab as Area)
  const lastOps=useRef<Area>('backup')
  if(isOps) lastOps.current=tab as Area
  const categories=useMemo(()=>Array.from(new Set(items.map(item=>item.category))),[items])
  const activeCategory=category||categories[0]||''
  const visible=items.filter(item=>item.category===activeCategory)
  const profiles=items.filter(item=>item.category==='Perfiles y permisos')

  useEffect(()=>{ let active=true; api.getSystemSettings().then(response=>{if(!active)return;setItems(response.items);setDrafts(Object.fromEntries(response.items.map(item=>[item.code,{value:item.value,effectiveFrom:item.effectiveFrom,sourceReference:item.sourceReference}])))}).catch(reason=>active&&setError(toMessage(reason)));return()=>{active=false}},[api])

  const update=(code:string,field:'value'|'effectiveFrom'|'sourceReference',value:string)=>setDrafts(current=>({...current,[code]:{...current[code], [field]:value}}))
  const save=async(item:SystemSetting)=>{const draft=drafts[item.code];if(!draft)return;setBusy(item.code);setError(null);setMessage(null);try{const saved=await api.createSystemSettingVersion(item.code,draft);setItems(current=>current.map(value=>value.code===saved.code?saved:value));setMessage(`Nueva versión guardada: ${item.label}.`)}catch(reason){setError(toMessage(reason))}finally{setBusy(null)}}
  const showHistory=async(item:SystemSetting)=>{setBusy(item.code);setError(null);try{const response=await api.getSystemSettingVersions(item.code);setHistoryCode(item.code);setHistory(response.items)}catch(reason){setError(toMessage(reason))}finally{setBusy(null)}}
  const prepareRestore=(item:SystemSetting,version:SystemSettingVersion)=>{setDrafts(current=>({...current,[item.code]:{value:version.value,effectiveFrom:new Date().toISOString().slice(0,10),sourceReference:`Restauración de versión ${version.effectiveFrom}: ${version.sourceReference}`}}));setMessage('Versión cargada como borrador. Revise la fecha y guarde para crear una restauración auditada.')}

  return <>
    <section className="page-heading"><div><p className="eyebrow">Administración · parámetros versionados</p><h1>Sistema</h1><p>Control central de reglas, flujos, catálogos, plazos, documentos y seguridad institucional.</p></div><span className="count-badge">{items.length} parámetros</span></section>
    {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
    <WorkspaceTabs label="Secciones de Sistema" active={tab} onChange={setTab} tabs={[{id:'backup',label:'Respaldos'},{id:'users',label:'Usuarios'},{id:'profiles',label:'Perfiles y accesos'},{id:'mail',label:'Correo'},{id:'brand',label:'Logos y colores'},{id:'audit',label:'Auditoría de accesos'},{id:'settings',label:`Parámetros (${items.length})`},...(bootstrapSlot?[{id:'bootstrap' as Section,label:'Configuración inicial'}]:[])]} />
    {/* La consola queda montada (oculta) para conservar lo escrito al cambiar de sección. */}
    <div hidden={!isOps}><SystemOperationsPanel api={api} area={lastOps.current} /></div>
    <WorkspacePanel id="profiles" active={tab==='profiles'}>{<section className="panel system-profile-matrix"><header><div><p className="eyebrow">Control de acceso</p><h2>Matriz vigente de perfiles</h2></div><span className="count-badge">{profiles.length} perfiles</span></header><div className="system-profile-table"><table><thead><tr><th>Perfil</th><th>Vistas y acciones habilitadas</th><th>Vigencia</th></tr></thead><tbody>{profiles.map(profile=><tr key={profile.code}><th>{profile.label}</th><td><div className="system-permission-chips">{profile.value.split('|').filter(Boolean).map(permission=><span key={permission}>{permission}</span>)}</div></td><td>{profile.effectiveFrom}</td></tr>)}</tbody></table></div><p className="system-profile-note">La matriz permite revisar el alcance completo antes de versionar cambios en las tarjetas inferiores. Las autorizaciones críticas continúan sujetas a segregación de funciones y auditoría.</p></section>}</WorkspacePanel>
    <WorkspacePanel id="settings" active={tab==='settings'}>
    <HelpNote>Cada cambio crea una nueva versión auditada con fecha de vigencia. Los expedientes iniciados bajo una regla anterior no cambian.</HelpNote>
    <WorkspaceTabs label="Categorías de parámetros" active={activeCategory} onChange={setCategory} tabs={categories.map(value=>({id:value,label:`${value} (${items.filter(item=>item.category===value).length})`}))} />
    <section className="system-settings-grid">{visible.map(item=>{const draft=drafts[item.code]??{value:item.value,effectiveFrom:item.effectiveFrom,sourceReference:item.sourceReference};return <article className="panel system-setting-card" key={item.code}><header><div><p className="eyebrow">{item.category}</p><h2>{item.label}</h2></div><span className={`system-setting-state ${item.status}`}>{item.status==='default'?'Valor base':item.status==='scheduled'?'Programado':'Vigente'}</span></header><p className="system-setting-value"><span>Valor vigente</span><strong>{item.value||'—'}</strong><small>Desde {item.effectiveFrom}{item.sourceReference?` · ${item.sourceReference}`:''}</small></p><div className="system-setting-actions"><ActionDrawer label="Cambiar valor" tone="secondary" title={`Cambiar: ${item.label}`} description="Se guarda como una nueva versión auditada, con su fecha de vigencia y fundamento." confirmMessage="Se creará una nueva versión del parámetro. Los expedientes iniciados con la regla anterior no cambian." keepOpen><div className="system-setting-form"><label><span>Valor</span>{item.valueType==='text'?<textarea rows={3} value={draft.value} onChange={event=>update(item.code,'value',event.target.value)}/>:<input type={item.valueType==='integer'?'number':'text'} value={draft.value} onChange={event=>update(item.code,'value',event.target.value)}/>} {item.valueType==='list'&&<small>Separe los valores con el carácter |</small>}</label><div className="system-setting-row"><label><span>Vigente desde</span><input type="date" value={draft.effectiveFrom} onChange={event=>update(item.code,'effectiveFrom',event.target.value)}/></label><label><span>Fundamento / referencia</span><input value={draft.sourceReference} onChange={event=>update(item.code,'sourceReference',event.target.value)}/></label></div><button type="button" disabled={busy===item.code} onClick={()=>void save(item)}>{busy===item.code?'Guardando…':'Guardar nueva versión'}</button></div></ActionDrawer><button className="secondary" type="button" disabled={busy===item.code} onClick={()=>void showHistory(item)}>Ver historial</button></div>{historyCode===item.code&&<div className="system-history"><h3>Historial y cambios programados</h3>{history.length===0?<p>Sin versiones guardadas.</p>:history.map(version=><div className="system-history-row" key={version.id}><div><strong>{version.effectiveFrom}</strong><span>{version.status==='scheduled'?'Programado':version.status==='retired'?'Retirado':'Vigente'} · {version.sourceReference}</span></div><button className="secondary" type="button" onClick={()=>prepareRestore(item,version)}>Restaurar como nueva</button></div>)}</div>}</article>})}</section>
    </WorkspacePanel>
    {bootstrapSlot&&<WorkspacePanel id="bootstrap" active={tab==='bootstrap'}>{bootstrapSlot}</WorkspacePanel>}
  </>
}

function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible guardar la configuración.'}
