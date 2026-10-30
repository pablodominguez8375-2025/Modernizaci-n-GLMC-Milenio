// «Ver como un perfil»: el Máster y los superusuarios miran la plataforma como la vería un cargo, sin elegir
// a ninguna persona. Solo lectura, 30 minutos, Taller 00 de soporte. El servidor es la autoridad (lista y reglas).
import type { DemoProfileKey } from '../demoProfiles'

export interface SupportViewProfile { key:string;label:string;group:'Taller'|'Gran Logia' }

// Un solo inventario de metadatos: evita duplicar las claves en el catálogo y el
// adaptador demo. La API real sigue siendo la autoridad y confirma cada perfil.
const PROFILE_ROWS: ReadonlyArray<readonly [string,string,SupportViewProfile['group'],DemoProfileKey]> = [
 ['hermano','Hermano','Taller','brother'],
 ['venerable','Venerable Maestro','Taller','lodge'],
 ['primer-vigilante','Primer Vigilante','Taller','lodgeFirstWarden'],
 ['segundo-vigilante','Segundo Vigilante','Taller','lodgeSecondWarden'],
 ['tesorero','Tesorero','Taller','lodgeTreasurer'],
 ['secretario','Secretario','Taller','lodgeSecretary'],
 ['hospitalario','Hospitalario','Taller','lodgeHospitalaria'],
 ['orador','Orador','Taller','lodgeOrator'],
 ['inmediato-ex-venerable','Inmediato Ex Venerable Maestro','Taller','lodgePastMaster'],
 ['gran-secretario','Gran Secretario','Gran Logia','secretariat'],
 ['regimen-interior','Régimen Interior','Gran Logia','regimen'],
 ['gran-tesorero','Gran Tesorero','Gran Logia','treasury'],
 ['gran-hospitalaria','Gran Hospitalaria','Gran Logia','hospitalaria'],
 ['gran-maestro','Gran Maestro','Gran Logia','grandMaster'],
 ['gran-segundo-vigilante','Gran Segundo Vigilante','Gran Logia','grandSecondWarden'],
 ['gran-primer-vigilante','Gran Primer Vigilante','Gran Logia','grandFirstWarden'],
 ['inmediato-ex-gran-maestro','Inmediato Ex Gran Maestro','Gran Logia','immediatePastGrandMaster'],
 ['jefatura-docencia','Jefatura de Docencia','Gran Logia','instructionDepartmentHead'],
 ['gran-archivero','Gran Archivero','Gran Logia','grandArchivist'],
 ['bibliotecario','Bibliotecario','Gran Logia','librarian'],
 ['comunicaciones','Comunicaciones','Gran Logia','communications'],
]

export const SUPPORT_VIEW_PROFILES:SupportViewProfile[]=PROFILE_ROWS.map(([key,label,group])=>({key,label,group}))

/** Solo para la demostración: qué perfil de demostración corresponde a cada perfil. */
export const DEMO_PROFILE_FOR:Record<string,DemoProfileKey>=Object.fromEntries(
 PROFILE_ROWS.map(([key,,,demo])=>[key,demo]),
)

export function profileDisplayName(profile:SupportViewProfile){return profile.group==='Taller'?`${profile.label} · Taller 00`:profile.label}
