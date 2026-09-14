import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import SystemOperationsPanel from './SystemOperationsPanel'
import { PmgmApiClient } from './api/pmgmApi'

describe('system operations console',()=>{
 it('exposes the four requested administration areas',()=>{const html=renderToStaticMarkup(<SystemOperationsPanel api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Backup y restauración');expect(html).toContain('Usuarios');expect(html).toContain('Correo');expect(html).toContain('Logos y colores');expect(html).toContain('Escriba RESTAURAR')})
})
