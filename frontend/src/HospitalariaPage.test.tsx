import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import HospitalariaPage from './HospitalariaPage'
import { PmgmApiClient } from './api/pmgmApi'

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
    expect(withManage).toContain('Registrar movimiento')

    const readOnly = renderToStaticMarkup(
      <HospitalariaPage {...baseProps()} canReadLocal canManageLocal={false} canApproveExpenses={false} canManageGrand={false} />
    )
    // Venerable Maestro / read-only inspection must not expose the reserved
    // movement-entry form (per PMGM-ARCH-011: inspects, does not edit).
    expect(readOnly).not.toContain('Registrar movimiento')
    expect(readOnly).toContain('Venerable Maestro · inspección Hospitalaria')
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
