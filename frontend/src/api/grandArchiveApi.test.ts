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

it('demo publishes with a degree, lists official documents and unpublishes with a reason', async () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch)
  const client = new GrandArchiveApiClient({ useMocks: true })
  const before = (await client.listOfficial()).items.length
  const candidate = (await client.candidates('Decreto')).items[0]
  const created = await client.register({ documentId: candidate.documentId, documentVersionId: candidate.documentVersionId, archiveCode: 'GA-QA-PUB-1', recordType: 'decree' })
  expect(created.publishedMinimumDegree ?? null).toBeNull()
  const published = await client.publish(created.id, 2)
  expect(published.publishedMinimumDegree).toBe(2)
  expect((await client.listOfficial()).items).toHaveLength(before + 1)
  await expect(client.publish(created.id, 4 as never)).rejects.toThrow()
  await expect(client.unpublish(created.id, 'corto')).rejects.toThrow()
  expect((await client.unpublish(created.id, 'Retiro por corrección del decreto.')).publishedMinimumDegree).toBeNull()
  expect((await client.listOfficial()).items).toHaveLength(before)
  const historical = (await client.list({})).items.find(x => x.recordType === 'historical_record')!
  await expect(client.publish(historical.id, 1)).rejects.toThrow(/interno/)
  expect(fetch).not.toHaveBeenCalled()
})

it('official documents use the read-only route and the publish route sends the degree', async () => {
  const fetch = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ total: 0, returned: 0, items: [] }), { status: 200 }))
  vi.stubGlobal('fetch', fetch)
  const client = new GrandArchiveApiClient({ getAccessToken: async () => 't' })
  await client.listOfficial({ recordType: 'decree', year: 2026 })
  expect(fetch.mock.calls[0][0]).toContain('/api/publicaciones-oficiales/?recordType=decree&year=2026')
  await client.publish('abc', 3)
  expect(fetch.mock.calls[1][0]).toContain('/api/grand-archive/abc/publish')
  expect(JSON.parse(fetch.mock.calls[1][1].body)).toEqual({ minimumDegree: 3 })
})
