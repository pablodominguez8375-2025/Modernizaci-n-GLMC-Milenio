// «Ver como» (#574): sesión de soporte de SOLO LECTURA. El servidor decide todo (sesión propia y vigente,
// solo lectura, solo pantallas permitidas); aquí solo se guarda la sesión activa y se adjunta la cabecera.
export interface SupportViewSession { sessionId:string;targetSubject:string;targetName:string;expiresAtUtc:string;demoProfile?:string }
export interface SupportViewStart { sessionId:string;targetSubject:string;targetName:string;startedAtUtc:string;expiresAtUtc:string;readOnly:true }

export const SUPPORT_VIEW_HEADER='X-Support-View-As'
const STORAGE_KEY='pmgm.supportViewAs'
const listeners=new Set<()=>void>()
let current:SupportViewSession|null=null
let snapshot:SupportViewSession|null=null

function load():SupportViewSession|null{
 try{
  const raw=sessionStorage.getItem(STORAGE_KEY);if(!raw)return null
  const parsed=JSON.parse(raw) as SupportViewSession
  return parsed&&typeof parsed.sessionId==='string'&&Date.parse(parsed.expiresAtUtc)>Date.now()?parsed:null
 }catch{return null}
}
current=load();snapshot=current

export function getSupportViewAs():SupportViewSession|null{
 if(current&&Date.parse(current.expiresAtUtc)<=Date.now())setSupportViewAs(null)
 return current
}
export function setSupportViewAs(next:SupportViewSession|null){
 current=next;snapshot=next
 try{if(next)sessionStorage.setItem(STORAGE_KEY,JSON.stringify(next));else sessionStorage.removeItem(STORAGE_KEY)}catch{/* sin almacenamiento: la sesión sigue en memoria */}
 listeners.forEach(l=>l())
}
export function subscribeSupportViewAs(listener:()=>void){listeners.add(listener);return()=>{listeners.delete(listener)}}
export function supportViewAsSnapshot(){return snapshot}

export function isViewAsEndpoint(path:string){return path.includes('/api/system/support/view-as')}

/** Agrega la cabecera a las llamadas /api mientras haya una sesión activa. Idempotente. */
export function installSupportViewAsFetch(target:typeof globalThis=globalThis){
 const marker='__pmgmSupportViewAs' as const
 const holder=target as unknown as Record<string,unknown>
 if(holder[marker])return
 const original=target.fetch.bind(target)
 target.fetch=((input:RequestInfo|URL,init?:RequestInit)=>{
  const session=getSupportViewAs()
  const url=typeof input==='string'?input:input instanceof URL?input.href:input.url
  if(session&&!session.demoProfile&&url.includes('/api/')&&!isViewAsEndpoint(url)){
   const headers=new Headers(init?.headers??(input instanceof Request?input.headers:undefined))
   headers.set(SUPPORT_VIEW_HEADER,session.sessionId)
   return original(input,{...init,headers})
  }
  return original(input,init)
 }) as typeof fetch
 holder[marker]=true
}

export function remainingSeconds(session:SupportViewSession,now=Date.now()){return Math.max(0,Math.floor((Date.parse(session.expiresAtUtc)-now)/1000))}
export function formatRemaining(seconds:number){const m=Math.floor(seconds/60),s=seconds%60;return `${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}`}
