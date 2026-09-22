import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import RegimenInteriorPage from './RegimenInteriorPage'
import { PmgmApiClient } from './api/pmgmApi'

describe('régimen interior page', () => {
  it('renders the institutional heading and loading state before the report resolves', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Régimen Interior')
    expect(html).toContain('Cargando reporte institucional')
  })

  it('renders the cross-workshop rejection-alerts section with its restricted-access note', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('Alertas de candidatos rechazados en Cámara del Medio')
    expect(html).toContain('No hay rechazos registrados.')
  })

  it('shows the demo Gran Asamblea roster only when the API client is in mock mode', () => {
    const withMocks = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(withMocks).toContain('Padrón preliminar de asambleístas')

    const withoutMocks = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: false })} />)
    expect(withoutMocks).not.toContain('Padrón preliminar de asambleístas')
  })

  it('keeps the ballot-secrecy guarantee visible: the roster tracks eligibility, never how someone voted', () => {
    // This line is the UI-level restatement of PMGM-ARCH-006 (balotaje y
    // sufragio siempre anónimos). It must never be removed or weakened.
    const html = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('El padrón identifica quién puede asistir y sufragar, pero nunca registra ni permite reconstruir cómo votó una persona.')
  })

  it('blocks closing the roster (Cerrar padrón) while pending/observed entries remain', () => {
    // The seeded demo data has 2 unresolved entries (one pending, one
    // observed), so on first render the close button must be disabled.
    const html = renderToStaticMarkup(<RegimenInteriorPage api={new PmgmApiClient({ useMocks: true })} />)
    expect(html).toContain('2 pendientes')
    const closeButtonMatch = html.match(/<button[^>]*>Cerrar padrón<\/button>/)
    expect(closeButtonMatch).not.toBeNull()
    expect(closeButtonMatch?.[0]).toContain('disabled')
  })
})
