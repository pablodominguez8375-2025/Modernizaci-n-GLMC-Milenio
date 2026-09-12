import { afterEach, expect, it, vi } from 'vitest'
import { DataQualityCaseApiClient } from './dataQualityCaseApi'
import { type DataQualityIssue } from './internalAffairsApi'

afterEach(() => vi.unstubAllGlobals())

const issue: DataQualityIssue = {
  code: 'office_after_death', severity: 'error', memberId: '10101010-1010-1010-1010-101010101010', institutionalNumber: 'GLM-0101', displayName: 'Hermana QA',
  organizationId: '11111111-1111-1111-1111-111111111111', organizationName: 'Taller QA', title: 'Cargo posterior a defunción', description: 'Caso QA', primaryDate: '2026-01-01', relatedDate: '2025-06-01', suggestedAction: 'Corroborar.',
}

it('opens a corroboration case with bearer-only boundary', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'case-1' }), { status: 201 }))
  vi.stubGlobal('fetch', fetch)
  const client = new DataQualityCaseApiClient({ getAccessToken: async () => 'case-token' })
  await client.openCase({ detectionAsOf: '2026-09-09', issue })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/regimen-interior/data-quality/cases/')
  expect(options.headers.get('Authorization')).toBe('Bearer case-token')
  expect(options).toMatchObject({ method: 'POST', credentials: 'omit', cache: 'no-store', redirect: 'error' })
  expect(JSON.parse(options.body)).toMatchObject({ ruleCode: 'office_after_death', memberId: issue.memberId, primaryDate: '2026-01-01' })
})

it('demo workflow opens claims and resolves a case without network calls', async () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  const client = new DataQualityCaseApiClient({ useMocks: true })
  const opened = await client.openCase({ detectionAsOf: '2026-09-09', issue })
  expect(opened.status).toBe('open')
  const claimed = await client.claimCase(opened.id)
  expect(claimed.status).toBe('under_review')
  const resolved = await client.resolveCase(opened.id, { outcome: 'confirmed', resolutionSummary: 'Hallazgo corroborado mediante respaldo institucional.', evidenceReference: 'ACTA-QA-001' })
  expect(resolved.status).toBe('resolved_confirmed')
  expect(resolved.events.length).toBe(3)
  expect(fetch).not.toHaveBeenCalled()
})
