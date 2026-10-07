import { useEffect,useState } from 'react'
import { ActionDrawer } from './actionKit'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AccountCandidates,AccountUser } from './api/userAccounts'

export default function UserAccountsPanel({api}:{api:PmgmApiClient}){
 const [data,setData]=useState<AccountCandidates|null>(null);const [users,setUsers]=useState<AccountUser[]>([])
 const [selection,setSelection]=useState('');const [platform,setPlatform]=useState(false);const [name,setName]=useState('');const [email,setEmail]=useState('')
 const [busy,setBusy]=useState(false);const [notice,setNotice]=useState('');const [error,setError]=useState('')
 const chosen=data?.items.find(x=>`${x.memberId}:${x.organizationId}`===selection)
 useEffect(()=>{let live=true;setData(null);setUsers([]);setPlatform(false);setSelection('');setError('')
  void Promise.all([api.getUserAccountCandidates(),api.getUserAccounts()]).then(([c,u])=>{if(live){setData(c);setUsers(u.items)}}).catch(e=>{if(live)setError(e instanceof Error?e.message:'No se pudo consultar el alta de usuarios.')})
  return()=>{live=false}
 },[api,api.demoAccessSubject])
 const create=async()=>{setBusy(true);setError('');setNotice('');try{
  const result=await api.createUserAccount(platform?{platformAdministrator:true,platformName:name,platformEmail:email}:{memberId:chosen?.memberId,organizationId:chosen?.organizationId})
  setSelection('');setName('');setEmail('')
  setNotice(api.useMocks?'Cuenta de demostración creada. El correo y el cambio de clave se simulan.':`Cuenta creada. La clave inicial temporal fue enviada al correo registrado ${result.email}; debe cambiarla al ingresar.`)
  try{setUsers((await api.getUserAccounts()).items)}catch{setError('La cuenta fue creada, pero no se pudo actualizar el listado. Recargue para consultarlo.')}
 }catch(e){setError(e instanceof Error?e.message:'No se completó el alta de usuario.')}finally{setBusy(false)}}
 return <section><p>Las cuentas institucionales se vinculan a un Hermano activo de un Taller. El correo se toma de su ficha y la clave inicial es aleatoria, con cambio obligatorio al primer ingreso.</p>
  {api.useMocks&&<p>Demostración con datos ficticios; no se envían correos ni se crean credenciales reales.</p>}
  {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
  {data&&!data.deliveryConfigured&&<p role="status">El envío de accesos no está configurado. Solicite su habilitación a la administración de plataforma.</p>}
  <div className="system-console-grid"><div className="action-bar"><ActionDrawer label="Crear usuario" title="Crear cuenta de acceso" description="Hermano activo, correo registrado y clave temporal." keepOpen>
   <form onSubmit={e=>{e.preventDefault();void create()}}>
    {data?.canCreatePlatformAdministrator&&<label><span>Tipo de cuenta</span><select value={platform?'platform':'member'} disabled={busy} onChange={e=>{setPlatform(e.target.value==='platform');setSelection('');setName('');setEmail('')}}><option value="member">Hermano de un Taller</option><option value="platform">Administrador de plataforma (excepción)</option></select></label>}
    {platform?<><p>Excepción exclusiva para administración técnica de la plataforma.</p><label><span>Nombre del administrador</span><input required maxLength={200} value={name} disabled={busy} onChange={e=>setName(e.target.value)}/></label><label><span>Correo del administrador</span><input required type="email" maxLength={320} value={email} disabled={busy} onChange={e=>setEmail(e.target.value)}/></label></>:<>
     <label><span>Hermano activo / Taller</span><select required value={selection} disabled={busy||!data} onChange={e=>setSelection(e.target.value)}><option value="">Seleccione un Hermano activo</option>{data?.items.map(c=><option key={`${c.memberId}:${c.organizationId}`} value={`${c.memberId}:${c.organizationId}`}>{c.name} · {c.workshop}</option>)}</select></label>
     <label><span>Correo registrado en la ficha</span><input readOnly type="email" value={chosen?.email??''}/></label><p>Si el correo falta o es incorrecto, actualice la ficha antes de crear la cuenta.</p>
    </>}
    <p>Los permisos técnicos y cargos se asignan mediante los flujos institucionales correspondientes.</p>
    <button disabled={busy||!data?.deliveryConfigured||(!platform&&!chosen)}>{busy?'Creando cuenta…':'Crear usuario y enviar clave inicial'}</button>
   </form></ActionDrawer></div><div><h3>Usuarios configurados</h3>{users.length===0&&<p>Sin cuentas creadas mediante este formulario.</p>}{users.map(u=><div className="system-user-row" key={u.subject}><strong>{u.name}</strong><span>{u.email}</span><small>{u.kind==='platform'?'Administrador de plataforma':'Hermano institucional'} · {u.enabled?'Habilitado':'Alta pendiente'}</small><small>Identificador de usuario: {u.subject}</small></div>)}</div></div>
 </section>
}
