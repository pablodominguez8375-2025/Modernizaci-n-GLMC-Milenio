import { describe,expect,it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import AccessProfileDesigner, { validateAccessProfile } from './AccessProfileDesigner'
import { PmgmApiClient } from './api/pmgmApi'

describe('access profile designer',()=>{
 it('renders the eight protected workshop offices',()=>{const html=renderToStaticMarkup(<AccessProfileDesigner api={new PmgmApiClient({useMocks:true})}/>);for(const role of ['Venerable Maestro','Secretaría del Taller','Tesorería del Taller','Hospitalaria del Taller','Orador del Taller','Primer Vigilante','Segundo Vigilante','Inmediato Ex-Venerable Maestro'])expect(html).toContain(role);expect(html).toContain('Consejo de Administración');expect(html).toContain('Docencia Compañeros');expect(html).toContain('Docencia Aprendices');expect(html).toContain('Docencia Maestros')})
 it('separates views and actions for editable profiles',()=>{const html=renderToStaticMarkup(<AccessProfileDesigner api={new PmgmApiClient({useMocks:true})}/>);expect(html).toContain('Vistas visibles');expect(html).toContain('Acciones permitidas');expect(html).toContain('Simular acceso')})
 it('prevents a Taller profile from escalating to system administration',()=>{const result=validateAccessProfile({id:'custom',name:'Perfil Taller',scope:'Taller',protected:false,views:['Sistema'],actions:['Configurar sistema']});expect(result.errors.length).toBeGreaterThan(0)})
 it('prevents locking out the protected system administrator',()=>{const result=validateAccessProfile({id:'profile-system',name:'Administrador del Sistema',scope:'Orden',protected:true,views:['Dashboard'],actions:['Consultar']});expect(result.errors.some(error=>error.includes('debe conservar'))).toBe(true)})
})
