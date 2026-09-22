import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import GrandSecretariatPage from './GrandSecretariatPage'
import { PmgmApiClient } from './api/pmgmApi'

// Nombre de archivo distinto de GrandSecretariatPage.test.tsx a propósito:
// PR #116 agrega su propio GrandSecretariatPage.test.tsx y ambos deben poder
// convivir sin conflicto add/add al integrarse.

const render = () => renderToStaticMarkup(<GrandSecretariatPage api={new PmgmApiClient({ useMocks: true })} />)

describe('gran secretaría — minimización de datos', () => {
  it('renders the institutional heading', () => {
    expect(render()).toContain('Gran Secretaría')
  })

  it('keeps the Plancha de Autorización tray free of brother/insinuado names', () => {
    // Regla institucional (no el texto del título): la bandeja de Plancha de
    // Autorización sólo muestra Taller, tipo, fecha y reserva institucional.
    // El título del panel puede evolucionar (p. ej. "... firmada" en PR #116);
    // la garantía de privacidad no.
    const html = render()
    expect(html).toContain('Plancha de Autorización')
    expect(html).toContain('Esta bandeja no expone nombres de hermanos o insinuados.')
  })

  it('renders the institutional space availability panel', () => {
    expect(render()).toContain('Disponibilidad de templos y salas')
  })
})
