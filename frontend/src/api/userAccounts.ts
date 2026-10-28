import { SUPPORT_VIEW_PROFILES,profileDisplayName } from './supportViewProfiles'
export interface AccountCandidate { memberId:string;organizationId:string;name:string;email:string;workshop:string }
export interface AccountCandidates { items:AccountCandidate[];canCreateUser?:boolean;canCreateSuperuser:boolean;deliveryConfigured:boolean;identityConfigured?:boolean;mailConfigured?:boolean }
export interface AccountUser { subject:string;name:string;email:string;enabled:boolean;kind:'member'|'superuser'|'platform' }
/** Inventario completo de cuentas (solo el Máster único; solo lectura). `protected`: cuenta preexistente que no se activa, desactiva ni elimina desde el sistema. */
export interface AccountInventoryItem { subject:string;username:string;name:string;enabled:boolean;kind:'legacy_protected'|'member'|'superuser'|string;managed:boolean;protected:boolean;origin:'provisioned'|'managed'|string }
export interface CreateUserAccount { memberId?:string|null;organizationId?:string|null;superuser?:boolean;platformName?:string|null;platformEmail?:string|null }
export interface UserAccountCreated { subject:string;email:string;memberId:string|null;organizationId:string|null;superuser:boolean;requiresPasswordChange:true;initialPasswordEmailSent:boolean }

export type SupportCorrectionFieldKey='email'|'phone'|'address'
export interface SupportCorrectionField { field:SupportCorrectionFieldKey;label:string }
export interface SupportCorrectionGrant { grantId:string;operatorSubject:string;operatorName:string;reason:string;expiresAtUtc:string }
export interface SupportCorrectionStatus { isMaster:boolean;canCorrect:boolean;grants:SupportCorrectionGrant[];maxHours:number;fields:SupportCorrectionField[] }
export interface SupportCorrectionGrantRequest { operatorSubject:string;reason:string;minutes:number }
export interface SupportCorrectionRequest { targetSubject:string;field:SupportCorrectionFieldKey;newValue:string;reason:string }
export interface MySupportCorrection { id:string;field:string;label:string;detail?:string|null;operatorName:string;reason:string;createdAtUtc:string }
export interface SupportNotice { messageId:string;typeCode:string;subject:string;createdAtUtc:string;emailStatus?:string|null }
export interface SupportResendRequest { targetSubject:string;messageId:string;reason:string }
export const SUPPORT_CORRECTION_FIELDS:SupportCorrectionField[]=[{field:'email',label:'Correo electrónico'},{field:'phone',label:'Teléfono'},{field:'address',label:'Domicilio'}]
export const SUPPORT_CORRECTION_DURATIONS=[{minutes:30,label:'30 minutos'},{minutes:60,label:'1 hora'},{minutes:120,label:'2 horas'},{minutes:240,label:'4 horas'}]

export interface SupportViewStartRequest { targetSubject?:string;reason?:string;profileKey?:string }
export type AccessResetModality='email'|'temporary'
export interface AccessResetRequest { modality:AccessResetModality;reason?:string }
export interface AccessResetResult { subject:string;name:string;modality:AccessResetModality;emailSent?:boolean;email?:string;expiresInMinutes?:number;temporaryPassword?:string;requiresPasswordChange?:boolean;shownOnce?:boolean }

// Synthetic institutional records only. Identity and email delivery are simulated on Pages.
export class UserAccountsDemo {
 private users:AccountUser[]
 private grants:SupportCorrectionGrant[]=[]
 private corrections:Array<MySupportCorrection&{targetSubject:string}>=[]
 constructor(private readonly candidates:AccountCandidate[],private readonly allowed:()=>boolean,private readonly platformAllowed:()=>boolean,seed:AccountUser[]=[],private readonly currentSubject:()=>string=()=>'',private readonly operatorAllowed:()=>boolean=()=>false){this.users=structuredClone(seed)}
 // Inventario sintético de la demostración (nunca cuentas reales): solo para el Máster registrado.
 inventory():{items:AccountInventoryItem[];configured:boolean}{
  this.authorize();if(!this.platformAllowed())throw new Error('Solo el Máster único puede ver el inventario completo de cuentas.')
  const legacy:AccountInventoryItem[]=[
   {subject:'demo:legacy-qa-admin',username:'qa.admin',name:'Administrador de pruebas (ficticio)',enabled:true,kind:'legacy_protected',managed:false,protected:true,origin:'provisioned'},
   {subject:'demo:legacy-admin',username:'admin',name:'Máster de plataforma (ficticio)',enabled:true,kind:'legacy_protected',managed:false,protected:true,origin:'provisioned'},
   {subject:'demo:legacy-bootstrap',username:'bootstrap.inicial',name:'Cuenta de carga inicial (ficticia)',enabled:false,kind:'legacy_protected',managed:false,protected:true,origin:'provisioned'},
  ]
  const managed:AccountInventoryItem[]=this.users.filter(u=>u.kind==='member'||u.kind==='superuser').map(u=>({subject:u.subject,username:u.email.split('@')[0]||'usuario',name:u.name,enabled:u.enabled,kind:u.kind,managed:true,protected:false,origin:'managed'}))
  return{items:[...legacy,...managed],configured:true}
 }
 // «Corregir como soporte»: el Máster da el permiso; el Superusuario corrige; el Hermano ve la corrección.
 correctionStatus():SupportCorrectionStatus{
  this.authorize();const now=Date.now();const me=this.currentSubject();const master=this.platformAllowed()
  const active=this.grants.filter(g=>Date.parse(g.expiresAtUtc)>now&&(master||g.operatorSubject===me))
  return{isMaster:master,canCorrect:!master&&this.operatorAllowed()&&active.length>0,grants:structuredClone(active),maxHours:4,fields:SUPPORT_CORRECTION_FIELDS}
 }
 grantCorrection(request:SupportCorrectionGrantRequest){
  this.authorize();if(!this.platformAllowed())throw new Error('Solo el Máster registrado puede dar este permiso.')
  if(request.reason.trim().length<10||request.reason.trim().length>500)throw new Error('Indique el motivo (10 a 500 caracteres).')
  if(request.minutes<5||request.minutes>240)throw new Error('La vigencia debe estar entre 5 minutos y 4 horas.')
  const op=this.users.find(u=>u.subject===request.operatorSubject&&u.enabled&&u.kind==='superuser');if(!op)throw new Error('El permiso solo se da a un Superusuario de soporte habilitado.')
  this.grants=this.grants.filter(g=>g.operatorSubject!==op.subject)
  const grant={grantId:crypto.randomUUID(),operatorSubject:op.subject,operatorName:op.name,reason:request.reason.trim(),expiresAtUtc:new Date(Date.now()+request.minutes*60000).toISOString()}
  this.grants.push(grant);return structuredClone(grant)
 }
 revokeCorrectionGrant(grantId:string){this.authorize();if(!this.platformAllowed())throw new Error('Solo el Máster registrado puede retirar este permiso.');this.grants=this.grants.filter(g=>g.grantId!==grantId)}
 correct(request:SupportCorrectionRequest){
  this.authorize();const st=this.correctionStatus();if(!st.canCorrect)throw new Error('No tiene un permiso vigente del Máster para corregir datos.')
  if(!SUPPORT_CORRECTION_FIELDS.some(f=>f.field===request.field))throw new Error('Solo se puede corregir correo, teléfono o domicilio.')
  if(request.reason.trim().length<10||request.reason.trim().length>500)throw new Error('Indique el motivo (10 a 500 caracteres).')
  if(request.field==='email'&&!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(request.newValue.trim()))throw new Error('El correo electrónico no tiene un formato válido.')
  if(!request.newValue.trim())throw new Error('Escriba el dato correcto.')
  const user=this.users.find(u=>u.subject===request.targetSubject&&u.enabled);if(!user||user.kind!=='member')throw new Error('Solo se corrigen datos de Hermanos con cuenta habilitada, no de cuentas de administración.')
  const op=st.grants[0];const label=SUPPORT_CORRECTION_FIELDS.find(f=>f.field===request.field)!.label
  this.corrections.unshift({id:crypto.randomUUID(),field:request.field,label,operatorName:op.operatorName,reason:request.reason.trim(),createdAtUtc:new Date().toISOString(),targetSubject:user.subject})
  return{status:'corrected' as const,field:request.field,label}
 }
 private notices:Record<string,SupportNotice[]>={'demo:brother':[{messageId:'demo-notice-1',typeCode:'candidate.interview.assigned',subject:'Entrevista asignada',createdAtUtc:'2026-10-20T14:00:00Z',emailStatus:'delivered'},{messageId:'demo-notice-2',typeCode:'grandarchive.official.published',subject:'Nuevo documento oficial publicado',createdAtUtc:'2026-10-22T16:30:00Z',emailStatus:'failed'}]}
 listNotices(targetSubject:string):SupportNotice[]{if(!this.correctionStatus().canCorrect)throw new Error('No tiene un permiso vigente del Máster para corregir datos.');return structuredClone(this.notices[targetSubject]??[])}
 resendNotice(request:SupportResendRequest){
  this.authorize();const st=this.correctionStatus();if(!st.canCorrect)throw new Error('No tiene un permiso vigente del Máster para corregir datos.')
  if(request.reason.trim().length<10||request.reason.trim().length>500)throw new Error('Indique el motivo (10 a 500 caracteres).')
  const notice=(this.notices[request.targetSubject]??[]).find(n=>n.messageId===request.messageId);if(!notice)throw new Error('El aviso no existe o no es de este Hermano.')
  if(this.corrections.some(c=>c.field==='resend_notice'&&c.detail===notice.subject&&c.targetSubject===request.targetSubject&&Date.now()-Date.parse(c.createdAtUtc)<600000))throw new Error('Este aviso se envió hace muy poco. Espere 10 minutos antes de reenviarlo.')
  this.corrections.unshift({id:crypto.randomUUID(),field:'resend_notice',label:'Aviso reenviado',detail:notice.subject,operatorName:st.grants[0].operatorName,reason:request.reason.trim(),createdAtUtc:new Date().toISOString(),targetSubject:request.targetSubject})
  return{status:'queued' as const,label:'Aviso reenviado'}
 }
 myCorrections():MySupportCorrection[]{const me=this.currentSubject();return this.corrections.filter(c=>c.targetSubject===me).map(({targetSubject:_t,...c})=>c)}
 startViewAs(request:SupportViewStartRequest){
  this.authorize();if(!this.platformAllowed())throw new Error('Sólo el Máster de plataforma puede usar «Ver como».')
  if(request.profileKey){const p=SUPPORT_VIEW_PROFILES.find(x=>x.key===request.profileKey);if(!p)throw new Error('Seleccione un perfil de la lista.');const t=Date.now();return{sessionId:crypto.randomUUID(),targetSubject:'profile:'+p.key,targetName:profileDisplayName(p),startedAtUtc:new Date(t).toISOString(),expiresAtUtc:new Date(t+30*60000).toISOString(),readOnly:true as const}}
  if((request.reason?.trim().length??0)<10||request.reason!.trim().length>500)throw new Error('Indique el motivo (10 a 500 caracteres).')
  const user=this.users.find(u=>u.subject===request.targetSubject&&u.enabled);if(!user)throw new Error('La cuenta no existe o no está habilitada.')
  if(user.kind!=='member')throw new Error('No se puede ver como una cuenta con cargos de administración.')
  const now=Date.now();return{sessionId:crypto.randomUUID(),targetSubject:user.subject,targetName:user.name,startedAtUtc:new Date(now).toISOString(),expiresAtUtc:new Date(now+30*60000).toISOString(),readOnly:true as const}
 }
 private authorize(){if(!this.allowed())throw new Error('No tiene permiso para administrar usuarios.')}
 candidatesForCreation():AccountCandidates{this.authorize();return{items:structuredClone(this.candidates),canCreateUser:this.platformAllowed(),canCreateSuperuser:this.platformAllowed(),deliveryConfigured:true}}
 list(){this.authorize();return{items:structuredClone(this.users),configured:true}}
 create(request:CreateUserAccount):UserAccountCreated{
  this.authorize();const platform=request.superuser===true
  if(platform&&!this.platformAllowed())throw new Error('Sólo el Máster de plataforma puede crear Superusuarios. No existe otro Máster ni se puede crear.')
  const candidate=this.candidates.find(x=>x.memberId===request.memberId&&x.organizationId===request.organizationId)
  if(platform?(request.memberId!=null||request.organizationId!=null||!request.platformName?.trim()||!request.platformEmail?.includes('@')):(!candidate||request.platformEmail!=null||request.platformName!=null))throw new Error('Seleccione un Hermano activo de un Taller con correo registrado.')
  const email=platform?request.platformEmail!:candidate!.email
  if(this.users.some(u=>u.email.toLowerCase()===email.toLowerCase()))throw new Error('Ya existe una cuenta para este correo.')
  const subject=crypto.randomUUID();this.users.push({subject,email,name:platform?request.platformName!.trim():candidate!.name,enabled:true,kind:platform?'superuser':'member'})
  return{subject,email,memberId:platform?null:candidate!.memberId,organizationId:platform?null:candidate!.organizationId,superuser:platform,requiresPasswordChange:true,initialPasswordEmailSent:false}
 }
 resetAccess(subject:string,request:AccessResetRequest):AccessResetResult{
  this.authorize();if(!this.platformAllowed())throw new Error('Sólo el Máster de plataforma puede restablecer accesos.')
  const user=this.users.find(u=>u.subject===subject);if(!user)throw new Error('La cuenta no existe o no es administrada por la plataforma.')
  if(request.modality==='email'){if(!user.email.includes('@'))throw new Error('La cuenta no tiene un correo válido. Use la clave temporal con motivo.');return{subject,name:user.name,modality:'email',emailSent:false,email:user.email,expiresInMinutes:30}}
  if((request.reason?.trim().length??0)<10)throw new Error('Indique el motivo (10 a 200 caracteres) para entregar una clave temporal.')
  const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789';const bytes=crypto.getRandomValues(new Uint8Array(20))
  return{subject,name:user.name,modality:'temporary',temporaryPassword:'Aa7!'+Array.from(bytes,b=>alphabet[b%alphabet.length]).join(''),requiresPasswordChange:true,shownOnce:true}
 }
}
