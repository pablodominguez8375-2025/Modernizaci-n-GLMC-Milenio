import { afterEach, expect, it, vi } from 'vitest'
import { ReportingApiClient } from './reportingApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary for executive reporting filters', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ workshops: [] }), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  const client = new ReportingApiClient({ getAccessToken: async () => 'report-token' })
  await client.getExecutiveReport({ asOf: '2026-09-09', from: '2026-01-01' })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/reporting/executive?asOf=2026-09-09&from=2026-01-01')
  expect(options.headers.get('Authorization')).toBe('Bearer report-token')
  expect(options).toMatchObject({ method: 'GET', credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('demo mode exposes order and workshop indicators without network calls', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const report = await new ReportingApiClient({ useMocks: true }).getExecutiveReport()
  expect(report.overview.currentMembers).toBeGreaterThan(0)
  expect(report.workshops.length).toBeGreaterThanOrEqual(3)
  expect(report.workshops.some(item => item.attentionRequired)).toBe(true)
  expect(report.pastActiveDefinition).toContain('cargo institucional')
  expect(fetch).not.toHaveBeenCalled()
})
