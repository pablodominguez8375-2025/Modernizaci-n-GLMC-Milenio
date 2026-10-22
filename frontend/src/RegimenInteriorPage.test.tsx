import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import RegimenInteriorPage from './RegimenInteriorPage'
import { PmgmApiClient } from './api/pmgmApi'

const mocks = () => new PmgmApiClient({ useMocks: true })

describe('régimen interior page', () => {
  it('opens as a card hub: informe y padrón, sin funciones repetidas', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} />)
    expect(html).toContain('Régimen Interior')
    expect(html).toContain('Informe y revisiones')
    expect(html).toContain('Padrón de la Gran Asamblea')
    expect(html).not.toContain('Cargando reporte institucional')
  })

  it('renders the institutional heading and loading state before the report resolves', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} initialSection="informe" />)
    expect(html).toContain('← Volver a Régimen Interior')
    expect(html).toContain('Cargando reporte institucional')
  })

  it('renders the cross-workshop rejection-alerts section with its restricted-access note', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} initialSection="informe" />)
    expect(html).toContain('Alertas de candidatos rechazados en Cámara del Medio')
    expect(html).toContain('No hay rechazos registrados.')
  })

  it('no longer ships the old fictitious "revisión preliminar" block', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} initialSection="informe" />)
    expect(html).not.toContain('Revisión preliminar de habilitaciones')
  })

  it('shows the Padrón view with the preliminary notice and no open forms', () => {
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} initialSection="padron" />)
    expect(html).toContain('← Volver a Régimen Interior')
    expect(html).toContain('Listados preliminares.')
    expect(html).toContain('Nueva Asamblea')
    // Los formularios solo viven dentro de paneles laterales (ActionDrawer).
    const outsideDrawers = html.replace(/<section class="action-drawer[\s\S]*?<\/section>/g, '')
    expect(outsideDrawers).not.toContain('<form')
  })

  it('limits the electoral roster term to enabled electors and preserves ballot secrecy', () => {
    // This line is the UI-level restatement of PMGM-ARCH-006 (balotaje y
    // sufragio siempre anónimos). It must never be removed or weakened.
    const html = renderToStaticMarkup(<RegimenInteriorPage api={mocks()} initialSection="padron" />)
    expect(html).toContain('El Padrón de la Gran Asamblea comprende exclusivamente a electores vigentes habilitados. Esta revisión nunca registra ni permite reconstruir cómo votó una persona.')
  })
})
