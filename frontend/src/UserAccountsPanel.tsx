import { useEffect,useState } from 'react'
import { ActionDrawer } from './actionKit'
import type { PmgmApiClient } from './api/pmgmApi'
import { setSupportViewAs } from './api/supportViewAs'
import { SUPPORT_CORRECTION_DURATIONS,type AccessResetModality,type AccessResetResult,type AccountCandidates,type AccountUser,type SupportCorrectionFieldKey,type SupportCorrectionGrant,type SupportCorrectionStatus,type SupportNotice } from './api/userAccounts'

/** Never bypass server-side delivery requirements. Expose a safe operator-facing reason, never secrets. */
export function accountCreationBlockReason(data:AccountCandidates|null):string|null{
 if(!data)return 'No se ha podido verificar el estado del servicio de altas. Consulte al administrador técnico antes de intentar crear una cuenta.'
 if(data.deliveryConfigured)return null
 if(data.identityConfigured===false&&data.mailConfigured===false)
  return 'La conexión de altas con el proveedor de identidad y el envío del correo inicial requieren configuración en el servidor. Compruebe Keycloak, SMTP y la URL HTTPS de ingreso.'
 if(data.identityConfigured===false)
  return 'La conexión administrativa para crear cuentas en el proveedor de identidad no está configurada. Verifique su integración con Keycloak.'
 if(data.mailConfigured===false)
  return 'Falta configurar el envío de claves iniciales o la URL HTTPS de ingreso en el servidor. El correo institucional general puede funcionar sin que esta función esté habilitada.'
 return 'El servidor indica que el alta de cuentas no está disponible. Verifique la configuración de identidad, correo inicial y URL HTTPS de ingreso.'
}
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

function ViewAsForm({api,user,onDone}:{api:PmgmApiClient;user:AccountUser;onDone:()=>void}){
 const [reason,setReason]=useState('');const [busy,setBusy]=useState(false);const [error,setError]=useState('')
 const submit=async()=>{setBusy(true);setError('');try{
  const started=await api.startSupportView({targetSubject:user.subject,reason:reason.trim()})
  setSupportViewAs({sessionId:started.sessionId,targetSubject:started.targetSubject,targetName:started.targetName,expiresAtUtc:started.expiresAtUtc,demoProfile:api.useMocks?user.subject.slice(5):undefined})
  onDone();if(!api.useMocks)window.location.reload()
 }catch(e){setError(e instanceof Error?e.message:'No se pudo iniciar «Ver como».')}finally{setBusy(false)}}
 return <form onSubmit={e=>{e.preventDefault();void submit()}}>
  <p>Verá la plataforma como la ve <strong>{user.name}</strong>, <strong>solo para mirar</strong>: no podrá cambiar nada, no verá su RUT, contacto ni pagos, y no conocerá su clave.</p>
  <p>Dura 30 minutos como máximo. Queda registrado quién miró, a quién y por qué.</p>
  {error&&<p role="alert">{error}</p>}
  <label><span>Motivo (10 a 500 caracteres)</span><input required minLength={10} maxLength={500} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} placeholder="Ej.: El Hermano no ve su Taller en el menú"/></label>
  <button disabled={busy||reason.trim().length<10}>{busy?'Iniciando…':'Ver como este Hermano'}</button>
 </form>
}
function ViewAsDrawer({api,user}:{api:PmgmApiClient;user:AccountUser}){
 const [open,setOpen]=useState(false)
 return <ActionDrawer label="Ver como" title="Ver como este Hermano" description="Apoyo de soporte en solo lectura, con motivo y auditoría." tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
  {open&&<ViewAsForm api={api} user={user} onDone={()=>setOpen(false)}/>}
 </ActionDrawer>
}
const clock=(iso:string)=>new Date(iso).toLocaleTimeString('es-CL',{hour:'2-digit',minute:'2-digit',timeZone:'America/Santiago'})

/** El Máster da al Superusuario un permiso con vigencia para corregir datos de contacto. */
function GrantCorrectionForm({api,user,onDone}:{api:PmgmApiClient;user:AccountUser;onDone:()=>void}){
 const [reason,setReason]=useState('');const [minutes,setMinutes]=useState(60);const [busy,setBusy]=useState(false);const [error,setError]=useState('')
 const submit=async()=>{setBusy(true);setError('');try{await api.grantSupportCorrection({operatorSubject:user.subject,reason:reason.trim(),minutes});onDone()}catch(e){setError(e instanceof Error?e.message:'No se pudo dar el permiso.')}finally{setBusy(false)}}
 return <form onSubmit={e=>{e.preventDefault();void submit()}}>
  <p>Con este permiso, <strong>{user.name}</strong> podrá corregir el <strong>correo, teléfono o domicilio</strong> de un Hermano y reenviarle un aviso ya emitido, uno por uno y siempre con motivo. No alcanza a Tesorería, Hospitalaria, firmas ni claves.</p>
  <p>Cada corrección queda registrada con su nombre y el Hermano la verá en «Privacidad y mis datos». El permiso se acaba solo al vencer.</p>
  {error&&<p role="alert">{error}</p>}
  <label><span>Vigencia</span><select value={minutes} disabled={busy} onChange={e=>setMinutes(Number(e.target.value))}>{SUPPORT_CORRECTION_DURATIONS.map(d=><option key={d.minutes} value={d.minutes}>{d.label}</option>)}</select></label>
  <label><span>Motivo (10 a 500 caracteres)</span><input required minLength={10} maxLength={500} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} placeholder="Ej.: Corregir contactos reportados esta semana"/></label>
  <button disabled={busy||reason.trim().length<10}>{busy?'Dando permiso…':'Dar permiso para corregir'}</button>
 </form>
}
function GrantCorrectionDrawer({api,user,onChanged}:{api:PmgmApiClient;user:AccountUser;onChanged:()=>void}){
 const [open,setOpen]=useState(false)
 return <ActionDrawer label="Permiso para corregir datos" title="Permiso para corregir datos" description="Lo da solo el Máster, por un tiempo limitado." tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
  {open&&<GrantCorrectionForm api={api} user={user} onDone={()=>{setOpen(false);onChanged()}}/>}
 </ActionDrawer>
}

/** El Superusuario con permiso vigente corrige UN dato de contacto, con motivo. */
function CorrectContactForm({api,user,status,onDone}:{api:PmgmApiClient;user:AccountUser;status:SupportCorrectionStatus;onDone:()=>void}){
 const [field,setField]=useState<SupportCorrectionFieldKey>('phone');const [value,setValue]=useState('');const [reason,setReason]=useState('')
 const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [done,setDone]=useState('')
 const submit=async()=>{setBusy(true);setError('');try{const r=await api.correctAsSupport({targetSubject:user.subject,field,newValue:value.trim(),reason:reason.trim()});setDone(r.label??'Dato')}catch(e){setError(e instanceof Error?e.message:'No se pudo corregir el dato.')}finally{setBusy(false)}}
 if(done)return <div role="status"><p><strong>{done} corregido.</strong> Quedó registrado a su nombre y {user.name} lo verá en «Privacidad y mis datos».</p><button type="button" onClick={onDone}>Listo</button></div>
 return <form onSubmit={e=>{e.preventDefault();void submit()}}>
  <p>Corrige un dato de contacto de <strong>{user.name}</strong>. Su permiso vence a las <strong>{clock(status.grants[0].expiresAtUtc)}</strong>.</p>
  {error&&<p role="alert">{error}</p>}
  <label><span>¿Qué dato corrige?</span><select value={field} disabled={busy} onChange={e=>setField(e.target.value as SupportCorrectionFieldKey)}>{status.fields.map(f=><option key={f.field} value={f.field}>{f.label}</option>)}</select></label>
  <label><span>Dato correcto</span><input required maxLength={field==='address'?500:field==='phone'?80:320} type={field==='email'?'email':'text'} value={value} disabled={busy} onChange={e=>setValue(e.target.value)}/></label>
  <label><span>Motivo (10 a 500 caracteres)</span><input required minLength={10} maxLength={500} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} placeholder="Ej.: El Hermano informó que su teléfono estaba mal digitado"/></label>
  <p>El motivo y su nombre quedan en la auditoría. El valor anterior no se guarda.</p>
  <button disabled={busy||reason.trim().length<10||!value.trim()}>{busy?'Corrigiendo…':'Corregir dato'}</button>
 </form>
}
function CorrectContactDrawer({api,user,status}:{api:PmgmApiClient;user:AccountUser;status:SupportCorrectionStatus}){
 const [open,setOpen]=useState(false)
 return <ActionDrawer label="Corregir contacto" title="Corregir un dato de contacto" description="Solo con permiso vigente del Máster; queda registrado a su nombre." tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
  {open&&<CorrectContactForm api={api} user={user} status={status} onDone={()=>setOpen(false)}/>}
 </ActionDrawer>
}
/** Reenvía al MISMO correo registrado un aviso ya emitido; no cambia contenido ni destino. */
function ResendNoticeForm({api,user,onDone}:{api:PmgmApiClient;user:AccountUser;onDone:()=>void}){
 const [notices,setNotices]=useState<SupportNotice[]|null>(null);const [messageId,setMessageId]=useState('');const [reason,setReason]=useState('')
 const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [done,setDone]=useState(false)
 useEffect(()=>{let live=true;void api.getSupportNotices(user.subject).then(n=>{if(live){setNotices(n);setMessageId(n[0]?.messageId??'')}}).catch(e=>{if(live){setNotices([]);setError(e instanceof Error?e.message:'No se pudieron consultar los avisos.')}});return()=>{live=false}},[api,user.subject])
 const submit=async()=>{setBusy(true);setError('');try{await api.resendSupportNotice({targetSubject:user.subject,messageId,reason:reason.trim()});setDone(true)}catch(e){setError(e instanceof Error?e.message:'No se pudo reenviar el aviso.')}finally{setBusy(false)}}
 if(done)return <div role="status"><p><strong>Aviso reenviado.</strong> Saldrá al correo registrado de {user.name}. Quedó anotado a su nombre y él lo verá en «Privacidad y mis datos».</p><button type="button" onClick={onDone}>Listo</button></div>
 const status:Record<string,string>={delivered:'entregado',failed:'falló el envío',queued:'en cola',dead_letter:'no se pudo entregar'}
 return <form onSubmit={e=>{e.preventDefault();void submit()}}>
  <p>Reenvía un aviso ya emitido a <strong>{user.name}</strong>, <strong>al mismo correo registrado</strong>. No se puede cambiar el contenido ni el destino, y los avisos reservados no se reenvían.</p>
  {error&&<p role="alert">{error}</p>}
  {notices&&notices.length===0&&!error&&<p>No hay avisos recientes que se puedan reenviar.</p>}
  {notices&&notices.length>0&&<><label><span>Aviso</span><select value={messageId} disabled={busy} onChange={e=>setMessageId(e.target.value)}>{notices.map(n=><option key={n.messageId} value={n.messageId}>{n.subject} · {new Date(n.createdAtUtc).toLocaleDateString('es-CL',{day:'2-digit',month:'short',timeZone:'America/Santiago'})}{n.emailStatus?` · ${status[n.emailStatus]??n.emailStatus}`:''}</option>)}</select></label>
  <label><span>Motivo (10 a 500 caracteres)</span><input required minLength={10} maxLength={500} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} placeholder="Ej.: El Hermano no recibió el correo"/></label>
  <p>Un mismo aviso no se reenvía más de una vez cada 10 minutos. Queda en la auditoría a su nombre.</p>
  <button disabled={busy||reason.trim().length<10||!messageId}>{busy?'Reenviando…':'Reenviar aviso'}</button></>}
 </form>
}
function ResendNoticeDrawer({api,user}:{api:PmgmApiClient;user:AccountUser}){
 const [open,setOpen]=useState(false)
 return <ActionDrawer label="Reenviar un aviso" title="Reenviar un aviso" description="Al mismo correo registrado, con motivo y a su nombre." tone="secondary" open={open} onOpenChange={setOpen} keepOpen>
  {open&&<ResendNoticeForm api={api} user={user} onDone={()=>setOpen(false)}/>}
 </ActionDrawer>
}
function CorrectionGrantLine({api,grant,onChanged}:{api:PmgmApiClient;grant:SupportCorrectionGrant;onChanged:()=>void}){
 const [busy,setBusy]=useState(false)
 return <small>Permiso para corregir datos hasta las {clock(grant.expiresAtUtc)} · <button type="button" className="secondary" disabled={busy} onClick={()=>{setBusy(true);void api.revokeSupportCorrection(grant.grantId).then(onChanged).finally(()=>setBusy(false))}}>Retirar permiso</button></small>
}

export default function UserAccountsPanel({api}:{api:PmgmApiClient}){
 const [data,setData]=useState<AccountCandidates|null>(null);const [users,setUsers]=useState<AccountUser[]>([])
 const [selection,setSelection]=useState('');const [platform,setPlatform]=useState(false);const [name,setName]=useState('');const [email,setEmail]=useState('')
 const [busy,setBusy]=useState(false);const [notice,setNotice]=useState('');const [error,setError]=useState('');const [blockedAttempt,setBlockedAttempt]=useState(false)
 const [corr,setCorr]=useState<SupportCorrectionStatus|null>(null)
 const refreshCorrections=()=>{void api.getSupportCorrectionStatus().then(setCorr).catch(()=>setCorr(null))}
 const chosen=data?.items.find(x=>`${x.memberId}:${x.organizationId}`===selection)
 const creationBlockReason=accountCreationBlockReason(data)
 useEffect(()=>{let live=true;setData(null);setUsers([]);setPlatform(false);setSelection('');setError('')
  void Promise.all([api.getUserAccountCandidates(),api.getUserAccounts()]).then(([c,u])=>{if(live){setData(c);setUsers(u.items)}}).catch(e=>{if(live)setError(e instanceof Error?e.message:'No se pudo consultar el alta de usuarios.')})
  setCorr(null);void api.getSupportCorrectionStatus().then(c=>{if(live)setCorr(c)}).catch(()=>{if(live)setCorr(null)})
  return()=>{live=false}
 },[api,api.demoAccessSubject])
 const create=async()=>{
  if(creationBlockReason){setBlockedAttempt(true);return}
  setBlockedAttempt(false);setBusy(true);setError('');setNotice('');try{
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
    {creationBlockReason&&<p role="alert" id="user-account-create-blocked"><strong>No se puede crear la cuenta todavía.</strong> {creationBlockReason}</p>}
    {blockedAttempt&&creationBlockReason&&<p role="status">No se envió ninguna solicitud ni se creó una cuenta. Cuando el servidor esté configurado, vuelva a abrir el formulario.</p>}
    {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
    {data?.canCreateSuperuser&&<label><span>Tipo de cuenta</span><select value={platform?'platform':'member'} disabled={busy} onChange={e=>{setPlatform(e.target.value==='platform');setSelection('');setName('');setEmail('')}}><option value="member">Usuario (Hermano de un Taller)</option><option value="platform">Superusuario de soporte</option></select></label>}
    {platform?<><p>El Superusuario es una cuenta de soporte con perfiles limitados, sin datos logiales. Solo existe un Máster y no se puede crear otro.</p><label><span>Nombre del superusuario</span><input required maxLength={200} value={name} disabled={busy} onChange={e=>setName(e.target.value)}/></label><label><span>Correo del superusuario</span><input required type="email" maxLength={320} value={email} disabled={busy} onChange={e=>setEmail(e.target.value)}/></label></>:<>
     <label><span>Hermano activo / Taller</span><select required value={selection} disabled={busy||!data} onChange={e=>setSelection(e.target.value)}><option value="">Seleccione un Hermano activo</option>{data?.items.map(c=><option key={`${c.memberId}:${c.organizationId}`} value={`${c.memberId}:${c.organizationId}`}>{c.name} · {c.workshop}</option>)}</select></label>
     <label><span>Correo registrado en la ficha</span><input readOnly type="email" value={chosen?.email??''}/></label><p>Si el correo falta o es incorrecto, actualice la ficha antes de crear la cuenta.</p>
    </>}
    <p>Los permisos técnicos y cargos se asignan mediante los flujos institucionales correspondientes.</p>
    <button type={creationBlockReason?'button':'submit'} disabled={busy||(!creationBlockReason&&!platform&&!chosen)}
     onClick={creationBlockReason?()=>setBlockedAttempt(true):undefined}
     aria-describedby={creationBlockReason?'user-account-create-blocked':undefined}>
     {busy?'Creando cuenta…':creationBlockReason?'Ver por qué no se puede crear la cuenta':'Crear usuario y enviar clave inicial'}
    </button>
   </form></ActionDrawer></div><div><h3>Usuarios configurados</h3>{users.length===0&&<p>Sin cuentas creadas mediante este formulario.</p>}{users.map(u=><div className="system-user-row" key={u.subject}><strong>{u.name}</strong><span>{u.email}</span><small>{u.kind==='platform'?'Máster de plataforma':u.kind==='superuser'?'Superusuario de soporte':'Hermano institucional'} · {u.enabled?'Habilitado':'Alta pendiente'}</small><small>Identificador de usuario: {u.subject}</small>{data?.canCreateUser&&u.enabled&&u.kind!=='platform'&&<div className="action-bar"><ResetAccessDrawer api={api} user={u}/>{u.kind==='member'&&<ViewAsDrawer api={api} user={u}/>}{corr?.isMaster&&u.kind==='superuser'&&<GrantCorrectionDrawer api={api} user={u} onChanged={refreshCorrections}/>}</div>}{corr?.isMaster&&u.kind==='superuser'&&corr.grants.filter(g=>g.operatorSubject===u.subject).map(g=><CorrectionGrantLine key={g.grantId} api={api} grant={g} onChanged={refreshCorrections}/>)}{corr?.canCorrect&&u.kind==='member'&&u.enabled&&<div className="action-bar"><CorrectContactDrawer api={api} user={u} status={corr}/><ResendNoticeDrawer api={api} user={u}/></div>}</div>)}</div></div>
 </section>
}
