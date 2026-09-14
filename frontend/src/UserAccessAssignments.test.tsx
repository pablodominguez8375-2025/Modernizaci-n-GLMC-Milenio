import {describe,expect,it} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import UserAccessAssignments,{assignmentStatus} from './UserAccessAssignments'
import {PmgmApiClient} from './api/pmgmApi'

describe('user access assignments',()=>{it('renders cumulative access and revocation controls',()=>{const html=renderToStaticMarkup(<UserAccessAssignments api={new PmgmApiClient({useMocks:true})} profiles={['Administrador del Sistema','Venerable Maestro']}/>);expect(html).toContain('Acceso acumulado efectivo');expect(html).toContain('Revocar');expect(html).toContain('Hasta (opcional)')});it('detects scheduled assignments',()=>expect(assignmentStatus('2099-01-01','')).toBe('scheduled'));it('detects revoked assignments explicitly',()=>expect(assignmentStatus('2026-01-01','',true)).toBe('revoked'))})
