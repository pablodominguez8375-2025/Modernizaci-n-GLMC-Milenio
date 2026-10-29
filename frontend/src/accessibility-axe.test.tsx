// @vitest-environment jsdom
import axe from 'axe-core'
import { createRoot, type Root } from 'react-dom/client'
import { act } from 'react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import App from './App'
import { LodgeCouncilApiProvider } from './LodgeCouncilApiContext'
import { createDefaultBootstrapApiClient } from './api/bootstrapApi'
import { createDefaultCalendarApiClient } from './api/calendarApi'
import { createDefaultCandidateIntakeApiClient } from './api/candidateIntakeApi'
import { createDefaultDataQualityCaseApiClient } from './api/dataQualityCaseApi'
import { createDefaultDocumentApiClient } from './api/documentApi'
import { createDefaultGrandArchiveApiClient } from './api/grandArchiveApi'
import { createDefaultInternalAffairsApiClient } from './api/internalAffairsApi'
import { createDefaultLodgeApiClient } from './api/lodgeApi'
import { createDefaultLodgeCouncilApiClient } from './api/lodgeCouncilApi'
import { createDefaultMembershipApiClient } from './api/membershipApi'
import { createDefaultNotificationApiClient } from './api/notificationApi'
import { createDefaultOrganizationProfileApiClient } from './api/organizationProfileApi'
import { createDefaultPmgmApiClient } from './api/pmgmApi'
import { createDefaultReportingApiClient } from './api/reportingApi'

;(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true

/** Chequeo automático de accesibilidad (WCAG 2.1 A/AA) con axe-core sobre el inicio de la demostración.
    jsdom no calcula diseño ni colores: se excluyen las reglas que dependen de ellos (contraste, región); el contraste se audita en el navegador. */
beforeAll(() => {
  vi.stubEnv('VITE_USE_MOCKS', 'true')
  // jsdom no trae matchMedia: se simula una pantalla de escritorio.
  HTMLCanvasElement.prototype.getContext = (() => null) as typeof HTMLCanvasElement.prototype.getContext
  window.matchMedia = ((query: string) => ({ matches: query.includes('min-width'), media: query, onchange: null, addEventListener: () => {}, removeEventListener: () => {}, addListener: () => {}, removeListener: () => {}, dispatchEvent: () => false })) as typeof window.matchMedia
})
let root: Root | null = null
let host: HTMLElement | null = null
afterEach(() => { act(() => root?.unmount()); host?.remove(); root = null; host = null })

async function audit(profile: string) {
  host = document.createElement('div'); document.body.appendChild(host)
  root = createRoot(host)
  await act(async () => {
    root!.render(<LodgeCouncilApiProvider api={createDefaultLodgeCouncilApiClient()}>
      <App api={createDefaultPmgmApiClient()} bootstrapApi={createDefaultBootstrapApiClient()} lodgeApi={createDefaultLodgeApiClient()} membershipApi={createDefaultMembershipApiClient()} organizationProfileApi={createDefaultOrganizationProfileApiClient()} reportingApi={createDefaultReportingApiClient()} internalAffairsApi={createDefaultInternalAffairsApiClient()} dataQualityCaseApi={createDefaultDataQualityCaseApiClient()} documentApi={createDefaultDocumentApiClient()} grandArchiveApi={createDefaultGrandArchiveApiClient()} calendarApi={createDefaultCalendarApiClient()} notificationApi={createDefaultNotificationApiClient()} candidateIntakeApi={createDefaultCandidateIntakeApiClient()} />
    </LodgeCouncilApiProvider>)
  })
  const select = host.querySelector<HTMLSelectElement>('select[aria-label="Seleccionar perfil de demostración"]')
  if (select && profile !== 'brother') {
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!
      setter.call(select, profile); select.dispatchEvent(new Event('change', { bubbles: true }))
    })
  }
  await act(async () => { await new Promise(resolve => setTimeout(resolve, 50)) })
  return axe.run(host, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }, rules: { 'color-contrast': { enabled: false }, region: { enabled: false } } })
}

const options = { runOnly: { type: 'tag' as const, values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }, rules: { 'color-contrast': { enabled: false }, region: { enabled: false } } }
const settle = () => act(async () => { await new Promise(resolve => setTimeout(resolve, 120)) })

describe('accesibilidad automática (axe-core)', () => {
  it('el detector funciona: marca un botón sin nombre accesible', async () => {
    const probe = document.createElement('div'); probe.innerHTML = '<button type="button"></button>'; document.body.appendChild(probe)
    const result = await axe.run(probe, options)
    probe.remove()
    expect(result.violations.map(v => v.id)).toContain('button-name')
  })

  for (const profile of ['brother', 'lodge', 'systemAdmin']) {
    it(`todas las pantallas del menú del perfil ${profile} no tienen violaciones WCAG A/AA`, async () => {
      const first = await audit(profile)
      expect(first.passes.length).toBeGreaterThan(10)
      const found: string[] = []
      const labels = Array.from(host!.querySelectorAll<HTMLElement>('nav.sidebar button.nav-item')).map(button => button.textContent?.trim() ?? '').filter(Boolean)
      expect(labels.length).toBeGreaterThan(3)
      for (const label of ['Inicio', ...labels]) {
        const button = Array.from(host!.querySelectorAll<HTMLElement>('nav.sidebar button.nav-item')).find(item => (item.textContent?.trim() ?? '') === label)
        if (!button) continue
        await act(async () => { button.click() }); await settle()
        const result = await axe.run(host!, options)
        for (const violation of result.violations) found.push(`${label}: ${violation.id} (${violation.impact}) × ${violation.nodes.length} → ${violation.nodes.slice(0, 2).map(node => node.target.join(' ')).join(' | ')}`)
      }
      expect(found).toEqual([])
    }, 120000)
  }
})
