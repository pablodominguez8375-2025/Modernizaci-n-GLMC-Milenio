import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { GrandHospitalariaAccess } from './api/dynamicAccess'

export function useGrandHospitalariaAccess(api:PmgmApiClient,enabled:boolean){
 const subject=api.demoAccessSubject
 const key=`${subject}:${enabled}`
 const [state,setState]=useState<{api:PmgmApiClient;key:string;access:GrandHospitalariaAccess|null;error:string|null}>({api,key:'',access:null,error:null})
 useEffect(()=>{
  let active=true;let generation=0
  const refresh=async()=>{const current=++generation;setState({api,key,access:null,error:null});try{const access=await api.getGrandHospitalariaAccess();if(active&&generation===current)setState({api,key,access,error:null})}catch(e){if(active&&generation===current)setState({api,key,access:null,error:e instanceof Error?e.message:'No se pudo comprobar el acceso.'})}}
  if(!enabled)return
  void refresh()
  const unsubscribe=api.dynamicAccess.subscribe(()=>void refresh())
  const onFocus=()=>void refresh();window.addEventListener('focus',onFocus)
  const interval=window.setInterval(()=>void refresh(),30000)
  return()=>{active=false;unsubscribe();window.removeEventListener('focus',onFocus);window.clearInterval(interval)}
 },[api,enabled,key])
 return state.api===api&&state.key===key?state:{key,access:null,error:null}
}
