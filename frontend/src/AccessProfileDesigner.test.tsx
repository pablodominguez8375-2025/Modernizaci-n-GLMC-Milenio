import { describe,expect,it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import AccessProfileDesigner from './AccessProfileDesigner'
import { PmgmApiClient } from './api/pmgmApi'

describe('access profile designer',()=>{it('separates views and actions for editable profiles',()=>{const html=renderToStaticMarkup(<AccessProfileDesigner api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Vistas visibles');expect(html).toContain('Acciones permitidas');expect(html).toContain('Venerable Maestro');expect(html).toContain('Duplicar perfil')})})
