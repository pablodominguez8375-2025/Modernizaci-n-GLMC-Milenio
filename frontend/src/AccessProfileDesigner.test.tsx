import { describe,expect,it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import AccessProfileDesigner, { validateAccessProfile } from './AccessProfileDesigner'
import { PmgmApiClient } from './api/pmgmApi'

describe('access profile designer',()=>{
 it('separates views and actions for editable profiles',()=>{const html=renderToStaticMarkup(<AccessProfileDesigner api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Vistas visibles');expect(html).toContain('Acciones permitidas');expect(html).toContain('Venerable Maestro');expect(html).toContain('Simular acceso')})
 it('prevents a Taller profile from escalating to system administration',()=>{const result=validateAccessProfile({id:'custom',name:'Perfil Taller',scope:'Taller',protected:false,views:['Sistema'],actions:['Configurar sistema']});expect(result.errors.length).toBeGreaterThan(0)})
 it('prevents locking out the protected system administrator',()=>{const result=validateAccessProfile({id:'profile-system',name:'Administrador del Sistema',scope:'Orden',protected:true,views:['Dashboard'],actions:['Consultar']});expect(result.errors.some(error=>error.includes('debe conservar'))).toBe(true)})
})
