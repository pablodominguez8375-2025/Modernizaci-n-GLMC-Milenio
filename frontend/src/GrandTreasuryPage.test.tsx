import {describe,expect,it,vi} from 'vitest'
import {renderToStaticMarkup} from 'react-dom/server'
import GrandTreasuryPage from './GrandTreasuryPage'
import RegularityPage from './RegularityPage'
import CeremonyRightsPage from './CeremonyRightsPage'
import {PmgmApiClient} from './api/pmgmApi'
const state=vi.hoisted(()=>({access:null as null|{version:number;managed:boolean;actions:string[]},error:null as string|null}))
vi.mock('./useGrandTreasuryAccess',()=>({useGrandTreasuryAccess:()=>state}))
vi.mock('./TreasuryStatementPage',()=>({default:({canReview}:{canReview:boolean})=><p>{canReview?'CONCILIAR CUADRO':'CONSULTAR CUADRO'}</p>}))
describe('Gran Tesorería — límite de pantalla',()=>{
 it('no monta las vistas financieras mientras carga o después de revocar',()=>{
  const api=new PmgmApiClient({useMocks:true})
  state.access=null;expect(renderToStaticMarkup(<GrandTreasuryPage api={api}/>)).toContain('Comprobando acceso')
  state.access={version:1,managed:true,actions:[]}
  const html=renderToStaticMarkup(<GrandTreasuryPage api={api}/> )
  expect(html).toContain('El perfil no permite consultar')
  expect(html).not.toContain('Cuadros mensuales');expect(html).not.toContain('Derechos ceremoniales')
 })
 it('view monta consulta de cuadros y write habilita conciliación',()=>{
  const api=new PmgmApiClient({useMocks:true})
  state.access={version:1,managed:true,actions:['view']}
  const html=renderToStaticMarkup(<GrandTreasuryPage api={api}/> )
  expect(html).toContain('CONSULTAR CUADRO');expect(html).not.toContain('CONCILIAR CUADRO')
  state.access={version:2,managed:true,actions:['view','write']}
  expect(renderToStaticMarkup(<GrandTreasuryPage api={api}/>)).toContain('CONCILIAR CUADRO')
 })
 it('la consulta mantiene visible el estado y no monta formularios de escritura',()=>{
  const api=new PmgmApiClient({useMocks:true})
  const readOnly=renderToStaticMarkup(<RegularityPage api={api} kind="treasury" canWrite={false}/> )
  expect(readOnly).toContain('Consultar estado');expect(readOnly).not.toContain('Actualizar regularidad');expect(readOnly).not.toContain('Registrar estado')
  expect(renderToStaticMarkup(<RegularityPage api={api} kind="treasury" canWrite/>)).toContain('Actualizar regularidad')
  expect(renderToStaticMarkup(<CeremonyRightsPage api={api} canWrite={false}/>)).not.toContain('Registrar abono')
 })
})
