import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import CeremoniesPage from './CeremoniesPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('ceremonies page', () => {
  it('renders the institutional heading and metrics', () => {
    const html = renderToStaticMarkup(<CeremoniesPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Ceremonias')
    expect(html).toContain('listas')
    expect(html).toContain('con pendientes')
  })

  it('shows a loading state before the review queue resolves', () => {
    // renderToStaticMarkup does not run effects, so the page must render a
    // non-empty loading state instead of silently showing "no results" or
    // crashing while items/loading are still at their initial values.
    const html = renderToStaticMarkup(<CeremoniesPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('loading-rows')
    expect(html).not.toContain('No hay ceremonias para los filtros seleccionados.')
  })

  it('exposes the institutional status filters required for triage', () => {
    const html = renderToStaticMarkup(<CeremoniesPage api={new PmgmApiClient({ useMocks: true })} />)
    for (const label of ['En gestión', 'Todos', 'En revisión', 'Observada', 'Autorizada', 'Rechazada']) {
      expect(html).toContain(label)
    }
  })

  it('delegates ceremony labels to the shared ceremonyTypes module (PMGM-GAP-001 · GAP-001 corregido)', () => {
    // Antes este test documentaba el gap: la página tenía su propia copia de
    // ceremonyTypeLabel() que rotulaba Afiliación/Incorporación como "Exaltación".
    const source = readFileSync(new URL('./CeremoniesPage.tsx', import.meta.url), 'utf-8')
    expect(source).toContain("from './ceremonyTypes'")
    expect(source).not.toMatch(/function ceremonyTypeLabel/)
  })
})
