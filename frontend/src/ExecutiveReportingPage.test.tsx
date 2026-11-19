// @vitest-environment jsdom
import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { afterEach, expect, it } from 'vitest'
import ExecutiveReportingPage from './ExecutiveReportingPage'
import { ReportingApiClient, type ExecutiveReport } from './api/reportingApi'

;(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true
let root: Root | null = null
afterEach(() => { act(() => root?.unmount()); document.body.innerHTML = ''; root = null })

/** #1035 T2b-2: un Taller de baja a la fecha de corte aparece como historia («De baja») y no se suma como activo. */
it('marca los Talleres de baja como historia y separa el conteo de activos', async () => {
  const api = new ReportingApiClient({ useMocks: true })
  const base = await api.getExecutiveReport()
  const retired = { ...base.workshops[0], name: 'Taller Retirado QA', status: 'retired' as const, retiredAtUtc: '2026-09-01T12:00:00Z', attentionRequired: false, attentionFlags: [] }
  const report: ExecutiveReport = { ...base, overview: { ...base.overview, retiredWorkshops: 1 }, workshops: [...base.workshops, retired] }
  api.getExecutiveReport = async () => report
  const host = document.createElement('div'); document.body.appendChild(host); root = createRoot(host)
  await act(async () => { root!.render(<ExecutiveReportingPage reportingApi={api} />) })
  for (let i = 0; i < 4; i++) await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
  expect(host.textContent).toContain('Taller Retirado QA')
  expect(host.textContent).toContain('De baja desde')
  expect(host.textContent).toContain('solo historia')
  expect(host.textContent).toContain(`de ${base.overview.workshops} activos · 1 de baja (historia)`)
})
