import {describe,it,expect} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import UserAccountsPanel,{accountCreationBlockReason} from './UserAccountsPanel'
import {PmgmApiClient} from './api/pmgmApi'
import type {AccountCandidates} from './api/userAccounts'

const data=(identity:boolean,mail:boolean):AccountCandidates=>({
 items:[],canCreateUser:true,canCreateSuperuser:true,
 identityConfigured:identity,mailConfigured:mail,deliveryConfigured:identity&&mail
})
describe('alta de usuarios QA #754: bloqueo sin falla silenciosa',()=>{
 it('requiere ambos servicios, sin enviar correo ni crear identidades al estar bloqueado',()=>{
  expect(accountCreationBlockReason(data(true,true))).toBeNull()
  expect(accountCreationBlockReason(data(false,true))).toMatch(/Keycloak/)
  expect(accountCreationBlockReason(data(true,false))).toMatch(/correo institucional general/)
  expect(accountCreationBlockReason(data(false,false))).toMatch(/SMTP/)
 })
 it('mantiene un diagnóstico seguro para APIs previas sin flags individuales',()=>{
  expect(accountCreationBlockReason({items:[],canCreateSuperuser:true,deliveryConfigured:false})).toMatch(/servidor indica/)
  expect(accountCreationBlockReason(null)).toMatch(/verificar el estado/)
 })
 it('preserva el formulario y los permisos de Máster sin crear cuentas en SSR',()=>{
  const html=renderToStaticMarkup(<UserAccountsPanel api={new PmgmApiClient({useMocks:true})}/>)
  expect(html).toContain('Crear usuario')
  expect(html).toContain('Usuarios configurados')
 })
})
