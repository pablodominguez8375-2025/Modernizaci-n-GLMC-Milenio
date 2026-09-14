import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import SystemOperationsPanel from './SystemOperationsPanel'

describe('system operations console',()=>{
 it('exposes the four requested administration areas',()=>{const html=renderToStaticMarkup(<SystemOperationsPanel/>);expect(html).toContain('Backup y restauración');expect(html).toContain('Usuarios');expect(html).toContain('Correo');expect(html).toContain('Logos y colores');expect(html).toContain('Escriba RESTAURAR')})
})
