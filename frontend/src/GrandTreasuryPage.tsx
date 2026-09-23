import { useState } from 'react'
import type { PmgmApiClient } from './api/pmgmApi'
import RegularityPage from './RegularityPage'
import TreasuryRoleNavigation from './TreasuryRoleNavigation'
import TreasuryStatementPage from './TreasuryStatementPage'
import TreasuryTerritoryPage from './TreasuryTerritoryPage'

type GrandSection = 'regularity' | 'statements' | 'territories'

export default function GrandTreasuryPage({ api }: { api: PmgmApiClient }) {
  const [section, setSection] = useState<GrandSection>('statements')
  return <div className="lodge-product-page">
    <section className="lodge-product-heading">
      <div><p className="lodge-product-breadcrumb">Orden <span>›</span> Gran Tesorería</p><h1>Gran Tesorería</h1><p>Revisión mensual de los Talleres, conciliación de pagos y regularidad institucional.</p></div>
    </section>
    <TreasuryRoleNavigation
      title="Gran Tesorería"
      sections={[
        { id:'statements', label:'Cuadros mensuales', description:'Montos por línea de cuota y conciliación' },
        { id:'regularity', label:'Estado de Talleres', description:'Consulta y regularidad institucional' },
        { id:'territories', label:'Tarifas y Orientes', description:'Clasificación territorial y cuotas del decreto' },
      ]}
      active={section}
      onChange={id => setSection(id as GrandSection)}
    />
    {section === 'statements' ? <TreasuryStatementPage api={api} canPrepare={false} canReview /> : section==='regularity'?<RegularityPage api={api} kind="treasury"/>:<TreasuryTerritoryPage api={api}/>}
  </div>
}
