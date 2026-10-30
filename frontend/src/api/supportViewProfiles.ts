// «Ver como un perfil»: el Máster y los superusuarios miran la plataforma como la vería un cargo, sin elegir
// a ninguna persona. Solo lectura, 30 minutos, Taller 00 de soporte. El servidor es la autoridad (lista y reglas).
import type { DemoProfileKey } from '../demoProfiles'

export interface SupportViewProfile { key:string;label:string;group:'Taller'|'Gran Logia' }

export const SUPPORT_VIEW_PROFILES:SupportViewProfile[]=[
 {key:'hermano',label:'Hermano',group:'Taller'},
 {key:'venerable',label:'Venerable Maestro',group:'Taller'},
 {key:'primer-vigilante',label:'Primer Vigilante',group:'Taller'},
 {key:'segundo-vigilante',label:'Segundo Vigilante',group:'Taller'},
 {key:'tesorero',label:'Tesorero',group:'Taller'},
 {key:'secretario',label:'Secretario',group:'Taller'},
 {key:'hospitalario',label:'Hospitalario',group:'Taller'},
 {key:'orador',label:'Orador',group:'Taller'},
 {key:'inmediato-ex-venerable',label:'Inmediato Ex Venerable Maestro',group:'Taller'},
 {key:'gran-secretario',label:'Gran Secretario',group:'Gran Logia'},
 {key:'regimen-interior',label:'Régimen Interior',group:'Gran Logia'},
 {key:'gran-tesorero',label:'Gran Tesorero',group:'Gran Logia'},
 {key:'gran-hospitalaria',label:'Gran Hospitalaria',group:'Gran Logia'},
 {key:'gran-maestro',label:'Gran Maestro',group:'Gran Logia'},
 {key:'gran-segundo-vigilante',label:'Gran Segundo Vigilante',group:'Gran Logia'},
 {key:'gran-primer-vigilante',label:'Gran Primer Vigilante',group:'Gran Logia'},
 {key:'inmediato-ex-gran-maestro',label:'Inmediato Ex Gran Maestro',group:'Gran Logia'},
 {key:'jefatura-docencia',label:'Jefatura de Docencia',group:'Gran Logia'},
 {key:'gran-archivero',label:'Gran Archivero',group:'Gran Logia'},
 {key:'bibliotecario',label:'Bibliotecario',group:'Gran Logia'},
 {key:'comunicaciones',label:'Comunicaciones',group:'Gran Logia'},
]

/** Solo para la demostración: qué perfil de demostración corresponde a cada perfil. */
export const DEMO_PROFILE_FOR:Record<string,DemoProfileKey>={
 hermano:'brother',venerable:'lodge','primer-vigilante':'lodgeFirstWarden','segundo-vigilante':'lodgeSecondWarden',
 tesorero:'lodgeTreasurer',secretario:'lodgeSecretary',hospitalario:'lodgeHospitalaria',orador:'lodgeOrator',
 'inmediato-ex-venerable':'lodgePastMaster',
 'gran-secretario':'secretariat','regimen-interior':'regimen','gran-tesorero':'treasury','gran-hospitalaria':'hospitalaria','gran-maestro':'grandMaster',
 'gran-segundo-vigilante':'grandSecondWarden','gran-primer-vigilante':'grandFirstWarden',
 'inmediato-ex-gran-maestro':'immediatePastGrandMaster','jefatura-docencia':'instructionDepartmentHead',
 'gran-archivero':'grandArchivist',bibliotecario:'librarian',comunicaciones:'communications',
}

export function profileDisplayName(profile:SupportViewProfile){return profile.group==='Taller'?`${profile.label} · Taller 00`:profile.label}
