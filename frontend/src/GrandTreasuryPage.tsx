import { Fragment, useState } from 'react'
import { useGrandTreasuryAccess } from './useGrandTreasuryAccess'
import type { PmgmApiClient } from './api/pmgmApi'
import RegularityPage from './RegularityPage'
import TreasuryRoleNavigation from './TreasuryRoleNavigation'
import TreasuryStatementPage from './TreasuryStatementPage'
import TariffDecreePage from './TariffDecreePage'
import CeremonyRightsPage from './CeremonyRightsPage'

type GrandSection = 'regularity' | 'statements' | 'territories' | 'rights'

export default function GrandTreasuryPage({ api }: { api: PmgmApiClient }) {
  const {access,error}=useGrandTreasuryAccess(api,true)
  const [section, setSection] = useState<GrandSection>('statements')
  if(!access)return <div className="panel" role={error?'alert':'status'}>{error??'Comprobando acceso a Gran Tesorería…'}</div>
  if(!access.actions.includes('view'))return <div className="panel" role="status">El perfil no permite consultar Gran Tesorería.</div>
  const canWrite=access.actions.includes('write')
  const context=`${api.demoAccessSubject}:${access.version}:${access.actions.join(',')}:${section}`
  return <div className="lodge-product-page">
    <section className="lodge-product-heading">
      <div><p className="lodge-product-breadcrumb">Orden <span>›</span> Gran Tesorería</p><h1>Gran Tesorería</h1><p>Revisión mensual de los Talleres, conciliación de pagos y regularidad institucional.</p></div>
    </section>
    <TreasuryRoleNavigation
      title="Gran Tesorería"
      sections={[
        { id:'statements', label:'Cuadros mensuales', description:'Montos por línea de cuota y conciliación' },
        { id:'regularity', label:'Estado de Talleres', description:'Consulta y regularidad institucional' },
        { id:'territories', label:'Tarifario por decreto', description:'Tarifario por decreto y ubicación desde la Ficha' },
        { id:'rights', label:'Derechos ceremoniales', description:'Pagos y saldos por expediente' },
      ]}
      active={section}
      onChange={id => setSection(id as GrandSection)}
    />
    <Fragment key={context}>{section === 'statements' ? <TreasuryStatementPage api={api} canPrepare={false} canReview={canWrite} /> : section==='regularity'?<RegularityPage api={api} kind="treasury" canWrite={canWrite}/>:section==='territories'?<TariffDecreePage api={api}/>:<CeremonyRightsPage api={api} canWrite={canWrite}/>}
    </Fragment>
  </div>
}
