import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import SystemOperationsPanel from './SystemOperationsPanel'
import { PmgmApiClient } from './api/pmgmApi'

describe('system operations console',()=>{
 it('exposes the requested administration areas',()=>{const html=renderToStaticMarkup(<SystemOperationsPanel api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Backup y restauración');expect(html).toContain('Usuarios');expect(html).toContain('Logos y colores');expect(html).toContain('Auditoría de accesos');expect(html).toContain('Escriba RESTAURAR')})
 it('only offers the access-profile designer and assignments to the Master',()=>{
  const api=new PmgmApiClient({useMocks:true})
  expect(renderToStaticMarkup(<SystemOperationsPanel api={api} area="profiles"/>)).not.toContain('Diseñar perfiles de acceso')
  expect(renderToStaticMarkup(<SystemOperationsPanel api={api} area="profiles" canManageProfiles/>)).toContain('Diseñar perfiles de acceso')
 })
})
