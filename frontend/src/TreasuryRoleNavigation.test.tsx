import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import TreasuryRoleNavigation from './TreasuryRoleNavigation'

describe('Treasury role navigation', () => {
  it('groups the lodge treasurer work in one clear operational menu', () => {
    const html = renderToStaticMarkup(<TreasuryRoleNavigation title="Tesorería del Taller" active="summary" onChange={vi.fn()} sections={[
      { id:'summary', label:'Resumen', description:'Estado del mes y cuotas vigentes' },
      { id:'collection', label:'Cuotas y cobranza', description:'Cargos, pagos y comprobantes' },
      { id:'expenses', label:'Egresos', description:'Registro y seguimiento de autorizaciones' },
      { id:'statement', label:'Cuadro mensual', description:'Pago y envío a Gran Tesorería' },
    ]} />)
    for (const label of ['Tesorería del Taller','Resumen','Cuotas y cobranza','Egresos','Cuadro mensual']) expect(html).toContain(label)
    expect(html).toContain('aria-selected="true"')
  })

  it('keeps Grand Treasury review separate from lodge operations', () => {
    const html = renderToStaticMarkup(<TreasuryRoleNavigation title="Gran Tesorería" active="statements" onChange={vi.fn()} sections={[
      { id:'statements', label:'Cuadros mensuales', description:'Montos por línea de cuota y conciliación' },
      { id:'regularity', label:'Estado de Talleres', description:'Consulta y regularidad institucional' },
    ]} />)
    expect(html).toContain('Cuadros mensuales')
    expect(html).toContain('Estado de Talleres')
    expect(html).not.toContain('Registrar pago de cuota')
    expect(html).not.toContain('Registrar egreso')
  })
})
