import { useEffect, useState } from 'react'
import type { MembershipApiClient, OwnOffice, OwnAttendanceResponse, OwnAttendance } from './api/membershipApi'
const date = (s:string|null) => s ? s.split('-').reverse().join('-') : '—'
const message = (e:unknown) => e instanceof Error ? e.message : 'No fue posible consultar tu historial.'
export function OwnOffices({api}:{api:MembershipApiClient}) {
 const [result,setResult]=useState<{api:MembershipApiClient;items:OwnOffice[]}|null>(null),[error,setError]=useState<string|null>(null)
 useEffect(()=>{let active=true;setResult(null);setError(null);api.getOwnOffices().then(r=>{if(active)setResult({api,items:r.items})}).catch(e=>{if(active)setError(message(e))});return()=>{active=false}},[api])
 if(error)return <p role="alert">{error}</p>
 if(result?.api!==api)return <p role="status">Consultando tus cargos…</p>
 return result.items.length ? <ul>{result.items.map(x=><li style={{overflowWrap:"anywhere"}} key={x.id}><strong>{x.cargo}</strong> · {x.taller} · Período {x.periodo} · {date(x.desde)} a {date(x.hasta)}</li>)}</ul> : <p>No tienes cargos registrados.</p>
}
export function OwnAttendanceHistory({api}:{api:MembershipApiClient}) {
 const [tipo,setTipo]=useState<OwnAttendance['tipo']|''>(''),[desde,setDesde]=useState(''),[hasta,setHasta]=useState('')
 const key=JSON.stringify({tipo,desde,hasta});const [result,setResult]=useState<{api:MembershipApiClient;key:string;data:OwnAttendanceResponse}|null>(null),[error,setError]=useState<string|null>(null)
 useEffect(()=>{let active=true;setResult(null);setError(null);api.getOwnAttendance({...(tipo?{tipo}:{}),...(desde?{desde}:{}),...(hasta?{hasta}:{})}).then(data=>{if(active)setResult({api,key,data})}).catch(e=>{if(active)setError(message(e))});return()=>{active=false}},[api,key,tipo,desde,hasta])
 const data=result?.api===api&&result.key===key?result.data:null
 return <><div className="regularity-form"><label className="regularity-field"><span>Tipo de asistencia</span><select value={tipo} onChange={e=>setTipo(e.target.value as typeof tipo)}><option value="">Todos</option><option value="tenida">Tenidas</option><option value="ceremonia">Ceremonias</option><option value="instruccion">Instrucciones</option></select></label><label className="regularity-field"><span>Desde</span><input type="date" value={desde} onChange={e=>setDesde(e.target.value)}/></label><label className="regularity-field"><span>Hasta</span><input type="date" value={hasta} onChange={e=>setHasta(e.target.value)}/></label></div>{error?<p role="alert">{error}</p>:!data?<p role="status">Consultando tus asistencias…</p>:<><p>Período {date(data.desde)} a {date(data.hasta)} · Total {data.resumen.total} · Presentes {data.resumen.presente} · Justificadas {data.resumen.justificado} · Ausentes {data.resumen.ausente}</p>{data.items.length?<ul>{data.items.map(x=><li key={`${x.tipo}-${x.id}`}>{date(x.fecha)} · {x.tipo} · {x.tema??'Sin tema registrado'} · {x.taller} · <strong>{x.estado}</strong></li>)}</ul>:<p>No tienes asistencias en el período seleccionado.</p>}</>}</>
}
