import { describe, expect, it, vi } from 'vitest'
import { formatRemaining, installSupportViewAsFetch, remainingSeconds, setSupportViewAs, getSupportViewAs, SUPPORT_VIEW_HEADER } from './supportViewAs'

describe('Ver como (#574)', () => {
 it('agrega la cabecera solo a llamadas /api y nunca a los endpoints de la propia función', async () => {
  const seen:Array<{url:string;header:string|null}>=[]
  const fake={fetch:vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>{seen.push({url:String(input),header:new Headers(init?.headers).get(SUPPORT_VIEW_HEADER)});return new Response('{}')})} as unknown as typeof globalThis
  installSupportViewAsFetch(fake);installSupportViewAsFetch(fake)
  setSupportViewAs({sessionId:'s-1',targetSubject:'t',targetName:'Hermano',expiresAtUtc:new Date(Date.now()+600000).toISOString()})
  await fake.fetch('/api/session/me');await fake.fetch('/api/system/support/view-as',{method:'POST'});await fake.fetch('/assets/x.js')
  expect(seen.map(x=>x.header)).toEqual(['s-1',null,null])
  setSupportViewAs(null);await fake.fetch('/api/session/me')
  expect(seen[3].header).toBeNull()
 })
 it('una sesión vencida se descarta sola', () => {
  setSupportViewAs({sessionId:'s-2',targetSubject:'t',targetName:'H',expiresAtUtc:new Date(Date.now()-1000).toISOString()})
  expect(getSupportViewAs()).toBeNull()
 })
 it('calcula y formatea el tiempo restante', () => {
  const s={sessionId:'x',targetSubject:'t',targetName:'H',expiresAtUtc:new Date(1000*125).toISOString()}
  expect(remainingSeconds(s,0)).toBe(125);expect(formatRemaining(125)).toBe('02:05');expect(remainingSeconds(s,1e9)).toBe(0)
 })
})
