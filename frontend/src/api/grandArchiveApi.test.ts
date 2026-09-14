import { afterEach, expect, it, vi } from 'vitest'
import { GrandArchiveApiClient } from './grandArchiveApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary when listing archive records', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, returned: 0, items: [] }), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  const client = new GrandArchiveApiClient({ getAccessToken: async () => 'archive-token' })
  await client.list({ status: 'active', search: 'decreto' })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toContain('/api/grand-archive/?')
  expect(url).toContain('status=active')
  expect(url).toContain('search=decreto')
  expect(options.headers.get('Authorization')).toBe('Bearer archive-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('demo workflow registers and withdraws an archive record without network calls', async () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  const client = new GrandArchiveApiClient({ useMocks: true })
  const candidates = await client.candidates('Decreto')
  expect(candidates.items).toHaveLength(1)
  const candidate = candidates.items[0]
  const created = await client.register({ documentId: candidate.documentId, documentVersionId: candidate.documentVersionId, archiveCode: 'GA-QA-020', recordType: 'decree', documentDate: '2026-09-09' })
  expect(created.status).toBe('active')
  const withdrawn = await client.withdraw(created.id, 'Retiro controlado para prueba demostrativa.')
  expect(withdrawn.status).toBe('withdrawn')
  expect(fetch).not.toHaveBeenCalled()
})
