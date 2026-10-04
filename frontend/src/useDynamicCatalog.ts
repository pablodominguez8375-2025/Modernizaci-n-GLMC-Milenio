import { useEffect, useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
export function useDynamicCatalog(api:PmgmApiClient){
 const [catalog,setCatalog]=useState(()=>api.dynamicAccess.snapshot())
 const [error,setError]=useState<string|null>(null)
 useEffect(()=>{let active=true;const unsubscribe=api.dynamicAccess.subscribe(()=>{if(active)setCatalog(api.dynamicAccess.snapshot())});void api.dynamicAccess.load().catch(e=>{if(active)setError(e instanceof Error?e.message:'No se pudo leer el catálogo.')});return()=>{active=false;unsubscribe()}},[api])
 return {catalog,error,reload:()=>api.dynamicAccess.load()}
}
