export type AccessAction = 'view' | 'create' | 'write' | 'edit' | 'delete' | 'search' | 'print'
export interface DynamicView { code:string; name:string; isActive:boolean }
export interface DynamicMenu { code:string; name:string; isActive:boolean; views:DynamicView[] }
export interface DynamicGrant { viewCode:string; actions:AccessAction[] }
export type AccessScope='order'|'lodge'|'own-lodge'|'all-lodges'
export const ACCESS_SCOPES:{value:AccessScope;label:string;help:string}[]=[
 {value:'own-lodge',label:'Solo su propio Taller',help:'Se toma de la pertenencia vigente en su ficha de hermano; si se traslada, el acceso cambia solo.'},
 {value:'all-lodges',label:'Todos los Talleres',help:'Cubre todos los Talleres, sin elegir ninguno.'},
 {value:'lodge',label:'Un Taller específico',help:'Se elige el Taller al asignar el perfil (excepción, con vigencia).'},
 {value:'order',label:'Toda la Gran Logia (Orden)',help:'Nivel Orden, sin Taller.'}]
export const scopeLabel=(scope:string)=>ACCESS_SCOPES.find(x=>x.value===scope)?.label??scope
export const isLodgeScoped=(scope:string)=>scope!=='order'
export const isDerivedScope=(scope:string)=>scope==='own-lodge'||scope==='all-lodges'
export interface DynamicProfile { id:string; code:string; name:string; scope:AccessScope; isSystem:boolean; isActive:boolean; menuCodes:string[]; grants:DynamicGrant[] }
export interface DynamicAssignment { id:string; subject:string; profileCode:string; organizationId:string|null; effectiveFrom:string; effectiveTo:string|null; isActive:boolean }
export interface DynamicCatalog { version:number; actions:AccessAction[]; menus:DynamicMenu[]; profiles:DynamicProfile[]; assignments:DynamicAssignment[] }
export type ProfileDraft = Pick<DynamicProfile,'code'|'name'|'scope'|'menuCodes'>
export const actionLabels:Record<AccessAction,string>={view:'Ver',create:'Crear / hacer',write:'Escribir / registrar',edit:'Editar',delete:'Borrar lógico',search:'Buscar',print:'Imprimir'}
const actions:AccessAction[]=['view','create','write','edit','delete','search','print']
export function initialDynamicCatalog():DynamicCatalog {
 const menu=(code:string,name:string,views:[string,string][]):DynamicMenu=>({code,name,isActive:true,views:views.map(([code,name])=>({code:code.toLowerCase(),name,isActive:true}))})
 const names=['Venerable Maestro','Secretaría del Taller','Tesorería del Taller','Hospitalaria del Taller','Orador del Taller','Primer Vigilante','Segundo Vigilante','Inmediato Ex-Venerable Maestro','Administrador del Sistema']
 return {version:0,actions,menus:[menu('personal','Mi espacio',[['member','Mi ficha'],['calendar','Agenda'],['notifications','Avisos']]),menu('admissions','Insinuaciones e Iniciación',[['candidates','Publicados'],['candidateProfile','Carga y revisión'],['initiationCircuit','Circuito de Iniciación'],['admissions','Afiliación e Incorporación']]),menu('lodge','Taller',[['lodge','Gestión Logial'],['lodgeProfile','Ficha del Taller'],['members','Fichas de miembros']]),menu('treasury','Tesorería',[['lodgeTreasury','Tesorería del Taller'],['treasury','Gran Tesorería']]),menu('hospitalaria','Hospitalaria',[['hospitalaria','Hospitalaria']]),menu('documents','Documentos',[['library','Biblioteca'],['documentManager','Gestor documental'],['grandArchive','Gran Archivo']]),menu('order','Gran Logia',[['secretariat','Gran Secretaría'],['regimen','Régimen Interior'],['ceremonies','Ceremonias']]),menu('system','Sistema',[['system','Parámetros'],['access-review','Revisión de accesos']])],profiles:names.map((name,i)=>({id:`00000000-0000-0000-0000-${String(i+1).padStart(12,'0')}`,code:`system-${i}`,name,scope:i===8?'order':'lodge',isSystem:true,isActive:true,menuCodes:[],grants:[]})),assignments:[]}
}
export function technicalGrant(c:DynamicCatalog,subject:string,viewCode:string,action:AccessAction,organizationId:string|null,today:string):boolean {
 const menu=c.menus.find(m=>m.isActive&&m.views.some(v=>v.isActive&&v.code===viewCode))
 if(!menu)return false
 return c.assignments.filter(a=>a.isActive&&a.subject===subject&&a.effectiveFrom<=today&&(!a.effectiveTo||a.effectiveTo>=today)).some(a=>c.profiles.some(p=>p.code===a.profileCode&&p.isActive&&!p.isSystem&&p.menuCodes.includes(menu.code)&&(p.scope==='lodge'?organizationId!==null&&a.organizationId===organizationId:isDerivedScope(p.scope)?organizationId!==null&&a.organizationId===null:organizationId===null&&a.organizationId===null)&&p.grants.some(g=>g.viewCode===viewCode&&g.actions.includes('view')&&g.actions.includes(action))))
}
type Transport=<T>(path:string,init?:RequestInit)=>Promise<T>
export type CatalogUpdateReason = 'load' | 'mutation'
export class DynamicAccessClient {
 private catalog=initialDynamicCatalog()
 private listeners=new Set<(reason:CatalogUpdateReason)=>void>()
 constructor(private transport:Transport,private useMocks:boolean,private organizationExists:(id:string)=>boolean = id=>/^[0-9a-f-]{36}$/i.test(id)){}
 subscribe(listener:(reason:CatalogUpdateReason)=>void){this.listeners.add(listener);return()=>{this.listeners.delete(listener)}}
 snapshot(){return structuredClone(this.catalog)}
 private emit(reason:CatalogUpdateReason){for(const fn of this.listeners)fn(reason)}
 async load(){if(!this.useMocks)this.catalog=await this.transport<DynamicCatalog>('/api/system/access/catalog');this.emit('load');return this.snapshot()}
 private async mutate(path:string,method:string,expectedVersion:number,payload?:object,apply?:(c:DynamicCatalog)=>void){
  if(this.useMocks){if(expectedVersion!==this.catalog.version)throw new Error('El catálogo cambió. Recargue antes de guardar.');const next=this.snapshot();apply?.(next);next.version++;this.catalog=next}
  else this.catalog=await this.transport<DynamicCatalog>(path,{method,headers:payload?{'Content-Type':'application/json'}:undefined,body:payload?JSON.stringify({...payload,expectedVersion}):undefined})
  this.emit('mutation');return this.snapshot()
 }
 async create(d:ProfileDraft,version:number){return this.mutate('/api/system/access/profiles','POST',version,d,c=>{
  this.validate(d,c);if(c.profiles.some(p=>p.code===d.code))throw new Error('El código del perfil ya existe.')
  c.profiles.push({...d,id:crypto.randomUUID(),isSystem:false,isActive:true,grants:[]})
 })}
 async update(code:string,d:Omit<ProfileDraft,'code'>,version:number){return this.mutate(`/api/system/access/profiles/${encodeURIComponent(code)}`,'PUT',version,d,c=>{
  const p=this.profile(c,code);if(!p.isActive)throw new Error('El perfil está inactivo.');this.validate({...d,code},c);if(p.scope!==d.scope&&c.assignments.some(a=>a.isActive&&a.profileCode===code))throw new Error('Revoque las asignaciones antes de cambiar el alcance.')
  Object.assign(p,d);p.grants=p.grants.filter(g=>c.menus.some(m=>d.menuCodes.includes(m.code)&&m.views.some(v=>v.code===g.viewCode)))
 })}
 async grants(code:string,grants:DynamicGrant[],version:number){return this.mutate(`/api/system/access/profiles/${encodeURIComponent(code)}/grants`,'PUT',version,{grants},c=>{
  const p=this.profile(c,code);if(!p.isActive)throw new Error('El perfil está inactivo.')
  const seen=new Set<string>();for(const g of grants){const menu=c.menus.find(m=>m.isActive&&m.views.some(v=>v.isActive&&v.code===g.viewCode));if(!menu||!p.menuCodes.includes(menu.code)||seen.has(g.viewCode)||g.actions.some(a=>!actions.includes(a))||(g.actions.some(a=>a!=='view')&&!g.actions.includes('view'))||(p.scope==='lodge'&&menu.code==='system'))throw new Error('Vista o acciones inválidas.');seen.add(g.viewCode)}p.grants=structuredClone(grants)
 })}
 async remove(code:string,version:number){return this.mutate(`/api/system/access/profiles/${encodeURIComponent(code)}?expectedVersion=${version}`,'DELETE',version,undefined,c=>{this.profile(c,code).isActive=false;c.assignments.filter(a=>a.profileCode===code).forEach(a=>{a.isActive=false})})}
 async assign(d:Omit<DynamicAssignment,'id'|'isActive'>,version:number){return this.mutate('/api/system/access/assignments','POST',version,d,c=>{
  const p=this.profile(c,d.profileCode);if(!p.isActive||!d.subject.trim()||d.subject!==d.subject.trim()||d.subject.length>320||d.effectiveTo&&d.effectiveTo<d.effectiveFrom||(p.scope==='lodge'?!d.organizationId||!this.organizationExists(d.organizationId):d.organizationId!==null))throw new Error('Sujeto, Taller o vigencia inválidos.')
  if(c.assignments.some(a=>a.isActive&&a.subject===d.subject&&a.profileCode===d.profileCode&&a.organizationId===d.organizationId&&a.effectiveFrom<=(d.effectiveTo??'9999-12-31')&&(a.effectiveTo??'9999-12-31')>=d.effectiveFrom))throw new Error('La asignación se superpone con una ya vigente o programada.')
  c.assignments.push({...d,id:crypto.randomUUID(),isActive:true})
 })}
 async revoke(id:string,version:number){return this.mutate(`/api/system/access/assignments/${encodeURIComponent(id)}?expectedVersion=${version}`,'DELETE',version,undefined,c=>{const a=c.assignments.find(a=>a.id===id);if(!a)throw new Error('Asignación inexistente.');a.isActive=false})}
 async print(){if(!this.useMocks)await this.transport('/api/system/access/print',{method:'POST'});return {allowed:true}}
 private profile(c:DynamicCatalog,code:string){const p=c.profiles.find(p=>p.code===code);if(!p||p.isSystem)throw new Error('El perfil no existe o es protegido.');return p}
 private validate(d:ProfileDraft,c:DynamicCatalog){if(!/^[a-z][a-z0-9-]{0,79}$/.test(d.code)||!d.name.trim()||d.name.length>240||!ACCESS_SCOPES.some(x=>x.value===d.scope)||(isLodgeScoped(d.scope)&&d.menuCodes.includes('system'))||d.menuCodes.some(code=>!c.menus.some(m=>m.isActive&&m.code===code)))throw new Error('Código, nombre, alcance o menús inválidos.')}
}

export interface TreasuryAccess { version:number; organizationId:string; managed:boolean; actions:AccessAction[] }
export function treasuryAccess(c:DynamicCatalog,subject:string,organizationId:string,today:string):TreasuryAccess {
 const managed=c.assignments.some(a=>a.subject===subject&&a.organizationId===organizationId)
 return {version:c.version,organizationId,managed,actions:actions.filter(action=>!managed||technicalGrant(c,subject,'lodgetreasury',action,organizationId,today))}
}

export interface TariffAccess { version:number; managed:boolean; actions:AccessAction[] }
export function tariffAccess(c:DynamicCatalog,subject:string,today:string):TariffAccess {
 const managed=c.assignments.some(a=>a.subject===subject&&a.organizationId===null)
 return {version:c.version,managed,actions:(['view','create'] as AccessAction[]).filter(action=>!managed||technicalGrant(c,subject,'treasury',action,null,today))}
}

export type HospitalariaAccess = TreasuryAccess
export function hospitalariaAccess(c:DynamicCatalog,subject:string,organizationId:string,today:string):HospitalariaAccess {
 const managed=c.assignments.some(a=>a.subject===subject&&a.organizationId===organizationId)
 return {version:c.version,organizationId,managed,actions:(['view','create','write'] as AccessAction[]).filter(action=>!managed||technicalGrant(c,subject,'hospitalaria',action,organizationId,today))}
}

export type GrandHospitalariaAccess = TariffAccess
export function grandHospitalariaAccess(c:DynamicCatalog,subject:string,today:string):GrandHospitalariaAccess {
 const managed=c.assignments.some(a=>a.subject===subject&&a.organizationId===null)
 return {version:c.version,managed,actions:(['view','create','write'] as AccessAction[]).filter(action=>!managed||technicalGrant(c,subject,'hospitalaria',action,null,today))}
}

export type GrandTreasuryAccess = GrandHospitalariaAccess
export function grandTreasuryAccess(c:DynamicCatalog,subject:string,today:string):GrandTreasuryAccess {
 const managed=c.assignments.some(a=>a.subject===subject&&a.organizationId===null)
 return {version:c.version,managed,actions:(['view','create','write'] as AccessAction[]).filter(action=>!managed||technicalGrant(c,subject,'treasury',action,null,today))}
}
