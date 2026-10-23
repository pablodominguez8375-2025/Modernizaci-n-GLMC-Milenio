export interface AccountCandidate { memberId:string;organizationId:string;name:string;email:string;workshop:string }
export interface AccountCandidates { items:AccountCandidate[];canCreateUser?:boolean;canCreateSuperuser:boolean;deliveryConfigured:boolean }
export interface AccountUser { subject:string;name:string;email:string;enabled:boolean;kind:'member'|'superuser'|'platform' }
export interface CreateUserAccount { memberId?:string|null;organizationId?:string|null;superuser?:boolean;platformName?:string|null;platformEmail?:string|null }
export interface UserAccountCreated { subject:string;email:string;memberId:string|null;organizationId:string|null;superuser:boolean;requiresPasswordChange:true;initialPasswordEmailSent:boolean }

export type AccessResetModality='email'|'temporary'
export interface AccessResetRequest { modality:AccessResetModality;reason?:string }
export interface AccessResetResult { subject:string;name:string;modality:AccessResetModality;emailSent?:boolean;email?:string;expiresInMinutes?:number;temporaryPassword?:string;requiresPasswordChange?:boolean;shownOnce?:boolean }

// Synthetic institutional records only. Identity and email delivery are simulated on Pages.
export class UserAccountsDemo {
 private users:AccountUser[]=[]
 constructor(private readonly candidates:AccountCandidate[],private readonly allowed:()=>boolean,private readonly platformAllowed:()=>boolean){}
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
