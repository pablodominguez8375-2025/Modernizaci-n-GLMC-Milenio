import seed from './decree-1759.json'
export type Zone = 'santiago'|'other_oriente'|'peru'
export interface TariffRate {feeType:string;territory:Zone;amount:number;currency:'CLP'|'USD'}
export interface CeremonyRate {ceremonyType:string;territory:Zone;amount:number;currency:'CLP'|'USD'}
export interface UnemploymentRate {territory:Zone;quarter:number;discountPercent:number;amount:number;currency:'CLP'|'USD'}
export interface TariffVersion {id:string;version:number;number:string;decreeDate:string;effectiveFrom:string;effectiveUntil:string|null;sourceReference:string;status:'draft'|'published';rates:TariffRate[];ceremonyRights:CeremonyRate[];unemployment:UnemploymentRate[]}
export type RegisterTariff = Omit<TariffVersion,'id'|'version'> & {expectedVersion:number}
export interface OfficialSchedule extends Omit<TariffVersion,'rates'|'status'> {asOf:string;territory:Zone|null;items:TariffRate[]}
export const initialTariff = seed as TariffVersion
export function tariffAt(catalog:TariffVersion[],date:string){const row=catalog.filter(x=>x.status==='published'&&x.effectiveFrom<=date).sort((a,b)=>b.effectiveFrom.localeCompare(a.effectiveFrom)||b.version-a.version)[0];return row&&(!row.effectiveUntil||row.effectiveUntil>=date)?row:null}
export function tariffRate(catalog:TariffVersion[],type:string,zone:Zone|null|undefined,date:string){return tariffAt(catalog,date)?.rates.find(x=>x.feeType===type&&x.territory===zone)??null}
const demoLocations = new Map<string,{city:string|null;country:string|null}>()
export function setDemoWorkshopLocation(id:string,city:string|null,country:string|null){demoLocations.set(id,{city,country})}
export function zoneFromLocation(city:string|null|undefined,country:string|null|undefined):Zone|null{if(!city?.trim())return null;const nation=country?.trim().toLocaleLowerCase('es');return nation==='chile'?(city.trim().toLocaleLowerCase('es')==='santiago'?'santiago':'other_oriente'):nation==='perú'||nation==='peru'?'peru':null}
export function demoWorkshopLocation(id:string,fallback:Zone|null){return demoLocations.get(id)??(id==='23232323-2323-2323-2323-232323232323'?{city:'Valparaíso',country:'Chile'}:{city:fallback==='santiago'?'Santiago':fallback==='other_oriente'?'Valparaíso':fallback==='peru'?'Lima':null,country:fallback==='peru'?'Perú':fallback?'Chile':null})}
export function nextTariffPeriod(date:string){const [y,m]=date.split('-').map(Number);return `${m===12?y+1:y}-${String(m===12?1:m+1).padStart(2,'0')}-01`}
