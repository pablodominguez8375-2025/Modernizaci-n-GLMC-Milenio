import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { HospitalariaAccess } from './api/dynamicAccess'

export function useHospitalariaAccess(api:PmgmApiClient,organizationId:string){
 const subject=api.demoAccessSubject
 const key=`${subject}:${organizationId}`
 const [state,setState]=useState<{key:string;access:HospitalariaAccess|null;error:string|null}>({key:'',access:null,error:null})
 useEffect(()=>{
  let active=true;let generation=0
  const refresh=async()=>{const current=++generation;setState({key,access:null,error:null});try{const access=await api.getHospitalariaAccess(organizationId);if(active&&generation===current)setState({key,access,error:null})}catch(e){if(active&&generation===current)setState({key,access:null,error:e instanceof Error?e.message:'No se pudo comprobar el acceso.'})}}
  if(!organizationId)return
  void refresh()
  const unsubscribe=api.dynamicAccess.subscribe(()=>void refresh())
  const onFocus=()=>void refresh();window.addEventListener('focus',onFocus)
  const interval=window.setInterval(()=>void refresh(),30000)
  return()=>{active=false;unsubscribe();window.removeEventListener('focus',onFocus);window.clearInterval(interval)}
 },[api,organizationId,key])
 return state.key===key?state:{key,access:null,error:null}
}

