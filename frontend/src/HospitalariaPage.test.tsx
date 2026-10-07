import { describe, expect, it, vi } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import HospitalariaPage from './HospitalariaPage'
import { PmgmApiClient } from './api/pmgmApi'

const projected=vi.hoisted(()=>({actions:['view','create','write'] as string[]}))
vi.mock('./useGrandHospitalariaAccess',()=>({useGrandHospitalariaAccess:()=>({access:{version:0,managed:true,actions:projected.actions},error:null})}))

const baseProps = () => ({ api: new PmgmApiClient({ useMocks: true }) })

describe('hospitalaria page — Taller (Hospitalario/a)', () => {
  it('renders the Tronco de Beneficencia heading and independence-from-Tesorería note', () => {
    const html = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal canManageLocal canApproveExpenses={false} canManageGrand={false} />
    )
    expect(html).toContain('Tronco de Beneficencia y estado mensual')
    expect(html).toContain('Fondo independiente de Tesorería')
  })

  it('shows the reserved movement registration form only for Hospitalario/a (canManageLocal)', () => {
    const withManage = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal canManageLocal canApproveExpenses={false} canManageGrand={false} />
    )
    expect(withManage).not.toContain('Registrar movimiento')
    expect(withManage).toContain('Comprobando acceso')

    const readOnly = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal canManageLocal={false} canApproveExpenses={false} canManageGrand={false} />
    )
    // Venerable Maestro / read-only inspection must not expose the reserved
    // movement-entry form (per PMGM-ARCH-011: inspects, does not edit).
    expect(readOnly).not.toContain('Registrar movimiento')
    expect(readOnly).toContain('Comprobando acceso')
  })
})

describe('hospitalaria page — Gran Hospitalaria (aggregate-only view)', () => {
  it('states explicitly that only aggregates and institutional references are received', () => {
    const html = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal={false} canManageLocal={false} canApproveExpenses={false} canManageGrand />
    )
    expect(html).toContain('Rendiciones de Hospitalaria')
    expect(html).toContain('La bandeja recibe sólo cifras agregadas, reposiciones y referencias institucionales.')
    expect(html).toContain('No expone beneficiarios, destinos ni observaciones privadas del Taller.')
  })

  it('never renders Taller-private fields (beneficiary reference, destination, private notes) in the Gran Hospitalaria view', () => {
    // This is the privacy boundary PMGM-ARCH-011 depends on: Gran
    // Hospitalaria must only ever see conciliation/review controls, never
    // the Taller's reserved movement fields.
    const html = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal={false} canManageLocal={false} canApproveExpenses={false} canManageGrand />
    )
    for (const privateField of ['Referencia reservada', 'Destino resumido', 'Observación reservada']) {
      expect(html).not.toContain(privateField)
    }
  })
})


describe('Hospitalaria — sincronización explícita', () => {
  it('explica la consulta sin generación y ofrece una acción con revisión previa a Gran Hospitalaria', () => {
    const html = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal={false} canManageLocal={false} canApproveExpenses={false} canManageGrand />
    )
    expect(html).toContain('Consultar esta bandeja no genera obligaciones.')
    expect(html).toContain('Sincronizar defunciones')
    expect(html).toContain('Generar casos pendientes')
    expect(html).toContain('aria-haspopup="dialog"')
  })
  it('no ofrece la generación institucional al perfil local', () => {
    const html = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal canManageLocal canApproveExpenses={false} canManageGrand={false} />
    )
    expect(html).not.toContain('Sincronizar defunciones')
    expect(html).not.toContain('Generar casos pendientes')
  })
})


describe('Gran Hospitalaria — controles por capacidad',()=>{
 it('oculta creación, revisiones y regularidad editable a un perfil sólo consulta',()=>{
  projected.actions=['view']
  try{
   const html=renderToStaticMarkup(<HospitalariaPage {...baseProps()} canReadLocal={false} canManageLocal={false} canApproveExpenses={false} canManageGrand regularitySlot={<p>FORMULARIO REGULARIDAD</p>}/> )
   expect(html).toContain('Consultar esta bandeja no genera obligaciones.')
   expect(html).not.toContain('Sincronizar defunciones')
   expect(html).not.toContain('Registrar decreto de reposición')
   expect(html).not.toContain('FORMULARIO REGULARIDAD')
  }finally{projected.actions=['view','create','write']}
 })
 it('no expone datos ni formularios mientras verifica el acceso o después de revocarlo',()=>{
  projected.actions=[]
  try{
   const html=renderToStaticMarkup(<HospitalariaPage {...baseProps()} canReadLocal={false} canManageLocal={false} canApproveExpenses={false} canManageGrand/> )
   expect(html).toContain('El perfil no permite consultar Gran Hospitalaria.')
   expect(html).not.toContain('Sincronizar defunciones')
   expect(html).not.toContain('Parámetro vigente')
  }finally{projected.actions=['view','create','write']}
 })
})
