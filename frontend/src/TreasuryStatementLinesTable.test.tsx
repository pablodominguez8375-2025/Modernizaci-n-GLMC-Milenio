import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { TreasuryStatementLinesTable } from './TreasuryStatementPage'
import type { TreasuryStatementLine } from './api/pmgmApi'

describe('nómina de Tesorería del Taller con formato del cuadro institucional', () => {
  it('muestra la nómina por grado y las columnas RUT, nombre, grado, cargos, cuota y respaldo', () => {
    const lines: TreasuryStatementLine[] = [
      { id:'member-apprentice', memberId:'apprentice-1', membershipId:'membership-apprentice-1', rut:'RUT-DEMO-002', firstNames:'Ana María', lastNames:'Fuentes Demo', degreeCodeAtCutoff:'apprentice', officeCodeAtCutoff:null, baseAmount:21000, adjustmentAmount:0, payableAmount:21000, adjustmentType:'normal', authorizationReference:null, observation:null, identityMatchStatus:'matched', contributionType:'normal' },
      { id:'member-master', memberId:'master-1', membershipId:'membership-master-1', rut:'RUT-DEMO-001', firstNames:'Pablo', lastNames:'Rojas Demo', degreeCodeAtCutoff:'master', officeCodeAtCutoff:'venerable_master|orator', baseAmount:13000, adjustmentAmount:0, payableAmount:13000, adjustmentType:'spouse', authorizationReference:'Plancha 004/2026', observation:null, identityMatchStatus:'matched', contributionType:'spouse' },
    ]
    const html = renderToStaticMarkup(<TreasuryStatementLinesTable lines={lines} />)

    for (const heading of ['RUT','Nombre y apellidos','Grado','Cargo(s)','Cuota','Respaldo']) expect(html).toContain(`<th>${heading}</th>`)
    expect(html.indexOf('Maestro/a')).toBeLessThan(html.indexOf('Aprendiz'))
    expect(html).toContain('RUT-DEMO-001')
    expect(html).toContain('Pablo Rojas Demo')
    expect(html).toContain('V∴M∴ · Or∴')
    expect(html).toContain('Cónyuge')
    expect(html).toContain('$13.000')
    expect(html).toContain('Plancha 004/2026')
  })

  it('marca sin plancha una cuota individual que no sea normal', () => {
    const html = renderToStaticMarkup(<TreasuryStatementLinesTable lines={[{
      id:'member-student', memberId:'student-1', membershipId:'membership-student-1', rut:'RUT-DEMO-003', firstNames:'Estudiante', lastNames:'Demo', degreeCodeAtCutoff:'fellowcraft', officeCodeAtCutoff:null, baseAmount:8000, adjustmentAmount:0, payableAmount:8000, adjustmentType:'student', authorizationReference:null, observation:null, identityMatchStatus:'matched', contributionType:'student'
    }]} />)
    expect(html).toContain('Pendiente de plancha')
  })
})
