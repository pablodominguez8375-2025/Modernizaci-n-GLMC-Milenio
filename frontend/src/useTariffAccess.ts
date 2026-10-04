import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import type { TariffAccess } from './api/dynamicAccess'

export function useTariffAccess(api:PmgmApiClient){
 const subject=api.demoAccessSubject
 const [state,setState]=useState<{api:PmgmApiClient;subject:string;access:TariffAccess|null;error:string|null}|null>(null)
 useEffect(()=>{
  let active=true;let generation=0
  const refresh=async(invalidate=false)=>{
   const current=++generation
   if(invalidate)setState({api,subject,access:null,error:null})
   try{const access=await api.getTariffAccess();if(active&&generation===current)setState({api,subject,access,error:null})}
   catch(reason){if(active&&generation===current)setState({api,subject,access:null,error:reason instanceof Error?reason.message:'No se pudo comprobar el acceso al tarifario.'})}
  }
  void refresh()
  const unsubscribe=api.dynamicAccess.subscribe(()=>void refresh(true))
  const onFocus=()=>void refresh();window.addEventListener('focus',onFocus)
  const interval=window.setInterval(()=>void refresh(),30000)
  return()=>{active=false;unsubscribe();window.removeEventListener('focus',onFocus);window.clearInterval(interval)}
 },[api,subject])
 return state?.api===api&&state.subject===subject?state:{api,subject,access:null,error:null}
}
