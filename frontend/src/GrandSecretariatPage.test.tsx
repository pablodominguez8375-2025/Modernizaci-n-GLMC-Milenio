import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import GrandSecretariatPage from './GrandSecretariatPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('gran secretaría page', () => {
  it('renders the institutional heading and initial loading badge', () => {
    const html = renderToStaticMarkup(<GrandSecretariatPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Gran Secretaría')
    expect(html).toContain('cargando…')
  })

  it('states that the ceremony authorization tray never exposes candidate names', () => {
    // The formal authorization (Plancha) queue must only ever show
    // Taller, ceremony type, date and the institutional reservation —
    // never the name of a brother or insinuado.
    const html = renderToStaticMarkup(<GrandSecretariatPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Esta bandeja no expone nombres de hermanos o insinuados.')
    expect(html).toContain('Plancha de Autorización de Ceremonia')
  })

  it('renders the institutional space availability form and an empty documents registry on first paint', () => {
    const html = renderToStaticMarkup(<GrandSecretariatPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Disponibilidad de templos y salas')
    expect(html).toContain('Aún no hay documentos emitidos.')
  })
})
