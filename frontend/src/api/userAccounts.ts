export interface AccountCandidate { memberId:string;organizationId:string;name:string;email:string;workshop:string }
export interface AccountCandidates { items:AccountCandidate[];canCreatePlatformAdministrator:boolean;deliveryConfigured:boolean }
export interface AccountUser { subject:string;name:string;email:string;enabled:boolean;kind:'member'|'platform' }
export interface CreateUserAccount { memberId?:string|null;organizationId?:string|null;platformAdministrator?:boolean;platformName?:string|null;platformEmail?:string|null }
export interface UserAccountCreated { subject:string;email:string;memberId:string|null;organizationId:string|null;platformAdministrator:boolean;requiresPasswordChange:true;initialPasswordEmailSent:boolean }

// Synthetic institutional records only. Identity and email delivery are simulated on Pages.
export class UserAccountsDemo {
 private users:AccountUser[]=[]
 constructor(private readonly candidates:AccountCandidate[],private readonly allowed:()=>boolean,private readonly platformAllowed:()=>boolean){}
 private authorize(){if(!this.allowed())throw new Error('No tiene permiso para administrar usuarios.')}
 candidatesForCreation():AccountCandidates{this.authorize();return{items:structuredClone(this.candidates),canCreatePlatformAdministrator:this.platformAllowed(),deliveryConfigured:true}}
 list(){this.authorize();return{items:structuredClone(this.users),configured:true}}
 create(request:CreateUserAccount):UserAccountCreated{
  this.authorize();const platform=request.platformAdministrator===true
  if(platform&&!this.platformAllowed())throw new Error('Sólo un administrador de plataforma puede crear esa excepción.')
  const candidate=this.candidates.find(x=>x.memberId===request.memberId&&x.organizationId===request.organizationId)
  if(platform?(request.memberId!=null||request.organizationId!=null||!request.platformName?.trim()||!request.platformEmail?.includes('@')):(!candidate||request.platformEmail!=null||request.platformName!=null))throw new Error('Seleccione un Hermano activo de un Taller con correo registrado.')
  const email=platform?request.platformEmail!:candidate!.email
  if(this.users.some(u=>u.email.toLowerCase()===email.toLowerCase()))throw new Error('Ya existe una cuenta para este correo.')
  const subject=crypto.randomUUID();this.users.push({subject,email,name:platform?request.platformName!.trim():candidate!.name,enabled:true,kind:platform?'platform':'member'})
  return{subject,email,memberId:platform?null:candidate!.memberId,organizationId:platform?null:candidate!.organizationId,platformAdministrator:platform,requiresPasswordChange:true,initialPasswordEmailSent:false}
 }
}
