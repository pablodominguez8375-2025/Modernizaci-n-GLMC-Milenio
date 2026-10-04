import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { TreasuryAccess } from './api/dynamicAccess'

export function useTreasuryAccess(api:PmgmApiClient,organizationId:string){
 const subject=api.demoAccessSubject
 const key=`${subject}:${organizationId}`
 const [state,setState]=useState<{key:string;access:TreasuryAccess|null;error:string|null}>({key:'',access:null,error:null})
 useEffect(()=>{
  let active=true;let generation=0
  const refresh=async()=>{const current=++generation;try{const access=await api.getTreasuryAccess(organizationId);if(active&&generation===current)setState({key,access,error:null})}catch(e){if(active&&generation===current)setState({key,access:null,error:e instanceof Error?e.message:'No se pudo comprobar el acceso.'})}}
  if(!organizationId)return
  void refresh()
  const unsubscribe=api.dynamicAccess.subscribe(()=>void refresh())
  const onFocus=()=>void refresh();window.addEventListener('focus',onFocus)
  const interval=window.setInterval(()=>void refresh(),30000)
  return()=>{active=false;unsubscribe();window.removeEventListener('focus',onFocus);window.clearInterval(interval)}
 },[api,organizationId,key])
 return state.key===key?state:{key,access:null,error:null}
}

export function useTreasuryNavigationAccess(api:PmgmApiClient,enabled:boolean,demoProfileKey:string){
 const key=`${enabled}:${demoProfileKey}`
 const [state,setState]=useState<{key:string;allowed:boolean}>({key:'',allowed:false})
 useEffect(()=>{
  let active=true;let generation=0
  const refresh=async()=>{const current=++generation;try{
   const organizations=await api.getOrganizationOptions()
   const access=await Promise.all(organizations.items.filter(o=>o.type==='workshop').map(o=>api.getTreasuryAccess(o.id)))
   if(active&&generation===current)setState({key,allowed:access.some(a=>a.actions.includes('view'))})
  }catch{if(active&&generation===current)setState({key,allowed:false})}}
  if(!enabled)return
  void refresh()
  const unsubscribe=api.dynamicAccess.subscribe(()=>void refresh())
  const onFocus=()=>void refresh();window.addEventListener('focus',onFocus)
  const interval=window.setInterval(()=>void refresh(),30000)
  return()=>{active=false;unsubscribe();window.removeEventListener('focus',onFocus);window.clearInterval(interval)}
 },[api,enabled,key])
 return enabled&&state.key===key&&state.allowed
}
