import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { HelpNote, WorkspacePanel, WorkspaceTabs } from './actionKit'
import { type PmgmApiClient, type SystemSetting } from './api/pmgmApi'
import './systemConfiguration.css'
import SystemOperationsPanel, { type Area } from './SystemOperationsPanel'
import FormSchemaAdminPanel from './FormSchemaAdminPanel'
import SystemSettingsCategoryPanel from './SystemSettingsCategoryPanel'

/* PMGM-UX administración ordenada (04-10-2026): un solo menú de secciones (antes había tres niveles de pestañas)
   y ningún formulario abierto: cada categoría se edita desde un único panel «Editar parámetros». */
type Section=Area|'profiles'|'settings'|'forms'|'bootstrap'

export default function SystemConfigurationPage({ api, bootstrapSlot }: { api: PmgmApiClient; bootstrapSlot?: ReactNode }) {
  const [items,setItems]=useState<SystemSetting[]>([])
  const [category,setCategory]=useState('')
  const [message,setMessage]=useState<string|null>(null)
  const [error,setError]=useState<string|null>(null)
  const [tab,setTab]=useState<Section>('backup')
  const [categoryOpen,setCategoryOpen]=useState(false)
  const OPS:Area[]=['backup','users','mail','brand','audit']
  const isOps=OPS.includes(tab as Area)
  const lastOps=useRef<Area>('backup')
  if(isOps) lastOps.current=tab as Area
  const categories=useMemo(()=>Array.from(new Set(items.map(item=>item.category))),[items])
  const activeCategory=category||categories[0]||''
  const visible=items.filter(item=>item.category===activeCategory)
  const profiles=items.filter(item=>item.category==='Perfiles y permisos')

  useEffect(()=>{ let active=true; api.getSystemSettings().then(response=>{if(!active)return;setItems(response.items)}).catch(reason=>active&&setError(toMessage(reason)));return()=>{active=false}},[api])


  return <>
    <section className="page-heading"><div><p className="eyebrow">Administración · parámetros versionados</p><h1>Sistema</h1><p>Control central de reglas, flujos, catálogos, plazos, documentos y seguridad institucional.</p></div><span className="count-badge">{items.length} parámetros</span></section>
    {message&&<div className="regularity-success" role="status">{message}</div>}{error&&<div className="error-banner" role="alert">{error}</div>}
    <WorkspaceTabs label="Secciones de Sistema" active={tab} onChange={setTab} hub={{ title: 'Parámetros del sistema', hideBack: tab==='settings'&&categoryOpen }} tabs={[{id:'backup',label:'Respaldos',icon:'archive' as const},{id:'users',label:'Usuarios',icon:'members' as const},{id:'profiles',label:'Perfiles y accesos',icon:'shield' as const},{id:'forms',label:'Formularios',icon:'settings' as const},{id:'mail',label:'Correo',icon:'bell' as const},{id:'brand',label:'Logos y colores',icon:'home' as const},{id:'audit',label:'Auditoría de accesos',icon:'check' as const},{id:'settings',label:`Parámetros (${items.length})`,icon:'settings' as const},...(bootstrapSlot?[{id:'bootstrap' as Section,label:'Configuración inicial',icon:'tasks' as const}]:[])]} />
    {/* La consola queda montada (oculta) para conservar lo escrito al cambiar de sección. */}
    <div hidden={!isOps}><SystemOperationsPanel api={api} area={lastOps.current} /></div>
    <WorkspacePanel id="profiles" active={tab==='profiles'}>{<section className="panel system-profile-matrix"><header><div><p className="eyebrow">Control de acceso</p><h2>Matriz vigente de perfiles</h2></div><span className="count-badge">{profiles.length} perfiles</span></header><div className="system-profile-table"><table><thead><tr><th>Perfil</th><th>Vistas y acciones habilitadas</th><th>Vigencia</th></tr></thead><tbody>{profiles.map(profile=><tr key={profile.code}><th>{profile.label}</th><td><div className="system-permission-chips">{profile.value.split('|').filter(Boolean).map(permission=><span key={permission}>{permission}</span>)}</div></td><td>{profile.effectiveFrom}</td></tr>)}</tbody></table></div><p className="system-profile-note">La matriz permite revisar el alcance completo antes de versionar cambios en las tarjetas inferiores. Las autorizaciones críticas continúan sujetas a segregación de funciones y auditoría.</p></section>}</WorkspacePanel>
    <WorkspacePanel id="forms" active={tab==='forms'}><FormSchemaAdminPanel api={api} /></WorkspacePanel>
    <WorkspacePanel id="settings" active={tab==='settings'}>
    <HelpNote>Cada cambio crea una nueva versión auditada con fecha de vigencia. Los expedientes iniciados bajo una regla anterior no cambian.</HelpNote>
    <WorkspaceTabs label="Categorías de parámetros" active={activeCategory} onChange={setCategory} hub={{ title: 'Parámetros', onOpenChange: setCategoryOpen, hints: Object.fromEntries(categories.map(value=>{const n=items.filter(item=>item.category===value).length;return [value,n===1?'1 parámetro':`${n} parámetros`]})) }} tabs={categories.map(value=>({id:value,label:value,icon:'settings' as const}))} />
    {activeCategory&&<SystemSettingsCategoryPanel key={activeCategory} api={api} category={activeCategory} items={visible} onSaved={saved=>setItems(current=>current.map(value=>saved.find(x=>x.code===value.code)??value))} onMessage={setMessage} onError={setError} />}
    </WorkspacePanel>
    {bootstrapSlot&&<WorkspacePanel id="bootstrap" active={tab==='bootstrap'}>{bootstrapSlot}</WorkspacePanel>}
  </>
}

function toMessage(reason:unknown){return reason instanceof Error?reason.message:'No fue posible guardar la configuración.'}
