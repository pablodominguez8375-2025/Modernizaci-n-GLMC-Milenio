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

  it('documents a known gap (PMGM-GAP-001): only 3 ceremony types are labeled', () => {
    // ceremonyTypeLabel() in CeremoniesPage.tsx only maps 'initiation',
    // 'wage_increase' and a fallback rendered as 'Exaltación'. The
    // institutional 2026 forms also require Afiliación, Incorporación and
    // Otra (see docs/PMGM-GAP-001-brechas-flujos-institucionales-2026.md,
    // GAP-001). This test intentionally documents the current, incomplete
    // behavior so it fails loudly once someone adds real Afiliación /
    // Incorporación handling and the fallback branch is removed or fixed.
    const source = readFileSync(new URL('./CeremoniesPage.tsx', import.meta.url), 'utf-8')
    expect(source).toContain("type === 'initiation'")
    expect(source).toContain("type === 'wage_increase'")
    expect(source).not.toContain('affiliation')
    expect(source).not.toContain('incorporation')
  })
})
