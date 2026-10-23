import { useEffect,useState } from 'react'
import { ActionDrawer } from './actionKit'
import type { PmgmApiClient } from './api/pmgmApi'
import type { AccessResetModality,AccessResetResult,AccountCandidates,AccountUser } from './api/userAccounts'

function ResetAccessForm({api,user,onDone}:{api:PmgmApiClient;user:AccountUser;onDone:()=>void}){
 const hasEmail=user.email.includes('@');const [modality,setModality]=useState<AccessResetModality>(hasEmail?'email':'temporary');const [reason,setReason]=useState('')
 const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [result,setResult]=useState<AccessResetResult|null>(null)
 const submit=async()=>{setBusy(true);setError('');try{setResult(await api.resetUserAccountAccess(user.subject,{modality,reason:reason.trim()||undefined}))}catch(e){setError(e instanceof Error?e.message:'No se pudo restablecer el acceso.')}finally{setBusy(false)}}
 if(result?.modality==='email')return <div role="status"><p><strong>Enlace enviado.</strong> {api.useMocks?'En esta demostración el correo se simula.':`Se envió a ${result.email}.`} El enlace es de un solo uso y vence en {result.expiresInMinutes} minutos. Se cerraron las sesiones activas de la cuenta.</p><button type="button" onClick={onDone}>Listo</button></div>
 if(result?.temporaryPassword)return <div role="status"><p><strong>Clave temporal de {result.name}</strong></p><p className="system-temp-password" aria-label="Clave temporal">{result.temporaryPassword}</p>
  <p>Esta clave <strong>se muestra una sola vez</strong>: no queda guardada y no se podrá volver a ver. Entréguela en forma segura al Hermano; deberá cambiarla en su primer ingreso. Se cerraron sus sesiones activas.</p>
  <button type="button" onClick={()=>void navigator.clipboard?.writeText(result.temporaryPassword!)}>Copiar clave</button> <button type="button" onClick={onDone}>Ya la entregué, cerrar</button></div>
 return <form onSubmit={e=>{e.preventDefault();void submit()}}>
  <p>Cuenta de <strong>{user.name}</strong>. Se cerrarán sus sesiones activas y la acción queda en la auditoría.</p>
  {error&&<p role="alert">{error}</p>}
  <fieldset className="system-reset-choice"><legend>¿Cómo desea restablecer el acceso?</legend>
   <label><input type="radio" name="modality" checked={modality==='email'} disabled={!hasEmail||busy} onChange={()=>setModality('email')}/> <span><strong>Enviar un enlace al correo</strong><small>{hasEmail?`Recomendado. Llega a ${user.email}, es de un solo uso y vence en 30 minutos.`:'No disponible: la cuenta no tiene correo.'}</small></span></label>
   <label><input type="radio" name="modality" checked={modality==='temporary'} disabled={busy} onChange={()=>setModality('temporary')}/> <span><strong>Entregar una clave temporal</strong><small>Para quien no recibe correo. Se muestra una sola vez y debe cambiarse al ingresar.</small></span></label>
  </fieldset>
  {modality==='temporary'&&<label><span>Motivo (10 a 200 caracteres)</span><input required minLength={10} maxLength={200} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} placeholder="Ej.: Hermano sin correo, identidad verificada en persona"/></label>}
  <button disabled={busy}>{busy?'Restableciendo…':modality==='email'?'Enviar enlace de acceso':'Generar clave temporal'}</button>
 </form>
}
function ResetAccessDrawer({api,user}:{api:PmgmApiClient;user:AccountUser}){
 const [open,setOpen]=useState(false)
 return <ActionDrawer label="Restablecer acceso" title="Restablecer acceso" description="El Hermano recupera su ingreso sin que nadie conozca su clave." tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
  {open&&<ResetAccessForm api={api} user={user} onDone={()=>setOpen(false)}/>}
 </ActionDrawer>
}

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
  const result=await api.createUserAccount(platform?{superuser:true,platformName:name,platformEmail:email}:{memberId:chosen?.memberId,organizationId:chosen?.organizationId})
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
    {data?.canCreateSuperuser&&<label><span>Tipo de cuenta</span><select value={platform?'platform':'member'} disabled={busy} onChange={e=>{setPlatform(e.target.value==='platform');setSelection('');setName('');setEmail('')}}><option value="member">Usuario (Hermano de un Taller)</option><option value="platform">Superusuario de soporte</option></select></label>}
    {platform?<><p>El Superusuario es una cuenta de soporte con perfiles limitados, sin datos logiales. Solo existe un Máster y no se puede crear otro.</p><label><span>Nombre del superusuario</span><input required maxLength={200} value={name} disabled={busy} onChange={e=>setName(e.target.value)}/></label><label><span>Correo del superusuario</span><input required type="email" maxLength={320} value={email} disabled={busy} onChange={e=>setEmail(e.target.value)}/></label></>:<>
     <label><span>Hermano activo / Taller</span><select required value={selection} disabled={busy||!data} onChange={e=>setSelection(e.target.value)}><option value="">Seleccione un Hermano activo</option>{data?.items.map(c=><option key={`${c.memberId}:${c.organizationId}`} value={`${c.memberId}:${c.organizationId}`}>{c.name} · {c.workshop}</option>)}</select></label>
     <label><span>Correo registrado en la ficha</span><input readOnly type="email" value={chosen?.email??''}/></label><p>Si el correo falta o es incorrecto, actualice la ficha antes de crear la cuenta.</p>
    </>}
    <p>Los permisos técnicos y cargos se asignan mediante los flujos institucionales correspondientes.</p>
    <button disabled={busy||!data?.deliveryConfigured||(!platform&&!chosen)}>{busy?'Creando cuenta…':'Crear usuario y enviar clave inicial'}</button>
   </form></ActionDrawer></div><div><h3>Usuarios configurados</h3>{users.length===0&&<p>Sin cuentas creadas mediante este formulario.</p>}{users.map(u=><div className="system-user-row" key={u.subject}><strong>{u.name}</strong><span>{u.email}</span><small>{u.kind==='platform'?'Máster de plataforma':u.kind==='superuser'?'Superusuario de soporte':'Hermano institucional'} · {u.enabled?'Habilitado':'Alta pendiente'}</small><small>Identificador de usuario: {u.subject}</small>{data?.canCreateUser&&u.enabled&&u.kind!=='platform'&&<div className="action-bar"><ResetAccessDrawer api={api} user={u}/></div>}</div>)}</div></div>
 </section>
}
