import {describe,expect,it} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import AccessProfileDesigner,{validateAccessProfile} from './AccessProfileDesigner'
import {PmgmApiClient} from './api/pmgmApi'
describe('access profile designer',()=>{
 it('shows protected offices and per-view actions',()=>{const html=renderToStaticMarkup(<AccessProfileDesigner api={new PmgmApiClient({useMocks:true})}/>);for(const name of ['Venerable Maestro','Secretaría del Taller','Tesorería del Taller','Hospitalaria del Taller','Orador del Taller','Primer Vigilante','Segundo Vigilante','Inmediato Ex-Venerable Maestro','Vistas visibles y acciones permitidas','Imprimir','Simular acceso'])expect(html).toContain(name)})
 it('rejects a lodge profile with system access',()=>expect(validateAccessProfile({code:'custom',name:'Perfil',scope:'lodge',menuCodes:['system']}).errors.length).toBeGreaterThan(0))
 it('requires canonical stable profile codes',()=>expect(validateAccessProfile({code:'Código libre',name:'Perfil',scope:'order',menuCodes:[]}).errors.length).toBeGreaterThan(0))
})
