import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import GrandSecretariatPage from './GrandSecretariatPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('GrandSecretariatPage', () => {
  it('requires an explicit physical-signature declaration before enabling official PDF upload', () => {
    const html = renderToStaticMarkup(<GrandSecretariatPage api={new PmgmApiClient({ useMocks: true })} />)

    expect(html).toContain('Registrar documento oficial firmado')
    expect(html).toContain('type="checkbox"')
    expect(html).toContain('Confirmo que el PDF contiene el documento firmado físicamente por los responsables.')
    expect(html).toContain('<button class="primary-action" disabled="">Cargar documento firmado</button>')
    expect(html).toContain('El sistema no genera Planchas ni Decretos.')
  })
})
