import {describe,expect,it} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import AuditableEventLog from './AuditableEventLog'
import {PmgmApiClient} from './api/pmgmApi'

describe('auditable event log',()=>{
 it('exposes immutable audit fields and export',()=>{const html=renderToStaticMarkup(<AuditableEventLog api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Bitácora de eventos auditables');expect(html).toContain('Usuario / IP');expect(html).toContain('Menú / submenú');expect(html).toContain('Exportar CSV');expect(html).toContain('Inmutable')})
 it('returns fictional login and system events in QA',async()=>{const result=await new PmgmApiClient({useMocks:true}).getAuditLog();expect(result.immutable).toBe(true);expect(result.items.some(x=>x.action==='identity.login.rejected')).toBe(true);expect(result.items.every(x=>x.user&&x.ipAddress&&x.summary)).toBe(true)})
})
