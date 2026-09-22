import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import DocumentManagementPage from './DocumentManagementPage'
import { DocumentApiClient } from './api/documentApi'
import { PmgmApiClient } from './api/pmgmApi'

const props = () => ({ api: new PmgmApiClient({ useMocks: true }), documentApi: new DocumentApiClient({}) })

describe('gestor documental page', () => {
  it('renders the heading and its no-physical-keys-exposed privacy note', () => {
    const html = renderToStaticMarkup(<DocumentManagementPage {...props()} />)
    expect(html).toContain('Gestor Documental')
    expect(html).toContain('sin exponer las claves físicas del almacenamiento')
  })

  it('gates document registration behind selecting or creating a collection first', () => {
    const html = renderToStaticMarkup(<DocumentManagementPage {...props()} />)
    expect(html).toContain('Seleccione o cree una colección para continuar.')
  })

  it('exposes the full confidentiality classification catalog (interno/confidencial/sensible/restringido)', () => {
    // These classification levels are what GAP-011 (protección de
    // información confidencial) depends on at the data-entry point. The
    // form that renders them only appears once a collection is selected
    // (no effects run under renderToStaticMarkup), so this checks the
    // source directly rather than requiring interactive selection.
    const source = readFileSync(new URL('./DocumentManagementPage.tsx', import.meta.url), 'utf-8')
    for (const label of ['Interno', 'Confidencial', 'Sensible', 'Restringido']) {
      expect(source).toContain(`>${label}<`)
    }
  })

  it('shows an empty documents registry before any collection has loaded', () => {
    const html = renderToStaticMarkup(<DocumentManagementPage {...props()} />)
    expect(html).toContain('Sin documentos registrados.')
  })
})
