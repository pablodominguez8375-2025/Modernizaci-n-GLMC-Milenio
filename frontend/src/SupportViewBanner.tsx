import { useEffect,useRef,useState,useSyncExternalStore } from 'react'
import { formatRemaining,remainingSeconds,subscribeSupportViewAs,supportViewAsSnapshot } from './api/supportViewAs'

export function useSupportViewAs(){return useSyncExternalStore(subscribeSupportViewAs,supportViewAsSnapshot,supportViewAsSnapshot)}

/** Franja fija y siempre visible mientras dura «Ver como». */
export function SupportViewBanner({onExit}:{onExit:()=>void}){
 const session=useSupportViewAs();const [now,setNow]=useState(Date.now())
 useEffect(()=>{if(!session)return;const id=window.setInterval(()=>setNow(Date.now()),1000);return()=>window.clearInterval(id)},[session])
 const ref=useRef<HTMLDivElement>(null)
 useEffect(()=>{
  const el=ref.current;if(!session||!el)return
  const apply=()=>document.documentElement.style.setProperty('--support-banner-h',`${el.offsetHeight}px`)
  apply();const observer=typeof ResizeObserver==='undefined'?null:new ResizeObserver(apply);observer?.observe(el)
  return()=>{observer?.disconnect();document.documentElement.style.removeProperty('--support-banner-h')}
 },[session])
 const left=session?remainingSeconds(session,now):0
 useEffect(()=>{if(session&&left===0)onExit()},[session,left,onExit])
 if(!session)return null
 return <div ref={ref} className="support-view-banner" role="status" aria-live="polite">
  <span><strong>Estás viendo como {session.targetName}</strong> · solo lectura · no se puede modificar nada</span>
  <span className="support-view-time" aria-label="Tiempo restante">Termina en {formatRemaining(left)}</span>
  <button type="button" onClick={onExit}>Salir de «Ver como»</button>
 </div>
}
