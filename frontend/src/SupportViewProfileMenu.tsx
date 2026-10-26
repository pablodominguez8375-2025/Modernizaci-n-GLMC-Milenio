import { useEffect,useRef,useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import { DEMO_PROFILE_FOR,SUPPORT_VIEW_PROFILES,profileDisplayName,type SupportViewProfile } from './api/supportViewProfiles'
import { setSupportViewAs } from './api/supportViewAs'
import { useSupportViewAs } from './SupportViewBanner'

/**
 * Selector «Ver como» en la cabecera para el Máster y los superusuarios: toman por un momento el perfil de un
 * cargo (solo lectura, 30 min, Taller 00) y vuelven a su perfil con un clic. Aparece solo si el servidor lo autoriza.
 */
export function SupportViewProfileMenu({api,enabled,onExit}:{api:PmgmApiClient;enabled:boolean;onExit:()=>void}){
 const session=useSupportViewAs();const [items,setItems]=useState<SupportViewProfile[]|null>(null)
 const [open,setOpen]=useState(false);const [busy,setBusy]=useState(false);const [error,setError]=useState('')
 const root=useRef<HTMLDivElement>(null)
 useEffect(()=>{
  if(!enabled||api.useMocks){setItems(enabled?SUPPORT_VIEW_PROFILES:null);return}
  let alive=true
  api.getSupportViewProfiles().then(list=>{if(alive)setItems(list)}).catch(()=>{if(alive)setItems(null)})
  return()=>{alive=false}
 },[api,enabled])
 useEffect(()=>{
  if(!open)return
  const close=(e:MouseEvent)=>{if(!root.current?.contains(e.target as Node))setOpen(false)}
  const esc=(e:KeyboardEvent)=>{if(e.key==='Escape')setOpen(false)}
  document.addEventListener('mousedown',close);document.addEventListener('keydown',esc)
  return()=>{document.removeEventListener('mousedown',close);document.removeEventListener('keydown',esc)}
 },[open])
 if(!items&&!session)return null
 const choose=async(profile:SupportViewProfile)=>{
  setBusy(true);setError('')
  try{
   if(api.useMocks){
    const now=Date.now()
    setSupportViewAs({sessionId:crypto.randomUUID(),targetSubject:'profile:'+profile.key,targetName:profileDisplayName(profile),expiresAtUtc:new Date(now+30*60000).toISOString(),demoProfile:DEMO_PROFILE_FOR[profile.key]})
    setOpen(false);return
   }
   const started=await api.startSupportView({profileKey:profile.key})
   setSupportViewAs({sessionId:started.sessionId,targetSubject:started.targetSubject,targetName:started.targetName,expiresAtUtc:started.expiresAtUtc})
   window.location.reload()
  }catch(e){setError(e instanceof Error?e.message:'No se pudo cambiar de perfil.')}finally{setBusy(false)}
 }
 const groups=(['Taller','Gran Logia'] as const).map(g=>({g,list:(items??SUPPORT_VIEW_PROFILES).filter(p=>p.group===g)}))
 return <div className="view-profile-menu" ref={root}>
  <button type="button" className="view-profile-button" aria-haspopup="menu" aria-expanded={open} onClick={()=>setOpen(o=>!o)}>
   <span aria-hidden="true">👁</span> {session?`Ver como: ${session.targetName}`:'Ver como'} <span aria-hidden="true">▾</span>
  </button>
  {open&&<div className="view-profile-popover" role="menu" aria-label="Ver como un perfil">
   {session&&<button type="button" role="menuitem" className="view-profile-exit" onClick={()=>{setOpen(false);onExit()}}>↩ Volver a mi perfil</button>}
   <p className="view-profile-note">Solo lectura · 30 minutos · Taller 00 de soporte. No se ven datos de Hermanos reales.</p>
   {groups.map(({g,list})=><div key={g} role="group" aria-label={g}><p className="view-profile-group">{g==='Taller'?'Perfiles de Taller':'Perfiles de Gran Logia'}</p>
    {list.map(p=><button type="button" role="menuitem" key={p.key} disabled={busy} onClick={()=>void choose(p)}>{p.label}</button>)}</div>)}
   {error&&<p role="alert" className="view-profile-error">{error}</p>}
  </div>}
 </div>
}
