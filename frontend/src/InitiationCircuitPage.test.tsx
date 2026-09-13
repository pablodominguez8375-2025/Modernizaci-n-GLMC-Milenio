import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import InitiationCircuitPage from './InitiationCircuitPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('InitiationCircuitPage', () => {
  it('muestra el expediente completo y mantiene separada la activación del Aprendiz', () => {
    const html = renderToStaticMarkup(<InitiationCircuitPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Circuito completo de Iniciación')
    expect(html).toContain('Ingreso del insinuado')
    expect(html).toContain('Plancha y programación')
    expect(html).toContain('Ceremonia y activación')
    expect(html).toContain('candidato aprobado ≠ ceremonia autorizada ≠ hermano iniciado')
    expect(html).toContain('Registrar etapa y continuar')
  })
})
